using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 적 스폰과 모든 적의 갱신을 담당한다.
    /// 적의 이동은 스킬들이 FixedUpdate에서 외부 힘을 더한 뒤에 계산되어야 하므로 실행 순서를 늦춘다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EnemySpawner : MonoBehaviour
    {
        readonly List<Enemy> pendingRelease = new List<Enemy>(32);
        float spawnTimer;
        GameManager gm;

        public Enemy Boss { get; private set; }

        public void Init(GameManager manager)
        {
            gm = manager;
            spawnTimer = 0.5f;
        }

        /// <summary>죽은 적은 같은 프레임에 재사용되지 않도록 다음 프레임에 풀로 돌려보낸다</summary>
        public void QueueRelease(Enemy e) => pendingRelease.Add(e);

        void Update()
        {
            for (int i = 0; i < pendingRelease.Count; i++) pendingRelease[i].Release();
            pendingRelease.Clear();

            if (gm == null || gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            GameConfig cfg = gm.ConfigAsset;
            PlayerController player = gm.Player;

            UpdateSpawning(dt, cfg, player);

            List<Enemy> list = Enemy.Active;
            float despawnSqr = cfg.despawnDistance * cfg.despawnDistance;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                Enemy e = list[i];
                e.Tick(dt, player, gm.Database);
                // 너무 멀어진 일반 적은 플레이어 주변 화면 밖으로 재배치
                if (!e.IsDead && !e.IsBoss && BossArena.Active == null &&
                    (e.Position - player.Position).sqrMagnitude > despawnSqr)
                    e.Teleport(RandomSpawnPoint(player.Position, cfg));
            }
        }

        void FixedUpdate()
        {
            if (gm == null || gm.State != GameState.Playing) return;
            float dt = Time.fixedDeltaTime;
            GameConfig cfg = gm.ConfigAsset;
            PlayerController player = gm.Player;
            List<Enemy> list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                list[i].FixedTick(dt, player, cfg);
            }
        }

        void UpdateSpawning(float dt, GameConfig cfg, PlayerController player)
        {
            float rate = 1f;
            if (gm.BossPhase)
            {
                rate = cfg.bossSpawnRateMultiplier;
                if (rate <= 0f) return;
            }

            spawnTimer -= dt * rate * gm.StageSpawnMul;
            int guard = 0;
            while (spawnTimer <= 0f && guard++ < 8)
            {
                float progress = Mathf.Clamp01(gm.Elapsed / Mathf.Max(1f, gm.RunDuration));
                spawnTimer += Mathf.Lerp(cfg.spawnIntervalStart, cfg.spawnIntervalEnd, progress);
                if (Enemy.Active.Count >= cfg.maxEnemies) continue;

                EnemyData data = PickEnemy(gm.Elapsed);
                if (data == null) continue;
                float hpMul = (1f + gm.Elapsed / 60f * cfg.enemyHpGrowthPerMinute) * gm.StageHpMul;
                Vector2 pos = gm.BossPhase && BossArena.Active != null
                    ? BossArena.Active.RandomEdgePoint(data.radius)
                    : RandomSpawnPoint(player.Position, cfg);
                Enemy.Spawn(data, pos, hpMul);
            }
        }

        EnemyData PickEnemy(float elapsed)
        {
            List<SpawnEntry> table = gm.Database.spawnTable;
            float total = 0f;
            for (int i = 0; i < table.Count; i++)
                if (IsAvailable(table[i], elapsed)) total += table[i].weight;
            if (total <= 0f) return null;
            float roll = Random.value * total;
            for (int i = 0; i < table.Count; i++)
            {
                if (!IsAvailable(table[i], elapsed)) continue;
                roll -= table[i].weight;
                if (roll <= 0f) return table[i].enemy;
            }
            return null;
        }

        static bool IsAvailable(SpawnEntry entry, float elapsed)
        {
            if (entry.enemy == null || entry.weight <= 0f) return false;
            if (elapsed < entry.startTime) return false;
            return entry.endTime <= 0f || elapsed < entry.endTime;
        }

        /// <summary>화면 밖 원 위의 무작위 지점</summary>
        static Vector2 RandomSpawnPoint(Vector2 center, GameConfig cfg)
        {
            Vector2 half = Gfx.ViewHalfExtents;
            float radius = half.magnitude + cfg.spawnMargin;
            return center + Gfx.Dir(Random.Range(0f, 360f)) * radius;
        }

        public Enemy SpawnBoss(Vector2 pos)
        {
            EnemyData data = gm.Database.boss;
            Boss = Enemy.Spawn(data, pos, gm.StageHpMul);
            var brain = Boss.gameObject.GetComponent<BossController>();
            if (brain == null) brain = Boss.gameObject.AddComponent<BossController>();
            brain.Init(Boss, gm);
            return Boss;
        }

        /// <summary>아레나 밖의 일반 적을 보상 없이 제거한다</summary>
        public void RemoveEnemiesOutside(Vector2 center, float radius)
        {
            List<Enemy> list = Enemy.Active;
            float sqr = radius * radius;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Enemy e = list[i];
                if (e.IsBoss) continue;
                if ((e.Position - center).sqrMagnitude > sqr) e.Kill(false);
            }
        }
    }
}
