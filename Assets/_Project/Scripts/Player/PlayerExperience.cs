using UnityEngine;

namespace ElementMage
{
    public class PlayerExperience : MonoBehaviour
    {
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext { get; private set; }
        /// <summary>아직 선택 화면을 열지 않은 레벨업 횟수</summary>
        public int PendingLevelUps { get; private set; }

        /// <summary>테스트용 경험치 배율</summary>
        public static float DebugXpMultiplier = 1f;

        GameManager gm;

        public void Init(GameManager manager)
        {
            gm = manager;
            Level = 1;
            Xp = 0f;
            XpToNext = Required(Level);
        }

        float Required(int level)
        {
            GameConfig cfg = gm.ConfigAsset;
            return cfg.xpBase + cfg.xpGrowthPerLevel * (level - 1);
        }

        public void AddXp(float amount)
        {
            Xp += amount * DebugXpMultiplier;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                XpToNext = Required(Level);
                PendingLevelUps++;
            }
        }

        public bool ConsumePendingLevelUp()
        {
            if (PendingLevelUps <= 0) return false;
            PendingLevelUps--;
            return true;
        }
    }
}
