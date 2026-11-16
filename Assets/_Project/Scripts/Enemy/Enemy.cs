using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 적 한 마리. 추적 이동, 체력, 외부 힘 누적(2-3), 상태이상(2-4)을 가진다.
    /// 보스도 이 스크립트를 그대로 쓰고 수치만 다르다.
    /// Update/FixedUpdate는 EnemySpawner가 한꺼번에 호출한다.
    /// </summary>
    public class Enemy : Pooled
    {
        public static readonly List<Enemy> Active = new List<Enemy>(256);
        static readonly Dictionary<Collider2D, Enemy> byCollider = new Dictionary<Collider2D, Enemy>(256);
        static int uidCounter;

        public static Enemy FromCollider(Collider2D c)
        {
            return byCollider.TryGetValue(c, out Enemy e) ? e : null;
        }

        public static void ResetStatics()
        {
            Active.Clear();
            byCollider.Clear();
        }

        public EnemyData Data { get; private set; }
        /// <summary>스폰될 때마다 새로 받는 번호. 풀 재사용 시 이전 개체와 구분하는 데 쓴다.</summary>
        public int Uid { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsBoss { get; private set; }
        public bool IsDead { get; private set; }
        public float Radius { get; private set; }
        public Vector2 Position => rb.position;

        /// <summary>값이 있으면 자체 추적 이동 대신 이 속도를 쓴다 (보스 돌진용)</summary>
        public Vector2? MoveOverride;
        public float ContactDamageMultiplier = 1f;

        // 상태이상
        public float BurnTimer { get; private set; }
        public float WetTimer { get; private set; }
        public float NextReactionTime;
        float burnTickTimer;

        Rigidbody2D rb;
        CircleCollider2D col;
        SpriteRenderer sr;
        Transform visual;
        float baseScale;
        float hitPop;
        float flashTimer;
        Material normalMaterial;

        // 외부 힘
        Vector2 continuousForce;
        Vector2 impulse;
        float impulseTimer;

        protected override void OnCreated()
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            col = gameObject.AddComponent<CircleCollider2D>();
            sr = Gfx.NewSprite("Visual", transform, null, Gfx.OrderEnemy, Color.white);
            visual = sr.transform;
            normalMaterial = sr.sharedMaterial;
            byCollider[col] = this;
        }

        public static Enemy Spawn(EnemyData data, Vector2 pos, float hpMultiplier)
        {
            Enemy e = Pool<Enemy>.Get();
            e.Init(data, pos, hpMultiplier);
            return e;
        }

        void Init(EnemyData data, Vector2 pos, float hpMultiplier)
        {
            Data = data;
            Uid = ++uidCounter;
            IsBoss = data.isBoss;
            IsDead = false;
            MaxHp = data.maxHp * hpMultiplier;
            Hp = MaxHp;
            Radius = data.radius;
            MoveOverride = null;
            ContactDamageMultiplier = 1f;
            BurnTimer = 0f;
            WetTimer = 0f;
            NextReactionTime = 0f;
            burnTickTimer = 0f;
            continuousForce = Vector2.zero;
            impulse = Vector2.zero;
            impulseTimer = 0f;
            hitPop = 0f;
            flashTimer = 0f;
            sr.sharedMaterial = normalMaterial;

            transform.position = pos;
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            rb.mass = IsBoss ? 200f : 1f;
            col.radius = Radius;

            sr.sprite = data.sprite;
            sr.color = data.tint;
            sr.sortingOrder = IsBoss ? Gfx.OrderEnemy + 1 : Gfx.OrderEnemy;
            baseScale = Gfx.ScaleForWidth(data.sprite, data.visualWidth);
            visual.localScale = Vector3.one * baseScale;

            if (!byCollider.ContainsKey(col)) byCollider[col] = this;
            Active.Add(this);
        }

        // ---------- 외부 힘 (스킬은 이 두 함수만 호출한다) ----------

        /// <summary>지속형 힘. 스킬의 FixedUpdate에서 매 물리 프레임 호출한다.</summary>
        public void AddContinuousForce(Vector2 force)
        {
            continuousForce += force;
        }

        /// <summary>순간형 힘. duration 동안 이 힘만 적용된다. 보스는 무시한다.</summary>
        public void ApplyImpulse(Vector2 force, float duration)
        {
            if (IsBoss) return;
            impulse = force;
            impulseTimer = duration;
        }

        // ---------- 갱신 ----------

        public void FixedTick(float dt, PlayerController player, GameConfig cfg)
        {
            if (IsDead) return;
            Vector2 pos = rb.position;
            Vector2 toPlayer = player.Position - pos;

            // 1) 자체 추적 이동 (젖음 감속은 여기에만 적용)
            Vector2 self;
            bool overridden = MoveOverride.HasValue;
            if (overridden)
            {
                self = MoveOverride.Value;
            }
            else
            {
                float speed = Data.moveSpeed;
                if (WetTimer > 0f) speed *= 1f - cfg.wetSlow * (IsBoss ? cfg.bossWetSlowFactor : 1f);
                float dist = toPlayer.magnitude;
                self = dist > 0.05f ? toPlayer / dist * speed : Vector2.zero;
            }

            // 2) 외부 힘: 순간형이 걸려 있으면 그 힘만, 아니면 지속형 합계(상한 적용)
            Vector2 ext;
            if (impulseTimer > 0f && !IsBoss)
            {
                impulseTimer -= dt;
                ext = impulse;
            }
            else
            {
                ext = Vector2.ClampMagnitude(continuousForce, cfg.maxContinuousForce);
            }
            if (IsBoss) ext *= cfg.bossForceFactor;
            continuousForce = Vector2.zero;

            // 3) 합산 후 전체 속도 상한 (보스 돌진은 상한 예외)
            Vector2 vel = self + ext;
            if (!overridden) vel = Vector2.ClampMagnitude(vel, cfg.maxEnemySpeed);
            rb.linearVelocity = vel;

            // 보스전 아레나 경계
            if (BossArena.Active != null)
            {
                Vector2 clamped = BossArena.Active.Clamp(pos, Radius);
                if (clamped != pos) rb.position = clamped;
            }

            // 접촉 피해
            float reach = Radius + cfg.playerRadius;
            if (toPlayer.sqrMagnitude <= reach * reach)
                player.Health.TakeDamage(Data.contactDamage * ContactDamageMultiplier * GameManager.I.StageDamageMul);
        }

        public void Tick(float dt, PlayerController player, GameDatabase db)
        {
            if (IsDead) return;
            GameConfig cfg = db.config;

            if (WetTimer > 0f) WetTimer -= dt;
            if (BurnTimer > 0f)
            {
                BurnTimer -= dt;
                burnTickTimer -= dt;
                if (burnTickTimer <= 0f)
                {
                    burnTickTimer += cfg.burnTickInterval;
                    // 화상 틱은 상태이상을 다시 걸지 않는다
                    DamageSystem.Deal(this, cfg.burnDps * cfg.burnTickInterval, Element.Fire, false);
                    if (IsDead) return;
                }
            }

            // 상태이상 색조 표시
            Color c = Data.tint;
            if (BurnTimer > 0f && WetTimer > 0f) c *= Color.Lerp(db.burnTint, db.wetTint, 0.5f);
            else if (BurnTimer > 0f) c *= db.burnTint;
            else if (WetTimer > 0f) c *= db.wetTint;
            sr.color = c;

            sr.flipX = player.Position.x < rb.position.x;
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) sr.sharedMaterial = normalMaterial;
            }
            if (hitPop > 0f)
            {
                // 맞는 순간 옆으로 눌렸다가 돌아오는 탄성
                hitPop = Mathf.Max(0f, hitPop - dt * 7f);
                float squash = hitPop * hitPop;
                visual.localScale = new Vector3(baseScale * (1f + squash * 0.3f), baseScale * (1f - squash * 0.16f), baseScale);
            }
        }

        // ---------- 피해·상태이상 (DamageSystem 전용) ----------

        /// <param name="feedback">피격 연출(흰색 번쩍임·눌림)을 낼지. 화상 틱 같은 잔피해는 false.</param>
        internal void ReceiveDamage(float amount, bool feedback = true)
        {
            Hp -= amount;
            if (!feedback) return;
            hitPop = 1f;
            Material flash = Vfx.Flash;
            if (flash != null)
            {
                flashTimer = IsBoss ? 0.05f : 0.075f;
                sr.sharedMaterial = flash;
            }
        }

        internal void SetBurn(float duration)
        {
            if (BurnTimer <= 0f) burnTickTimer = GameManager.Config.burnTickInterval;
            BurnTimer = duration;
        }

        internal void SetWet(float duration) => WetTimer = duration;

        internal void ClearStatuses()
        {
            BurnTimer = 0f;
            WetTimer = 0f;
        }

        public void Teleport(Vector2 pos)
        {
            transform.position = pos;
            rb.position = pos;
        }

        /// <summary>처치 처리. dropXp가 false면 보상 없이 제거한다.</summary>
        public void Kill(bool dropXp)
        {
            if (IsDead) return;
            IsDead = true;
            Active.Remove(this);
            MoveOverride = null;
            if (dropXp)
            {
                XpOrb.Drop(Position, Data.xp);
                Fx.Puff(Position, Data.visualWidth * 1.3f, Gfx.WithAlpha(Color.white, 0.7f));
                Vfx.Death(Position, Data.visualWidth);
                Juice.Shake(IsBoss ? 0.9f : 0.035f);
            }
            gameObject.SetActive(false);
            GameManager.I.Spawner.QueueRelease(this);
            GameManager.I.OnEnemyKilled(this, dropXp);
        }
    }
}
