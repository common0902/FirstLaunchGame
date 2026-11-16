using UnityEngine;

namespace ElementMage
{
    /// <summary>스테이지 한 개. 같은 적·같은 규칙에 배율과 바닥 색만 다르다 (임시 테스트 값).</summary>
    [CreateAssetMenu(menuName = "ElementMage/Stage Data", fileName = "StageData")]
    public class StageData : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        [Tooltip("바닥 타일에 곱하는 색")]
        public Color groundTint = Color.white;
        public float enemyHpMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;
        [Tooltip("1보다 크면 적이 더 자주 나온다")]
        public float spawnRateMultiplier = 1f;
    }
}
