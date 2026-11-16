using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementMage
{
    /// <summary>
    /// 플레이어 이동. 가상 조이스틱 입력을 쓰고, 에디터·PC에서는 키보드 WASD/방향키도 받는다.
    /// 플레이어는 콜라이더가 없으며 적과의 접촉은 거리 계산으로 판정한다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public PlayerHealth Health { get; private set; }
        public PlayerExperience Experience { get; private set; }
        public SkillManager Skills { get; private set; }

        public Vector2 Position { get; private set; }
        /// <summary>마지막으로 이동한 방향 (정지해도 유지)</summary>
        public Vector2 Facing { get; private set; } = Vector2.up;
        /// <summary>이번 프레임의 이동 입력 방향. 정지 중이면 (0,0)</summary>
        public Vector2 MoveDir { get; private set; }

        /// <summary>자동 플레이 테스트용 입력. 값이 있으면 조이스틱 대신 사용한다.</summary>
        public Vector2? DebugMoveInput;

        GameManager gm;
        Transform tr;
        SpriteRenderer sr;
        float speedBoost = 1f;
        float baseScale;
        float bobTime;

        public void Init(GameManager manager)
        {
            gm = manager;
            tr = transform;
            GameDatabase db = gm.Database;
            sr = Gfx.NewSprite("Visual", tr, db.player, Gfx.OrderPlayer, Color.white);
            baseScale = Gfx.ScaleForWidth(db.player, 1.1f);
            sr.transform.localScale = Vector3.one * baseScale;

            Health = gameObject.AddComponent<PlayerHealth>();
            Health.Init(gm, sr);
            Experience = gameObject.AddComponent<PlayerExperience>();
            Experience.Init(gm);
            Skills = gameObject.AddComponent<SkillManager>();
            Skills.Init(gm, this);
            Position = tr.position;
        }

        /// <summary>
        /// 이동속도 배율 요청 (격류 가속). 같은 프레임에 여러 번 들어오면 가장 큰 값만 쓴다.
        /// </summary>
        public void RequestSpeedBoost(float multiplier)
        {
            if (multiplier > speedBoost) speedBoost = multiplier;
        }

        void Update()
        {
            if (gm == null || gm.State != GameState.Playing) { MoveDir = Vector2.zero; return; }

            Vector2 input = ReadInput();
            if (input.sqrMagnitude > 1f) input.Normalize();
            MoveDir = input.sqrMagnitude > 0.0004f ? input.normalized : Vector2.zero;
            if (MoveDir != Vector2.zero) Facing = MoveDir;

            float speed = gm.ConfigAsset.playerMoveSpeed * Skills.MoveSpeedMul * speedBoost;
            bool boosted = speedBoost > 1.001f;
            speedBoost = 1f;

            Vector2 pos = Position + input * (speed * Time.deltaTime);
            if (BossArena.Active != null) pos = BossArena.Active.Clamp(pos, gm.ConfigAsset.playerRadius);
            Position = pos;
            tr.position = pos;

            // 비주얼: 좌우 반전, 걷는 동안 살짝 통통 튀기
            if (Mathf.Abs(input.x) > 0.05f) sr.flipX = input.x < 0f;
            if (MoveDir != Vector2.zero) bobTime += Time.deltaTime * (boosted ? 16f : 11f);
            float bob = MoveDir != Vector2.zero ? Mathf.Abs(Mathf.Sin(bobTime)) * 0.06f : 0f;
            sr.transform.localPosition = new Vector3(0f, bob, 0f);
            sr.transform.localScale = new Vector3(baseScale, baseScale * (1f + bob * 0.5f), 1f);
        }

        Vector2 ReadInput()
        {
            if (DebugMoveInput.HasValue) return DebugMoveInput.Value;

            Vector2 input = gm.Joystick != null ? gm.Joystick.Direction : Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null && input.sqrMagnitude < 0.0004f)
            {
                float x = 0f, y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                input = new Vector2(x, y);
            }
            return input;
        }
    }
}
