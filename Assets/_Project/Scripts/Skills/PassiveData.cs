using UnityEngine;

namespace ElementMage
{
    [CreateAssetMenu(menuName = "ElementMage/Passive Data", fileName = "PassiveData")]
    public class PassiveData : ScriptableObject
    {
        public PassiveType type;
        public string displayName;
        public Sprite icon;
        public int maxLevel = 5;
        [Tooltip("레벨당 증가량 (0.08 = 레벨당 8%)")]
        public float valuePerLevel = 0.08f;
        [Tooltip("{0}에 누적 퍼센트가 들어간다")]
        public string descriptionFormat = "+{0}%";

        public string DescribeLevel(int level)
        {
            return string.Format(descriptionFormat, Mathf.RoundToInt(valuePerLevel * level * 100f));
        }
    }
}
