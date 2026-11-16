using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 수류 군집: 물방울들이 하나의 군집으로 곡선 이동하며 적을 연속 타격한다.
    /// 적이 남아 있으면 다음 적을 추적하고, 없을 때만 플레이어 주변으로 돌아온다.
    /// Lv.3 5개 / Lv.6 7개 / Lv.7 날카롭고 길쭉한 수류.
    /// 수치: damage(물방울 1개당 피해), count(물방울 수), area(군집 반지름), speed, range(플레이어로부터 추적 한계),
    ///       tickInterval(적별 재피격 간격), special(선회 속도 도/초)
    /// </summary>
    public class WaterSwarmSkill : SkillBase
    {
        static readonly List<Enemy> buffer = new List<Enemy>(32);

        readonly List<SpriteRenderer> droplets = new List<SpriteRenderer>(7);
        readonly Dictionary<int, float> nextHit = new Dictionary<int, float>(64);
        Transform root;
        SpriteRenderer glow;
        Vector2 pos, dir = Vector2.right;
        Enemy target;
        int targetUid;
        float orbitAngle;

        protected override void OnAcquired()
        {
            root = new GameObject("WaterSwarm").transform;
            pos = player.Position + Vector2.right;
        }

        protected override void OnLevelChanged()
        {
            int want = Mathf.Max(1, Stats.count);
            while (droplets.Count < want)
                droplets.Add(Gfx.NewSprite("Droplet", root, null, Gfx.OrderProjectile, Color.white));

            Sprite sprite = IsFinal && Data.subSprite != null ? Data.subSprite : Data.mainSprite;
            float size = IsFinal ? 0.75f : 0.42f;
            for (int i = 0; i < droplets.Count; i++)
            {
                droplets[i].sprite = sprite;
                Gfx.SetDiameter(droplets[i].transform, sprite, size);
            }
        }

        bool TargetValid(float leash)
        {
            if (target == null || target.IsDead || target.Uid != targetUid) return false;
            return (target.Position - player.Position).sqrMagnitude <= leash * leash;
        }

        /// <summary>군집에서 가장 가까우면서 아직 재피격 대기 중이 아닌 적을 고른다</summary>
        Enemy PickTarget(float range)
        {
            Enemy best = null;
            float bestSqr = float.MaxValue;
            float rangeSqr = range * range;
            float now = Time.time;
            Vector2 playerPos = player.Position;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if ((e.Position - playerPos).sqrMagnitude > rangeSqr) continue;
                if (nextHit.TryGetValue(e.Uid, out float t) && now < t) continue;
                float d = (e.Position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = e; }
            }
            return best;
        }

        public override void Tick(float dt)
        {
            SkillLevelStats s = Stats;
            float radius = Area(s.area);
            float now = Time.time;

            if (!TargetValid(s.range * 1.2f))
            {
                target = PickTarget(s.range);
                targetUid = target != null ? target.Uid : 0;
            }

            // 목적지: 대상이 있으면 대상, 없으면 플레이어 주변을 맴돈다
            Vector2 dest;
            float speed = s.speed;
            if (target != null)
            {
                dest = target.Position;
            }
            else
            {
                orbitAngle += dt * 150f;
                dest = player.Position + Gfx.Dir(orbitAngle) * 1.3f;
                float dist = (dest - pos).magnitude;
                speed = Mathf.Min(speed, Mathf.Max(player.MoveDir != Vector2.zero ? 5f : 1.5f, dist * 4f));
            }

            // 곡선 이동: 현재 방향을 목적지 방향으로 제한된 속도로 돌린다
            Vector2 to = dest - pos;
            if (to.sqrMagnitude > 0.0001f)
            {
                float current = Gfx.Angle(dir);
                float wanted = Gfx.Angle(to);
                // 너무 멀리 벗어났으면 더 급하게 꺾어 큰 원을 그리며 맴도는 것을 막는다
                float turn = s.special * ((pos - player.Position).sqrMagnitude > s.range * s.range ? 3f : 1f);
                dir = Gfx.Dir(Mathf.MoveTowardsAngle(current, wanted, turn * dt));
            }
            pos += dir * (speed * dt);

            // 피해: 군집에 닿은 적을 물방울 수만큼 타격
            if (nextHit.Count > 300) nextHit.Clear();
            int n = Mathf.Max(1, s.count);
            float damage = Dmg(s.damage) * n;
            EnemyQuery.Circle(pos, radius, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                if (nextHit.TryGetValue(e.Uid, out float t) && now < t) continue;
                nextHit[e.Uid] = now + s.tickInterval;
                DamageSystem.Deal(e, damage, Element.Water);
                Fx.Puff(e.Position, 0.7f, Gfx.WithAlpha(db.waterColor, 0.7f), 0.18f);
                Vfx.Splash(e.Position, MainColor, IsFinal ? 1.3f : 1f);
                if (IsFinal) Vfx.SparksCone(e.Position, dir, 25f, Color.white, 3, 6f, 11f);
                // 방금 친 대상은 놓고 다음 적으로 넘어간다
                if (e == target) target = null;
            }

            UpdateVisuals(radius);
        }

        void UpdateVisuals(float radius)
        {
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float angle = Gfx.Angle(dir);
            float time = Time.time;
            int n = Mathf.Max(1, Stats.count);
            for (int i = 0; i < droplets.Count; i++)
            {
                bool on = i < n;
                if (droplets[i].gameObject.activeSelf != on) droplets[i].gameObject.SetActive(on);
                if (!on) continue;
                // 물고기 떼처럼 선두 뒤로 좌우로 흔들리며 따라온다
                float back = i * (IsFinal ? 0.2f : 0.16f);
                float sway = Mathf.Sin(time * 9f + i * 1.7f) * radius * 0.55f * (i == 0 ? 0.2f : 1f);
                Vector2 p = pos - dir * back + perp * sway;
                Transform t = droplets[i].transform;
                t.position = p;
                t.rotation = Quaternion.Euler(0f, 0f, angle + Mathf.Cos(time * 9f + i * 1.7f) * 12f);
                // 물방울마다 짧은 물꼬리가 따라온다
                if ((Time.frameCount + i) % 2 == 0)
                    Vfx.Glow(p - dir * 0.12f, -dir * 0.6f, IsFinal ? 0.3f : 0.2f, 0.2f, Gfx.WithAlpha(MainColor, 0.55f));
            }
            if (glow == null) glow = Gfx.NewGlow("SwarmGlow", root, db.softCircle, Gfx.OrderProjectile - 1, Color.white);
            glow.transform.position = pos - dir * (n * 0.07f);
            Gfx.SetDiameter(glow.transform, glow.sprite, radius * 2.2f + n * 0.12f);
            glow.color = Gfx.WithAlpha(MainColor, 0.28f);
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
