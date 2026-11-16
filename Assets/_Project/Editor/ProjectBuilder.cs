using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace ElementMage.EditorTools
{
    /// <summary>
    /// 프로토타입에 필요한 에셋을 한 번에 만든다: 스프라이트 임포트 설정, 머티리얼,
    /// 데이터(ScriptableObject), 게임 씬, 빌드 설정(씬 목록·세로 고정).
    /// 메뉴: ElementMage
    /// </summary>
    public static class ProjectBuilder
    {
        public const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/Game.unity";
        public const string DatabasePath = Root + "/Data/GameDatabase.asset";
        internal const string SpriteDir = Root + "/Art/Sprites";
        internal const string IconDir = Root + "/Art/Icons";
        const string DataDir = Root + "/Data";
        /// <summary>데이터 구성이 바뀔 때마다 올린다. 에디터가 이 번호를 보고 빠진 데이터를 자동으로 채운다.</summary>
        public const int DataVersion = 2;

        [MenuItem("ElementMage/프로젝트 구성 다시 만들기 (수정한 수치 유지)")]
        static void BuildKeep()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(false);
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("ElementMage/모든 데이터를 기본값으로 초기화")]
        static void BuildReset()
        {
            if (!EditorUtility.DisplayDialog("데이터 초기화",
                    "스킬·적·설정 데이터를 모두 기본값으로 되돌립니다. Inspector에서 수정한 수치가 사라집니다.", "초기화", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("ElementMage/게임 씬 열기")]
        static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>명령줄(-executeMethod)용</summary>
        public static void BuildBatch()
        {
            Build(true);
            Debug.Log("ELEMENTMAGE_BUILD_DONE");
        }

        public static void Build(bool overwriteData)
        {
            foreach (string dir in new[] { "Scripts", "Data", "Data/Skills", "Data/Passives", "Data/Enemies", "Data/Stages", "Data/Equipment", "Prefabs", "Scenes", "Art", "Art/Sprites", "Art/Icons" })
                Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();

            ConfigureSprites();
            Material material = BuildMaterial();
            GameDatabase db = BuildData(overwriteData, material);
            BuildScene(db, overwriteData);
            ApplyProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ 스프라이트

        static void ConfigureSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, IconDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool nineSlice = name.StartsWith("ui_");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.spriteBorder = nineSlice ? new Vector4(32f, 32f, 32f, 32f) : Vector4.zero;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static Sprite S(string name)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");
            if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/" + name + ".png");
            if (s == null) Debug.LogWarning("스프라이트 없음: " + name);
            return s;
        }

        static Material BuildMaterial()
        {
            const string path = Root + "/Art/SpriteUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------ 데이터

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static SkillLevelStats L(string description, float damage, float cooldown, int count, float area,
            float speed = 0f, float duration = 0f, float range = 0f, float tick = 0f, float special = 0f, float special2 = 0f)
        {
            return new SkillLevelStats
            {
                description = description, damage = damage, cooldown = cooldown, count = count, area = area,
                speed = speed, duration = duration, range = range, tickInterval = tick, special = special, special2 = special2
            };
        }

        static ExtraParam X(string key, float value) => new ExtraParam { key = key, value = value };

        static GameDatabase BuildData(bool overwrite, Material material)
        {
            GameConfig config = LoadOrCreate<GameConfig>(DataDir + "/GameConfig.asset", out bool configCreated);
            if (overwrite && !configCreated)
            {
                // 기본값으로 되돌리기: 새 인스턴스의 값을 덮어쓴다
                var fresh = ScriptableObject.CreateInstance<GameConfig>();
                EditorUtility.CopySerialized(fresh, config);
                config.name = "GameConfig";
                Object.DestroyImmediate(fresh);
            }
            EditorUtility.SetDirty(config);

            Color fire = new Color(1f, 0.55f, 0.15f);
            Color blue = new Color(0.35f, 0.7f, 1f);
            Color water = new Color(0.3f, 0.85f, 1f);
            Color deepWater = new Color(0.25f, 0.55f, 1f);

            // ---- 패시브
            PassiveData pArea = Passive("Passive_Area", PassiveType.Area, "광역 강화", "icon_area", 0.08f, "모든 스킬 범위 +{0}%", overwrite);
            PassiveData pDamage = Passive("Passive_Damage", PassiveType.Damage, "마력 증폭", "icon_damage", 0.08f, "모든 스킬 피해 +{0}%", overwrite);
            PassiveData pCooldown = Passive("Passive_Cooldown", PassiveType.Cooldown, "신속 영창", "icon_cooldown", 0.05f, "스킬 쿨타임 -{0}%", overwrite);
            PassiveData pSpeed = Passive("Passive_MoveSpeed", PassiveType.MoveSpeed, "바람 걸음", "icon_speed", 0.06f, "이동속도 +{0}%", overwrite);

            var skills = new List<SkillData>();

            // ---- 불 일반기
            skills.Add(Skill("Skill_Fireball", overwrite, s =>
            {
                s.id = SkillId.Fireball; s.displayName = "화염구"; s.element = Element.Fire; s.kind = SkillKind.Normal;
                s.summary = "가장 가까운 적에게 화염구를 발사한다. 충돌하면 범위 폭발.";
                s.fieldGuide = "damage 폭발 피해 / cooldown / count 발사 수 / area 폭발 반지름 / speed 탄속 / range 사거리\nspecial 두 번째 폭발 피해 배율(0=없음) / special2 두 번째 폭발 반지름 배율\nextras: fanAngle 부채꼴 간격(도), secondExplosionDelay 두 번째 폭발 지연";
                s.icon = S("icon_fireball"); s.mainSprite = S("fireball"); s.color = fire; s.finalColor = blue;
                s.levels = new[]
                {
                    L("화염구 1발. 충돌 시 범위 폭발", 12, 1.4f, 1, 1.0f, 9, 0, 9),
                    L("피해량 증가", 17, 1.4f, 1, 1.0f, 9, 0, 9),
                    L("화염구 3발을 부채꼴로 발사", 17, 1.4f, 3, 1.0f, 9, 0, 9),
                    L("폭발 범위 증가", 17, 1.4f, 3, 1.5f, 9, 0, 9),
                    L("피해량 추가 증가", 23, 1.4f, 3, 1.5f, 9, 0, 9),
                    L("발동 주기 감소", 23, 1.0f, 3, 1.5f, 9, 0, 9),
                    L("폭발 직후 푸른 화염의 두 번째 큰 폭발", 23, 1.0f, 3, 1.5f, 9, 0, 9, 0, 1.5f, 1.8f),
                };
                s.extras = new[] { X("fanAngle", 18f), X("secondExplosionDelay", 0.25f) };
            }));

            skills.Add(Skill("Skill_FlameSword", overwrite, s =>
            {
                s.id = SkillId.FlameSword; s.displayName = "화염검"; s.element = Element.Fire; s.kind = SkillKind.Normal;
                s.summary = "이동 방향 전방을 화염검으로 크게 벤다.";
                s.fieldGuide = "damage 참격 1회 피해 / cooldown / count 연속 베기 횟수 / area 검 사거리(반지름)\nspeed 검기 속도 / range 검기 사거리 / special 검기 피해 배율(0=검기 없음) / special2 참격 각도(360=검무)\nextras: comboInterval 연속 베기 간격";
                s.icon = S("icon_flame_sword"); s.mainSprite = S("slash"); s.effectSprite = S("sword_wave"); s.color = fire; s.finalColor = blue;
                s.levels = new[]
                {
                    L("전방 화염검 참격", 14, 1.6f, 1, 2.2f, 12, 0, 7, 0, 0, 130),
                    L("피해량 증가", 20, 1.6f, 1, 2.2f, 12, 0, 7, 0, 0, 130),
                    L("화염 검기 발사", 20, 1.6f, 1, 2.2f, 12, 0, 7, 0, 0.6f, 130),
                    L("검 크기 및 사거리 증가", 20, 1.6f, 1, 3.0f, 12, 0, 9, 0, 0.6f, 130),
                    // Lv.5: 공격 횟수가 2배가 되므로 공격당 피해 계수를 낮춰 DPS 급등을 막는다
                    L("2연속 베기. 두 참격 모두 검기 발사", 14, 1.6f, 2, 3.0f, 12, 0, 9, 0, 0.6f, 130),
                    L("피해량 추가 증가", 19, 1.6f, 2, 3.0f, 12, 0, 9, 0, 0.6f, 130),
                    L("푸른 화염의 360도 검무", 22, 1.6f, 2, 3.2f, 12, 0, 9, 0, 0.6f, 360),
                };
                s.extras = new[] { X("comboInterval", 0.22f) };
            }));

            skills.Add(Skill("Skill_FlameRing", overwrite, s =>
            {
                s.id = SkillId.FlameRing; s.displayName = "화염 고리"; s.element = Element.Fire; s.kind = SkillKind.Normal;
                s.summary = "화염 고리가 화면 안을 돌아다니며 경계에서 튕긴다. 적을 관통한다.";
                s.fieldGuide = "damage 1회 피해 / count 고리 수 / area 고리 반지름 / speed 이동속도 / tickInterval 적별 재피격 간격\nspecial 반사 팽창 배율(0=없음) / special2 푸른 화염 속도·피해 배율(0=없음) / duration 푸른 화염 지속\nextras: expandDuration 팽창 시간, blueCooldown Lv.7 재발동 대기(미정, 기본 0), boundsScale 전투 영역 크기(화면 대비)";
                s.icon = S("icon_flame_ring"); s.mainSprite = S("flame_ring"); s.color = fire; s.finalColor = blue;
                s.levels = new[]
                {
                    L("반사 이동하는 화염 고리 1개", 6, 0, 1, 0.9f, 5f, 0, 0, 0.4f),
                    L("피해량 증가", 8.5f, 0, 1, 0.9f, 5f, 0, 0, 0.4f),
                    L("고리 크기 및 이동속도 증가", 8.5f, 0, 1, 1.2f, 6.5f, 0, 0, 0.4f),
                    L("화염 고리 2개", 8.5f, 0, 2, 1.2f, 6.5f, 0, 0, 0.4f),
                    L("피해량 추가 증가", 11.5f, 0, 2, 1.2f, 6.5f, 0, 0, 0.4f),
                    L("경계에서 반사할 때 순간 팽창", 11.5f, 0, 2, 1.2f, 6.5f, 0, 0, 0.4f, 1.6f),
                    L("고리가 적을 처치하면 일정 시간 푸른 화염 (속도·피해 증가)", 11.5f, 0, 2, 1.2f, 6.5f, 4f, 0, 0.4f, 1.6f, 1.5f),
                };
                s.extras = new[] { X("expandDuration", 0.5f), X("blueCooldown", 0f), X("boundsScale", 0.95f) };
            }));

            skills.Add(Skill("Skill_Meteor", overwrite, s =>
            {
                s.id = SkillId.Meteor; s.displayName = "운석"; s.element = Element.Fire; s.kind = SkillKind.Normal;
                s.summary = "적이 몰린 곳에 운석이 떨어져 넓은 범위를 폭격한다.";
                s.fieldGuide = "damage 폭발 피해 / cooldown / count 운석 수 / area 폭발 반지름 / duration 낙하 시간\ntickInterval 운석 사이 간격 / range 대상 탐색 반경";
                s.icon = S("icon_meteor"); s.mainSprite = S("meteor"); s.subSprite = S("meteor_blue"); s.color = fire; s.finalColor = blue;
                s.levels = new[]
                {
                    L("운석 1개 낙하", 30, 5f, 1, 1.6f, 0, 0.7f, 6.5f, 0.35f),
                    L("피해량 증가", 42, 5f, 1, 1.6f, 0, 0.7f, 6.5f, 0.35f),
                    L("운석 3개 순차 낙하", 42, 5f, 3, 1.6f, 0, 0.7f, 6.5f, 0.35f),
                    L("운석 크기 및 폭발 범위 증가", 42, 5f, 3, 2.2f, 0, 0.7f, 6.5f, 0.35f),
                    L("피해량 추가 증가", 56, 5f, 3, 2.2f, 0, 0.7f, 6.5f, 0.35f),
                    L("운석 5개 순차 낙하", 56, 5f, 5, 2.2f, 0, 0.7f, 6.5f, 0.35f),
                    L("푸른 화염 운석. 체급·폭발 화력 강화, 큰 화염 파동", 78, 5f, 5, 2.8f, 0, 0.7f, 6.5f, 0.35f),
                };
            }));

            // ---- 물 일반기
            skills.Add(Skill("Skill_WaterSwarm", overwrite, s =>
            {
                s.id = SkillId.WaterSwarm; s.displayName = "수류 군집"; s.element = Element.Water; s.kind = SkillKind.Normal;
                s.summary = "물방울 무리가 곡선으로 헤엄치며 적을 차례로 타격한다.";
                s.fieldGuide = "damage 물방울 1개당 피해 / count 물방울 수 / area 군집 반지름 / speed 이동속도\nrange 플레이어로부터의 추적 한계 / tickInterval 적별 재피격 간격 / special 선회 속도(도/초)";
                s.icon = S("icon_water_swarm"); s.mainSprite = S("droplet"); s.subSprite = S("water_blade"); s.color = water; s.finalColor = water;
                s.levels = new[]
                {
                    L("물방울 3개", 3f, 0, 3, 0.7f, 7f, 0, 8, 0.35f, 300),
                    L("피해량 증가", 4.2f, 0, 3, 0.7f, 7f, 0, 8, 0.35f, 300),
                    L("물방울 5개", 4.2f, 0, 5, 0.8f, 7f, 0, 8, 0.35f, 300),
                    L("군집 이동속도 증가", 4.2f, 0, 5, 0.8f, 9.5f, 0, 8, 0.35f, 360),
                    L("피해량 추가 증가", 5.6f, 0, 5, 0.8f, 9.5f, 0, 8, 0.35f, 360),
                    L("물방울 7개", 5.6f, 0, 7, 0.9f, 9.5f, 0, 8, 0.35f, 360),
                    L("날카롭고 길쭉한 수류. 이동속도·공격력 강화", 7.2f, 0, 7, 1.0f, 12.5f, 0, 8, 0.3f, 420),
                };
            }));

            skills.Add(Skill("Skill_WaterJet", overwrite, s =>
            {
                s.id = SkillId.WaterJet; s.displayName = "수압 분출"; s.element = Element.Water; s.kind = SkillKind.Normal;
                s.summary = "무작위 방향으로 물줄기를 분사해 적을 밀어낸다.";
                s.fieldGuide = "damage 틱당 피해 / cooldown / count 줄기 수 / area 분사 폭 / range 사거리 / duration 분사 시간\ntickInterval / speed 밀어내는 힘 / special 파동 피해 배율(0=없음) / special2 파동 간격\nextras: angleJitter 줄기 각도 흔들림, pulseTravelTime 파동이 끝까지 가는 시간";
                s.icon = S("icon_water_jet"); s.mainSprite = S("jet"); s.effectSprite = S("jet_pulse"); s.color = water; s.finalColor = water;
                s.levels = new[]
                {
                    L("무작위 방향 수압 1개", 4f, 4f, 1, 1.0f, 3.5f, 2.5f, 5f, 0.25f),
                    L("피해량 증가", 5.6f, 4f, 1, 1.0f, 3.5f, 2.5f, 5f, 0.25f),
                    L("분사 폭 및 사거리 증가", 5.6f, 4f, 1, 1.4f, 3.5f, 2.5f, 6.5f, 0.25f),
                    L("수압 2개 동시 분출", 5.6f, 4f, 2, 1.4f, 3.5f, 2.5f, 6.5f, 0.25f),
                    L("피해량 추가 증가", 7.6f, 4f, 2, 1.4f, 3.5f, 2.5f, 6.5f, 0.25f),
                    L("수압 3개 동시 분출", 7.6f, 4f, 3, 1.4f, 3.5f, 2.5f, 6.5f, 0.25f),
                    L("압축 수압 파동이 물줄기를 따라 진행하는 맥동 분사", 7.6f, 4f, 3, 1.4f, 3.5f, 2.5f, 6.5f, 0.25f, 2.5f, 0.6f),
                };
                s.extras = new[] { X("angleJitter", 25f), X("pulseTravelTime", 0.35f) };
            }));

            skills.Add(Skill("Skill_Torrent", overwrite, s =>
            {
                s.id = SkillId.Torrent; s.displayName = "격류"; s.element = Element.Water; s.kind = SkillKind.Normal;
                s.summary = "화면을 가로지르는 물살. 적을 흐름 방향으로 밀고, 흐름을 따라 이동하면 빨라진다.";
                s.fieldGuide = "damage 틱당 피해(매우 낮음) / cooldown / count 격류 수 / area 격류 폭 / duration / tickInterval\nspeed 물살 강도 / special 플레이어 가속 배율\nLv.6은 기획 미정 → 수치만 소폭 강화해 둠\nextras: length 물살 길이, searchRange 위치 탐색 반경, denseChance 밀집 지역 우선 확률, angleSpread 방향 편차(도)";
                s.icon = S("icon_torrent"); s.mainSprite = S("torrent"); s.effectSprite = S("chevron"); s.color = deepWater; s.finalColor = water;
                s.levels = new[]
                {
                    L("격류 1개", 1f, 12f, 1, 2.2f, 2.5f, 7f, 0, 0.5f, 1.3f),
                    L("격류 폭 증가", 1f, 12f, 1, 3.0f, 2.5f, 7f, 0, 0.5f, 1.3f),
                    L("물살 강도 및 플레이어 가속 강화", 1f, 12f, 1, 3.0f, 3.5f, 7f, 0, 0.5f, 1.5f),
                    L("쿨타임 감소", 1f, 9f, 1, 3.0f, 3.5f, 7f, 0, 0.5f, 1.5f),
                    L("격류 2개", 1f, 9f, 2, 3.0f, 3.5f, 7f, 0, 0.5f, 1.5f),
                    // TODO(미정): 격류 Lv.6 효과. 확정 전까지 수치만 소폭 강화
                    L("(효과 미정) 물살 소폭 강화", 1.2f, 9f, 2, 3.2f, 3.7f, 7f, 0, 0.5f, 1.5f),
                    L("더 거센 격류. 직접 피해 소폭 강화", 2f, 9f, 2, 3.4f, 4.5f, 7f, 0, 0.5f, 1.6f),
                };
                s.extras = new[] { X("length", 60f), X("searchRange", 8f), X("denseChance", 0.6f), X("angleSpread", 45f) };
            }));

            skills.Add(Skill("Skill_Rain", overwrite, s =>
            {
                s.id = SkillId.Rain; s.displayName = "소나기"; s.element = Element.Water; s.kind = SkillKind.Normal;
                s.summary = "적이 몰린 곳에 소나기를 내려 범위 안의 적에게 지속 피해를 준다.";
                s.fieldGuide = "damage 틱당 피해 / cooldown / count 소나기 수 / area 반지름 / duration / tickInterval 타격 주기 / range 탐색 반경";
                s.icon = S("icon_rain"); s.mainSprite = S("rain_streak"); s.color = water; s.finalColor = deepWater;
                s.levels = new[]
                {
                    L("소나기 1개", 3f, 7f, 1, 2.0f, 0, 4f, 7f, 0.5f),
                    L("피해량 증가", 4.4f, 7f, 1, 2.0f, 0, 4f, 7f, 0.5f),
                    L("비가 내리는 범위 확대", 4.4f, 7f, 1, 2.8f, 0, 4f, 7f, 0.5f),
                    L("서로 다른 밀집 지역에 소나기 2개", 4.4f, 7f, 2, 2.8f, 0, 4f, 7f, 0.5f),
                    L("타격 주기 감소", 4.4f, 7f, 2, 2.8f, 0, 4f, 7f, 0.35f),
                    L("쿨타임 감소", 4.4f, 5.5f, 2, 2.8f, 0, 4f, 7f, 0.35f),
                    L("집중 호우: 굵은 빗줄기. 직접 피해 크게 강화", 8.8f, 5.5f, 2, 2.8f, 0, 4f, 7f, 0.35f),
                };
            }));

            // ---- 전용 궁극기 (최대 Lv.5)
            skills.Add(Skill("Skill_FirePillar", overwrite, s =>
            {
                s.id = SkillId.FirePillar; s.displayName = "화염 기둥"; s.element = Element.Fire; s.kind = SkillKind.Ultimate;
                s.summary = "체력이 높은 적을 쫓아가는 화염 기둥. 같은 적이 안에 오래 있을수록 피해가 오른다.";
                s.fieldGuide = "damage 틱당 피해 / cooldown / area 반지름 / speed 추적 속도 / duration / tickInterval / range 대상 탐색 반경\nspecial 초당 누적 화력 상승량 / special2 누적 화력 최대 보너스(1.5 = +150%)\nextras: evolutionMaxRadius 대화염 기둥 크기 상한, evolutionRampFactor 진화 시 누적 화력 계수(0이면 누적 제거)";
                s.icon = S("icon_fire_pillar"); s.mainSprite = S("pillar"); s.subSprite = S("pillar_blue"); s.color = fire; s.finalColor = blue;
                s.levels = new[]
                {
                    L("추적 화염 기둥 + 약한 누적 화력", 8f, 12f, 1, 1.3f, 2.2f, 8f, 10f, 0.25f, 0.15f, 0.6f),
                    L("피해 증가", 11f, 12f, 1, 1.3f, 2.2f, 8f, 10f, 0.25f, 0.15f, 0.6f),
                    L("추적속도 증가", 11f, 12f, 1, 1.3f, 3.2f, 8f, 10f, 0.25f, 0.15f, 0.6f),
                    L("누적 화력 강화 (상승 속도·최대치 증가)", 11f, 12f, 1, 1.3f, 3.2f, 8f, 10f, 0.25f, 0.3f, 1.5f),
                    L("푸른 화염 기둥: 크기·기본 피해 강화", 14.5f, 12f, 1, 1.7f, 3.2f, 8f, 10f, 0.25f, 0.3f, 1.5f),
                };
                s.extras = new[] { X("evolutionMaxRadius", 4f), X("evolutionRampFactor", 0.5f) };
                s.hasEvolution = true;
                s.evolutionName = "대화염 기둥";
                s.evolutionDescription = "범위가 크게 늘어난 거대한 화염 기둥.\n대신 느리게 움직이고 누적 화력이 약해진다.";
                s.evolutionIcon = S("icon_great_pillar");
                s.evolutionRequiredPassive = pArea;
                // 진화 데이터 세트: Lv.5 기준에서 범위↑ 이동속도↓ (누적 화력 약화는 evolutionRampFactor)
                s.evolutionStats = L("대화염 기둥", 14.5f, 12f, 1, 2.3f, 1.8f, 8f, 10f, 0.25f, 0.3f, 1.5f);
            }));

            skills.Add(Skill("Skill_AbyssVortex", overwrite, s =>
            {
                s.id = SkillId.AbyssVortex; s.displayName = "심해 소용돌이"; s.element = Element.Water; s.kind = SkillKind.Ultimate;
                s.summary = "적을 나선형으로 중심에 끌어당기는 소용돌이. 중심에 가까울수록 자주 타격한다.";
                s.fieldGuide = "damage 타격당 피해 / cooldown / area 반지름 / speed 흡입력 / duration / tickInterval 가장자리 타격 간격 / range 탐색 반경\nspecial 심연의 붕괴 기본 피해(0=없음) / special2 붕괴 시 적 1마리당 피해 증가율\nextras: minTickInterval 타격 주기 하한, coreRadiusRatio 중심부 크기 비율, tangentialSpeed 회전 힘, collapseEnemyCap 붕괴 피해 적 수 상한, scatterSpeed 흩뿌리는 속도";
                s.icon = S("icon_abyss_vortex"); s.mainSprite = S("vortex"); s.color = water; s.finalColor = water;
                s.levels = new[]
                {
                    L("기본 소용돌이 (나선 흡입)", 5f, 14f, 1, 3.0f, 2.5f, 6f, 8f, 0.8f),
                    L("범위 증가", 5f, 14f, 1, 4.0f, 2.5f, 6f, 8f, 0.8f),
                    L("기본 피해 증가", 7f, 14f, 1, 4.0f, 2.5f, 6f, 8f, 0.8f),
                    L("흡입력 증가", 7f, 14f, 1, 4.0f, 4.0f, 6f, 8f, 0.8f),
                    L("심연의 붕괴: 종료 시 중심부 폭발, 끌려온 적이 많을수록 강해지고 적을 흩뿌린다", 7f, 14f, 1, 4.0f, 4.0f, 6f, 8f, 0.8f, 40f, 0.1f),
                };
                s.extras = new[] { X("minTickInterval", 0.2f), X("coreRadiusRatio", 0.3f), X("tangentialSpeed", 3f), X("collapseEnemyCap", 15f), X("scatterSpeed", 9f) };
            }));

            // ---- 적 (모두 같은 스크립트, 수치만 다름)
            EnemyData slime = Enemy("Enemy_Slime", "슬라임", "slime", 10f, 1.5f, 5f, 0.38f, 0.95f, 1, false, overwrite);
            EnemyData bat = Enemy("Enemy_Bat", "박쥐", "bat", 7f, 2.6f, 4f, 0.32f, 1.05f, 2, false, overwrite);
            EnemyData golem = Enemy("Enemy_Golem", "골렘", "golem", 45f, 1.1f, 10f, 0.55f, 1.45f, 5, false, overwrite);
            EnemyData boss = Enemy("Enemy_Boss", "심연의 군주", "boss", 12000f, 1.6f, 20f, 1.25f, 3.2f, 0, true, overwrite);

            // ---- 데이터베이스
            GameDatabase db = LoadOrCreate<GameDatabase>(DatabasePath, out bool dbCreated);
            db.config = config;
            db.skills = skills;
            db.passives = new List<PassiveData> { pArea, pDamage, pCooldown, pSpeed };
            db.boss = boss;

            // ---- 로비: 스테이지와 장비 (기획이 없는 영역이라 내용·수치 모두 임시 테스트 값)
            db.stages = new List<StageData>
            {
                Stage("Stage_1", "초록 숲", "슬라임이 모여드는 평화로운 숲.\n기본 난이도", Color.white, 1f, 1f, 1f, overwrite),
                Stage("Stage_2", "황혼 늪", "붉게 물든 늪지대.\n적 체력·피해·수가 늘어난다", new Color(1f, 0.78f, 0.7f), 1.6f, 1.3f, 1.2f, overwrite),
                Stage("Stage_3", "서리 폐허", "얼어붙은 옛 왕국의 폐허.\n적이 훨씬 강하고 많다", new Color(0.72f, 0.88f, 1f), 2.4f, 1.6f, 1.4f, overwrite),
            };
            db.equipment = new List<EquipmentData>
            {
                Equip("Equip_Staff", "견습생 지팡이", EquipSlot.Weapon, "eq_staff", EquipStat.Damage, 0.10f, overwrite),
                Equip("Equip_Wand", "불씨 완드", EquipSlot.Weapon, "eq_wand", EquipStat.Cooldown, 0.08f, overwrite),
                Equip("Equip_Necklace", "수정 목걸이", EquipSlot.Necklace, "eq_necklace", EquipStat.Area, 0.08f, overwrite),
                Equip("Equip_Gloves", "가죽 장갑", EquipSlot.Gloves, "eq_gloves", EquipStat.Damage, 0.06f, overwrite),
                Equip("Equip_Robe", "여행자 로브", EquipSlot.Armor, "eq_robe", EquipStat.MaxHp, 0.20f, overwrite),
                Equip("Equip_Cloak", "바람 망토", EquipSlot.Armor, "eq_cloak", EquipStat.MoveSpeed, 0.08f, overwrite),
                Equip("Equip_Belt", "튼튼한 벨트", EquipSlot.Belt, "eq_belt", EquipStat.MaxHp, 0.12f, overwrite),
                Equip("Equip_Boots", "가벼운 장화", EquipSlot.Boots, "eq_boots", EquipStat.MoveSpeed, 0.06f, overwrite),
            };
            db.tabEquipIcon = S("tab_equip");
            db.tabBattleIcon = S("tab_battle");
            db.tabCodexIcon = S("tab_codex");
            db.lockIcon = S("icon_lock");
            db.dataVersion = DataVersion;
            if (dbCreated || overwrite || db.spawnTable == null || db.spawnTable.Count == 0)
            {
                db.spawnTable = new List<SpawnEntry>
                {
                    new SpawnEntry { enemy = slime, startTime = 0f, weight = 1f },
                    new SpawnEntry { enemy = bat, startTime = 60f, weight = 0.6f },
                    new SpawnEntry { enemy = golem, startTime = 180f, weight = 0.35f },
                };
                db.fireColor = fire;
                db.blueFlameColor = blue;
                db.waterColor = water;
            }
            db.spriteMaterial = material;
            db.player = S("player");
            db.xpOrb = S("xp_orb");
            db.groundTile = S("ground");
            db.circle = S("circle");
            db.softCircle = S("soft_circle");
            db.ring = S("ring");
            db.square = S("square");
            db.burst = S("burst");
            db.roundedRect = S("ui_rounded");
            db.roundedOutline = S("ui_outline");
            db.fireEmblem = S("emblem_fire");
            db.waterEmblem = S("emblem_water");
            EditorUtility.SetDirty(db);
            return db;
        }

        static StageData Stage(string file, string name, string desc, Color tint, float hp, float damage, float spawn, bool overwrite)
        {
            StageData s = LoadOrCreate<StageData>(DataDir + "/Stages/" + file + ".asset", out bool created);
            if (created || overwrite)
            {
                s.displayName = name; s.description = desc; s.groundTint = tint;
                s.enemyHpMultiplier = hp; s.enemyDamageMultiplier = damage; s.spawnRateMultiplier = spawn;
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        static EquipmentData Equip(string file, string name, EquipSlot slot, string icon, EquipStat stat, float value, bool overwrite)
        {
            EquipmentData e = LoadOrCreate<EquipmentData>(DataDir + "/Equipment/" + file + ".asset", out bool created);
            if (created || overwrite)
            {
                e.displayName = name; e.slot = slot; e.icon = S(icon); e.stat = stat; e.value = value;
                EditorUtility.SetDirty(e);
            }
            return e;
        }

        static SkillData Skill(string file, bool overwrite, System.Action<SkillData> fill)
        {
            SkillData s = LoadOrCreate<SkillData>(DataDir + "/Skills/" + file + ".asset", out bool created);
            if (created || overwrite)
            {
                fill(s);
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        static PassiveData Passive(string file, PassiveType type, string name, string icon, float perLevel, string format, bool overwrite)
        {
            PassiveData p = LoadOrCreate<PassiveData>(DataDir + "/Passives/" + file + ".asset", out bool created);
            if (created || overwrite)
            {
                p.type = type; p.displayName = name; p.icon = S(icon); p.maxLevel = 5;
                p.valuePerLevel = perLevel; p.descriptionFormat = format;
                EditorUtility.SetDirty(p);
            }
            return p;
        }

        static EnemyData Enemy(string file, string name, string sprite, float hp, float speed, float damage,
            float radius, float width, int xp, bool isBoss, bool overwrite)
        {
            EnemyData e = LoadOrCreate<EnemyData>(DataDir + "/Enemies/" + file + ".asset", out bool created);
            if (created || overwrite)
            {
                e.displayName = name; e.sprite = S(sprite); e.tint = Color.white; e.maxHp = hp; e.moveSpeed = speed;
                e.contactDamage = damage; e.radius = radius; e.visualWidth = width; e.xp = xp; e.isBoss = isBoss;
                EditorUtility.SetDirty(e);
            }
            return e;
        }

        // ------------------------------------------------------------------ 씬

        static void BuildScene(GameDatabase db, bool overwrite)
        {
            if (!overwrite && File.Exists(ScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = db.config.cameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.1f, 0.12f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("database").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ 프로젝트 설정

        /// <summary>게임 씬을 빌드 목록 맨 앞에 넣고 세로 화면으로 고정한다</summary>
        public static void ApplyProjectSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }
    }

    /// <summary>
    /// 에디터가 스크립트를 다시 불러올 때 한 번 확인: 게임 씬이 빌드 목록에 없으면 프로젝트 설정을 적용하고,
    /// 데이터가 없으면 새로 만든다. 이미 구성돼 있으면 아무 일도 하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectAutoSetup
    {
        static ProjectAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool hasData = File.Exists(ProjectBuilder.DatabasePath) && File.Exists(ProjectBuilder.ScenePath);
            bool inBuildList = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path == ProjectBuilder.ScenePath && s.enabled) inBuildList = true;
            // 데이터 구성이 바뀌었으면(스테이지·장비 추가 등) 빠진 에셋만 새로 채운다. 수정한 수치는 유지된다.
            if (hasData)
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ProjectBuilder.DatabasePath);
                if (db != null && db.dataVersion < ProjectBuilder.DataVersion && File.Exists(ProjectBuilder.IconDir + "/tab_battle.png"))
                {
                    ProjectBuilder.Build(false);
                    Debug.Log("[ElementMage] 데이터 구성을 갱신했습니다 (로비·스테이지·장비 추가).");
                }
            }
            if (hasData && inBuildList) return;

            // 아트 파일이 아직 없으면(작업 도중) 자동 생성하지 않는다
            if (!hasData && !File.Exists(ProjectBuilder.SpriteDir + "/player.png")) return;
            if (!hasData)
            {
                // 열려 있는 씬에 저장하지 않은 변경이 있으면 건드리지 않는다
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) return;
                ProjectBuilder.Build(false);
                EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
                Debug.Log("[ElementMage] 프로토타입 에셋을 생성했습니다. Game 뷰를 세로(1080×1920)로 맞추고 Play를 누르세요.");
                return;
            }

            ProjectBuilder.ApplyProjectSettings();
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            Debug.Log("[ElementMage] 빌드 씬 목록과 세로 화면 설정을 적용했습니다. Game 뷰를 세로(1080×1920)로 맞추고 Play를 누르세요.");
        }
    }

    /// <summary>
    /// 배치 모드 스모크 테스트: 환경 변수 EM_AUTOPLAY가 있으면 게임 씬을 열고 플레이 모드로 들어간다.
    /// 이후 진행과 종료는 런타임의 AutoPlay가 맡는다.
    /// </summary>
    [InitializeOnLoad]
    static class SmokeTestDriver
    {
        const string StartedKey = "ElementMage.SmokeStarted";

        static SmokeTestDriver()
        {
            if (!Application.isBatchMode) return;
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("EM_AUTOPLAY"))) return;
            EditorApplication.update += Tick;
        }

        static double readySince = -1;

        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // 컴파일·임포트가 끝나고 몇 초 동안 조용해진 뒤에 플레이 모드로 들어간다.
            // (컴파일 도중에 들어가면 플레이 중 도메인 리로드가 일어나 게임 상태가 사라진다)
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { readySince = -1; return; }
            if (readySince < 0) { readySince = EditorApplication.timeSinceStartup; AssetDatabase.Refresh(); return; }
            if (EditorApplication.timeSinceStartup - readySince < 4.0) return;

            if (SessionState.GetBool(StartedKey, false))
            {
                // 플레이 모드가 스스로 끝났다면 AutoPlay가 종료하지 못한 것 → 실패로 종료
                Debug.LogError("SMOKE: play mode ended unexpectedly");
                EditorApplication.Exit(4);
                return;
            }
            SessionState.SetBool(StartedKey, true);
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
