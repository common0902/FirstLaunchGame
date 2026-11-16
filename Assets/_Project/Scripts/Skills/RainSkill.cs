using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 소나기: 적 밀집 위치에 떨어지는 고정형 광역 지속 공격.
    /// 빗방울 개별 판정 없이 영역 단위로 주기 피해를 준다. 넉백·둔화 없음.
    /// Lv.4 서로 다른 밀집 지역에 2개 / Lv.7 집중 호우.
    /// 수치: damage(틱당 피해), cooldown, count(소나기 수), area(반지름), duration, tickInterval, range(탐색 반경)
    /// </summary>
    public class RainSkill : SkillBase
    {
        readonly List<Vector2> placed = new List<Vector2>(2);

        protected override bool TryActivate()
        {
            SkillLevelStats s = Stats;
            float radius = Area(s.area);
            int n = Mathf.Max(1, s.count);
            placed.Clear();
            for (int i = 0; i < n; i++)
            {
                // 이미 놓은 소나기와 겹치지 않는 다른 밀집 지역을 찾는다
                if (!Targeting.DensePoint(player.Position, s.range, radius, out Vector2 point, placed, radius * 1.6f))
                    break;
                placed.Add(point);
                Pool<RainZone>.Get().Launch(this, point, radius);
            }
            return placed.Count > 0;
        }
    }

    public class RainZone : Pooled
    {
        const int MaxStreaks = 16;
        static readonly List<Enemy> buffer = new List<Enemy>(64);

        readonly SpriteRenderer[] streaks = new SpriteRenderer[MaxStreaks];
        readonly Vector2[] streakPos = new Vector2[MaxStreaks];
        readonly float[] streakPhase = new float[MaxStreaks];
        Transform tr;
        SpriteRenderer floor, edge;
        Vector2 center;
        float radius, duration, time, tickTimer, tickInterval, damage;
        int streakCount;
        bool heavy;
        Color color;

        protected override void OnCreated()
        {
            tr = transform;
            GameDatabase db = GameManager.Db;
            floor = Gfx.NewSprite("Floor", tr, db.softCircle, Gfx.OrderZone, Color.white);
            edge = Gfx.NewSprite("Edge", tr, db.ring, Gfx.OrderZone + 1, Color.white);
            for (int i = 0; i < MaxStreaks; i++)
                streaks[i] = Gfx.NewSprite("Streak", tr, null, Gfx.OrderFx, Color.white);
        }

        public void Launch(RainSkill skill, Vector2 point, float zoneRadius)
        {
            SkillLevelStats s = skill.Stats;
            center = point;
            radius = zoneRadius;
            duration = s.duration;
            tickInterval = Mathf.Max(0.05f, s.tickInterval);
            damage = skill.Dmg(s.damage);
            heavy = skill.IsFinal;
            color = skill.MainColor;
            time = 0f;
            tickTimer = tickInterval * 0.5f;

            tr.position = center;
            Gfx.SetDiameter(floor.transform, floor.sprite, radius * 2.2f);
            Gfx.SetDiameter(edge.transform, edge.sprite, radius * 2f);

            streakCount = heavy ? MaxStreaks : MaxStreaks * 5 / 8;
            float streakHeight = heavy ? 1.3f : 0.9f;
            float streakWidth = heavy ? 0.16f : 0.09f;
            for (int i = 0; i < MaxStreaks; i++)
            {
                bool on = i < streakCount;
                streaks[i].gameObject.SetActive(on);
                if (!on) continue;
                streaks[i].sprite = skill.Data.mainSprite;
                streaks[i].transform.localScale = Gfx.ScaleFor(streaks[i].sprite, streakWidth, streakHeight);
                streakPos[i] = Random.insideUnitCircle * radius * 0.9f;
                streakPhase[i] = Random.value;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (time >= duration) { Release(); return; }
            float fade = Mathf.Clamp01(Mathf.Min(time / 0.3f, (duration - time) / 0.4f));

            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer += tickInterval;
                EnemyQuery.Circle(center, radius, buffer);
                for (int i = 0; i < buffer.Count; i++)
                    DamageSystem.Deal(buffer[i], damage, Element.Water);
            }

            // 바닥에 깔리는 옅은 물안개
            if (Random.value < (heavy ? 0.22f : 0.12f))
                Vfx.Smoke(center + Random.insideUnitCircle * radius * 0.8f, new Vector2(Random.Range(-0.3f, 0.3f), 0.3f),
                    radius * 0.7f, 0.9f, new Color(0.75f, 0.9f, 1f, 0.12f * fade));
            floor.color = Gfx.WithAlpha(color, (heavy ? 0.4f : 0.28f) * fade);
            edge.color = Gfx.WithAlpha(color, 0.55f * fade);

            // 빗줄기: 위에서 떨어져 바닥 지점에 닿으면 다른 곳에서 다시 시작
            float fallSpeed = heavy ? 3.2f : 2.4f;
            for (int i = 0; i < streakCount; i++)
            {
                streakPhase[i] += dt * fallSpeed;
                if (streakPhase[i] >= 1f)
                {
                    // 빗방울이 바닥에 닿는 순간: 물결 고리와 튀는 물방울
                    Vector2 land = center + streakPos[i];
                    float k = Gfx.ScaleForWidth(edge.sprite, heavy ? 0.75f : 0.5f);
                    SpriteFx.Play(edge.sprite, land, 0f, Vector3.one * k * 0.2f, Vector3.one * k,
                        Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.6f * fade), Gfx.WithAlpha(color, 0f), 0.3f, Gfx.OrderZone + 2);
                    Vfx.Glow(land, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.8f, 2.2f)), heavy ? 0.16f : 0.11f, 0.25f,
                        Gfx.WithAlpha(Color.white, 0.7f * fade));
                    streakPhase[i] -= 1f;
                    streakPos[i] = Random.insideUnitCircle * radius * 0.9f;
                }
                float p = streakPhase[i];
                streaks[i].transform.localPosition = streakPos[i] + new Vector2(0.25f, 1.6f) * (1f - p);
                streaks[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(p * 2.5f) * (heavy ? 0.95f : 0.75f) * fade);
            }
        }
    }
}
