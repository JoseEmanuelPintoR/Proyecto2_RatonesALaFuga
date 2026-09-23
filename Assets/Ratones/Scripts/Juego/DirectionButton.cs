using UnityEngine;
using UnityEngine.EventSystems;

namespace Ratones.Basic
{
    public sealed class DirectionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Vector2 Direction;
        public bool Held { get; private set; }
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        { if (!Held) { pointer = e.pointerId; Held = true; } }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) Clear(); }
        public void Clear() { pointer = int.MinValue; Held = false; }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
    }
}
