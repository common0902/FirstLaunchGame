using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 화염구: 가장 가까운 적에게 발사, 충돌 시 범위 폭발. 관통 없음.
    /// Lv.3 부채꼴 3발 / Lv.7 시간차를 둔 푸른 화염의 두 번째 폭발.
    /// 수치: damage, cooldown, count(발사 수), area(폭발 반지름), speed, range,
    ///       special(두 번째 폭발 피해 배율, 0이면 없음), special2(두 번째 폭발 반지름 배율)
    /// </summary>
    public class FireballSkill : SkillBase
    {
        protected override bool TryActivate()
        {
            SkillLevelStats s = Stats;
            Enemy target = Targeting.Nearest(player.Position, s.range);
            if (target == null) return false;

            Vector2 dir = target.Position - player.Position;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : player.Facing;
            int n = Mathf.Max(1, s.count);
            float fan = Data.Extra("fanAngle", 18f);
            for (int i = 0; i < n; i++)
            {
                float offset = (i - (n - 1) * 0.5f) * fan;
                Pool<FireballProjectile>.Get().Launch(this, player.Position, Gfx.Rotate(dir, offset));
            }
            // 발사 섬광
            Vector2 muzzle = player.Position + dir * 0.5f;
            Fx.Glow(muzzle, 1.6f, Gfx.WithAlpha(MainColor, 0.7f), 0.14f);
            Vfx.SparksCone(muzzle, dir, 35f, Color.Lerp(MainColor, Color.white, 0.4f), 5, 4f, 9f);
            return true;
        }
    }

    public class FireballProjectile : Pooled
    {
        static readonly List<Enemy> buffer = new List<Enemy>(16);

        Transform tr;
        SpriteRenderer sr, glow;
        FireballSkill owner;
        Vector2 dir;
        float speed, maxDistance, traveled;
        float damage, radius, secondMul, secondRadiusMul, secondDelay;
        float trailTimer;
        Color color;

        protected override void OnCreated()
        {
            tr = transform;
            glow = Gfx.NewGlow("Glow", tr, GameManager.Db.softCircle, Gfx.OrderProjectile - 1, Color.white);
            sr = Gfx.NewSprite("Visual", tr, null, Gfx.OrderProjectile, Color.white);
        }

        public void Launch(FireballSkill skill, Vector2 pos, Vector2 direction)
        {
            owner = skill;
            SkillLevelStats s = skill.Stats;
            dir = direction;
            speed = s.speed;
            maxDistance = s.range * 1.2f;
            traveled = 0f;
            damage = skill.Dmg(s.damage);
            radius = skill.Area(s.area);
            secondMul = s.special;
            secondRadiusMul = s.special2;
            secondDelay = skill.Data.Extra("secondExplosionDelay", 0.25f);

            tr.position = pos;
            tr.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(dir));
            sr.sprite = skill.Data.mainSprite;
            sr.color = Color.white;
            Gfx.SetDiameter(sr.transform, sr.sprite, 0.8f);
            color = skill.Data.color;
            trailTimer = 0f;
            glow.color = Gfx.WithAlpha(color, 0.55f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float step = speed * dt;
            Vector2 pos = (Vector2)tr.position + dir * step;
            tr.position = pos;
            traveled += step;

            // 일렁이는 빛 번짐과 꼬리 불티
            Gfx.SetDiameter(glow.transform, glow.sprite, 1.7f + 0.25f * Mathf.Sin(Time.time * 40f));
            trailTimer -= dt;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.022f;
                Vector2 back = pos - dir * 0.3f;
                Vfx.Glow(back + Random.insideUnitCircle * 0.08f, -dir * 1.2f, Random.Range(0.3f, 0.48f), 0.24f, Gfx.WithAlpha(color, 0.75f));
                if (Random.value < 0.45f)
                    Vfx.Glow(back, Gfx.Rotate(-dir, Random.Range(-50f, 50f)) * Random.Range(1.5f, 3.5f),
                        Random.Range(0.08f, 0.15f), Random.Range(0.3f, 0.5f), Color.Lerp(color, Color.white, 0.35f));
                if (Random.value < 0.2f)
                    Vfx.Smoke(back, Vector2.up * 0.4f, 0.4f, 0.5f, new Color(0.15f, 0.13f, 0.14f, 0.22f));
            }

            if (EnemyQuery.Circle(pos, 0.3f, buffer) > 0 || traveled >= maxDistance)
                Explode(pos);
        }

        void Explode(Vector2 pos)
        {
            SkillData data = owner.Data;
            AreaDamage.Circle(pos, radius, damage, Element.Fire);
            Fx.Explosion(pos, radius, data.color);
            // 날아가던 방향으로 불꽃이 더 튄다
            Vfx.SparksCone(pos, dir, 55f, Color.Lerp(data.color, Color.white, 0.5f), 6, radius * 4f, radius * 9f);

            if (secondMul > 0f)
            {
                Pool<DelayedExplosion>.Get().Arm(pos, secondDelay, radius * secondRadiusMul,
                    damage * secondMul, Element.Fire, data.finalColor);
            }
            Release();
        }
    }

    /// <summary>잠시 뒤 터지는 폭발 (화염구 Lv.7의 두 번째 폭발)</summary>
    public class DelayedExplosion : Pooled
    {
        Vector2 pos;
        float timer, radius, damage;
        Element element;
        Color color;

        public void Arm(Vector2 position, float delay, float explosionRadius, float explosionDamage, Element el, Color fxColor)
        {
            pos = position;
            timer = delay;
            radius = explosionRadius;
            damage = explosionDamage;
            element = el;
            color = fxColor;
            transform.position = position;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                // 터지기 직전 불씨가 중심으로 모여든다
                if (Random.value < 0.6f)
                {
                    Vector2 off = Random.insideUnitCircle.normalized * radius * 0.9f;
                    Vfx.Glow(pos + off, -off * 3.5f, 0.16f, 0.22f, color);
                }
                return;
            }
            AreaDamage.Circle(pos, radius, damage, element);
            Fx.Explosion(pos, radius, color);
            Fx.Shockwave(pos, radius * 1.4f, color, 0.4f);
            Fx.Glow(pos, radius * 3.4f, Gfx.WithAlpha(color, 0.5f), 0.3f);
            Juice.Shake(0.12f);
            Release();
        }
    }
}
