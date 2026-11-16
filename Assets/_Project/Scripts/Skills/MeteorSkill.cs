using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 운석: 여러 운석이 차례로 떨어지는 광역 폭격. 파편·추가 투사체·장판 없음.
    /// Lv.3 3개 / Lv.6 5개 / Lv.7 푸른 화염 운석과 큰 화염 파동.
    /// 수치: damage, cooldown, count(운석 수), area(폭발 반지름), duration(낙하 시간),
    ///       tickInterval(운석 사이 간격), range(대상 탐색 반경)
    /// </summary>
    public class MeteorSkill : SkillBase
    {
        int dropsLeft;
        float dropTimer;

        protected override bool TryActivate()
        {
            if (!Targeting.AnyInRange(player.Position, Stats.range)) return false;
            dropsLeft = Mathf.Max(1, Stats.count);
            dropTimer = 0f;
            return true;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (dropsLeft <= 0) return;
            dropTimer -= dt;
            if (dropTimer > 0f) return;

            SkillLevelStats s = Stats;
            float radius = Area(s.area);
            Vector2 target;
            // 첫 발은 가장 몰린 곳, 나머지는 범위 안의 무작위 적
            if (dropsLeft == Mathf.Max(1, s.count))
            {
                if (!Targeting.DensePoint(player.Position, s.range, radius, out target))
                    target = player.Position + Random.insideUnitCircle * s.range * 0.6f;
            }
            else
            {
                Enemy e = Targeting.RandomInRange(player.Position, s.range);
                target = e != null ? e.Position : player.Position + Random.insideUnitCircle * s.range * 0.6f;
            }

            Pool<MeteorStrike>.Get().Launch(this, target, radius, Dmg(s.damage), s.duration);
            dropsLeft--;
            dropTimer = s.tickInterval;
        }
    }

    public class MeteorStrike : Pooled
    {
        static readonly Vector2 FallOffset = new Vector2(2.4f, 7.5f);

        Transform tr;
        SpriteRenderer warning, warningRing, rock, rockGlow;
        float trailTimer;
        Vector2 target;
        float radius, damage, fallTime, time;
        bool final;
        Color color;

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            warning = Gfx.NewSprite("Warning", tr, db.softCircle, Gfx.OrderZone + 2, Color.white);
            warningRing = Gfx.NewSprite("WarningRing", tr, db.ring, Gfx.OrderZone + 3, Color.white);
            rockGlow = Gfx.NewGlow("MeteorGlow", tr, db.softCircle, Gfx.OrderFx + 1, Color.white);
            rock = Gfx.NewSprite("Meteor", tr, null, Gfx.OrderFx + 2, Color.white);
        }

        public void Launch(MeteorSkill skill, Vector2 pos, float explosionRadius, float explosionDamage, float fall)
        {
            target = pos;
            radius = explosionRadius;
            damage = explosionDamage;
            fallTime = Mathf.Max(0.1f, fall);
            time = 0f;
            final = skill.IsFinal;
            color = skill.MainColor;

            tr.position = pos;
            Gfx.SetDiameter(warning.transform, warning.sprite, radius * 2f);
            Gfx.SetDiameter(warningRing.transform, warningRing.sprite, radius * 2f);
            rock.sprite = final && skill.Data.subSprite != null ? skill.Data.subSprite : skill.Data.mainSprite;
            // 운석 크기는 폭발 범위를 따라가되, 화면을 가리지 않게 상한을 둔다
            Gfx.SetDiameter(rock.transform, rock.sprite, Mathf.Min(radius * (final ? 0.95f : 0.85f), final ? 2.8f : 2.2f));
            // 운석 스프라이트는 머리가 아래를 향한다 → 낙하 방향에 맞춰 회전
            rock.transform.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(-FallOffset) + 90f);
            Gfx.SetDiameter(rockGlow.transform, rockGlow.sprite, Mathf.Min(radius * 2f, 5f));
            rockGlow.color = Gfx.WithAlpha(color, 0.5f);
            trailTimer = 0f;
            Apply(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            float t = time / fallTime;
            if (t < 1f)
            {
                Apply(t);
                // 떨어지는 동안 불꼬리와 연기를 남긴다
                trailTimer -= dt;
                if (trailTimer <= 0f)
                {
                    trailTimer = 0.016f;
                    Vector2 p = rock.transform.position;
                    Vector2 back = FallOffset.normalized;
                    Vfx.Glow(p + back * 0.3f + Random.insideUnitCircle * 0.2f, back * Random.Range(1f, 3f),
                        Random.Range(0.35f, 0.7f), Random.Range(0.25f, 0.4f), Gfx.WithAlpha(color, 0.8f));
                    Vfx.Smoke(p + back * 0.5f, back * 1.2f + Random.insideUnitCircle * 0.5f,
                        Random.Range(0.5f, 0.9f), Random.Range(0.5f, 0.8f), new Color(0.13f, 0.11f, 0.12f, 0.3f));
                    if (Random.value < 0.5f)
                        Vfx.Streak(p, Gfx.Rotate(back, Random.Range(-40f, 40f)) * Random.Range(3f, 7f), 0.1f, 0.3f,
                            Color.Lerp(color, Color.white, 0.4f));
                }
                return;
            }

            AreaDamage.Circle(target, radius, damage, Element.Fire);
            Fx.Explosion(target, radius, color);
            // 착탄: 큰 섬광 + 이중 충격파 + 사방으로 튀는 불똥 + 오래 남는 그을음
            Fx.Glow(target, radius * 4f, Gfx.WithAlpha(color, 0.55f), 0.32f);
            Fx.Shockwave(target, radius * 1.5f, Gfx.WithAlpha(color, 0.8f), 0.42f);
            Vfx.Sparks(target, Color.Lerp(color, Color.white, 0.5f), 12, radius * 4f, radius * 9f);
            Vfx.Burst(target, color, 10, radius * 2f, radius * 5f, 0.14f, 0.3f, 0.4f, 0.8f);
            Vfx.SmokePuff(target, new Color(0.12f, 0.1f, 0.11f, 0.38f), 5, radius * 0.6f, radius * 1.1f, 1.1f);
            Fx.Decal(target, radius * 0.95f, new Color(0.02f, 0.02f, 0.03f, 0.42f), 3.2f);
            Juice.Shake(final ? 0.6f : 0.42f);
            Juice.HitStop(final ? 0.07f : 0.05f);
            if (final)
            {
                Fx.Shockwave(target, radius * 1.9f, color, 0.55f);
                Fx.Shockwave(target, radius * 1.1f, Color.Lerp(color, Color.white, 0.5f), 0.35f);
                Fx.Glow(target, radius * 5.5f, Gfx.WithAlpha(color, 0.4f), 0.45f);
            }
            Release();
        }

        void Apply(float t)
        {
            Vector2 p = target + FallOffset * (1f - t * t);
            rock.transform.position = p;
            rockGlow.transform.position = p;
            // 경고 표시: 떨어질수록 빠르게 깜빡이고 고리가 안쪽으로 조여든다
            float blink = 0.5f + 0.5f * Mathf.Sin(t * t * 40f);
            warning.color = new Color(1f, 0.2f, 0.1f, 0.06f + 0.12f * t + 0.05f * blink);
            warningRing.color = new Color(1f, 0.3f, 0.15f, 0.35f + 0.4f * t);
            Gfx.SetDiameter(warningRing.transform, warningRing.sprite, radius * 2f * Mathf.Lerp(1.35f, 1f, t));
        }
    }
}
