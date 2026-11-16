using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    public class PassiveSlot
    {
        public PassiveData data;
        public int level;
    }

    /// <summary>보유 액티브·패시브 관리와 스킬 갱신</summary>
    public class SkillManager : MonoBehaviour
    {
        public readonly List<SkillBase> Actives = new List<SkillBase>(5);
        public readonly List<PassiveSlot> Passives = new List<PassiveSlot>(5);

        public float AreaMul { get; private set; } = 1f;
        public float DamageMul { get; private set; } = 1f;
        public float CooldownMul { get; private set; } = 1f;
        public float MoveSpeedMul { get; private set; } = 1f;

        GameManager gm;
        PlayerController player;
        Transform skillRoot;

        public void Init(GameManager manager, PlayerController owner)
        {
            gm = manager;
            player = owner;
            skillRoot = new GameObject("Skills").transform;
            skillRoot.SetParent(transform, false);
        }

        public SkillBase Find(SkillData data)
        {
            for (int i = 0; i < Actives.Count; i++)
                if (Actives[i].Data == data) return Actives[i];
            return null;
        }

        public int LevelOf(SkillData data)
        {
            SkillBase s = Find(data);
            return s != null ? s.Level : 0;
        }

        public int PassiveLevel(PassiveData data)
        {
            for (int i = 0; i < Passives.Count; i++)
                if (Passives[i].data == data) return Passives[i].level;
            return 0;
        }

        /// <summary>스킬을 새로 얻거나 한 레벨 올린다</summary>
        public void Acquire(SkillData data, bool evolve = false)
        {
            SkillBase skill = Find(data);
            if (skill != null)
            {
                skill.LevelUp(evolve);
                return;
            }
            var go = new GameObject(data.id.ToString());
            go.transform.SetParent(skillRoot, false);
            skill = AddSkillComponent(go, data.id);
            skill.Setup(data, player, this);
            Actives.Add(skill);
        }

        static SkillBase AddSkillComponent(GameObject go, SkillId id)
        {
            switch (id)
            {
                case SkillId.Fireball: return go.AddComponent<FireballSkill>();
                case SkillId.FlameSword: return go.AddComponent<FlameSwordSkill>();
                case SkillId.FlameRing: return go.AddComponent<FlameRingSkill>();
                case SkillId.Meteor: return go.AddComponent<MeteorSkill>();
                case SkillId.WaterSwarm: return go.AddComponent<WaterSwarmSkill>();
                case SkillId.WaterJet: return go.AddComponent<WaterJetSkill>();
                case SkillId.Torrent: return go.AddComponent<TorrentSkill>();
                case SkillId.Rain: return go.AddComponent<RainSkill>();
                case SkillId.FirePillar: return go.AddComponent<FirePillarSkill>();
                default: return go.AddComponent<AbyssVortexSkill>();
            }
        }

        public void AcquirePassive(PassiveData data)
        {
            PassiveSlot slot = null;
            for (int i = 0; i < Passives.Count; i++)
                if (Passives[i].data == data) slot = Passives[i];
            if (slot == null)
            {
                slot = new PassiveSlot { data = data, level = 0 };
                Passives.Add(slot);
            }
            if (slot.level < data.maxLevel) slot.level++;
            RecalculateMultipliers();
        }

        EquipmentBonus equipment;

        /// <summary>장착 장비 효과를 반영한다 (판 시작 시 한 번)</summary>
        public void SetEquipmentBonus(EquipmentBonus bonus)
        {
            equipment = bonus;
            RecalculateMultipliers();
        }

        void RecalculateMultipliers()
        {
            float area = equipment.area, damage = equipment.damage, cooldown = equipment.cooldown, move = equipment.moveSpeed;
            for (int i = 0; i < Passives.Count; i++)
            {
                float v = Passives[i].data.valuePerLevel * Passives[i].level;
                switch (Passives[i].data.type)
                {
                    case PassiveType.Area: area += v; break;
                    case PassiveType.Damage: damage += v; break;
                    case PassiveType.Cooldown: cooldown += v; break;
                    case PassiveType.MoveSpeed: move += v; break;
                }
            }
            AreaMul = 1f + area;
            DamageMul = 1f + damage;
            CooldownMul = Mathf.Max(0.3f, 1f - cooldown);
            MoveSpeedMul = 1f + move;
        }

        void Update()
        {
            if (gm == null || gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < Actives.Count; i++) Actives[i].Tick(dt);
        }

        void FixedUpdate()
        {
            if (gm == null || gm.State != GameState.Playing) return;
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < Actives.Count; i++) Actives[i].FixedTick(dt);
        }
    }
}
