using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    [Serializable]
    public class SpawnEntry
    {
        public EnemyData enemy;
        [Tooltip("이 시간(초)부터 등장")]
        public float startTime;
        [Tooltip("이 시간(초)까지 등장. 0이면 끝까지")]
        public float endTime;
        public float weight = 1f;
    }

    /// <summary>
    /// 게임이 참조하는 모든 데이터·공용 스프라이트를 모아 둔 에셋.
    /// 아트를 교체할 때는 여기와 SkillData/EnemyData의 스프라이트만 바꾸면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ElementMage/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public GameConfig config;
        public List<SkillData> skills = new List<SkillData>();
        public List<PassiveData> passives = new List<PassiveData>();
        public List<SpawnEntry> spawnTable = new List<SpawnEntry>();
        public EnemyData boss;

        [Header("로비 (스테이지·장비 — 내용과 수치는 임시 테스트 값)")]
        public List<StageData> stages = new List<StageData>();
        public List<EquipmentData> equipment = new List<EquipmentData>();
        public Sprite tabEquipIcon;
        public Sprite tabBattleIcon;
        public Sprite tabCodexIcon;
        public Sprite lockIcon;
        [Tooltip("레벨업 후보가 0개일 때의 체력 회복 카드 아이콘")]
        public Sprite healIcon;
        [Tooltip("에디터의 자동 구성이 데이터를 갱신해야 하는지 판단하는 번호")]
        public int dataVersion;

        [Header("공용 스프라이트")]
        public Material spriteMaterial;
        public Sprite player;
        public Sprite xpOrb;
        [Tooltip("중급 경험치 오브")] public Sprite xpOrbMid;
        [Tooltip("대급 경험치 오브")] public Sprite xpOrbLarge;
        [Tooltip("자석 (화면의 오브를 전부 끌어옴)")] public Sprite magnet;
        [Tooltip("화면 밖 바닥 오브 방향 화살표")] public Sprite pickupArrow;
        public Sprite groundTile;
        [Tooltip("흰색 원 (틴트용)")] public Sprite circle;
        [Tooltip("가장자리가 부드러운 원 (틴트용)")] public Sprite softCircle;
        [Tooltip("고리 (틴트용)")] public Sprite ring;
        [Tooltip("흰색 사각형 (틴트용)")] public Sprite square;
        [Tooltip("폭발 (틴트용)")] public Sprite burst;
        [Tooltip("UI용 둥근 사각형 (9-slice)")] public Sprite roundedRect;
        [Tooltip("UI용 둥근 테두리 (9-slice)")] public Sprite roundedOutline;
        public Sprite fireEmblem;
        public Sprite waterEmblem;

        [Header("색상")]
        public Color fireColor = new Color(1f, 0.55f, 0.15f);
        public Color blueFlameColor = new Color(0.35f, 0.7f, 1f);
        public Color waterColor = new Color(0.3f, 0.85f, 1f);
        public Color steamColor = new Color(0.92f, 0.96f, 1f);
        public Color burnTint = new Color(1f, 0.7f, 0.5f);
        public Color wetTint = new Color(0.6f, 0.8f, 1f);
    }
}
