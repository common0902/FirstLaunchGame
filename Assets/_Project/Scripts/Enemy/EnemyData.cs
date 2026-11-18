using UnityEngine;

namespace ElementMage
{
    [CreateAssetMenu(menuName = "ElementMage/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        public string displayName;
        public Sprite sprite;
        public Color tint = Color.white;
        public float maxHp = 10f;
        public float moveSpeed = 1.6f;
        public float contactDamage = 8f;
        [Tooltip("충돌 반지름 (월드 단위)")]
        public float radius = 0.4f;
        [Tooltip("스프라이트 표시 폭 (월드 단위)")]
        public float visualWidth = 1f;
        [Tooltip("처치 즉시 경험치의 기준값 (실제 지급 = xp × GameConfig.killXpRatio)")]
        public int xp = 1;
        [Tooltip("처치 시 경험치 오브가 떨어질 확률")]
        [Range(0f, 1f)] public float orbDropChance = 0.35f;
        public bool isBoss;
    }
}
