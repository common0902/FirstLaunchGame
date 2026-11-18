using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 경험치 오브와 자석. 개별 Update 없이 XpOrb.TickAll에서 한꺼번에 갱신한다.
    /// 오브는 적 종류와 무관한 등급(소·중·대)을 가지며, 처치 시 확률로 떨어지거나 바닥에 묶음으로 생긴다.
    /// 처치 즉시 경험치는 Enemy.Kill에서 따로 지급한다.
    /// </summary>
    public class XpOrb : Pooled
    {
        public const int TierSmall = 0, TierMid = 1, TierLarge = 2;

        static readonly List<XpOrb> active = new List<XpOrb>(256);

        /// <summary>판 통계 (테스트 리포트용)</summary>
        public static float XpFromKills, XpFromOrbs;
        public static int MagnetsPicked;

        Transform tr, visual;
        SpriteRenderer sr, glow;
        int value, tier;
        bool attracted, isMagnet;
        float speed, phase;

        /// <summary>바닥 오브 묶음 번호 (0 = 처치 드롭)</summary>
        public int Cluster { get; private set; }

        public static void ResetStatics()
        {
            active.Clear();
            XpFromKills = 0f;
            XpFromOrbs = 0f;
            MagnetsPicked = 0;
        }

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            glow = Gfx.NewGlow("Glow", tr, db.softCircle, Gfx.OrderOrb - 1, Color.white);
            sr = Gfx.NewSprite("Visual", tr, db.xpOrb, Gfx.OrderOrb, Color.white);
            visual = sr.transform;
        }

        // ------------------------------------------------------------------ 생성

        /// <summary>적 처치 시: 확률로 오브 하나를 떨어뜨린다 (등급은 확률로 결정)</summary>
        public static void DropFromKill(Vector2 pos, EnemyData data)
        {
            GameConfig cfg = GameManager.Config;
            if (data == null || data.isBoss || Random.value >= data.orbDropChance) return;
            int tier = RollTier(cfg);
            Drop(pos, TierValue(cfg, tier), 0);
        }

        static int RollTier(GameConfig cfg)
        {
            float[] chances = cfg.orbTierChances;
            if (chances == null || chances.Length == 0) return TierSmall;
            float total = 0f;
            for (int i = 0; i < chances.Length; i++) total += Mathf.Max(0f, chances[i]);
            float roll = Random.value * total;
            for (int i = 0; i < chances.Length; i++)
            {
                roll -= Mathf.Max(0f, chances[i]);
                if (roll <= 0f) return Mathf.Min(i, TierLarge);
            }
            return TierSmall;
        }

        public static int TierValue(GameConfig cfg, int tier)
        {
            int[] values = cfg.orbTierValues;
            if (values == null || values.Length == 0) return 1;
            return values[Mathf.Clamp(tier, 0, values.Length - 1)];
        }

        /// <summary>경험치 오브를 만든다. 개수 상한이면 가장 가까운 오브에 경험치를 합친다.</summary>
        public static void Drop(Vector2 pos, int value, int cluster)
        {
            if (value <= 0) return;
            GameConfig cfg = GameManager.Config;
            if (active.Count >= cfg.maxXpOrbs)
            {
                XpOrb nearest = Nearest(pos, false);
                if (nearest != null)
                {
                    nearest.value += value;
                    nearest.ApplyLook(cfg);
                    return;
                }
            }
            XpOrb orb = Spawn(pos, cluster);
            orb.value = value;
            orb.isMagnet = false;
            orb.ApplyLook(cfg);
        }

        /// <summary>자석: 주우면 화면의 오브를 전부 끌어온다</summary>
        public static void DropMagnet(Vector2 pos, int cluster)
        {
            XpOrb orb = Spawn(pos, cluster);
            orb.value = 0;
            orb.isMagnet = true;
            orb.ApplyLook(GameManager.Config);
        }

        static XpOrb Spawn(Vector2 pos, int cluster)
        {
            XpOrb orb = Pool<XpOrb>.Get();
            orb.tr.position = pos + Random.insideUnitCircle * 0.2f;
            orb.attracted = false;
            orb.speed = 0f;
            orb.phase = Random.value * 10f;
            orb.Cluster = cluster;
            active.Add(orb);
            return orb;
        }

        static XpOrb Nearest(Vector2 pos, bool includeMagnet)
        {
            XpOrb best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
            {
                XpOrb o = active[i];
                if (o.isMagnet && !includeMagnet) continue;
                float d = ((Vector2)o.tr.position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = o; }
            }
            return best;
        }

        /// <summary>경험치량에 맞춰 등급·모양을 정한다 (합쳐져서 커진 오브도 다시 계산)</summary>
        void ApplyLook(GameConfig cfg)
        {
            GameDatabase db = GameManager.Db;
            Sprite sprite;
            float size;
            Color glowColor;
            if (isMagnet)
            {
                tier = TierLarge;
                sprite = db.magnet != null ? db.magnet : db.xpOrb;
                size = 0.85f;
                glowColor = new Color(1f, 0.82f, 0.3f, 0.55f);
            }
            else
            {
                tier = value >= TierValue(cfg, TierLarge) ? TierLarge : value >= TierValue(cfg, TierMid) ? TierMid : TierSmall;
                sprite = tier == TierLarge && db.xpOrbLarge != null ? db.xpOrbLarge
                       : tier == TierMid && db.xpOrbMid != null ? db.xpOrbMid : db.xpOrb;
                float[] sizes = cfg.orbTierSizes;
                size = sizes != null && sizes.Length > tier ? sizes[tier] : 0.38f;
                glowColor = tier == TierLarge ? new Color(0.8f, 0.45f, 1f, 0.5f)
                          : tier == TierMid ? new Color(0.35f, 0.6f, 1f, 0.35f) : new Color(0f, 0f, 0f, 0f);
            }
            sr.sprite = sprite;
            Gfx.SetDiameter(visual, sprite, size);
            glow.color = glowColor;
            glow.enabled = tier > TierSmall || isMagnet;
            if (glow.enabled) Gfx.SetDiameter(glow.transform, glow.sprite, size * 2.6f);
        }

        // ------------------------------------------------------------------ 갱신

        public static void TickAll(float dt, PlayerController player, GameConfig cfg)
        {
            Vector2 p = player.Position;
            float pickSqr = cfg.pickupRadius * cfg.pickupRadius;
            float time = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;   // 자석이 목록을 건드린 경우
                XpOrb orb = active[i];
                Vector2 pos = orb.tr.position;
                Vector2 to = p - pos;
                float sqr = to.sqrMagnitude;
                if (!orb.attracted && sqr <= pickSqr) orb.attracted = true;

                // 중·대 오브와 자석은 둥실거리며 반짝인다
                if (orb.tier > TierSmall || orb.isMagnet)
                {
                    float t = time * 3f + orb.phase;
                    orb.visual.localPosition = new Vector3(0f, Mathf.Sin(t) * 0.07f, 0f);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.3f);
                    Color gc = orb.glow.color;
                    gc.a = (orb.tier == TierLarge || orb.isMagnet ? 0.55f : 0.35f) * pulse;
                    orb.glow.color = gc;
                }
                if (!orb.attracted) continue;

                orb.speed = Mathf.MoveTowards(orb.speed, cfg.orbMagnetSpeed, cfg.orbMagnetSpeed * 3f * dt);
                float step = orb.speed * dt;
                if (sqr <= 0.09f || step * step >= sqr)
                {
                    active[i] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                    orb.Collect(player);
                    continue;
                }
                orb.tr.position = pos + to / Mathf.Sqrt(sqr) * step;
            }
        }

        void Collect(PlayerController player)
        {
            Vector2 pos = player.Position;
            bool magnet = isMagnet;
            int v = value;
            int t = tier;
            Release();
            if (magnet)
            {
                MagnetsPicked++;
                AttractAll();
                Fx.Shockwave(pos, 3f, new Color(1f, 0.85f, 0.35f, 0.8f), 0.5f);
                Fx.Glow(pos, 4f, new Color(1f, 0.8f, 0.3f, 0.5f), 0.35f);
                Juice.Shake(0.15f);
                return;
            }
            XpFromOrbs += v;
            player.Experience.AddXp(v);
            if (t == TierLarge)
            {
                Fx.Glow(pos, 2.6f, new Color(0.8f, 0.5f, 1f, 0.6f), 0.3f);
                Vfx.Burst(pos, new Color(0.85f, 0.6f, 1f), 10, 2f, 5f, 0.1f, 0.2f, 0.3f, 0.5f);
                Juice.Shake(0.08f);
            }
            else if (t == TierMid)
            {
                Fx.Glow(pos, 1.6f, new Color(0.4f, 0.65f, 1f, 0.5f), 0.22f);
            }
        }

        /// <summary>남은 오브를 전부 끌어당긴다 (보스 등장·자석). 다른 자석은 끌어오지 않는다.</summary>
        public static void AttractAll()
        {
            for (int i = 0; i < active.Count; i++)
                if (!active[i].isMagnet) active[i].attracted = true;
        }

        /// <summary>해당 묶음에서 아직 끌려오지 않은 오브의 중심. 없으면 false.</summary>
        public static bool ClusterCenter(int cluster, out Vector2 center, out bool hasMagnet)
        {
            center = Vector2.zero;
            hasMagnet = false;
            int n = 0;
            for (int i = 0; i < active.Count; i++)
            {
                XpOrb o = active[i];
                if (o.Cluster != cluster || o.attracted) continue;
                center += (Vector2)o.tr.position;
                hasMagnet |= o.isMagnet;
                n++;
            }
            if (n == 0) return false;
            center /= n;
            return true;
        }

        /// <summary>테스트 봇용: 가장 가까운 오브 위치</summary>
        public static bool TryNearest(Vector2 pos, float maxDist, out Vector2 orbPos)
        {
            XpOrb o = Nearest(pos, true);
            orbPos = o != null ? (Vector2)o.tr.position : Vector2.zero;
            return o != null && (orbPos - pos).sqrMagnitude <= maxDist * maxDist;
        }
    }

    /// <summary>
    /// 바닥 오브: 일정 간격으로 화면 바로 밖에 중·대 오브 묶음(가끔 자석)을 만들고,
    /// 화면 밖에 있는 동안 가장자리에 방향 화살표를 띄운다.
    /// </summary>
    public class FloorOrbSpawner : MonoBehaviour
    {
        GameManager gm;
        float timer;
        int clusterCounter;
        int currentCluster;
        SpriteRenderer arrow;

        public void Init(GameManager manager)
        {
            gm = manager;
            timer = gm.ConfigAsset.floorOrbFirstTime;
            GameDatabase db = gm.Database;
            arrow = Gfx.NewSprite("PickupArrow", transform, db.pickupArrow != null ? db.pickupArrow : db.circle, Gfx.OrderPopup - 1, Color.white);
            arrow.gameObject.SetActive(false);
        }

        void Update()
        {
            if (gm == null || gm.State != GameState.Playing) return;
            GameConfig cfg = gm.ConfigAsset;
            if (!gm.BossPhase && cfg.floorOrbInterval > 0f)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    timer += cfg.floorOrbInterval;
                    SpawnCluster(cfg);
                }
            }
        }

        void LateUpdate() => UpdateArrow();

        void SpawnCluster(GameConfig cfg)
        {
            Vector2 half = Gfx.ViewHalfExtents;
            Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
            // 화면 사각형 가장자리까지의 거리 + 여유
            float tx = Mathf.Abs(dir.x) > 0.001f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.001f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 center = gm.Player.Position + dir * (Mathf.Min(tx, ty) + cfg.floorOrbMargin);

            int id = ++clusterCounter;
            currentCluster = id;
            if (Random.value < cfg.floorMagnetChance)
            {
                XpOrb.DropMagnet(center, id);
            }
            else
            {
                int mids = Random.Range(cfg.floorOrbMidMin, cfg.floorOrbMidMax + 1);
                for (int i = 0; i < mids; i++)
                    XpOrb.Drop(center + Gfx.Dir(i * 360f / Mathf.Max(1, mids)) * 0.55f, XpOrb.TierValue(cfg, XpOrb.TierMid), id);
                if (Random.value < cfg.floorOrbLargeChance)
                    XpOrb.Drop(center, XpOrb.TierValue(cfg, XpOrb.TierLarge), id);
            }
        }

        void UpdateArrow()
        {
            bool show = false;
            if (currentCluster != 0 && gm.State == GameState.Playing &&
                XpOrb.ClusterCenter(currentCluster, out Vector2 target, out bool magnet))
            {
                Vector2 cam = gm.Cam.transform.position;
                Vector2 half = Gfx.ViewHalfExtents;
                Vector2 rel = target - cam;
                bool onScreen = Mathf.Abs(rel.x) < half.x - 0.3f && Mathf.Abs(rel.y) < half.y - 0.3f;
                if (!onScreen && rel.sqrMagnitude > 0.01f)
                {
                    show = true;
                    // 화면 가장자리 안쪽에 붙인다 (상단 HUD를 피해 위쪽 여백을 더 둔다)
                    Vector2 inner = new Vector2(half.x - 0.6f, half.y - 1.6f);
                    Vector2 d = rel.normalized;
                    float tx = Mathf.Abs(d.x) > 0.001f ? inner.x / Mathf.Abs(d.x) : float.MaxValue;
                    float ty = Mathf.Abs(d.y) > 0.001f ? inner.y / Mathf.Abs(d.y) : float.MaxValue;
                    Vector2 p = cam + d * Mathf.Min(tx, ty);
                    float bob = Mathf.Sin(Time.time * 6f) * 0.12f;
                    arrow.transform.position = p + d * bob;
                    arrow.transform.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(d));
                    Gfx.SetDiameter(arrow.transform, arrow.sprite, 0.75f);
                    arrow.color = magnet ? new Color(1f, 0.85f, 0.35f) : new Color(0.85f, 0.6f, 1f);
                }
            }
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        }
    }
}
