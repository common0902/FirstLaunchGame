using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 장비 한 개. 내용과 수치는 전부 임시 테스트 값이다 (장비 기획은 아직 없음).
    /// 에셋 이름이 저장에 쓰이는 고유 id가 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ElementMage/Equipment Data", fileName = "EquipmentData")]
    public class EquipmentData : ScriptableObject
    {
        public string displayName;
        public EquipSlot slot;
        public Sprite icon;
        public EquipStat stat;
        [Tooltip("0.1 = +10% (쿨타임은 감소량)")]
        public float value = 0.1f;

        public string Id => name;

        public string Describe()
        {
            int percent = Mathf.RoundToInt(value * 100f);
            switch (stat)
            {
                case EquipStat.MaxHp: return "최대 체력 +" + percent + "%";
                case EquipStat.Damage: return "스킬 피해 +" + percent + "%";
                case EquipStat.Cooldown: return "스킬 쿨타임 -" + percent + "%";
                case EquipStat.MoveSpeed: return "이동속도 +" + percent + "%";
                default: return "스킬 범위 +" + percent + "%";
            }
        }

        public static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return "무기";
                case EquipSlot.Necklace: return "목걸이";
                case EquipSlot.Gloves: return "장갑";
                case EquipSlot.Armor: return "갑옷";
                case EquipSlot.Belt: return "벨트";
                default: return "신발";
            }
        }
    }
}
