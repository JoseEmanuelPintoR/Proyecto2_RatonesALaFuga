using UnityEngine;

namespace Ratones.Basic
{
    public sealed class PlayerView : MonoBehaviour
    {
        public Transform Visual;
        public Renderer[] Model;
        // Una textura del ratón por color de llave, en el mismo orden que Palette.Colors.
        public Material[] KeyMaterials;
        public Renderer Aura;
        // Fuente del nombre sobre el ratón ("J1:LAURA"); la asigna Ratones/UI nueva en el prefab.
        public Font NameFont;
        public float NameHeight = 1.6f;
        Material auraMaterial;
        MaterialPropertyBlock tint;
        TextMesh nameLabel;
        int shownKey = -1;
        bool first = true;
        void Awake()
        { if (Aura != null) auraMaterial = Aura.material; tint = new MaterialPropertyBlock(); }
        // Solo en partida: la vista de Personalizar usa el mismo prefab sin nombre.
        void SetName(string text, Color color)
        {
            if (nameLabel == null)
            {
                var go = new GameObject("Nombre");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, NameHeight, 0);
                nameLabel = go.AddComponent<TextMesh>();
                nameLabel.anchor = TextAnchor.MiddleCenter; nameLabel.alignment = TextAlignment.Center;
                nameLabel.fontSize = 64; nameLabel.characterSize = .05f;
                if (NameFont != null) { nameLabel.font = NameFont; go.GetComponent<MeshRenderer>().sharedMaterial = NameFont.material; }
            }
            nameLabel.text = text; nameLabel.color = color;
        }
        Camera viewer;
        void LateUpdate()
        {
            if (nameLabel == null) return;
            if (viewer == null) viewer = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (viewer != null) nameLabel.transform.rotation = viewer.transform.rotation;
        }
        public void Show(Player player, Position? predicted = null)
        {
            Position position = predicted ?? player.Position;
            var target = new Vector3(position.X, ArenaLayout.HeightAt(position), position.Z);
            bool moving = Vector3.Distance(transform.position, target) > .07f;
            transform.position = first ? target : Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-(predicted.HasValue ? 45 : 25) * Time.unscaledDeltaTime));
            Vector3 grounded = transform.position;
            grounded.y = ArenaLayout.HeightAt(new Position(grounded.x, grounded.z));
            transform.position = grounded;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, player.Angle, 0), Time.unscaledDeltaTime * 16);
            first = false;
            ShowColors(player.KeyColor, player.AuraColor, player.FreezeLeft > 0, player.BoostLeft > 0);
            SetName("J" + (player.Slot + 1) + ":" + (player.Name ?? "").ToUpperInvariant(), Palette.Colors[player.AuraColor]);
            if (Visual != null) Visual.localPosition = new Vector3(0, moving && player.FreezeLeft <= 0 ? Mathf.Abs(Mathf.Sin(Time.time * 14)) * .12f : 0, 0);
            gameObject.SetActive(player.Connected);
        }
        public void ShowColors(int key, int aura, bool frozen = false, bool boosted = false)
        {
            if (tint == null) Awake();
            if (auraMaterial != null) auraMaterial.color = Palette.Colors[aura];
            if (Model == null) return;
            if (key != shownKey && KeyMaterials != null && key >= 0 && key < KeyMaterials.Length && KeyMaterials[key] != null)
            {
                foreach (Renderer r in Model)
                {
                    var materials = r.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = KeyMaterials[key];
                    r.sharedMaterials = materials;
                }
                shownKey = key;
            }
            // El tinte va en un MaterialPropertyBlock para no duplicar materiales por jugador.
            tint.SetColor("_Color", frozen ? new Color(.45f,.45f,1) : boosted ? Color.cyan : Color.white);
            foreach (Renderer r in Model) r.SetPropertyBlock(tint);
        }
        void OnDestroy()
        { if (auraMaterial != null) Destroy(auraMaterial); }
    }
}
