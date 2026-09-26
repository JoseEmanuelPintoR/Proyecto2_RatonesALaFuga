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
        Material auraMaterial;
        MaterialPropertyBlock tint;
        int shownKey = -1;
        bool first = true;
        void Awake()
        { if (Aura != null) auraMaterial = Aura.material; tint = new MaterialPropertyBlock(); }
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
