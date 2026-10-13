using UnityEngine;
using UnityEngine.EventSystems;

namespace Ratones.Basic
{
    // Joystick táctil: la perilla (el queso) se arrastra dentro de la base y da la dirección.
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob;
        // Distancia máxima (px de referencia) que se separa la perilla del centro.
        public float Radius = 70;
        public float DeadZone = .15f;
        public Vector2 Value { get; private set; }
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        { if (pointer != int.MinValue) return; pointer = e.pointerId; Follow(e); }
        public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) Follow(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) Clear(); }
        void Follow(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local, Radius);
            if (Knob != null) Knob.anchoredPosition = offset;
            Vector2 value = offset / Radius;
            Value = value.magnitude < DeadZone ? Vector2.zero : value;
        }
        public void Clear()
        {
            pointer = int.MinValue; Value = Vector2.zero;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
        }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
    }
}
