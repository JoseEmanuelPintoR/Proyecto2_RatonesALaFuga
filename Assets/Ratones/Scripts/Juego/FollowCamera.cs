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
        // Intro durante la cuenta regresiva: primero la cocina completa de lejos (IntroHold segundos)
        // y luego un zoom hasta el ratón que termina justo al empezar la ronda.
        public float IntroHold = 1.2f;
        // Qué tan lejos se ve el cuarto en la intro, respecto a su lado más corto: más chico = más cerca.
        // Con ~0.35 la pantalla queda llena de cocina (sin el fondo gris de la cámara en las esquinas).
        public float IntroView = .35f;
        // Copias del piso que agrega Ratones/Preparar cocina: no cuentan para encuadrar el cuarto.
        const string ExtraFloor = "PisoExtra";
        Camera view, background;
        Vector3 velocity, introCenter;
        float introZoom = 1, farClip;
        bool first = true;
        readonly Vector3[] corners = new Vector3[4];
        void Awake()
        {
            view = GetComponent<Camera>();
            if (view.orthographic) view.orthographicSize = Size;
            farClip = view.farClipPlane;
            int layer = LayerMask.NameToLayer(KitchenLayer);
            FrameKitchen(layer);
            if (layer < 0) return;
            // Misma vista, un paso antes: primero la cocina y encima (sin profundidad compartida) todo el juego.
            background = new GameObject("CamaraCocina").AddComponent<Camera>();
            background.CopyFrom(view);
            background.transform.SetParent(transform, false);
            background.transform.localPosition = Vector3.zero; background.transform.localRotation = Quaternion.identity;
            background.cullingMask = 1 << layer; background.depth = view.depth - 1;
            view.cullingMask &= ~(1 << layer); view.clearFlags = CameraClearFlags.Depth;
        }
        // Centro y zoom de la vista lejana: el cuarto de la cocina (o la arena si no hay cocina).
        void FrameKitchen(int layer)
        {
            var area = new Bounds(Vector3.zero, new Vector3(Rules.ArenaSize, 0, Rules.ArenaSize));
            bool found = false;
            if (layer >= 0)
                foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.gameObject.layer == layer && r.name != ExtraFloor) { if (found) area.Encapsulate(r.bounds); else area = r.bounds; found = true; }
            introCenter = new Vector3(area.center.x, 0, area.center.z);
            introZoom = Mathf.Max(1, Mathf.Min(area.size.x, area.size.z) * IntroView / Size);
        }
        void LateUpdate()
        {
            if (Viewport != null && Screen.width > 0 && Screen.height > 0)
            {
                Viewport.GetWorldCorners(corners);
                float x = corners[0].x / Screen.width, y = corners[0].y / Screen.height;
                view.rect = new Rect(x, y, (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
            }
            // 0 = cocina de lejos, 1 = sobre el ratón.
            float t = 1;
            State state = Session.Instance != null ? Session.Instance.State : null;
            if (state != null && state.Phase == Phase.Countdown)
                t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(IntroHold, Rules.CountdownTime, Rules.CountdownTime - state.CountdownLeft));
            float zoom = Mathf.Lerp(introZoom, 1, t);
            // Todo se escala junto (tamaño, distancia y recorte del fondo) para que la vista lejana se vea igual, solo más amplia.
            if (view.orthographic) view.orthographicSize = Size * zoom;
            view.farClipPlane = Mathf.Max(farClip, Offset.magnitude * zoom * 4);
            if (background != null)
            {
                background.rect = view.rect; background.orthographicSize = view.orthographicSize;
                background.nearClipPlane = Mathf.Max(.3f, (Offset.magnitude - ClipAhead) * zoom);
                background.farClipPlane = view.farClipPlane;
            }
            transform.rotation = Quaternion.LookRotation(-Offset.normalized, Vector3.up);
            if (t < 1)
            {
                Vector3 focus = Target != null ? Vector3.Lerp(introCenter, Target.position, t) : introCenter;
                transform.position = focus + Offset * zoom;
                velocity = Vector3.zero; first = false;
                return;
            }
            if (Target == null) return;
            Vector3 target = Target.position + Offset;
            transform.position = first ? target : Vector3.SmoothDamp(transform.position, target, ref velocity, SmoothTime);
            first = false;
        }
    }
}
