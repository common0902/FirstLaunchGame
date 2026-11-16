using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 격류: 화면 양쪽 바깥까지 이어지는 긴 직선 물살.
    /// 적은 흐름 방향으로 밀리고(지속형 힘), 플레이어는 흐름을 따라 이동할 때만 가속한다.
    /// Lv.5 격류 2개 / Lv.7 더 거센 격류.
    /// TODO(미정): Lv.6 효과는 기획이 확정되지 않았다. 현재는 SkillData에서 수치만 소폭 강화해 두었다.
    /// 수치: damage(틱당 피해), cooldown, count(격류 수), area(격류 폭), duration, tickInterval,
    ///       speed(물살 강도), special(플레이어 가속 배율)
    /// </summary>
    public class TorrentSkill : SkillBase
    {
        protected override bool TryActivate()
        {
            SkillLevelStats s = Stats;
            int n = Mathf.Max(1, s.count);
            float searchRange = Data.Extra("searchRange", 8f);
            float angleSpread = Data.Extra("angleSpread", 45f);
            for (int i = 0; i < n; i++)
            {
                // 위치: 적 밀집 지역을 어느 정도 우선하는 반무작위
                Vector2 point;
                if (Random.value < Data.Extra("denseChance", 0.6f) &&
                    Targeting.DensePoint(player.Position, searchRange, 2f, out Vector2 dense))
                {
                    point = dense + Random.insideUnitCircle * 1.5f;
                }
                else
                {
                    Enemy e = Targeting.RandomInRange(player.Position, searchRange);
                    point = e != null ? e.Position : player.Position + Random.insideUnitCircle * 5f;
                }

                // 방향: 플레이어 → 생성 지점의 바깥 방향 기준 ±45도
                Vector2 outward = point - player.Position;
                outward = outward.sqrMagnitude > 0.25f ? outward.normalized : Gfx.Dir(Random.Range(0f, 360f));
                Vector2 dir = Gfx.Rotate(outward, Random.Range(-angleSpread, angleSpread));
                Pool<TorrentStream>.Get().Launch(this, point, dir);
            }
            return true;
        }
    }

    public class TorrentStream : Pooled
    {
        const int ChevronCount = 12;
        static readonly List<Enemy> buffer = new List<Enemy>(96);

        readonly SpriteRenderer[] chevrons = new SpriteRenderer[ChevronCount];
        Transform tr;
        SpriteRenderer body;
        PlayerController player;
        Vector2 origin, dir, perp;
        float length, width, duration, time, tickTimer, tickInterval, damage, strength, playerBoost;
        float flowOffset;
        bool final;
        Color color;

        protected override void OnCreated()
        {
            tr = transform;
            body = Gfx.NewSprite("Body", tr, null, Gfx.OrderZone, Color.white);
            for (int i = 0; i < ChevronCount; i++)
                chevrons[i] = Gfx.NewSprite("Foam", tr, null, Gfx.OrderZone + 1, Color.white);
        }

        public void Launch(TorrentSkill skill, Vector2 point, Vector2 direction)
        {
            player = GameManager.I.Player;
            SkillLevelStats s = skill.Stats;
            origin = point;
            dir = direction;
            perp = new Vector2(-dir.y, dir.x);
            length = skill.Data.Extra("length", 60f);
            width = skill.Area(s.area);
            duration = s.duration;
            tickInterval = Mathf.Max(0.05f, s.tickInterval);
            damage = skill.Dmg(s.damage);
            strength = s.speed;
            playerBoost = s.special;
            final = skill.IsFinal;
            color = skill.MainColor;
            time = 0f;
            tickTimer = tickInterval;
            flowOffset = Random.Range(0f, 10f);

            tr.position = origin;
            tr.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(dir));
            body.sprite = skill.Data.mainSprite;
            body.transform.localScale = Gfx.ScaleFor(body.sprite, length, width);
            for (int i = 0; i < ChevronCount; i++)
            {
                chevrons[i].sprite = skill.Data.effectSprite;
                float h = width * (final ? 0.5f : 0.38f);
                chevrons[i].transform.localScale = Gfx.ScaleFor(chevrons[i].sprite, h * 0.7f, h);
            }
            UpdateVisuals(0f);
            // 물살이 터져 나오는 순간
            Fx.Shockwave(origin, width * 1.2f, Gfx.WithAlpha(color, 0.7f), 0.4f);
            Vfx.Splash(origin, color, 1.8f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (time >= duration) { Release(); return; }
            float fade = Mathf.Clamp01(Mathf.Min(time / 0.4f, (duration - time) / 0.6f));
            UpdateVisuals(fade);

            // 매우 낮은 지속 피해 + 젖음
            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer += tickInterval;
                EnemyQuery.Box(origin, new Vector2(length, width), Gfx.Angle(dir), buffer);
                for (int i = 0; i < buffer.Count; i++)
                    DamageSystem.Deal(buffer[i], damage, Element.Water);
            }

            // 플레이어: 물살 안에서 흐름을 따라 이동할 때만 가속. 반대로 가도 밀지 않는다.
            Vector2 rel = player.Position - origin;
            bool inside = Mathf.Abs(Vector2.Dot(rel, perp)) <= width * 0.5f &&
                          Mathf.Abs(Vector2.Dot(rel, dir)) <= length * 0.5f;
            if (inside && Vector2.Dot(player.MoveDir, dir) > 0.3f)
                player.RequestSpeedBoost(playerBoost);
        }

        void FixedUpdate()
        {
            if (GameManager.I.State != GameState.Playing) return;
            // 적: 흐름 방향으로 밀림 (지속형 힘). 격류 2개가 겹치는 구역은 적 쪽에서 합산·상한 처리된다.
            EnemyQuery.Box(origin, new Vector2(length, width), Gfx.Angle(dir), buffer);
            Vector2 force = dir * strength;
            for (int i = 0; i < buffer.Count; i++) buffer[i].AddContinuousForce(force);
        }

        void UpdateVisuals(float fade)
        {
            body.color = Gfx.WithAlpha(color, (final ? 0.5f : 0.38f) * fade);

            // 포말: 카메라 근처 구간에서만 흐름 방향으로 흘러가게 해 물살의 방향이 읽히게 한다
            const float Span = 26f;
            float spacing = Span / ChevronCount;
            flowOffset += Time.deltaTime * (strength * 1.6f + 2f);
            Vector2 view = GameManager.I.Cam != null ? (Vector2)GameManager.I.Cam.transform.position : player.Position;
            float along = Vector2.Dot(view - origin, dir);
            float snapped = Mathf.Floor(along / spacing) * spacing;
            for (int i = 0; i < ChevronCount; i++)
            {
                float local = Mathf.Repeat(flowOffset + i * spacing, Span) - Span * 0.5f;
                float x = snapped + local;
                float lane = ((i % 3) - 1) * width * 0.28f;
                chevrons[i].transform.localPosition = new Vector3(x, lane, 0f);
                float edge = 1f - Mathf.Abs(local) / (Span * 0.5f);
                chevrons[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(edge * 3f) * (final ? 0.9f : 0.65f) * fade);
            }

            // 물살을 타고 흘러가는 물거품과 가장자리의 잔물결 (카메라 근처 구간에서만)
            if (fade > 0.1f && Time.timeScale > 0f)
            {
                float flow = strength * 1.6f + 2f;
                int n = final ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    float x = along + Random.Range(-Span * 0.45f, Span * 0.45f);
                    if (Mathf.Abs(x) > length * 0.5f) continue;
                    bool rim = Random.value < 0.35f;
                    float y = rim ? (Random.value < 0.5f ? -0.5f : 0.5f) * width : Random.Range(-0.45f, 0.45f) * width;
                    Vector2 p = origin + dir * x + perp * y;
                    if (rim)
                        Vfx.Glow(p, dir * flow * 0.3f, Random.Range(0.18f, 0.32f), 0.45f, Gfx.WithAlpha(Color.white, 0.4f * fade));
                    else
                        Vfx.Streak(p, dir * flow * Random.Range(0.8f, 1.3f), Random.Range(0.07f, 0.12f), Random.Range(0.3f, 0.5f),
                            Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.6f), 0.7f * fade));
                }
            }
        }
    }
}
