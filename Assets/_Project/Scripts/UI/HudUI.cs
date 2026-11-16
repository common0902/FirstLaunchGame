using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElementMage
{
    /// <summary>전투 중 표시: 경험치 바, 체력, 타이머, 처치 수, 보유 스킬, 보스 체력</summary>
    public class HudUI : MonoBehaviour
    {
        GameManager gm;
        GameDatabase db;
        RectTransform root, skillRow;
        Image xpFill, hpFill, bossFill, hitFlash;
        Text levelText, timerText, killText, hpText, bossName, banner;
        GameObject bossBar;
        Enemy boss;

        int shownSecond = -1, shownKills = -1, shownLevel = -1, shownHp = -1;
        float bannerTimer, flashTimer;
        readonly List<GameObject> skillIcons = new List<GameObject>(10);

        public static HudUI Create(Transform canvas, GameManager manager)
        {
            RectTransform safe = UIFactory.SafeArea(canvas);
            safe.name = "HUD";
            var hud = safe.gameObject.AddComponent<HudUI>();
            hud.gm = manager;
            hud.db = manager.Database;
            hud.root = safe;
            hud.Build(canvas);
            return hud;
        }

        void Build(Transform canvas)
        {
            Sprite bar = db.roundedRect;

            // 피격 시 화면 붉은 번쩍임
            hitFlash = UIFactory.Image("HitFlash", canvas, null, new Color(1f, 0f, 0f, 0f));
            UIFactory.Stretch(hitFlash.rectTransform);
            hitFlash.transform.SetSiblingIndex(root.GetSiblingIndex());

            // 경험치 바 (맨 위, 가로 전체)
            Image xpBack = UIFactory.Image("XpBar", root, bar, new Color(0.05f, 0.07f, 0.12f, 0.85f), true);
            xpBack.rectTransform.anchorMin = new Vector2(0f, 1f);
            xpBack.rectTransform.anchorMax = new Vector2(1f, 1f);
            xpBack.rectTransform.pivot = new Vector2(0.5f, 1f);
            xpBack.rectTransform.offsetMin = new Vector2(24f, -76f);
            xpBack.rectTransform.offsetMax = new Vector2(-24f, -24f);
            xpFill = MakeFill(xpBack.rectTransform, new Color(0.35f, 0.9f, 0.75f));
            levelText = UIFactory.Label("Level", xpBack.transform, "Lv.1", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Stretch(levelText.rectTransform);

            // 타이머
            timerText = UIFactory.Label("Timer", root, "00:00", 76, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(400f, 100f));

            // 체력 바
            Image hpBack = UIFactory.Image("HpBar", root, bar, new Color(0.05f, 0.07f, 0.12f, 0.85f), true);
            UIFactory.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(330f, 48f));
            hpFill = MakeFill(hpBack.rectTransform, new Color(0.95f, 0.3f, 0.3f));
            hpText = UIFactory.Label("HpText", hpBack.transform, "100", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Stretch(hpText.rectTransform);

            // 처치 수
            killText = UIFactory.Label("Kills", root, "처치 0", 38, new Color(0.95f, 0.95f, 1f), TextAnchor.MiddleRight, FontStyle.Bold, true);
            UIFactory.Place(killText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -100f), new Vector2(330f, 48f));

            // 보유 스킬 아이콘 줄
            skillRow = UIFactory.Rect("Skills", root);
            UIFactory.Place(skillRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1030f, 76f));
            var layout = skillRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 보스 체력 바
            RectTransform bossRoot = UIFactory.Rect("BossBar", root);
            UIFactory.Place(bossRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 90f));
            bossBar = bossRoot.gameObject;
            bossName = UIFactory.Label("Name", bossRoot, "", 36, new Color(1f, 0.75f, 0.75f), TextAnchor.UpperCenter, FontStyle.Bold, true);
            UIFactory.Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 44f));
            Image bossBack = UIFactory.Image("Back", bossRoot, bar, new Color(0.08f, 0.02f, 0.04f, 0.9f), true);
            UIFactory.Place(bossBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 40f));
            bossFill = MakeFill(bossBack.rectTransform, new Color(0.85f, 0.15f, 0.3f));
            bossBar.SetActive(false);

            // 알림 문구
            banner = UIFactory.Label("Banner", root, "", 92, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1000f, 160f));
            banner.gameObject.SetActive(false);
        }

        static Image MakeFill(RectTransform back, Color color)
        {
            // 채움 이미지: 가로로 줄어드는 Filled 타입
            Image fill = UIFactory.Image("Fill", back, GameManager.Db.square, color);
            UIFactory.Stretch(fill.rectTransform, 5f, 5f, 5f, 5f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void OnPlayerHit() => flashTimer = 0.25f;

        public void ShowBanner(string message, float seconds)
        {
            banner.text = message;
            banner.gameObject.SetActive(true);
            bannerTimer = seconds;
        }

        public void HideBanner()
        {
            bannerTimer = 0f;
            banner.gameObject.SetActive(false);
        }

        public void ShowBoss(Enemy bossEnemy)
        {
            boss = bossEnemy;
            bossName.text = bossEnemy.Data.displayName;
            bossBar.SetActive(true);
        }

        /// <summary>보유 스킬·패시브 아이콘 줄을 다시 만든다 (스킬을 얻거나 올릴 때만 호출)</summary>
        public void RefreshSkills()
        {
            for (int i = 0; i < skillIcons.Count; i++) Destroy(skillIcons[i]);
            skillIcons.Clear();
            SkillManager skills = gm.Player.Skills;
            for (int i = 0; i < skills.Actives.Count; i++)
            {
                SkillBase s = skills.Actives[i];
                bool max = s.IsFinal;
                AddIcon(s.Evolved && s.Data.evolutionIcon != null ? s.Data.evolutionIcon : s.Data.icon,
                    max ? "MAX" : s.Level.ToString(), max ? new Color(1f, 0.85f, 0.3f) : Color.white);
            }
            for (int i = 0; i < skills.Passives.Count; i++)
            {
                PassiveSlot p = skills.Passives[i];
                bool max = p.level >= p.data.maxLevel;
                AddIcon(p.data.icon, max ? "MAX" : p.level.ToString(), max ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.85f, 0.95f));
            }
        }

        void AddIcon(Sprite sprite, string label, Color labelColor)
        {
            Image icon = UIFactory.Image("SkillIcon", skillRow, sprite, Color.white);
            icon.rectTransform.sizeDelta = new Vector2(76f, 76f);
            Text text = UIFactory.Label("Lv", icon.transform, label, 26, labelColor, TextAnchor.LowerRight, FontStyle.Bold, true);
            UIFactory.Stretch(text.rectTransform, 0f, 0f, 2f, -4f);
            skillIcons.Add(icon.gameObject);
        }

        void LateUpdate()
        {
            if (gm == null || gm.Player == null) return;
            PlayerExperience xp = gm.Player.Experience;
            PlayerHealth hp = gm.Player.Health;

            xpFill.fillAmount = Mathf.Clamp01(xp.Xp / Mathf.Max(1f, xp.XpToNext));
            hpFill.fillAmount = Mathf.Clamp01(hp.Hp / Mathf.Max(1f, hp.MaxHp));

            // 문자열은 값이 바뀔 때만 다시 만든다 (GC 방지)
            if (xp.Level != shownLevel) { shownLevel = xp.Level; levelText.text = "Lv." + xp.Level; }
            int hpInt = Mathf.CeilToInt(hp.Hp);
            if (hpInt != shownHp) { shownHp = hpInt; hpText.text = hpInt.ToString(); }
            if (gm.Kills != shownKills) { shownKills = gm.Kills; killText.text = "처치 " + gm.Kills; }

            if (gm.BossPhase)
            {
                if (shownSecond != -2) { shownSecond = -2; timerText.text = "BOSS"; timerText.color = new Color(1f, 0.4f, 0.4f); }
            }
            else
            {
                int second = Mathf.FloorToInt(gm.Elapsed);
                if (second != shownSecond)
                {
                    shownSecond = second;
                    timerText.text = (second / 60).ToString("00") + ":" + (second % 60).ToString("00");
                }
            }

            if (boss != null && bossBar.activeSelf)
                bossFill.fillAmount = boss.IsDead ? 0f : Mathf.Clamp01(boss.Hp / Mathf.Max(1f, boss.MaxHp));

            float dt = Time.unscaledDeltaTime;
            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 12f);
                banner.rectTransform.localScale = Vector3.one * pulse;
                if (bannerTimer <= 0f) banner.gameObject.SetActive(false);
            }
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                hitFlash.color = new Color(1f, 0f, 0f, Mathf.Max(0f, flashTimer) * 0.8f);
            }
        }
    }
}
