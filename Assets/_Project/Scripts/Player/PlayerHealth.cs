using UnityEngine;

namespace ElementMage
{
    public class PlayerHealth : MonoBehaviour
    {
        public float MaxHp { get; private set; }
        public float Hp { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>테스트용 무적</summary>
        public static bool DebugInvincible;

        GameManager gm;
        SpriteRenderer sr;
        float invincibleTimer;
        float flashTimer;

        public void Init(GameManager manager, SpriteRenderer visual)
        {
            gm = manager;
            sr = visual;
            MaxHp = gm.ConfigAsset.playerMaxHp;
            Hp = MaxHp;
        }

        /// <summary>장비의 최대 체력 보너스를 적용하고 체력을 가득 채운다 (판 시작 시)</summary>
        public void ApplyMaxHpBonus(float bonus)
        {
            MaxHp = gm.ConfigAsset.playerMaxHp * (1f + bonus);
            Hp = MaxHp;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            Vector2 pos = transform.position;
            Fx.Glow(pos, 2.4f, new Color(0.4f, 1f, 0.5f, 0.55f), 0.4f);
            Fx.Shockwave(pos, 1.6f, new Color(0.5f, 1f, 0.6f, 0.7f), 0.4f);
            for (int i = 0; i < 10; i++)
                Vfx.Glow(pos + Random.insideUnitCircle * 0.6f, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 3f)),
                    Random.Range(0.12f, 0.22f), Random.Range(0.5f, 0.8f), new Color(0.5f, 1f, 0.6f, 0.9f));
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || invincibleTimer > 0f || amount <= 0f) return;
            if (gm.State != GameState.Playing) return;
            invincibleTimer = gm.ConfigAsset.playerHitInvincibleTime;
            flashTimer = 0.2f;
            if (DebugInvincible) return;

            Hp = Mathf.Max(0f, Hp - amount);
            gm.Hud.OnPlayerHit();
            // 피격 연출: 붉은 파편 + 화면 흔들림 + 짧은 히트스톱
            Vector2 pos = transform.position;
            Vfx.Sparks(pos, new Color(1f, 0.35f, 0.3f), 8, 4f, 9f);
            Fx.Shockwave(pos, 1.3f, new Color(1f, 0.3f, 0.3f, 0.7f), 0.3f);
            Juice.Shake(0.5f);
            Juice.HitStop(0.07f);
            if (Hp <= 0f)
            {
                IsDead = true;
                gm.OnPlayerDead();
            }
        }

        void Update()
        {
            if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                sr.color = flashTimer > 0f ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
        }
    }
}
