using UnityEngine;

namespace Ratones.Basic
{
    public sealed class PlayerView : MonoBehaviour
    {
        public Transform Visual;
        public Renderer Body, Key, Aura;
        Material bodyMaterial, keyMaterial, auraMaterial;
        bool first = true;
        void Awake()
        { bodyMaterial = Body.material; keyMaterial = Key.material; auraMaterial = Aura.material; }
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
            keyMaterial.color = Palette.Colors[player.KeyColor]; auraMaterial.color = Palette.Colors[player.AuraColor];
            bodyMaterial.color = player.FreezeLeft > 0 ? new Color(.45f,.45f,1) : player.BoostLeft > 0 ? Color.cyan : new Color(.65f,.65f,.65f);
            if (Visual != null) Visual.localPosition = new Vector3(0, moving && player.FreezeLeft <= 0 ? Mathf.Abs(Mathf.Sin(Time.time * 14)) * .12f : 0, 0);
            gameObject.SetActive(player.Connected);
        }
        void OnDestroy()
        { if (bodyMaterial != null) Destroy(bodyMaterial); if (keyMaterial != null) Destroy(keyMaterial); if (auraMaterial != null) Destroy(auraMaterial); }
    }
}
