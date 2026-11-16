using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 게임 전역 수치. 전부 테스트 값이며 Inspector에서 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ElementMage/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("판 진행")]
        [Tooltip("보스가 등장하는 시간(초). 테스트할 때는 줄여서 쓴다.")]
        public float runDuration = 600f;
        public int maxActiveSlots = 5;
        public int maxPassiveSlots = 5;
        [Tooltip("이 플레이어 레벨부터 궁극기가 후보에 등장")]
        public int ultimateUnlockLevel = 5;

        [Header("플레이어")]
        public float playerMaxHp = 100f;
        public float playerMoveSpeed = 4.2f;
        public float playerRadius = 0.35f;
        [Tooltip("피격 후 무적 시간")]
        public float playerHitInvincibleTime = 0.6f;
        public float pickupRadius = 1.8f;
        public float orbMagnetSpeed = 12f;

        [Header("경험치 (다음 레벨 필요량 = base + growth × (레벨-1))")]
        public float xpBase = 5f;
        public float xpGrowthPerLevel = 3f;
        public int maxXpOrbs = 250;

        [Header("카메라")]
        [Tooltip("세로 화면 기준 직교 카메라 크기(화면 절반 높이)")]
        public float cameraOrthoSize = 9f;

        [Header("스폰")]
        public float spawnIntervalStart = 0.7f;
        public float spawnIntervalEnd = 0.1f;
        public int maxEnemies = 220;
        [Tooltip("화면 대각선 바깥으로 이만큼 더 떨어진 곳에 생성")]
        public float spawnMargin = 1.5f;
        [Tooltip("분당 적 체력 증가율 (0.35 = 1분마다 +35%)")]
        public float enemyHpGrowthPerMinute = 0.35f;
        [Tooltip("플레이어와 이보다 멀어진 적은 반대편으로 재배치")]
        public float despawnDistance = 20f;

        [Header("이동 제어 (2-3)")]
        [Tooltip("지속형 외부 힘 합계 상한")]
        public float maxContinuousForce = 6f;
        [Tooltip("적 전체 속도 상한")]
        public float maxEnemySpeed = 9f;
        [Tooltip("순간형 힘이 적용되는 시간")]
        public float impulseDuration = 0.25f;
        [Tooltip("보스에게 적용되는 외부 힘 비율")]
        public float bossForceFactor = 0.3f;

        [Header("상태이상 (2-4)")]
        public float burnDuration = 3f;
        public float burnDps = 4f;
        public float burnTickInterval = 0.5f;
        public float wetDuration = 3f;
        [Range(0f, 1f)] public float wetSlow = 0.1f;
        [Tooltip("보스가 받는 젖음 감속 비율")]
        public float bossWetSlowFactor = 0.5f;

        [Header("증발 폭발 (2-5)")]
        public float reactionDamage = 18f;
        public float reactionRadius = 1.5f;
        [Tooltip("적별 재발동 대기 시간")]
        public float reactionCooldown = 1f;

        [Header("레벨업 후보 가중치")]
        [Tooltip("보유 스킬 강화 카드 가중치")]
        public float ownedUpgradeWeight = 2f;
        [Tooltip("신규 스킬 카드 가중치")]
        public float newSkillWeight = 1f;
        [Tooltip("보유 액티브가 fewActivesThreshold 이하일 때의 신규 스킬 가중치")]
        public float newSkillWeightWhenFew = 3f;
        public int fewActivesThreshold = 2;
        public float passiveWeight = 1f;
        public float ultimateWeight = 1.5f;
        [Tooltip("히든 4번째 카드 등장 연출 시간. 이 동안 모든 카드 입력 잠금")]
        public float hiddenCardRevealTime = 0.5f;

        [Header("보스 (2-6)")]
        [Tooltip("보스전 원형 아레나 반지름")]
        public float bossArenaRadius = 4.6f;
        [Tooltip("보스전 중 일반 적 스폰 배율 (0 = 스폰 중단)")]
        public float bossSpawnRateMultiplier = 0f;
        public float bossTelegraphTime = 1f;
        public float bossDashSpeed = 14f;
        public float bossDashDuration = 0.55f;
        public float bossDashCooldown = 3.5f;
        public float bossDashDamageMultiplier = 1.5f;

        [Header("기타")]
        public bool showDamageNumbers = true;
        public int maxDamagePopups = 40;
    }
}
