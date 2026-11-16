using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    public enum EquipSlot { Weapon, Necklace, Gloves, Armor, Belt, Boots }

    public enum EquipStat { MaxHp, Damage, Cooldown, MoveSpeed, Area }

    /// <summary>장착한 장비의 효과 합계</summary>
    public struct EquipmentBonus
    {
        public float maxHp, damage, cooldown, moveSpeed, area;

        public static EquipmentBonus Compute(GameDatabase db)
        {
            var bonus = new EquipmentBonus();
            if (db == null || db.equipment == null) return bonus;
            for (int i = 0; i < db.equipment.Count; i++)
            {
                EquipmentData e = db.equipment[i];
                if (e == null || SaveData.GetEquipped(e.slot) != e.Id) continue;
                switch (e.stat)
                {
                    case EquipStat.MaxHp: bonus.maxHp += e.value; break;
                    case EquipStat.Damage: bonus.damage += e.value; break;
                    case EquipStat.Cooldown: bonus.cooldown += e.value; break;
                    case EquipStat.MoveSpeed: bonus.moveSpeed += e.value; break;
                    case EquipStat.Area: bonus.area += e.value; break;
                }
            }
            return bonus;
        }

        public string Describe()
        {
            var parts = new List<string>(5);
            if (maxHp > 0f) parts.Add("체력 +" + Mathf.RoundToInt(maxHp * 100f) + "%");
            if (damage > 0f) parts.Add("피해 +" + Mathf.RoundToInt(damage * 100f) + "%");
            if (cooldown > 0f) parts.Add("쿨타임 -" + Mathf.RoundToInt(cooldown * 100f) + "%");
            if (moveSpeed > 0f) parts.Add("이동속도 +" + Mathf.RoundToInt(moveSpeed * 100f) + "%");
            if (area > 0f) parts.Add("범위 +" + Mathf.RoundToInt(area * 100f) + "%");
            return parts.Count > 0 ? string.Join("   ", parts) : "장착 효과 없음";
        }
    }

    /// <summary>판과 판 사이에 유지되는 저장 값 (PlayerPrefs)</summary>
    public static class SaveData
    {
        const string Prefix = "EM_";

        /// <summary>플레이할 수 있는 마지막 스테이지 번호 (0부터)</summary>
        public static int UnlockedStage
        {
            get => PlayerPrefs.GetInt(Prefix + "UnlockedStage", 0);
            set { PlayerPrefs.SetInt(Prefix + "UnlockedStage", value); PlayerPrefs.Save(); }
        }

        public static int SelectedStage
        {
            get => PlayerPrefs.GetInt(Prefix + "SelectedStage", 0);
            set { PlayerPrefs.SetInt(Prefix + "SelectedStage", value); PlayerPrefs.Save(); }
        }

        public static Element SlotA
        {
            get => (Element)PlayerPrefs.GetInt(Prefix + "SlotA", (int)Element.Fire);
            set { PlayerPrefs.SetInt(Prefix + "SlotA", (int)value); PlayerPrefs.Save(); }
        }

        public static Element SlotB
        {
            get => (Element)PlayerPrefs.GetInt(Prefix + "SlotB", (int)Element.Water);
            set { PlayerPrefs.SetInt(Prefix + "SlotB", (int)value); PlayerPrefs.Save(); }
        }

        public static string GetEquipped(EquipSlot slot)
        {
            return PlayerPrefs.GetString(Prefix + "Equip_" + slot, "");
        }

        public static void SetEquipped(EquipSlot slot, string id)
        {
            PlayerPrefs.SetString(Prefix + "Equip_" + slot, id ?? "");
            PlayerPrefs.Save();
        }
    }
}
