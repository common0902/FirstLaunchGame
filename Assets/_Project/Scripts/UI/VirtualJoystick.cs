using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElementMage
{
    /// <summary>
    /// 플로팅 가상 조이스틱. 화면 아무 곳이나 누르면 그 자리에 조이스틱이 나타난다.
    /// 화면 전체를 덮는 투명 이미지가 입력을 받으며, 마우스로도 동작한다.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Radius = 130f;
        const float DeadZone = 0.12f;

        public Vector2 Direction { get; private set; }

        RectTransform area, baseRect, knobRect;
        Image baseImage, knobImage;
        int pointerId = int.MinValue;
        Vector2 startLocal;

        public static VirtualJoystick Create(Transform canvas, GameDatabase db)
        {
            Image hit = UIFactory.Image("JoystickArea", canvas, null, new Color(0f, 0f, 0f, 0f), false, true);
            UIFactory.Stretch(hit.rectTransform);
            var joystick = hit.gameObject.AddComponent<VirtualJoystick>();
            joystick.area = hit.rectTransform;

            joystick.baseImage = UIFactory.Image("Base", hit.transform, db.circle, new Color(1f, 1f, 1f, 0.18f));
            joystick.baseRect = joystick.baseImage.rectTransform;
            UIFactory.Place(joystick.baseRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Radius * 2f);

            joystick.knobImage = UIFactory.Image("Knob", joystick.baseRect, db.circle, new Color(1f, 1f, 1f, 0.55f));
            joystick.knobRect = joystick.knobImage.rectTransform;
            UIFactory.Place(joystick.knobRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Radius * 0.9f);

            joystick.baseRect.gameObject.SetActive(false);
            return joystick;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId != int.MinValue) return;
            if (!ToLocal(e, out Vector2 local)) return;
            pointerId = e.pointerId;
            startLocal = local;
            baseRect.anchoredPosition = local;
            knobRect.anchoredPosition = Vector2.zero;
            baseRect.gameObject.SetActive(true);
            Direction = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            if (!ToLocal(e, out Vector2 local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local - startLocal, Radius);
            knobRect.anchoredPosition = offset;
            Vector2 value = offset / Radius;
            Direction = value.magnitude < DeadZone ? Vector2.zero : value;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            ResetInput();
        }

        /// <summary>레벨업 창이 뜨는 등 입력을 끊어야 할 때 호출</summary>
        public void ResetInput()
        {
            pointerId = int.MinValue;
            Direction = Vector2.zero;
            if (baseRect != null) baseRect.gameObject.SetActive(false);
        }

        bool ToLocal(PointerEventData e, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out local);
        }

        void OnDisable() => ResetInput();
    }
}
