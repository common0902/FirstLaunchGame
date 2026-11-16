using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 화염 기둥 (불+불 전용 궁극기, 최대 Lv.5): 체력 높은 적을 우선 추적하는 이동형 기둥.
    /// 같은 대상이 기둥 안에 오래 머물수록 피해가 오른다 (적별 누적, 스택 UI 없이 불꽃 세기로만 표현). 보스 킬러 역할.
    /// 특수 진화 '대화염 기둥'은 새 스킬이 아니라 SkillData의 진화 데이터 세트(evolutionStats)를 갈아 끼운 것이다.
    /// 수치: damage(틱당 피해), cooldown, area(반지름), speed(추적 속도), duration, tickInterval, range(대상 탐색 반경),
    ///       special(초당 누적 화력 상승량), special2(누적 화력 최대 보너스)
    /// </summary>
    public class FirePillarSkill : SkillBase
    {
        FirePillarEntity active;

        protected override bool TryActivate()
        {
            if (active != null && active.Alive) return false;
            if (Targeting.HighestHp(player.Position, Stats.range) == null) return false;
            active = Pool<FirePillarEntity>.Get();
            active.Launch(this, player.Position);
            return true;
        }

        public float Radius
        {
            get
            {
                float r = Area(Stats.area);
                // 대화염 기둥은 세로 화면을 덮지 않도록 크기 상한을 둔다
                if (Evolved) r = Mathf.Min(r, Data.Extra("evolutionMaxRadius", 4f));
                return r;
            }
        }

        /// <summary>진화 시 누적 화력에 곱하는 계수 (나중에 0으로 두면 누적을 제거할 수 있다)</summary>
        public float RampFactor => Evolved ? Data.Extra("evolutionRampFactor", 0.5f) : 1f;
    }

    public class FirePillarEntity : Pooled
    {
        static readonly List<Enemy> buffer = new List<Enemy>(64);

        // 적별 노출 시간. 기둥을 벗어나면 초기화되도록 틱마다 새 사전으로 옮겨 담는다.
        Dictionary<int, float> exposure = new Dictionary<int, float>(64);
        Dictionary<int, float> exposureNext = new Dictionary<int, float>(64);

        Transform tr;
        SpriteRenderer flame, glow, baseRing, halo;
        float scorchTimer;
        FirePillarSkill owner;
        PlayerController player;
        Enemy target;
        int targetUid;
        Vector2 pos;
        float time, duration, tickTimer;
        float intensity;   // 0~1, 현재 누적 화력이 최대치에 얼마나 가까운지
        float flareTimer;

        public bool Alive { get; private set; }

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            glow = Gfx.NewSprite("Glow", tr, db.softCircle, Gfx.OrderZone + 2, Color.white);
            baseRing = Gfx.NewSprite("Ring", tr, db.ring, Gfx.OrderZone + 3, Color.white);
            flame = Gfx.NewSprite("Flame", tr, null, Gfx.OrderFx - 2, Color.white);
            halo = Gfx.NewGlow("Halo", tr, db.softCircle, Gfx.OrderFx - 1, Color.white);
        }

        public void Launch(FirePillarSkill skill, Vector2 start)
        {
            owner = skill;
            player = GameManager.I.Player;
            pos = start;
            time = 0f;
            duration = skill.Stats.duration;
            tickTimer = 0f;
            intensity = 0f;
            flareTimer = 0f;
            target = null;
            exposure.Clear();
            exposureNext.Clear();
            Alive = true;
            tr.position = pos;
            bool blue = skill.IsFinal;
            flame.sprite = blue && skill.Data.subSprite != null ? skill.Data.subSprite : skill.Data.mainSprite;
            scorchTimer = 0f;
            UpdateVisuals(0f);
            // 기둥이 솟아오르는 순간
            Color c0 = skill.MainColor;
            Fx.Explosion(pos, skill.Radius * 0.9f, c0);
            Fx.Shockwave(pos, skill.Radius * 1.8f, Gfx.WithAlpha(c0, 0.8f), 0.45f);
            Juice.Shake(0.3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (time >= duration) { Alive = false; Release(); return; }

            SkillLevelStats s = owner.Stats;
            float radius = owner.Radius;

            // 대상: 체력이 가장 높은 적. 죽으면 남은 시간 동안 다음 대상을 추적한다.
            if (target == null || target.IsDead || target.Uid != targetUid)
            {
                target = Targeting.HighestHp(player.Position, s.range);
                targetUid = target != null ? target.Uid : 0;
            }
            if (target != null)
                pos = Vector2.MoveTowards(pos, target.Position, s.speed * dt);

            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                float interval = Mathf.Max(0.05f, s.tickInterval);
                tickTimer += interval;
                float damage = owner.Dmg(s.damage);
                float rampMax = s.special2 * owner.RampFactor;
                float rampRate = s.special * owner.RampFactor;
                float best = 0f;

                exposureNext.Clear();
                EnemyQuery.Circle(pos, radius, buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    Enemy e = buffer[i];
                    exposure.TryGetValue(e.Uid, out float seconds);
                    seconds += interval;
                    float bonus = Mathf.Min(seconds * rampRate, rampMax);
                    if (rampMax > 0f) best = Mathf.Max(best, bonus / rampMax);
                    if (!DamageSystem.Deal(e, damage * (1f + bonus), Element.Fire))
                        exposureNext[e.Uid] = seconds;
                }
                Dictionary<int, float> swap = exposure;
                exposure = exposureNext;
                exposureNext = swap;
                intensity = Mathf.MoveTowards(intensity, best, 0.35f);
            }

            UpdateVisuals(Mathf.Clamp01(Mathf.Min(time / 0.3f, (duration - time) / 0.5f)));

            // 누적 최대치에 도달하면 불꽃이 거세진다
            if (intensity >= 0.99f)
            {
                flareTimer -= dt;
                if (flareTimer <= 0f)
                {
                    flareTimer = 0.18f;
                    Color c = owner.MainColor;
                    Fx.Shockwave(pos, radius * 1.25f, Gfx.WithAlpha(c, 0.8f), 0.3f);
                    Vfx.Sparks(pos, Color.Lerp(c, Color.white, 0.5f), 6, radius * 3f, radius * 7f);
                    Juice.Shake(0.06f);
                }
            }
        }

        void UpdateVisuals(float fade)
        {
            float radius = owner.Radius;
            Color c = owner.MainColor;
            tr.position = pos;
            float pulse = 1f + (0.05f + 0.1f * intensity) * Mathf.Sin(Time.time * (10f + 14f * intensity));
            Gfx.SetDiameter(flame.transform, flame.sprite, radius * 2.1f * pulse);
            flame.transform.Rotate(0f, 0f, (120f + 260f * intensity) * Time.deltaTime);
            flame.color = new Color(1f, 1f, 1f, fade);
            Gfx.SetDiameter(glow.transform, glow.sprite, radius * (2.6f + 0.8f * intensity));
            glow.color = Gfx.WithAlpha(c, (0.3f + 0.35f * intensity) * fade);
            Gfx.SetDiameter(baseRing.transform, baseRing.sprite, radius * 2f);
            baseRing.color = Gfx.WithAlpha(c, 0.7f * fade);

            // 가산 혼합 불빛: 화력이 오를수록 밝고 크게 일렁인다
            Gfx.SetDiameter(halo.transform, halo.sprite, radius * (3.2f + 1.2f * intensity) * pulse);
            halo.color = Gfx.WithAlpha(c, (0.22f + 0.3f * intensity) * fade);

            if (fade <= 0.05f || Time.timeScale <= 0f) return;
            // 치솟는 불티와 불꽃 혀
            int embers = 1 + Mathf.RoundToInt(intensity * 2f);
            for (int i = 0; i < embers; i++)
            {
                Vector2 off = Random.insideUnitCircle * radius * 0.9f;
                Vfx.Glow(pos + off, new Vector2(-off.x * 0.8f, Random.Range(2.5f, 5.5f + 3f * intensity)),
                    Random.Range(0.14f, 0.34f), Random.Range(0.35f, 0.7f), Gfx.WithAlpha(c, fade));
            }
            if (Random.value < 0.5f)
            {
                Vector2 off = Random.insideUnitCircle * radius * 0.6f;
                Vfx.Streak(pos + off, new Vector2(Random.Range(-1f, 1f), Random.Range(5f, 9f)), 0.12f, 0.3f,
                    Gfx.WithAlpha(Color.Lerp(c, Color.white, 0.5f), fade));
            }
            if (Random.value < 0.18f)
                Vfx.Smoke(pos + new Vector2(Random.Range(-0.5f, 0.5f) * radius, radius * 0.6f), new Vector2(0f, 1.6f),
                    radius * 0.9f, 0.9f, new Color(0.14f, 0.12f, 0.13f, 0.22f * fade));

            // 지나간 자리에 그을음이 남는다
            scorchTimer -= Time.deltaTime;
            if (scorchTimer <= 0f)
            {
                scorchTimer = 0.35f;
                Fx.Decal(pos, radius * 0.9f, new Color(0.02f, 0.02f, 0.03f, 0.3f), 2.2f);
            }
        }
    }
}
