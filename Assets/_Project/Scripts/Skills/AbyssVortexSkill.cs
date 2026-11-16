using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 심해 소용돌이 (물+물 전용 궁극기, 최대 Lv.5): 적을 나선형으로 중심에 끌어당긴다 (지속형 힘). 군중 제어 역할.
    /// 중심에 가까울수록 회전 속도와 타격 빈도가 오르고, 타격당 피해는 같다.
    /// 중심에 도달한 적은 고정되지 않고 작은 원 궤도로 회전한다. 중심부 경계를 항상 표시한다.
    /// Lv.5 심연의 붕괴: 지속 종료 시 중심부 경계 안에서만 폭발, 끌려온 적 수 비례 피해(상한), 맞은 적은 바깥으로 흩뿌림(순간형 힘).
    /// 수치: damage(타격당 피해), cooldown, area(반지름), speed(흡입력), duration, tickInterval(가장자리 타격 간격),
    ///       range(탐색 반경), special(붕괴 기본 피해, 0이면 없음), special2(붕괴 시 적 1마리당 피해 증가율)
    /// </summary>
    public class AbyssVortexSkill : SkillBase
    {
        AbyssVortexEntity active;

        protected override bool TryActivate()
        {
            if (active != null && active.Alive) return false;
            SkillLevelStats s = Stats;
            float radius = Area(s.area);
            if (!Targeting.DensePoint(player.Position, s.range, radius, out Vector2 point)) return false;
            active = Pool<AbyssVortexEntity>.Get();
            active.Launch(this, point, radius);
            return true;
        }
    }

    public class AbyssVortexEntity : Pooled
    {
        static readonly List<Enemy> buffer = new List<Enemy>(96);

        readonly Dictionary<int, float> nextHit = new Dictionary<int, float>(96);
        Transform tr;
        SpriteRenderer swirl, swirlInner, coreRing, floor, coreGlow;
        AbyssVortexSkill owner;
        Vector2 center;
        float radius, coreRadius, orbitRadius, duration, time;
        float pull, tangential, damage, outerInterval, minInterval;
        float collapseDamage, collapsePerEnemy;
        int collapseCap;
        Color color;

        public bool Alive { get; private set; }

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            floor = Gfx.NewSprite("Floor", tr, db.softCircle, Gfx.OrderZone - 1, Color.white);
            swirl = Gfx.NewSprite("Swirl", tr, null, Gfx.OrderZone, Color.white);
            swirlInner = Gfx.NewSprite("SwirlInner", tr, null, Gfx.OrderZone + 1, Color.white);
            coreRing = Gfx.NewSprite("CoreRing", tr, db.ring, Gfx.OrderZone + 2, Color.white);
            coreGlow = Gfx.NewGlow("CoreGlow", tr, db.softCircle, Gfx.OrderZone + 3, Color.white);
        }

        public void Launch(AbyssVortexSkill skill, Vector2 point, float vortexRadius)
        {
            owner = skill;
            SkillData data = skill.Data;
            SkillLevelStats s = skill.Stats;
            center = point;
            radius = vortexRadius;
            coreRadius = radius * data.Extra("coreRadiusRatio", 0.3f);
            orbitRadius = coreRadius * 0.6f;
            duration = s.duration;
            time = 0f;
            pull = s.speed;
            tangential = data.Extra("tangentialSpeed", 3f);
            damage = skill.Dmg(s.damage);
            outerInterval = Mathf.Max(0.05f, s.tickInterval);
            // 타격 주기 하한: 중심부 DPS 폭증 방지
            minInterval = Mathf.Max(0.05f, data.Extra("minTickInterval", 0.2f));
            collapseDamage = skill.Dmg(s.special);
            collapsePerEnemy = s.special2;
            collapseCap = Mathf.RoundToInt(data.Extra("collapseEnemyCap", 15f));
            color = skill.MainColor;
            nextHit.Clear();
            Alive = true;

            tr.position = center;
            swirl.sprite = data.mainSprite;
            swirlInner.sprite = data.mainSprite;
            Gfx.SetDiameter(swirl.transform, swirl.sprite, radius * 2f);
            Gfx.SetDiameter(swirlInner.transform, swirlInner.sprite, radius * 1.1f);
            Gfx.SetDiameter(floor.transform, floor.sprite, radius * 2.3f);
            Gfx.SetDiameter(coreRing.transform, coreRing.sprite, coreRadius * 2f);
            ApplyColors(0f);
            coreGlow.color = new Color(0f, 0f, 0f, 0f);
            // 소용돌이가 열리는 순간
            Fx.Shockwave(center, radius, Gfx.WithAlpha(color, 0.7f), 0.5f);
            Vfx.Splash(center, color, 2.2f);
            Juice.Shake(0.15f);
        }

        void ApplyColors(float fade)
        {
            floor.color = new Color(0.05f, 0.2f, 0.45f, 0.35f * fade);
            swirl.color = new Color(1f, 1f, 1f, 0.75f * fade);
            swirlInner.color = new Color(1f, 1f, 1f, 0.85f * fade);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            coreRing.color = Gfx.WithAlpha(color, pulse * Mathf.Max(fade, 0.3f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (time >= duration)
            {
                if (collapseDamage > 0f) Collapse();
                Alive = false;
                Release();
                return;
            }

            ApplyColors(Mathf.Clamp01(Mathf.Min(time / 0.4f, (duration - time) / 0.3f)));
            swirl.transform.Rotate(0f, 0f, -110f * dt);
            swirlInner.transform.Rotate(0f, 0f, -260f * dt);
            EmitSwirl(Mathf.Clamp01(Mathf.Min(time / 0.4f, (duration - time) / 0.3f)));

            // 타격: 중심에 가까울수록 간격이 짧아진다 (하한 있음). 타격당 피해는 동일.
            float now = Time.time;
            EnemyQuery.Circle(center, radius, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                if (nextHit.TryGetValue(e.Uid, out float t) && now < t) continue;
                float ratio = Mathf.Clamp01((e.Position - center).magnitude / radius);
                nextHit[e.Uid] = now + Mathf.Max(minInterval, Mathf.Lerp(minInterval, outerInterval, ratio));
                DamageSystem.Deal(e, damage, Element.Water);
            }
        }

        /// <summary>가장자리에서 중심으로 빨려 들어가는 물줄기와 중심부의 푸른 빛</summary>
        void EmitSwirl(float fade)
        {
            Gfx.SetDiameter(coreGlow.transform, coreGlow.sprite, coreRadius * (3.4f + 0.5f * Mathf.Sin(Time.time * 7f)));
            coreGlow.color = Gfx.WithAlpha(color, 0.45f * fade);
            if (fade <= 0.05f) return;

            for (int i = 0; i < 3; i++)
            {
                Vector2 d = Gfx.Dir(Random.Range(0f, 360f));
                float dist = radius * Random.Range(0.45f, 1f);
                // 회전 방향(시계 방향)의 접선 + 안쪽으로 끌리는 속도
                Vector2 tangent = new Vector2(d.y, -d.x);
                Vector2 vel = tangent * Random.Range(4f, 7.5f) - d * Random.Range(2.5f, 5f);
                Vfx.Streak(center + d * dist, vel, Random.Range(0.08f, 0.15f), Random.Range(0.3f, 0.5f),
                    Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.45f), 0.85f * fade));
            }
            if (Random.value < 0.5f)
            {
                Vector2 d = Gfx.Dir(Random.Range(0f, 360f));
                Vfx.Glow(center + d * coreRadius * Random.Range(0.2f, 1f), new Vector2(d.y, -d.x) * 2.5f,
                    Random.Range(0.15f, 0.3f), 0.4f, Gfx.WithAlpha(color, 0.8f * fade));
            }
        }

        void FixedUpdate()
        {
            if (!Alive || GameManager.I.State != GameState.Playing) return;
            // 나선 흡입: 중심 방향 힘 + 접선 방향 힘 (지속형). 보스 저항은 적 쪽에서 처리한다.
            EnemyQuery.Circle(center, radius, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                Vector2 rel = e.Position - center;
                float dist = rel.magnitude;
                if (dist < 0.01f) { rel = Vector2.right * 0.01f; dist = 0.01f; }
                Vector2 inward = -rel / dist;
                Vector2 tangent = new Vector2(-inward.y, inward.x);
                float ratio = Mathf.Clamp01(dist / radius);
                // 중심에 가까울수록 빨리 돈다
                float spin = tangential * Mathf.Lerp(1.7f, 0.6f, ratio);
                // 궤도 반지름보다 안쪽이면 바깥으로 밀어 작은 원 궤도를 유지시킨다 (제자리 고정 아님)
                float radial = dist > orbitRadius ? pull : (dist - orbitRadius) * 6f;
                e.AddContinuousForce(inward * radial + tangent * spin);
            }
        }

        /// <summary>심연의 붕괴 (Lv.5)</summary>
        void Collapse()
        {
            EnemyQuery.Circle(center, coreRadius, buffer);
            int n = Mathf.Min(buffer.Count, collapseCap);          // 적 수 비례 피해 상한
            float dmg = collapseDamage * (1f + collapsePerEnemy * n);
            float scatter = owner.Data.Extra("scatterSpeed", 9f);
            float impulseTime = GameManager.Config.impulseDuration;
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                Vector2 rel = e.Position - center;
                Vector2 outward = rel.sqrMagnitude > 0.0001f ? rel.normalized : Gfx.Dir(Random.Range(0f, 360f));
                if (!DamageSystem.Deal(e, dmg, Element.Water))
                    e.ApplyImpulse(outward * scatter, impulseTime);   // 보스는 ApplyImpulse에서 무시된다
            }
            Fx.Explosion(center, coreRadius * 1.3f, color);
            Fx.Shockwave(center, coreRadius * 2.2f, color, 0.45f);
            // 붕괴: 모였던 물이 한꺼번에 터져 나간다
            Fx.Shockwave(center, radius * 1.1f, Gfx.WithAlpha(Color.white, 0.6f), 0.55f);
            Fx.Glow(center, radius * 2.4f, Gfx.WithAlpha(color, 0.5f), 0.4f);
            Vfx.Sparks(center, Color.Lerp(color, Color.white, 0.6f), 16, 6f, 15f);
            Vfx.Burst(center, color, 12, 3f, 8f, 0.14f, 0.3f, 0.4f, 0.8f);
            Juice.Shake(0.55f);
            Juice.HitStop(0.07f);
        }
    }
}
