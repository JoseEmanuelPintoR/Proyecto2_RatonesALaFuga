using UnityEngine;
using UnityEngine.EventSystems;

namespace Ratones.Gameplay
{
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Handle;
        public Vector2 Value { get; private set; }
        int activePointer = int.MinValue;
        RectTransform rect;
        void Awake() { rect = GetComponent<RectTransform>(); }
        public void OnPointerDown(PointerEventData e)
        { if (activePointer != int.MinValue) return; activePointer = e.pointerId; OnDrag(e); }
        public void OnDrag(PointerEventData e)
        {
            if (activePointer != e.pointerId) return;
            Vector2 position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out position);
            float radius = rect.rect.width * .36f;
            Value = Vector2.ClampMagnitude(position / radius, 1f);
            Handle.anchoredPosition = Value * radius;
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == activePointer) ResetInput(); }
        public void ResetInput()
        { activePointer = int.MinValue; Value = Vector2.zero; if (Handle != null) Handle.anchoredPosition = Vector2.zero; }
        void OnDisable() { ResetInput(); }
    }
}
