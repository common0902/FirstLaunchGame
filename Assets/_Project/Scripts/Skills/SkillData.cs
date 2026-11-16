using System;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 스킬 한 레벨의 수치. 필드의 의미는 스킬마다 다르며 SkillData 에셋의 fieldGuide에 적혀 있다.
    /// </summary>
    [Serializable]
    public struct SkillLevelStats
    {
        [Tooltip("레벨업 카드에 표시되는 이 레벨의 효과 설명")]
        public string description;
        public float damage;
        public float cooldown;
        public int count;
        public float area;
        public float speed;
        public float duration;
        public float range;
        public float tickInterval;
        public float special;
        public float special2;
    }

    [Serializable]
    public struct ExtraParam
    {
        public string key;
        public float value;
    }

    [CreateAssetMenu(menuName = "ElementMage/Skill Data", fileName = "SkillData")]
    public class SkillData : ScriptableObject
    {
        public SkillId id;
        public string displayName;
        public Element element;
        public SkillKind kind;
        public Sprite icon;
        [TextArea] public string summary;
        [Tooltip("각 수치 필드가 이 스킬에서 무엇을 뜻하는지 (개발 메모)")]
        [TextArea(3, 10)] public string fieldGuide;

        [Header("비주얼 (아트 교체 지점)")]
        public Sprite mainSprite;
        public Sprite subSprite;
        public Sprite effectSprite;
        public Color color = Color.white;
        [Tooltip("최종 단계(푸른 화염 등) 색")]
        public Color finalColor = Color.white;

        [Header("레벨별 수치 (배열 길이 = 최대 레벨)")]
        public SkillLevelStats[] levels;

        [Header("스킬 고유 상수")]
        public ExtraParam[] extras;

        [Header("특수 진화 (없으면 비워 둠)")]
        public bool hasEvolution;
        public string evolutionName;
        [TextArea] public string evolutionDescription;
        public Sprite evolutionIcon;
        [Tooltip("이 패시브가 만렙이어야 진화 카드가 등장")]
        public PassiveData evolutionRequiredPassive;
        [Tooltip("진화 시 최종 레벨 수치 대신 사용하는 데이터 세트")]
        public SkillLevelStats evolutionStats;

        public int MaxLevel => levels != null ? levels.Length : 0;

        public SkillLevelStats GetStats(int level)
        {
            if (levels == null || levels.Length == 0) return default;
            return levels[Mathf.Clamp(level, 1, levels.Length) - 1];
        }

        public float Extra(string key, float fallback)
        {
            if (extras != null)
            {
                for (int i = 0; i < extras.Length; i++)
                    if (extras[i].key == key) return extras[i].value;
            }
            return fallback;
        }
    }
}
