using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>경험치 오브. 개별 Update 없이 XpOrb.TickAll에서 한꺼번에 갱신한다.</summary>
    public class XpOrb : Pooled
    {
        static readonly List<XpOrb> active = new List<XpOrb>(256);

        Transform tr;
        int value;
        bool attracted;
        float speed;

        public static void ResetStatics() => active.Clear();

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            SpriteRenderer sr = Gfx.NewSprite("Visual", tr, db.xpOrb, Gfx.OrderOrb, Color.white);
            Gfx.SetDiameter(sr.transform, db.xpOrb, 0.38f);
        }

        public static void Drop(Vector2 pos, int value)
        {
            if (value <= 0) return;
            GameConfig cfg = GameManager.Config;
            // 오브가 너무 많으면 새로 만들지 않고 기존 오브에 합친다
            if (active.Count >= cfg.maxXpOrbs)
            {
                active[Random.Range(0, active.Count)].value += value;
                return;
            }
            XpOrb orb = Pool<XpOrb>.Get();
            orb.tr.position = pos + Random.insideUnitCircle * 0.2f;
            orb.value = value;
            orb.attracted = false;
            orb.speed = 0f;
            active.Add(orb);
        }

        public static void TickAll(float dt, PlayerController player, GameConfig cfg)
        {
            Vector2 p = player.Position;
            float pickSqr = cfg.pickupRadius * cfg.pickupRadius;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                XpOrb orb = active[i];
                Vector2 pos = orb.tr.position;
                Vector2 to = p - pos;
                float sqr = to.sqrMagnitude;
                if (!orb.attracted && sqr <= pickSqr) orb.attracted = true;
                if (!orb.attracted) continue;

                orb.speed = Mathf.MoveTowards(orb.speed, cfg.orbMagnetSpeed, cfg.orbMagnetSpeed * 3f * dt);
                float step = orb.speed * dt;
                if (sqr <= 0.09f || step * step >= sqr)
                {
                    active[i] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                    int v = orb.value;
                    orb.Release();
                    player.Experience.AddXp(v);
                    continue;
                }
                orb.tr.position = pos + to / Mathf.Sqrt(sqr) * step;
            }
        }

        /// <summary>남은 오브를 전부 끌어당긴다 (보스 등장 시)</summary>
        public static void AttractAll()
        {
            for (int i = 0; i < active.Count; i++) active[i].attracted = true;
        }
    }
}
