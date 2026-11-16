using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 수압 분출: 매번 무작위 방향으로 직선 물줄기를 분사한다. 분사 중 방향은 유지된다.
    /// 지속 피해 + 밀어내기(지속형 힘). 여러 줄기는 각도를 분산한다.
    /// Lv.4 2개 / Lv.6 3개 / Lv.7 압축 수압 파동이 물줄기를 따라 진행하는 맥동 분사.
    /// 수치: damage(틱당 피해), cooldown, count(줄기 수), area(분사 폭), range(사거리), duration(분사 시간),
    ///       tickInterval, speed(밀어내는 힘), special(파동 피해 배율, 0이면 없음), special2(파동 간격)
    /// </summary>
    public class WaterJetSkill : SkillBase
    {
        protected override bool TryActivate()
        {
            SkillLevelStats s = Stats;
            if (!Targeting.AnyInRange(player.Position, s.range * 1.5f)) return false;

            int n = Mathf.Max(1, s.count);
            float baseAngle = Random.Range(0f, 360f);
            float jitter = Data.Extra("angleJitter", 25f);
            for (int i = 0; i < n; i++)
            {
                // 줄기끼리 겹치지 않게 균등하게 벌린 뒤 약간 흔든다
                float angle = baseAngle + i * (360f / n) + (n > 1 ? Random.Range(-jitter, jitter) : 0f);
                Pool<WaterJetBeam>.Get().Launch(this, Gfx.Dir(angle));
            }
            // 분사 시작: 발밑에서 물이 터져 나온다
            Fx.Shockwave(player.Position, 1.4f, Gfx.WithAlpha(MainColor, 0.7f), 0.3f);
            Fx.Glow(player.Position, 2.4f, Gfx.WithAlpha(MainColor, 0.4f), 0.25f);
            Juice.Shake(0.08f);
            return true;
        }
    }

    public class WaterJetBeam : Pooled
    {
        static readonly List<Enemy> buffer = new List<Enemy>(48);

        readonly HashSet<int> pulseHits = new HashSet<int>();
        Transform tr;
        SpriteRenderer beam, pulse, core;
        Color color;
        WaterJetSkill owner;
        PlayerController player;
        Vector2 dir;
        float length, width, duration, time, tickTimer, tickInterval, damage, push;
        float pulseMul, pulseInterval, pulseTimer, pulsePos, pulseSpeed;
        bool pulseActive;

        protected override void OnCreated()
        {
            tr = transform;
            // 물줄기는 플레이어와 적을 가리지 않게 그 아래에 그린다
            beam = Gfx.NewSprite("Beam", tr, null, Gfx.OrderOrb + 2, Color.white);
            core = Gfx.NewGlow("Core", tr, null, Gfx.OrderOrb + 3, Color.white);
            pulse = Gfx.NewSprite("Pulse", tr, null, Gfx.OrderOrb + 3, Color.white);
        }

        public void Launch(WaterJetSkill skill, Vector2 direction)
        {
            owner = skill;
            player = GameManager.I.Player;
            SkillLevelStats s = skill.Stats;
            dir = direction;
            length = skill.Area(s.range);
            width = skill.Area(s.area);
            duration = s.duration;
            tickInterval = Mathf.Max(0.05f, s.tickInterval);
            damage = skill.Dmg(s.damage);
            push = s.speed;
            pulseMul = s.special;
            pulseInterval = Mathf.Max(0.2f, s.special2);
            pulseSpeed = length / skill.Data.Extra("pulseTravelTime", 0.35f);
            time = 0f;
            tickTimer = 0f;
            pulseTimer = pulseInterval * 0.5f;
            pulseActive = false;

            beam.sprite = skill.Data.mainSprite;
            core.sprite = skill.Data.mainSprite;
            color = skill.MainColor;
            pulse.sprite = skill.Data.effectSprite;
            pulse.gameObject.SetActive(false);
            tr.rotation = Quaternion.Euler(0f, 0f, Gfx.Angle(dir));
            UpdateTransform(0f);
        }

        Vector2 Center => player.Position + dir * (length * 0.5f);

        void UpdateTransform(float fade)
        {
            tr.position = player.Position;
            beam.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
            float wobble = 1f + 0.08f * Mathf.Sin(Time.time * 30f);
            beam.transform.localScale = Gfx.ScaleFor(beam.sprite, length, width * wobble * Mathf.Lerp(0.3f, 1f, fade));
            beam.color = new Color(1f, 1f, 1f, 0.75f * fade);
            // 물줄기 중심의 밝은 심
            core.transform.localPosition = beam.transform.localPosition;
            core.transform.localScale = Gfx.ScaleFor(core.sprite, length, width * 0.45f * (2f - wobble) * fade);
            core.color = Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.4f * fade);
        }

        /// <summary>물줄기를 따라 흐르는 물보라와 끝에서 흩어지는 물안개</summary>
        void EmitSpray(float fade)
        {
            if (fade <= 0.05f) return;
            Vector2 start = player.Position;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float flow = length / 0.35f;
            for (int i = 0; i < 2; i++)
            {
                float along = Random.Range(0.05f, 0.85f) * length;
                Vfx.Streak(start + dir * along + side * Random.Range(-0.45f, 0.45f) * width,
                    dir * flow * Random.Range(0.5f, 0.9f) + side * Random.Range(-1.5f, 1.5f),
                    Random.Range(0.08f, 0.14f), 0.18f, Gfx.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.8f * fade));
            }
            Vector2 tip = start + dir * length;
            Vfx.Glow(tip + side * Random.Range(-0.5f, 0.5f) * width, Gfx.Rotate(dir, Random.Range(-60f, 60f)) * Random.Range(1.5f, 4f),
                Random.Range(0.14f, 0.3f), Random.Range(0.25f, 0.45f), Gfx.WithAlpha(color, 0.7f * fade));
            if (Random.value < 0.25f)
                Vfx.Smoke(tip, dir * 1.2f, width * 1.2f, 0.5f, new Color(0.8f, 0.93f, 1f, 0.16f * fade));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (time >= duration) { Release(); return; }

            // 시작과 끝에서 물줄기가 차오르고 잦아든다
            float fade = Mathf.Clamp01(Mathf.Min(time / 0.15f, (duration - time) / 0.2f));
            UpdateTransform(fade);
            EmitSpray(fade);
            float angle = Gfx.Angle(dir);

            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer += tickInterval;
                EnemyQuery.Box(Center, new Vector2(length, width), angle, buffer);
                for (int i = 0; i < buffer.Count; i++)
                    DamageSystem.Deal(buffer[i], damage, Element.Water);
            }

            // Lv.7 맥동: 파동이 물줄기를 따라 나아가며 지나간 적에게 추가 피해
            if (pulseMul > 0f)
            {
                if (!pulseActive)
                {
                    pulseTimer -= dt;
                    if (pulseTimer <= 0f)
                    {
                        pulseActive = true;
                        pulsePos = 0f;
                        pulseHits.Clear();
                        pulse.gameObject.SetActive(true);
                    }
                }
                else
                {
                    pulsePos += pulseSpeed * dt;
                    float pulseLength = width * 1.2f;
                    pulse.transform.localPosition = new Vector3(pulsePos, 0f, 0f);
                    pulse.transform.localScale = Gfx.ScaleFor(pulse.sprite, pulseLength, width * 1.7f);
                    EnemyQuery.Box(player.Position + dir * pulsePos, new Vector2(pulseLength, width * 1.5f), angle, buffer);
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        if (!pulseHits.Add(buffer[i].Uid)) continue;
                        DamageSystem.Deal(buffer[i], damage * pulseMul, Element.Water);
                    }
                    Vector2 pulseCenter = player.Position + dir * pulsePos;
                    Vfx.Glow(pulseCenter, Random.insideUnitCircle * 2f, width * 0.9f, 0.16f, Gfx.WithAlpha(Color.white, 0.5f));
                    if (pulsePos >= length)
                    {
                        Vfx.Splash(player.Position + dir * length, color, 1.6f);
                        pulseActive = false;
                        pulseTimer = pulseInterval;
                        pulse.gameObject.SetActive(false);
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (GameManager.I.State != GameState.Playing) return;
            // 밀어내기: 지속형 힘 (2-3). 합산과 상한은 적 쪽에서 처리한다.
            EnemyQuery.Box(Center, new Vector2(length, width), Gfx.Angle(dir), buffer);
            Vector2 force = dir * push;
            for (int i = 0; i < buffer.Count; i++) buffer[i].AddContinuousForce(force);
        }
    }
}
