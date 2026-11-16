using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 보스 패턴 하나: 예고선(빨간 선) 표시 → 돌진.
    /// 이동 자체는 Enemy가 처리하고, 여기서는 MoveOverride만 바꾼다.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        enum Phase { Chase, Telegraph, Dash }

        Enemy enemy;
        GameManager gm;
        Phase phase;
        float timer;
        Vector2 dashDir;
        SpriteRenderer line;

        public void Init(Enemy owner, GameManager manager)
        {
            enemy = owner;
            gm = manager;
            phase = Phase.Chase;
            timer = gm.ConfigAsset.bossDashCooldown;
            if (line == null)
            {
                line = Gfx.NewSprite("DashTelegraph", null, gm.Database.square, Gfx.OrderZone + 5, new Color(1f, 0.15f, 0.1f, 0.5f));
            }
            line.gameObject.SetActive(false);
            enabled = true;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || gm.State != GameState.Playing) return;
            GameConfig cfg = gm.ConfigAsset;
            timer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Chase:
                    if (timer <= 0f)
                    {
                        Vector2 to = gm.Player.Position - enemy.Position;
                        dashDir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.up;
                        phase = Phase.Telegraph;
                        timer = cfg.bossTelegraphTime;
                        enemy.MoveOverride = Vector2.zero;
                        line.gameObject.SetActive(true);
                    }
                    break;

                case Phase.Telegraph:
                {
                    // 예고선: 보스 위치에서 돌진 방향으로 뻗는 빨간 띠
                    float length = cfg.bossDashSpeed * cfg.bossDashDuration + enemy.Radius;
                    float width = enemy.Radius * 2f;
                    Transform lt = line.transform;
                    lt.position = enemy.Position + dashDir * (length * 0.5f);
                    lt.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(dashDir));
                    lt.localScale = Gfx.ScaleFor(line.sprite, length, width);
                    float blink = 0.35f + 0.25f * Mathf.Sin(Time.time * 18f);
                    line.color = new Color(1f, 0.15f, 0.1f, blink);
                    if (timer <= 0f)
                    {
                        phase = Phase.Dash;
                        timer = cfg.bossDashDuration;
                        enemy.MoveOverride = dashDir * cfg.bossDashSpeed;
                        enemy.ContactDamageMultiplier = cfg.bossDashDamageMultiplier;
                        line.gameObject.SetActive(false);
                    }
                    break;
                }

                case Phase.Dash:
                    if (timer <= 0f)
                    {
                        phase = Phase.Chase;
                        timer = cfg.bossDashCooldown;
                        enemy.MoveOverride = null;
                        enemy.ContactDamageMultiplier = 1f;
                    }
                    break;
            }
        }

        void OnDisable()
        {
            if (line != null) line.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
        }
    }

    /// <summary>
    /// 보스전 원형 아레나. 플레이어와 적 모두 경계 밖으로 나갈 수 없다.
    /// </summary>
    public class BossArena : MonoBehaviour
    {
        public static BossArena Active { get; private set; }

        public Vector2 Center { get; private set; }
        public float Radius { get; private set; }

        LineRenderer ring;
        SpriteRenderer glow;

        public static void ResetStatics() => Active = null;

        public static BossArena Create(Vector2 center, float radius, GameDatabase db)
        {
            var go = new GameObject("BossArena");
            var arena = go.AddComponent<BossArena>();
            arena.Center = center;
            arena.Radius = radius;
            go.transform.position = center;

            // 테두리 원
            arena.ring = go.AddComponent<LineRenderer>();
            const int Segments = 96;
            arena.ring.useWorldSpace = false;
            arena.ring.loop = true;
            arena.ring.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
                arena.ring.SetPosition(i, Gfx.Dir(i * 360f / Segments) * radius);
            arena.ring.widthMultiplier = 0.22f;
            if (db.spriteMaterial != null) arena.ring.sharedMaterial = db.spriteMaterial;
            arena.ring.sortingOrder = Gfx.OrderZone + 6;
            arena.ring.startColor = arena.ring.endColor = new Color(1f, 0.25f, 0.3f, 0.95f);

            // 바닥을 살짝 물들여 영역을 더 분명하게 한다
            arena.glow = Gfx.NewSprite("ArenaFloor", go.transform, db.circle, Gfx.OrderGround + 1, new Color(0.6f, 0.05f, 0.1f, 0.14f));
            Gfx.SetDiameter(arena.glow.transform, db.circle, radius * 2f);

            Active = arena;
            return arena;
        }

        void Update()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            ring.startColor = ring.endColor = new Color(1f, 0.25f, 0.3f, pulse);
        }

        /// <summary>반지름 bodyRadius인 물체가 아레나 안에 머물도록 위치를 보정한다</summary>
        public Vector2 Clamp(Vector2 pos, float bodyRadius)
        {
            float limit = Mathf.Max(0.1f, Radius - bodyRadius);
            Vector2 rel = pos - Center;
            if (rel.sqrMagnitude <= limit * limit) return pos;
            return Center + rel.normalized * limit;
        }

        public Vector2 RandomEdgePoint(float bodyRadius)
        {
            return Center + Gfx.Dir(Random.Range(0f, 360f)) * Mathf.Max(0.1f, Radius - bodyRadius - 0.1f);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
