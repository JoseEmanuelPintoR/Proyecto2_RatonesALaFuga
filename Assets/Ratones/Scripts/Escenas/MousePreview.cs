using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    // Muestra el ratón girando dentro de un RawImage. El escenario está lejos del origen
    // para que la cámara de la UI no lo vea; con varias vistas (Lobby) cada una tiene su propio escenario.
    public sealed class MousePreview : MonoBehaviour
    {
        public PlayerView Prefab;
        public RawImage Target;
        public float Speed = 40;
        // Encuadre: posición de la cámara respecto al ratón y altura a la que mira.
        public Vector3 CameraPosition = new Vector3(0, 1.6f, 4.2f);
        public float LookHeight = .85f;
        public int Resolution = 512;
        static readonly Vector3 FirstStage = new Vector3(500, 0, 0);
        const float StageSpacing = 50;
        static int stages;
        // Una sola luz para todas las vistas (varias luces direccionales sumarían y quemarían los colores).
        static Light sharedLight;
        static int lightUsers;
        PlayerView mouse;
        RenderTexture texture;
        GameObject stage;
        int key, aura;
        void Awake()
        {
            if (Prefab == null || Target == null) return;
            Rect rect = Target.rectTransform.rect;
            int height = Resolution, width = Mathf.Clamp(Mathf.RoundToInt(height * (rect.height > 0 ? rect.width / rect.height : 1)), 128, 1024);
            // Con canal alfa: lo que no es ratón queda transparente y se ve el fondo de la escena.
            texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            Target.texture = texture; Target.color = Color.white;

            Vector3 position = FirstStage + Vector3.right * StageSpacing * stages++;
            stage = new GameObject("EscenarioRaton");
            stage.transform.position = position;
            mouse = Instantiate(Prefab, position, Quaternion.Euler(0, 180, 0), stage.transform);
            var camera = new GameObject("CamaraRaton").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = CameraPosition;
            camera.transform.LookAt(position + new Vector3(0, LookHeight, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.fieldOfView = 35; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.targetTexture = texture;
            if (sharedLight == null)
            {
                sharedLight = new GameObject("LuzRaton").AddComponent<Light>();
                sharedLight.type = LightType.Directional; sharedLight.intensity = 1.1f;
                sharedLight.transform.rotation = Quaternion.Euler(40, 150, 0);
            }
            lightUsers++;
            Show(key, aura);
        }
        void OnEnable() { if (stage != null) stage.SetActive(true); }
        void OnDisable() { if (stage != null) stage.SetActive(false); }
        void Update()
        { if (mouse != null) mouse.transform.Rotate(0, Speed * Time.unscaledDeltaTime, 0, Space.World); }
        public void Show(int key, int aura)
        {
            this.key = key; this.aura = aura;
            if (mouse != null) mouse.ShowColors(key, aura);
        }
        void OnDestroy()
        {
            if (stage != null) { Destroy(stage); stages = Mathf.Max(0, stages - 1); }
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (mouse != null && --lightUsers <= 0 && sharedLight != null) { Destroy(sharedLight.gameObject); sharedLight = null; lightUsers = 0; }
        }
    }
}
