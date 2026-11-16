using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 모든 액티브 스킬의 기반. 레벨, 쿨타임 루프, 패시브 배율 적용을 담당한다.
    /// 각 스킬은 TryActivate()와 필요하면 Tick()/FixedTick()/OnLevelChanged()만 구현한다.
    /// </summary>
    public abstract class SkillBase : MonoBehaviour
    {
        public SkillData Data { get; private set; }
        public int Level { get; private set; }
        /// <summary>특수 진화 여부 (진화 데이터 세트를 사용)</summary>
        public bool Evolved { get; private set; }

        protected PlayerController player;
        protected SkillManager manager;
        protected GameDatabase db;
        protected float cooldownTimer;

        public void Setup(SkillData data, PlayerController owner, SkillManager skillManager)
        {
            Data = data;
            player = owner;
            manager = skillManager;
            db = GameManager.Db;
            Level = 1;
            cooldownTimer = 0.3f;
            OnAcquired();
            OnLevelChanged();
        }

        public void LevelUp(bool evolve)
        {
            if (Level < Data.MaxLevel) Level++;
            if (evolve && Data.hasEvolution) Evolved = true;
            OnLevelChanged();
        }

        /// <summary>현재 적용 중인 수치. 진화했으면 진화 데이터 세트를 쓴다.</summary>
        public SkillLevelStats Stats => Evolved ? Data.evolutionStats : Data.GetStats(Level);

        public bool IsFinal => Level >= Data.MaxLevel;
        /// <summary>최종 단계면 푸른 화염 등 최종 색을 쓴다</summary>
        public Color MainColor => IsFinal ? Data.finalColor : Data.color;

        public float Dmg(float value) => value * manager.DamageMul;
        public float Area(float value) => value * manager.AreaMul;
        public float CooldownTime => Mathf.Max(0.05f, Stats.cooldown * manager.CooldownMul);

        public virtual void Tick(float dt)
        {
            cooldownTimer -= dt;
            if (cooldownTimer > 0f) return;
            // 발동 조건(대상 없음 등)이 안 맞으면 잠시 뒤 다시 시도
            cooldownTimer = TryActivate() ? CooldownTime : 0.15f;
        }

        public virtual void FixedTick(float dt) { }

        protected virtual bool TryActivate() => true;
        protected virtual void OnAcquired() { }
        protected virtual void OnLevelChanged() { }
    }
}
