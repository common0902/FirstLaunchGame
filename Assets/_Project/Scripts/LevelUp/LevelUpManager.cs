using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    public enum LevelUpOptionType { Skill, Passive, Evolution, Heal }

    public class LevelUpOption
    {
        public LevelUpOptionType type;
        public SkillData skill;
        public PassiveData passive;
        public int nextLevel;
        public bool isNew;
        public float weight;
        /// <summary>보유 액티브 스킬의 강화 카드인지 (최소 1장 보장 대상)</summary>
        public bool isOwnedSkillUpgrade;
        /// <summary>히든 4번째 카드 여부</summary>
        public bool isHidden;

        public string title;
        public string tag;
        public string levelText;
        public string description;
        public Sprite icon;
        public Color accent;
    }

    /// <summary>
    /// 레벨업 후보 선정 규칙 (2-1, 2-2).
    /// - 후보 최대 3개. 보유 액티브 강화 카드가 있으면 최소 1장 보장, 나머지는 가중치 무작위.
    /// - 슬롯이 가득 차면 새 스킬 제외, 만렙 스킬 제외, 후보가 모자라면 있는 만큼만.
    /// - 궁극기는 같은 원소 2개일 때만, 플레이어 레벨이 기준 이상일 때부터 등장.
    /// - 히든 카드: 진화 가능한 궁극기의 최종 레벨 카드가 후보에 나왔고 조건 패시브가 만렙이면 4번째 카드로 추가.
    /// </summary>
    public class LevelUpManager
    {
        const int MaxOptions = 3;

        readonly GameManager gm;
        readonly List<LevelUpOption> pool = new List<LevelUpOption>(24);

        public LevelUpManager(GameManager manager) => gm = manager;

        /// <summary>시작 스킬: 장착한 원소의 일반기 중 무작위 3개</summary>
        public List<LevelUpOption> BuildStartOptions()
        {
            pool.Clear();
            foreach (SkillData skill in gm.Database.skills)
            {
                if (skill == null || skill.kind != SkillKind.Normal || !gm.HasElement(skill.element)) continue;
                pool.Add(MakeSkillOption(skill, 0, 1f));
            }
            return Draw(false);
        }

        public List<LevelUpOption> BuildOptions()
        {
            GameConfig cfg = gm.ConfigAsset;
            SkillManager skills = gm.Player.Skills;
            int playerLevel = gm.Player.Experience.Level;
            bool sameElement = gm.SlotA == gm.SlotB;
            pool.Clear();

            foreach (SkillData skill in gm.Database.skills)
            {
                if (skill == null || !gm.HasElement(skill.element)) continue;
                int level = skills.LevelOf(skill);
                if (level > 0)
                {
                    if (level >= skill.MaxLevel) continue;                 // 만렙 제외
                    pool.Add(MakeSkillOption(skill, level, cfg.ownedUpgradeWeight));
                    continue;
                }
                if (skills.Actives.Count >= cfg.maxActiveSlots) continue;   // 슬롯이 가득 차면 새 스킬 제외
                float weight;
                if (skill.kind == SkillKind.Ultimate)
                {
                    if (!sameElement || playerLevel < cfg.ultimateUnlockLevel) continue;
                    weight = cfg.ultimateWeight;
                }
                else
                {
                    // 보유 액티브가 적을수록 신규 스킬이 잘 나오게 해 초반에 슬롯이 늦게 차는 것을 막는다
                    weight = skills.Actives.Count <= cfg.fewActivesThreshold ? cfg.newSkillWeightWhenFew : cfg.newSkillWeight;
                }
                pool.Add(MakeSkillOption(skill, 0, weight));
            }

            foreach (PassiveData passive in gm.Database.passives)
            {
                if (passive == null) continue;
                int level = skills.PassiveLevel(passive);
                if (level >= passive.maxLevel) continue;
                if (level == 0 && skills.Passives.Count >= cfg.maxPassiveSlots) continue;
                pool.Add(MakePassiveOption(passive, level, cfg.passiveWeight));
            }

            List<LevelUpOption> result = Draw(true);
            // TODO(미정): 후보가 0개일 때의 대체 카드. 지금은 빈 목록을 돌려주고 호출 쪽에서 창을 열지 않는다.
            AppendHiddenEvolution(result, skills);
            return result;
        }

        /// <summary>pool에서 가중치 무작위로 최대 3장을 뽑는다</summary>
        List<LevelUpOption> Draw(bool guaranteeOwnedUpgrade)
        {
            var result = new List<LevelUpOption>(4);
            if (guaranteeOwnedUpgrade)
            {
                LevelUpOption guaranteed = PickWeighted(true);
                if (guaranteed != null) { result.Add(guaranteed); pool.Remove(guaranteed); }
            }
            while (result.Count < MaxOptions && pool.Count > 0)
            {
                LevelUpOption pick = PickWeighted(false);
                if (pick == null) break;
                result.Add(pick);
                pool.Remove(pick);
            }
            // 보장 카드가 항상 맨 위에 오지 않도록 섞는다
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                LevelUpOption tmp = result[i]; result[i] = result[j]; result[j] = tmp;
            }
            return result;
        }

        LevelUpOption PickWeighted(bool ownedUpgradeOnly)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (!ownedUpgradeOnly || pool[i].isOwnedSkillUpgrade) total += Mathf.Max(0.0001f, pool[i].weight);
            if (total <= 0f) return null;
            float roll = Random.value * total;
            LevelUpOption last = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (ownedUpgradeOnly && !pool[i].isOwnedSkillUpgrade) continue;
                last = pool[i];
                roll -= Mathf.Max(0.0001f, pool[i].weight);
                if (roll <= 0f) return last;
            }
            return last;
        }

        void AppendHiddenEvolution(List<LevelUpOption> options, SkillManager skills)
        {
            for (int i = 0; i < options.Count; i++)
            {
                LevelUpOption o = options[i];
                if (o.type != LevelUpOptionType.Skill || o.isNew) continue;
                SkillData skill = o.skill;
                if (!skill.hasEvolution || skill.evolutionRequiredPassive == null) continue;
                if (o.nextLevel != skill.MaxLevel) continue;   // 최종 레벨 카드가 나온 경우에만
                PassiveData req = skill.evolutionRequiredPassive;
                if (skills.PassiveLevel(req) < req.maxLevel) continue;

                options.Add(new LevelUpOption
                {
                    type = LevelUpOptionType.Evolution,
                    skill = skill,
                    nextLevel = skill.MaxLevel,
                    isHidden = true,
                    title = skill.evolutionName,
                    tag = "특수 진화",
                    levelText = "히든",
                    description = skill.evolutionDescription,
                    icon = skill.evolutionIcon != null ? skill.evolutionIcon : skill.icon,
                    accent = new Color(1f, 0.85f, 0.3f)
                });
                return;
            }
        }

        LevelUpOption MakeSkillOption(SkillData skill, int currentLevel, float weight)
        {
            GameDatabase db = gm.Database;
            bool isNew = currentLevel == 0;
            int next = currentLevel + 1;
            string elementName = skill.element == Element.Fire ? "불" : "물";
            string effect = skill.GetStats(next).description;
            return new LevelUpOption
            {
                type = LevelUpOptionType.Skill,
                skill = skill,
                nextLevel = next,
                isNew = isNew,
                weight = weight,
                isOwnedSkillUpgrade = !isNew,
                title = skill.displayName,
                tag = elementName + (skill.kind == SkillKind.Ultimate ? " · 궁극기" : " · 일반기"),
                levelText = isNew ? "신규" : "Lv." + currentLevel + " → Lv." + next,
                description = isNew ? skill.summary : effect,
                icon = skill.icon,
                accent = skill.element == Element.Fire ? db.fireColor : db.waterColor
            };
        }

        static LevelUpOption MakePassiveOption(PassiveData passive, int currentLevel, float weight)
        {
            bool isNew = currentLevel == 0;
            int next = currentLevel + 1;
            return new LevelUpOption
            {
                type = LevelUpOptionType.Passive,
                passive = passive,
                nextLevel = next,
                isNew = isNew,
                weight = weight,
                title = passive.displayName,
                tag = "패시브",
                levelText = isNew ? "신규" : "Lv." + currentLevel + " → Lv." + next,
                description = passive.DescribeLevel(next),
                icon = passive.icon,
                accent = new Color(0.75f, 0.8f, 0.9f)
            };
        }

        public void Apply(LevelUpOption option)
        {
            SkillManager skills = gm.Player.Skills;
            switch (option.type)
            {
                case LevelUpOptionType.Skill:
                    // 진화 조건에서 기본 최종 레벨을 고르면 그 스킬은 만렙이 되어 다시 나오지 않으므로 진화는 그 판에서 소멸한다
                    skills.Acquire(option.skill);
                    break;
                case LevelUpOptionType.Passive:
                    skills.AcquirePassive(option.passive);
                    break;
                case LevelUpOptionType.Evolution:
                    skills.Acquire(option.skill, true);
                    break;
                case LevelUpOptionType.Heal:
                    gm.Player.Health.Heal(gm.Player.Health.MaxHp * gm.ConfigAsset.fallbackHealRatio);
                    break;
            }
        }

        /// <summary>
        /// 후보가 0개일 때(모든 스킬·패시브가 최대 레벨이거나 슬롯이 가득 참) 대신 보여 주는 체력 회복 카드
        /// </summary>
        public LevelUpOption MakeHealOption()
        {
            float ratio = gm.ConfigAsset.fallbackHealRatio;
            return new LevelUpOption
            {
                type = LevelUpOptionType.Heal,
                weight = 1f,
                title = "체력 회복",
                tag = "회복",
                levelText = "",
                description = "최대 체력의 " + Mathf.RoundToInt(ratio * 100f) + "%를 회복합니다.",
                icon = gm.Database.healIcon,
                accent = new Color(0.45f, 0.95f, 0.55f)
            };
        }
    }
}
