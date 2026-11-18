#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 자동 플레이 스모크 테스트 (에디터 전용). 환경 변수 EM_AUTOPLAY가 있을 때만 동작한다.
    /// 원소 조합을 고르고, 카드를 자동으로 고르고, 무작위로 돌아다니며 한 판을 끝까지 진행한 뒤
    /// 스크린샷과 결과 보고서를 EM_OUT 폴더에 남기고 에디터를 종료한다.
    ///
    /// 환경 변수:
    ///   EM_AUTOPLAY      fire+fire | water+water | fire+water
    ///   EM_OUT           결과 폴더
    ///   EM_RUN_DURATION  보스 등장 시간(게임 초)        EM_XP_MULT   경험치 배율
    ///   EM_INVINCIBLE    1이면 무적                      EM_TIMESCALE 게임 배속
    ///   EM_MAX_SECONDS   실제 시간 제한(초)              EM_SHOTS     스크린샷을 찍을 게임 시각 목록 "5,20,40"
    ///   EM_TEST_RESTART  1이면 결과 화면 뒤 다시 하기까지 검사
    /// </summary>
    public class AutoPlay : MonoBehaviour
    {
        string mode, outDir;
        float timeScale = 1f, maxSeconds = 240f;
        bool testRestart, restarted, finished;
        readonly List<float> shotTimes = new List<float>();
        readonly List<string> errors = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        int errorCount, shotIndex, levelUpsSeen;
        bool hiddenShown, evolutionPicked, bossSeen, levelUpShot, startShot;
        float startRealtime, wanderTimer, pickDelay, postRestartTimer, nextHeartbeat;
        Vector2 wanderDir = Vector2.right;
        RenderTexture rt;
        Texture2D tex;
        GameManager boundGm, resultGm;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            string m = Environment.GetEnvironmentVariable("EM_AUTOPLAY");
            if (string.IsNullOrEmpty(m)) return;
            var go = new GameObject("AutoPlay");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoPlay>().mode = m.ToLowerInvariant();
        }

        static float EnvFloat(string key, float fallback)
        {
            string v = Environment.GetEnvironmentVariable(key);
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }

        void Awake()
        {
            outDir = Environment.GetEnvironmentVariable("EM_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.temporaryCachePath, "autoplay");
            Directory.CreateDirectory(outDir);

            GameManager.DebugRunDuration = EnvFloat("EM_RUN_DURATION", 0f);
            PlayerExperience.DebugXpMultiplier = EnvFloat("EM_XP_MULT", 1f);
            PlayerHealth.DebugInvincible = EnvFloat("EM_INVINCIBLE", 0f) > 0f;
            timeScale = EnvFloat("EM_TIMESCALE", 1f);
            maxSeconds = EnvFloat("EM_MAX_SECONDS", 240f);
            testRestart = EnvFloat("EM_TEST_RESTART", 0f) > 0f;
            string shots = Environment.GetEnvironmentVariable("EM_SHOTS");
            if (!string.IsNullOrEmpty(shots))
                foreach (string s in shots.Split(','))
                    if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) shotTimes.Add(f);

            Application.logMessageReceived += OnLog;
            startRealtime = Time.realtimeSinceStartup;
            rt = new RenderTexture(540, 960, 24);
            tex = new Texture2D(540, 960, TextureFormat.RGB24, false);
        }

        void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            if (errors.Count < 30) errors.Add(type + ": " + condition + "\n" + stack);
        }

        /// <summary>카메라와 UI를 세로 렌더 텍스처에 그리게 해서 배치 모드에서도 세로 화면으로 검사한다</summary>
        void Bind(GameManager gm)
        {
            boundGm = gm;
            gm.Cam.targetTexture = rt;
            gm.MainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            gm.MainCanvas.worldCamera = gm.Cam;
            gm.MainCanvas.planeDistance = 5f;
            gm.MainCanvas.sortingOrder = 1000;
        }

        void Capture(string name)
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.Cam == null) return;
            try
            {
                Canvas.ForceUpdateCanvases();
                gm.Cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                log.AppendLine("shot " + name + " t=" + gm.Elapsed.ToString("0.0"));
            }
            catch (Exception e)
            {
                log.AppendLine("capture failed: " + e.Message);
            }
        }

        void Update()
        {
            if (finished) return;
            GameManager gm = GameManager.I;
            if (Time.realtimeSinceStartup >= nextHeartbeat)
            {
                nextHeartbeat = Time.realtimeSinceStartup + 5f;
                Debug.Log("AUTOPLAY_HEARTBEAT frame=" + Time.frameCount + " real=" + Time.realtimeSinceStartup.ToString("0.0") +
                          " state=" + (gm != null ? gm.State.ToString() : "no-gm") +
                          " elapsed=" + (gm != null ? gm.Elapsed.ToString("0.0") : "-") + " enemies=" + Enemy.Active.Count);
            }
            if (gm == null || gm.Player == null) return;
            if (gm != boundGm) Bind(gm);

            if (Time.realtimeSinceStartup - startRealtime > maxSeconds)
            {
                Finish("timeout");
                return;
            }

            switch (gm.State)
            {
                case GameState.ElementSelect:
                    if (!startShot)
                    {
                        startShot = true;
                        // 로비의 세 페이지를 차례로 찍는다
                        for (int p = 0; p < 3; p++)
                        {
                            gm.Lobby.DebugShowPage(p);
                            Capture("00_lobby_page" + p);
                        }
                        gm.Lobby.DebugShowPage(1);
                    }
                    Element a = mode.StartsWith("water") ? Element.Water : Element.Fire;
                    Element b = mode.EndsWith("fire") ? Element.Fire : Element.Water;
                    gm.ChooseElements(a, b);
                    pickDelay = 0.2f;
                    break;

                case GameState.LevelUp:
                    HandleLevelUp(gm);
                    break;

                case GameState.Playing:
                    Time.timeScale = timeScale;
                    Wander(gm);
                    if (shotIndex < shotTimes.Count && gm.Elapsed >= shotTimes[shotIndex])
                    {
                        Capture("play_" + shotIndex.ToString("00") + "_t" + Mathf.RoundToInt(shotTimes[shotIndex]));
                        shotIndex++;
                    }
                    if (gm.BossPhase && !bossSeen && gm.Elapsed >= gm.RunDuration + 1.5f)
                    {
                        bossSeen = true;
                        Capture("boss_arena");
                        log.AppendLine("boss_start level=" + gm.Player.Experience.Level + " kills=" + gm.Kills +
                                       " xpKills=" + Mathf.RoundToInt(XpOrb.XpFromKills) + " xpOrbs=" + Mathf.RoundToInt(XpOrb.XpFromOrbs) +
                                       " magnets=" + XpOrb.MagnetsPicked);
                    }
                    if (restarted)
                    {
                        postRestartTimer += Time.deltaTime;
                        if (postRestartTimer > 6f) { Capture("after_restart"); Finish("restart-ok"); }
                    }
                    break;

                case GameState.Victory:
                case GameState.Defeat:
                    // 씬을 다시 불러오는 동안 같은 결과를 두 번 처리하지 않는다
                    if (resultGm == gm) break;
                    resultGm = gm;
                    Capture("result_" + gm.State.ToString().ToLowerInvariant());
                    log.AppendLine("result=" + gm.State + " elapsed=" + gm.Elapsed.ToString("0.0"));
                    AppendBuild(gm);
                    if (testRestart && !restarted)
                    {
                        restarted = true;
                        startShot = true;
                        gm.Restart();
                    }
                    else Finish(gm.State.ToString());
                    break;
            }
        }

        void Wander(GameManager gm)
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = UnityEngine.Random.Range(0.8f, 2f);
                wanderDir = Gfx.Dir(UnityEngine.Random.Range(0f, 360f));
            }
            // 가까운 적에게서 멀어지는 방향을 섞어 사람처럼 피해 다니게 한다
            Vector2 pos = gm.Player.Position;
            Vector2 away = Vector2.zero;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                Vector2 d = pos - Enemy.Active[i].Position;
                float sqr = d.sqrMagnitude;
                if (sqr < 9f && sqr > 0.0001f) away += d / sqr;
            }
            // 사람처럼 근처 오브를 주우러 간다 (위험할수록 회피를 우선)
            Vector2 seek = wanderDir;
            if (XpOrb.TryNearest(pos, 14f, out Vector2 orbPos) && (orbPos - pos).sqrMagnitude > 0.01f)
                seek = (orbPos - pos).normalized;
            Vector2 dir = away.sqrMagnitude > 0.01f ? (away.normalized * 0.75f + seek * 0.25f).normalized : seek;
            gm.Player.DebugMoveInput = dir;
        }

        void HandleLevelUp(GameManager gm)
        {
            LevelUpUI ui = gm.LevelUpScreen;
            if (!ui.IsOpen) return;
            IReadOnlyList<LevelUpOption> options = ui.Options;

            bool hasHidden = false;
            for (int i = 0; i < options.Count; i++) hasHidden |= options[i].isHidden;

            // 입력 잠금 중에 고르기를 시도해서 잠금이 실제로 막는지 확인한다
            if (ui.InputLocked)
            {
                if (ui.DebugPick(0)) log.AppendLine("ERROR: pick succeeded while input locked");
                pickDelay = 0.15f;
                return;
            }
            pickDelay -= Time.unscaledDeltaTime;
            if (pickDelay > 0f) return;

            if (!levelUpShot && options.Count >= 3 && gm.Player.Skills.Actives.Count >= 2)
            {
                levelUpShot = true;
                Capture("levelup_cards");
            }
            if (hasHidden && !hiddenShown)
            {
                hiddenShown = true;
                Capture("hidden_card");
                log.AppendLine("hidden card shown at level " + gm.Player.Experience.Level + " options=" + options.Count);
            }

            // 고르기 정책: 히든 진화 > 범위 패시브 > (진화 가능한 스킬의 기본 최종 레벨은 피함) > 나머지 무작위
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                LevelUpOption o = options[i];
                float score = UnityEngine.Random.Range(0f, 10f);
                if (o.type == LevelUpOptionType.Evolution) score += 100f;
                else if (o.type == LevelUpOptionType.Passive && o.passive.type == PassiveType.Area) score += 50f;
                else if (o.type == LevelUpOptionType.Skill && o.skill.hasEvolution && o.nextLevel == o.skill.MaxLevel && !hasHidden) score -= 30f;
                else if (o.type == LevelUpOptionType.Skill && o.skill.kind == SkillKind.Ultimate) score += 20f;
                else if (o.type == LevelUpOptionType.Skill) score += 5f;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            LevelUpOption chosen = options[best];
            if (chosen.type == LevelUpOptionType.Evolution) evolutionPicked = true;
            levelUpsSeen++;
            log.AppendLine("pick[" + levelUpsSeen + "] " + chosen.title + " " + chosen.levelText + " (of " + options.Count + ")");
            ui.DebugPick(best);
            pickDelay = 0.05f;
        }

        void AppendBuild(GameManager gm)
        {
            SkillManager skills = gm.Player.Skills;
            log.AppendLine("level=" + gm.Player.Experience.Level + " kills=" + gm.Kills +
                           " reactions=" + DamageSystem.ReactionCount + " totalDamage=" + Mathf.RoundToInt(DamageSystem.TotalDamage));
            foreach (SkillBase s in skills.Actives)
                log.AppendLine("  skill " + s.Data.displayName + " Lv." + s.Level + (s.Evolved ? " (evolved)" : ""));
            foreach (PassiveSlot p in skills.Passives)
                log.AppendLine("  passive " + p.data.displayName + " Lv." + p.level);
        }

        void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            GameManager gm = GameManager.I;
            if (gm != null && gm.Player != null && reason == "timeout")
            {
                Capture("timeout");
                AppendBuild(gm);
            }
            var sb = new StringBuilder();
            sb.AppendLine("AUTOPLAY mode=" + mode + " end=" + reason);
            sb.AppendLine("errors=" + errorCount + " hiddenShown=" + hiddenShown + " evolutionPicked=" + evolutionPicked +
                          " bossSeen=" + bossSeen + " levelUps=" + levelUpsSeen +
                          " realSeconds=" + (Time.realtimeSinceStartup - startRealtime).ToString("0"));
            sb.Append(log);
            foreach (string e in errors) sb.AppendLine("---- " + e);
            File.WriteAllText(Path.Combine(outDir, "report.txt"), sb.ToString());
            Debug.Log("AUTOPLAY_DONE " + reason + " errors=" + errorCount);
            Time.timeScale = 1f;
            UnityEditor.EditorApplication.Exit(errorCount > 0 ? 3 : 0);
        }
    }
}
#endif
