using UnityEngine;

namespace Ratones.Basic
{
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public RectTransform Viewport;
        public Vector3 Offset = new Vector3(30, 45, -30);
        public float SmoothTime = .12f;
        Camera view;
        Vector3 velocity;
        bool first = true;
        readonly Vector3[] corners = new Vector3[4];
        void Awake() { view = GetComponent<Camera>(); }
        void LateUpdate()
        {
            if (Viewport != null && Screen.width > 0 && Screen.height > 0)
            {
                Viewport.GetWorldCorners(corners);
                float x = corners[0].x / Screen.width, y = corners[0].y / Screen.height;
                view.rect = new Rect(x, y, (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
            }
            if (Target == null) return;
            Vector3 target = Target.position + Offset;
            transform.position = first ? target : Vector3.SmoothDamp(transform.position, target, ref velocity, SmoothTime);
            transform.rotation = Quaternion.LookRotation(-Offset.normalized, Vector3.up);
            first = false;
        }
    }
}
