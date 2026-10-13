using UnityEngine;

namespace Ratones.Basic
{
    public sealed class FollowCamera : MonoBehaviour
    {
        // Capa de la cocina: la dibuja una cámara de fondo, así nunca tapa a los ratones, objetos, rampas ni el piso.
        public const string KitchenLayer = "Cocina";
        public Transform Target;
        public RectTransform Viewport;
        public Vector3 Offset = new Vector3(30, 45, -30);
        public float SmoothTime = .12f;
        // Tamaño de la cámara ortográfica: más chico = más cerca del ratón.
        public float Size = 14;
        // El fondo solo se dibuja desde ClipAhead metros antes del ratón: las paredes y muebles de la cocina que
        // quedan del lado de la cámara se recortan para que no llenen la pantalla.
        public float ClipAhead = 16;
        Camera view, background;
        Vector3 velocity;
        bool first = true;
        readonly Vector3[] corners = new Vector3[4];
        void Awake()
        {
            view = GetComponent<Camera>();
            if (view.orthographic) view.orthographicSize = Size;
            int layer = LayerMask.NameToLayer(KitchenLayer);
            if (layer < 0) return;
            // Misma vista, un paso antes: primero la cocina y encima (sin profundidad compartida) todo el juego.
            background = new GameObject("CamaraCocina").AddComponent<Camera>();
            background.CopyFrom(view);
            background.transform.SetParent(transform, false);
            background.transform.localPosition = Vector3.zero; background.transform.localRotation = Quaternion.identity;
            background.cullingMask = 1 << layer; background.depth = view.depth - 1;
            view.cullingMask &= ~(1 << layer); view.clearFlags = CameraClearFlags.Depth;
        }
        void LateUpdate()
        {
            if (Viewport != null && Screen.width > 0 && Screen.height > 0)
            {
                Viewport.GetWorldCorners(corners);
                float x = corners[0].x / Screen.width, y = corners[0].y / Screen.height;
                view.rect = new Rect(x, y, (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
            }
            if (background != null)
            {
                background.rect = view.rect; background.orthographicSize = view.orthographicSize;
                background.nearClipPlane = Mathf.Max(.3f, Offset.magnitude - ClipAhead);
            }
            if (Target == null) return;
            Vector3 target = Target.position + Offset;
            transform.position = first ? target : Vector3.SmoothDamp(transform.position, target, ref velocity, SmoothTime);
            transform.rotation = Quaternion.LookRotation(-Offset.normalized, Vector3.up);
            first = false;
        }
    }
}
