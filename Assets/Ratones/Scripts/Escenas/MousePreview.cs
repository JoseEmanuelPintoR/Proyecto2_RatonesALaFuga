using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    // Muestra el ratón girando dentro de un RawImage. El escenario está lejos del origen
    // para que la cámara de la UI no lo vea.
    public sealed class MousePreview : MonoBehaviour
    {
        public PlayerView Prefab;
        public RawImage Target;
        public float Speed = 40;
        static readonly Vector3 Stage = new Vector3(500, 0, 0);
        PlayerView mouse;
        RenderTexture texture;
        GameObject stage;
        int key, aura;
        void Awake()
        {
            if (Prefab == null || Target == null) return;
            Rect rect = Target.rectTransform.rect;
            int height = 512, width = Mathf.Clamp(Mathf.RoundToInt(height * (rect.height > 0 ? rect.width / rect.height : 1)), 128, 1024);
            texture = new RenderTexture(width, height, 16) { antiAliasing = 2 };
            Target.texture = texture; Target.color = Color.white;

            stage = new GameObject("EscenarioRaton");
            stage.transform.position = Stage;
            mouse = Instantiate(Prefab, Stage, Quaternion.Euler(0, 180, 0), stage.transform);
            var camera = new GameObject("CamaraRaton").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.6f, 4.2f);
            camera.transform.LookAt(Stage + new Vector3(0, .85f, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.fieldOfView = 35; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.targetTexture = texture;
            var light = new GameObject("LuzRaton").AddComponent<Light>();
            light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(40, 150, 0);
            Show(key, aura);
        }
        void Update()
        { if (mouse != null) mouse.transform.Rotate(0, Speed * Time.unscaledDeltaTime, 0, Space.World); }
        public void Show(int key, int aura)
        {
            this.key = key; this.aura = aura;
            if (mouse != null) mouse.ShowColors(key, aura);
        }
        void OnDestroy()
        {
            if (stage != null) Destroy(stage);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
