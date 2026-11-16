using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementMage
{
    /// <summary>
    /// 한 판의 흐름을 관리한다: 원소 선택 → 시작 스킬 선택 → 전투 → 10분 뒤 보스 → 승리/패배.
    /// 씬에는 이 컴포넌트와 카메라만 있으면 되고, 나머지(플레이어·UI·스포너)는 여기서 코드로 조립한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public static GameDatabase Db => I != null ? I.database : null;
        public static GameConfig Config => I != null && I.database != null ? I.database.config : null;

        /// <summary>테스트용: 0보다 크면 보스 등장 시간을 이 값으로 덮어쓴다 (에셋은 건드리지 않음)</summary>
        public static float DebugRunDuration;

        [SerializeField] GameDatabase database;
        [Tooltip("0보다 크면 GameConfig.runDuration 대신 이 값을 쓴다 (보스 테스트용)")]
        [SerializeField] float runDurationOverride;

        public GameDatabase Database => database;
        public GameConfig ConfigAsset => database.config;
        public float RunDuration => DebugRunDuration > 0f ? DebugRunDuration
            : runDurationOverride > 0f ? runDurationOverride : database.config.runDuration;

        public GameState State { get; private set; }
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public bool BossPhase { get; private set; }
        public Element SlotA { get; private set; }
        public Element SlotB { get; private set; }

        public Camera Cam { get; private set; }
        public PlayerController Player { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public VirtualJoystick Joystick { get; private set; }
        public HudUI Hud { get; private set; }
        public LevelUpUI LevelUpScreen { get; private set; }
        public Canvas MainCanvas { get; private set; }

        LevelUpManager levelUps;
        LobbyUI lobby;
        GroundScroller ground;

        // 스테이지 (로비에서 선택). 배율은 스테이지가 없으면 1
        public StageData Stage { get; private set; }
        public int StageIndex { get; private set; }
        public float StageHpMul => Stage != null ? Stage.enemyHpMultiplier : 1f;
        public float StageDamageMul => Stage != null ? Stage.enemyDamageMultiplier : 1f;
        public float StageSpawnMul => Stage != null ? Stage.spawnRateMultiplier : 1f;
        public LobbyUI Lobby => lobby;
        ResultUI result;

        void Awake()
        {
            I = this;
            if (database == null || database.config == null)
            {
                Debug.LogError("GameManager: GameDatabase가 연결되지 않았습니다. 메뉴 ElementMage > 프로젝트 구성 다시 만들기를 실행하세요.");
                enabled = false;
                return;
            }

            // 씬을 다시 불러와도 정적 상태가 남지 않게 초기화
            Enemy.ResetStatics();
            XpOrb.ResetStatics();
            BossArena.ResetStatics();
            DamageSystem.ResetStatics();
            DamagePopup.ResetStatics();
            Juice.Reset();
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;

            SetupCamera();
            ground = new GameObject("Ground").AddComponent<GroundScroller>();
            ground.Init(this);

            var playerGo = new GameObject("Player");
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(this);
            Cam.gameObject.AddComponent<CameraFollow>().Init(Player.transform);

            Spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            Spawner.Init(this);

            levelUps = new LevelUpManager(this);
            BuildUI();
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Cam = go.AddComponent<Camera>();
            }
            Cam.orthographic = true;
            Cam.orthographicSize = ConfigAsset.cameraOrthoSize;
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.backgroundColor = new Color(0.07f, 0.1f, 0.12f);
        }

        void BuildUI()
        {
            UIFactory.EnsureEventSystem();
            MainCanvas = UIFactory.CreateCanvas("Canvas", 0);
            Transform canvas = MainCanvas.transform;
            Joystick = VirtualJoystick.Create(canvas, database);
            Hud = HudUI.Create(canvas, this);
            LevelUpScreen = LevelUpUI.Create(canvas, database);
            result = ResultUI.Create(canvas, database, Restart);
            lobby = LobbyUI.Create(canvas, this, StartRun);
            Hud.SetVisible(false);
        }

        void Start()
        {
            if (!enabled) return;
            State = GameState.ElementSelect;
            Time.timeScale = 0f;
        }

        public bool HasElement(Element e) => SlotA == e || SlotB == e;

        // ---------- 시작 ----------

        /// <summary>로비에서 고른 스테이지를 바닥 색으로 미리 보여 준다</summary>
        public void PreviewStage(StageData stage)
        {
            if (ground != null && stage != null) ground.SetTint(stage.groundTint);
        }

        /// <summary>저장된 스테이지로 바로 시작 (테스트용)</summary>
        public void ChooseElements(Element a, Element b)
        {
            StartRun(a, b, Mathf.Min(SaveData.SelectedStage, SaveData.UnlockedStage));
        }

        public void StartRun(Element a, Element b, int stageIndex)
        {
            if (State != GameState.ElementSelect) return;
            SlotA = a;
            SlotB = b;
            if (database.stages != null && database.stages.Count > 0)
            {
                StageIndex = Mathf.Clamp(stageIndex, 0, database.stages.Count - 1);
                Stage = database.stages[StageIndex];
                PreviewStage(Stage);
            }

            // 장착 장비 효과 적용
            EquipmentBonus bonus = EquipmentBonus.Compute(database);
            Player.Skills.SetEquipmentBonus(bonus);
            Player.Health.ApplyMaxHpBonus(bonus.maxHp);

            lobby.Hide();
            Hud.SetVisible(true);

            // 시작 스킬: 장착한 원소의 일반기 중 3개에서 하나를 골라 Lv.1로 시작
            List<LevelUpOption> options = levelUps.BuildStartOptions();
            if (options.Count == 0) { Resume(); return; }
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            LevelUpScreen.Show("시작 스킬 선택", "처음 사용할 스킬을 고르세요", options, OnOptionPicked);
        }

        // ---------- 레벨업 ----------

        /// <returns>선택 창을 열었으면 true. 후보가 0개면 창을 열지 않고 false</returns>
        bool OpenLevelUp()
        {
            List<LevelUpOption> options = levelUps.BuildOptions();
            // TODO(미정): 후보 0개일 때의 대체 카드. 지금은 창을 열지 않고 그냥 넘어간다.
            if (options.Count == 0) return false;
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            Joystick.ResetInput();
            LevelUpScreen.Show("레벨 업!", "Lv." + Player.Experience.Level, options, OnOptionPicked);
            return true;
        }

        void OnOptionPicked(LevelUpOption option)
        {
            levelUps.Apply(option);
            Hud.RefreshSkills();
            // 한 번에 여러 레벨이 올랐으면 이어서 연다
            while (Player.Experience.ConsumePendingLevelUp())
                if (OpenLevelUp()) return;
            Resume();
        }

        void Resume()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
        }

        // ---------- 진행 ----------

        void Update()
        {
            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;
            Elapsed += dt;

            XpOrb.TickAll(dt, Player, ConfigAsset);

            if (!BossPhase && Elapsed >= RunDuration) StartBossPhase();

            while (State == GameState.Playing && Player.Experience.ConsumePendingLevelUp())
                if (OpenLevelUp()) break;
        }

        void StartBossPhase()
        {
            BossPhase = true;
            GameConfig cfg = ConfigAsset;
            Vector2 center = Player.Position;
            BossArena.Create(center, cfg.bossArenaRadius, database);
            // 등장 시점에 아레나 밖에 있는 일반 적은 제거
            Spawner.RemoveEnemiesOutside(center, cfg.bossArenaRadius);

            EnemyData boss = database.boss;
            Vector2 spawn = center + Vector2.up * Mathf.Max(0.5f, cfg.bossArenaRadius - boss.radius - 0.3f);
            Enemy bossEnemy = Spawner.SpawnBoss(spawn);
            Hud.ShowBoss(bossEnemy);
            Hud.ShowBanner("보스 등장!", 2.5f);
            XpOrb.AttractAll();
        }

        public void OnEnemyKilled(Enemy enemy, bool rewarded)
        {
            if (rewarded) Kills++;
            if (enemy.IsBoss && rewarded) Finish(true);
        }

        public void OnPlayerDead() => Finish(false);

        void Finish(bool victory)
        {
            if (State == GameState.Victory || State == GameState.Defeat) return;
            State = victory ? GameState.Victory : GameState.Defeat;
            // 승리하면 다음 스테이지가 열린다
            if (victory && StageIndex >= SaveData.UnlockedStage) SaveData.UnlockedStage = StageIndex + 1;
            Time.timeScale = 0f;
            Joystick.ResetInput();
            LevelUpScreen.Hide();
            Hud.HideBanner();
            result.Show(victory, Elapsed, Player.Experience.Level, Kills);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }

    /// <summary>카메라가 플레이어를 부드럽게 따라간다</summary>
    public class CameraFollow : MonoBehaviour
    {
        Transform target;
        Vector3 basePos;

        public void Init(Transform follow)
        {
            target = follow;
            basePos = transform.position;
        }

        void LateUpdate()
        {
            if (target == null) return;
            // 흔들림은 따라가는 위치 위에 덧씌운다 (흔들림이 추적 계산에 섞이지 않게 기준 위치를 따로 둔다)
            Vector3 goal = new Vector3(target.position.x, target.position.y, basePos.z);
            basePos = Vector3.Lerp(basePos, goal, 1f - Mathf.Exp(-14f * Time.deltaTime));
            Vector2 shake = Juice.Tick(Time.unscaledDeltaTime);
            transform.position = basePos + new Vector3(shake.x, shake.y, 0f);
        }
    }

    /// <summary>
    /// 무한 바닥. 타일 몇 장을 카메라 주변에 격자로 깔고, 카메라가 움직이면 타일 크기 단위로 다시 맞춘다.
    /// </summary>
    public class GroundScroller : MonoBehaviour
    {
        const float TileSize = 8f;
        const int Cols = 3, Rows = 5;

        GameManager gm;
        Transform[] tiles;
        SpriteRenderer[] renderers;

        public void SetTint(Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) renderers[i].color = color;
        }

        public void Init(GameManager manager)
        {
            gm = manager;
            Sprite sprite = gm.Database.groundTile;
            tiles = new Transform[Cols * Rows];
            renderers = new SpriteRenderer[Cols * Rows];
            for (int i = 0; i < tiles.Length; i++)
            {
                SpriteRenderer sr = Gfx.NewSprite("Tile", transform, sprite, Gfx.OrderGround, Color.white);
                // 타일 사이에 틈이 보이지 않도록 아주 조금 크게 깐다
                sr.transform.localScale = Gfx.ScaleFor(sprite, TileSize * 1.004f, TileSize * 1.004f);
                tiles[i] = sr.transform;
                renderers[i] = sr;
            }
            Reposition();
        }

        void LateUpdate() => Reposition();

        void Reposition()
        {
            if (gm == null || gm.Cam == null) return;
            Vector3 cam = gm.Cam.transform.position;
            float baseX = Mathf.Round(cam.x / TileSize) * TileSize;
            float baseY = Mathf.Round(cam.y / TileSize) * TileSize;
            int i = 0;
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
            {
                tiles[i++].position = new Vector3(
                    baseX + (x - (Cols - 1) * 0.5f) * TileSize,
                    baseY + (y - (Rows - 1) * 0.5f) * TileSize, 0f);
            }
        }
    }
}
