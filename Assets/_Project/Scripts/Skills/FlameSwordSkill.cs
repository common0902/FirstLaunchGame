using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 화염검: 플레이어가 바라보는 방향 전방을 크게 베는 참격.
    /// Lv.3 화염 검기 / Lv.5 2연속 베기 / Lv.7 푸른 화염의 360도 검무.
    /// 수치: damage(참격 1회 피해), cooldown, count(연속 베기 횟수), area(검 사거리=반지름),
    ///       speed(검기 속도), range(검기 사거리), special(검기 피해 배율, 0이면 검기 없음), special2(참격 각도)
    /// </summary>
    public class FlameSwordSkill : SkillBase
    {
        static readonly List<Enemy> buffer = new List<Enemy>(48);

        int slashesLeft;
        float slashTimer;
        bool mirror;

        protected override bool TryActivate()
        {
            slashesLeft = Mathf.Max(1, Stats.count);
            slashTimer = 0f;
            return true;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (slashesLeft <= 0) return;
            slashTimer -= dt;
            if (slashTimer > 0f) return;
            DoSlash();
            slashesLeft--;
            slashTimer = Data.Extra("comboInterval", 0.22f);
        }

        void DoSlash()
        {
            SkillLevelStats s = Stats;
            Vector2 origin = player.Position;
            Vector2 dir = player.Facing;
            float reach = Area(s.area);
            float arc = s.special2;
            bool fullCircle = arc >= 359f;
            float damage = Dmg(s.damage);

            int hitCount = 0;
            EnemyQuery.Circle(origin, reach, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                Vector2 to = e.Position - origin;
                if (!fullCircle)
                {
                    // 플레이어와 겹친 적은 각도와 상관없이 맞는다
                    if (to.sqrMagnitude > 0.25f && Vector2.Angle(dir, to) > arc * 0.5f) continue;
                }
                hitCount++;
                // 베인 방향으로 불꽃이 튄다
                if (hitCount <= 6 && to.sqrMagnitude > 0.01f)
                    Vfx.SparksCone(e.Position, to.normalized, 30f, Color.Lerp(MainColor, Color.white, 0.5f), 3, 5f, 10f);
                DamageSystem.Deal(e, damage, Element.Fire);
            }
            // 여럿을 벨수록 손맛이 묵직하다
            if (hitCount > 0) Juice.Shake(Mathf.Min(0.08f + hitCount * 0.02f, 0.25f));
            if (hitCount >= 6) Juice.HitStop(0.03f);

            // 참격 연출
            Color color = MainColor;
            Sprite slash = Data.mainSprite;
            float k = Gfx.ScaleForWidth(slash, reach * 2f);
            float angle = Gfx.Angle(dir);
            mirror = !mirror;
            if (fullCircle)
            {
                for (int i = 0; i < 3; i++)
                {
                    SpriteFx.Play(slash, origin, angle + i * 120f, Vector3.one * k * 0.9f, Vector3.one * k * 1.05f,
                        color, Gfx.WithAlpha(color, 0f), 0.35f, Gfx.OrderFx, mirror ? -900f : 900f);
                }
                Fx.Shockwave(origin, reach, Gfx.WithAlpha(color, 0.7f), 0.35f);
                Fx.Glow(origin, reach * 2.6f, Gfx.WithAlpha(color, 0.4f), 0.3f);
                for (int i = 0; i < 14; i++)
                {
                    Vector2 d = Gfx.Dir(i * (360f / 14f) + Random.Range(-10f, 10f));
                    Vector2 tangent = new Vector2(-d.y, d.x) * (mirror ? -1f : 1f);
                    Vfx.Glow(origin + d * reach * Random.Range(0.7f, 1f), tangent * Random.Range(2f, 5f) + d * 1.5f,
                        Random.Range(0.12f, 0.24f), Random.Range(0.3f, 0.55f), color);
                }
            }
            else
            {
                float sweep = mirror ? -1f : 1f;
                Vector3 from = new Vector3(k * 0.85f, k * 0.85f * sweep, 1f);
                Vector3 to = new Vector3(k * 1.05f, k * 1.05f * sweep, 1f);
                SpriteFx.Play(slash, origin, angle - 25f * sweep, from, to,
                    color, Gfx.WithAlpha(color, 0f), 0.22f, Gfx.OrderFx, 220f * sweep);
                // 밝은 잔상 (가산 혼합): 조금 늦게, 조금 더 크게 따라온다
                Color bright = Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.55f), 0.7f);
                SpriteFx.Play(slash, origin, angle - 40f * sweep, from * 0.92f, to * 1.08f,
                    bright, Gfx.WithAlpha(bright, 0f), 0.16f, Gfx.OrderFx + 1, 300f * sweep, true);
                Fx.Glow(origin + dir * (reach * 0.55f), reach * 1.5f, Gfx.WithAlpha(color, 0.35f), 0.2f);
                // 칼끝 궤적을 따라 흩날리는 불티
                float half = Mathf.Min(arc, 200f) * 0.5f;
                for (int i = 0; i < 9; i++)
                {
                    Vector2 d = Gfx.Rotate(dir, Random.Range(-half, half));
                    Vfx.Glow(origin + d * reach * Random.Range(0.6f, 1f), d * Random.Range(1.5f, 4.5f),
                        Random.Range(0.1f, 0.22f), Random.Range(0.25f, 0.5f), color);
                }
            }

            // 화염 검기
            if (s.special > 0f)
            {
                Pool<SwordWave>.Get().Launch(this, origin + dir * (reach * 0.4f), dir,
                    damage * s.special, s.speed, s.range, reach * 0.9f, color);
            }
        }
    }

    /// <summary>화염 검기: 직선으로 날아가며 적을 관통한다. 같은 적은 한 번만 맞는다.</summary>
    public class SwordWave : Pooled
    {
        static readonly List<Enemy> buffer = new List<Enemy>(32);

        readonly HashSet<int> hitIds = new HashSet<int>();
        Transform tr;
        SpriteRenderer sr;
        Vector2 dir;
        float speed, range, traveled, damage, width;
        Color color;

        protected override void OnCreated()
        {
            tr = transform;
            sr = Gfx.NewSprite("Visual", tr, null, Gfx.OrderProjectile, Color.white);
        }

        public void Launch(FlameSwordSkill skill, Vector2 pos, Vector2 direction, float waveDamage,
            float waveSpeed, float waveRange, float waveWidth, Color waveColor)
        {
            dir = direction;
            speed = waveSpeed;
            range = waveRange;
            damage = waveDamage;
            width = waveWidth;
            color = waveColor;
            traveled = 0f;
            hitIds.Clear();

            tr.position = pos;
            tr.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(dir));
            sr.sprite = skill.Data.effectSprite;
            sr.color = color;
            // 검기 스프라이트는 세로로 긴 초승달 모양 → 높이를 폭에 맞춘다
            float k = sr.sprite != null ? width / sr.sprite.bounds.size.y : 1f;
            sr.transform.localScale = new Vector3(k, k, 1f);
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            Vector2 pos = (Vector2)tr.position + dir * step;
            tr.position = pos;
            traveled += step;

            EnemyQuery.Circle(pos, width * 0.5f, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Enemy e = buffer[i];
                if (!hitIds.Add(e.Uid)) continue;
                DamageSystem.Deal(e, damage, Element.Fire);
            }

            float remain = 1f - traveled / range;
            sr.color = Gfx.WithAlpha(color, Mathf.Clamp01(remain * 3f));
            // 검기가 지나간 자리에 불티가 남는다
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < 2; i++)
                Vfx.Glow(pos + side * Random.Range(-0.5f, 0.5f) * width, -dir * Random.Range(0.5f, 2f),
                    Random.Range(0.12f, 0.26f), Random.Range(0.2f, 0.4f), Gfx.WithAlpha(color, Mathf.Clamp01(remain * 3f)));
            if (traveled >= range) Release();
        }
    }
}
