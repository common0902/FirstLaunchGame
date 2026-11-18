using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 화염 고리: 플레이어 중심의 가상 전투 영역 안을 돌아다니며 경계에서 반사된다 (공전형 아님).
    /// 적을 관통하고 적별 재피격 간격을 관리한다.
    /// Lv.4 고리 2개 / Lv.6 반사 시 순간 팽창 / Lv.7 적 처치 시 일정 시간 푸른 화염 (고리별 개별 적용).
    /// 수치: damage(1회 피해), count(고리 수), area(고리 반지름), speed, tickInterval(적별 재피격 간격),
    ///       special(반사 팽창 배율, 0이면 없음), special2(푸른 화염 속도·피해 배율, 0이면 없음), duration(푸른 화염 지속)
    /// </summary>
    public class FlameRingSkill : SkillBase
    {
        class Ring
        {
            public Transform tr;
            public SpriteRenderer sr, glow;
            public float emberTimer;
            public Vector2 pos, dir;
            public float expandTimer, blueTimer, blueReadyTime;
            public readonly Dictionary<int, float> nextHit = new Dictionary<int, float>(64);
        }

        static readonly List<Enemy> buffer = new List<Enemy>(48);
        readonly List<Ring> rings = new List<Ring>(2);

        protected override void OnLevelChanged()
        {
            int want = Mathf.Max(1, Stats.count);
            while (rings.Count < want)
            {
                var ring = new Ring();
                ring.sr = Gfx.NewSprite("FlameRing", null, Data.mainSprite, Gfx.OrderProjectile, Data.color);
                ring.tr = ring.sr.transform;
                // 고리 아래에 깔리는 빛 번짐 (회전하지 않도록 따로 둔다)
                ring.glow = Gfx.NewGlow("FlameRingGlow", null, db.softCircle, Gfx.OrderZone + 4, Data.color);
                ring.pos = player.Position + Random.insideUnitCircle * 1.5f;
                ring.dir = Gfx.Dir(45f + 90f * rings.Count + Random.Range(-15f, 15f));
                ring.tr.position = ring.pos;
                rings.Add(ring);
            }
        }

        public override void Tick(float dt)
        {
            SkillLevelStats s = Stats;
            float baseRadius = Area(s.area);
            float expandDuration = Data.Extra("expandDuration", 0.5f);
            float blueCooldown = Data.Extra("blueCooldown", 0f);

            // 전투 영역: 플레이어를 중심으로 화면 크기에 비례
            Vector2 half = Gfx.ViewHalfExtents * Data.Extra("boundsScale", 0.95f);
            Vector2 min = player.Position - half;
            Vector2 max = player.Position + half;

            for (int r = 0; r < rings.Count; r++)
            {
                Ring ring = rings[r];
                bool blue = ring.blueTimer > 0f;
                if (blue) ring.blueTimer -= dt;
                if (ring.expandTimer > 0f) ring.expandTimer -= dt;

                float expand = ring.expandTimer > 0f && s.special > 1f
                    ? Mathf.Lerp(1f, s.special, ring.expandTimer / expandDuration)
                    : 1f;
                float radius = baseRadius * expand;
                float speed = s.speed * (blue ? s.special2 : 1f);

                // 이동과 경계 반사
                ring.pos += ring.dir * (speed * dt);
                bool bounced = false;
                if (ring.pos.x - baseRadius < min.x) { ring.pos.x = min.x + baseRadius; ring.dir.x = Mathf.Abs(ring.dir.x); bounced = true; }
                else if (ring.pos.x + baseRadius > max.x) { ring.pos.x = max.x - baseRadius; ring.dir.x = -Mathf.Abs(ring.dir.x); bounced = true; }
                if (ring.pos.y - baseRadius < min.y) { ring.pos.y = min.y + baseRadius; ring.dir.y = Mathf.Abs(ring.dir.y); bounced = true; }
                else if (ring.pos.y + baseRadius > max.y) { ring.pos.y = max.y - baseRadius; ring.dir.y = -Mathf.Abs(ring.dir.y); bounced = true; }
                if (bounced && s.special > 1f && ring.expandTimer <= 0f)
                {
                    ring.expandTimer = expandDuration;
                    Fx.Shockwave(ring.pos, baseRadius * s.special, Gfx.WithAlpha(Data.color, 0.6f), 0.3f);
                    Fx.Glow(ring.pos, baseRadius * s.special * 2.4f, Gfx.WithAlpha(Data.color, 0.45f), 0.3f);
                    Juice.Shake(0.1f);
                }
                if (bounced)
                {
                    // 벽에 부딪혀 튕기는 순간 불꽃이 흩어진다
                    Vfx.Sparks(ring.pos, Color.Lerp(Data.color, Color.white, 0.4f), 8, 4f, 9f);
                }

                // 피해 (적별 재피격 간격)
                float damage = Dmg(s.damage) * (blue ? s.special2 : 1f);
                float now = Time.time;
                if (ring.nextHit.Count > 300) ring.nextHit.Clear();
                EnemyQuery.Circle(ring.pos, radius, buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    Enemy e = buffer[i];
                    if (ring.nextHit.TryGetValue(e.Uid, out float t) && now < t) continue;
                    ring.nextHit[e.Uid] = now + s.tickInterval;
                    bool killed = DamageSystem.Deal(e, damage, Element.Fire);
                    // Lv.7: 이 고리가 적을 처치하면 푸른 화염 상태
                    if (killed && s.special2 > 0f && now >= ring.blueReadyTime)
                    {
                        ring.blueTimer = s.duration;
                        // Lv.7 재발동 대기 시간: 0으로 확정 (SkillData extras의 blueCooldown으로 조정 가능)
                        ring.blueReadyTime = now + blueCooldown;
                    }
                }

                // 비주얼
                ring.tr.position = ring.pos;
                ring.tr.Rotate(0f, 0f, (blue ? 320f : 200f) * dt);
                Gfx.SetDiameter(ring.tr, ring.sr.sprite, radius * 2f);
                Color ringColor = ring.blueTimer > 0f ? Data.finalColor : Data.color;
                ring.sr.color = ringColor;
                ring.glow.transform.position = ring.pos;
                Gfx.SetDiameter(ring.glow.transform, ring.glow.sprite, radius * (3f + 0.2f * Mathf.Sin(Time.time * 14f)));
                ring.glow.color = Gfx.WithAlpha(ringColor, blue ? 0.42f : 0.3f);

                // 고리 둘레에서 불티가 회전 방향으로 떨어져 나온다
                ring.emberTimer -= dt;
                if (ring.emberTimer <= 0f)
                {
                    ring.emberTimer = blue ? 0.02f : 0.035f;
                    Vector2 d = Gfx.Dir(Random.Range(0f, 360f));
                    Vector2 tangent = new Vector2(-d.y, d.x);
                    Vfx.Glow(ring.pos + d * radius * Random.Range(0.75f, 1f), tangent * Random.Range(1.5f, 3.5f) - ring.dir * 1.5f,
                        Random.Range(0.12f, 0.26f), Random.Range(0.25f, 0.5f), ringColor);
                }
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i].tr != null) Destroy(rings[i].tr.gameObject);
                if (rings[i].glow != null) Destroy(rings[i].glow.gameObject);
            }
        }
    }
}
