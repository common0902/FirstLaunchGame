using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 모든 피해가 지나가는 단일 창구. 피해 적용, 원소 상태이상 부여, 원소 반응 판정을 여기서 일괄 처리한다.
    /// </summary>
    public static class DamageSystem
    {
        static readonly List<Enemy> reactionBuffer = new List<Enemy>(32);

        public static float TotalDamage;
        public static int ReactionCount;

        public static void ResetStatics()
        {
            TotalDamage = 0f;
            ReactionCount = 0;
        }

        /// <summary>
        /// 적에게 피해를 준다. applyStatus가 true면 원소에 맞는 상태이상(불=화상, 물=젖음)을 100% 부여한다.
        /// </summary>
        /// <returns>이 피해로 적이 죽었으면 true</returns>
        public static bool Deal(Enemy enemy, float amount, Element element, bool applyStatus = true)
        {
            if (enemy == null || enemy.IsDead || amount <= 0f) return false;

            // 화상 틱 같은 잔피해는 연출 없이 숫자만 띄운다
            bool feedback = applyStatus || element == Element.None;
            enemy.ReceiveDamage(amount, feedback);
            if (feedback) Vfx.Hit(enemy.Position, element);
            TotalDamage += amount;
            DamagePopup.Show(enemy.Position, amount, element);

            if (enemy.Hp <= 0f)
            {
                enemy.Kill(true);
                return true;
            }

            if (applyStatus && element != Element.None)
            {
                GameConfig cfg = GameManager.Config;
                if (element == Element.Fire) enemy.SetBurn(cfg.burnDuration);
                else if (element == Element.Water) enemy.SetWet(cfg.wetDuration);
                TryReaction(enemy, cfg);
            }
            return enemy.IsDead;
        }

        /// <summary>
        /// 증발 폭발(2-5): 화상과 젖음이 함께 걸린 순간 두 상태를 소모하고 범위 폭발.
        /// 적별 재발동 대기 시간이 있어 난사되지 않는다.
        /// </summary>
        static void TryReaction(Enemy enemy, GameConfig cfg)
        {
            if (enemy.BurnTimer <= 0f || enemy.WetTimer <= 0f) return;
            if (Time.time < enemy.NextReactionTime) return;

            enemy.ClearStatuses();
            enemy.NextReactionTime = Time.time + cfg.reactionCooldown;
            ReactionCount++;

            Vector2 center = enemy.Position;
            GameDatabase db = GameManager.Db;
            Fx.Explosion(center, cfg.reactionRadius, db.steamColor);
            Fx.Shockwave(center, cfg.reactionRadius * 1.2f, Gfx.WithAlpha(db.steamColor, 0.8f), 0.35f);
            // 증기: 흰 김이 피어오르고 불티와 물방울이 함께 튄다
            Vfx.SmokePuff(center, new Color(0.95f, 0.97f, 1f, 0.42f), 6, cfg.reactionRadius * 0.7f, cfg.reactionRadius * 1.1f, 0.9f);
            Vfx.Sparks(center, db.fireColor, 5, 5f, 11f);
            Vfx.Burst(center, db.waterColor, 6, 2f, 6f, 0.1f, 0.2f, 0.3f, 0.55f);
            Juice.HitStop(0.035f);

            // 반응 피해는 원소 없음 → 상태이상을 걸지 않으므로 연쇄 반응이 일어나지 않는다
            EnemyQuery.Circle(center, cfg.reactionRadius, reactionBuffer);
            for (int i = 0; i < reactionBuffer.Count; i++)
                Deal(reactionBuffer[i], cfg.reactionDamage, Element.None, false);
        }
    }

    /// <summary>원형 범위 피해 공용 함수</summary>
    public static class AreaDamage
    {
        static readonly List<Enemy> buffer = new List<Enemy>(64);

        /// <returns>처치한 적 수</returns>
        public static int Circle(Vector2 center, float radius, float damage, Element element)
        {
            int kills = 0;
            EnemyQuery.Circle(center, radius, buffer);
            for (int i = 0; i < buffer.Count; i++)
                if (DamageSystem.Deal(buffer[i], damage, element)) kills++;
            return kills;
        }
    }
}
