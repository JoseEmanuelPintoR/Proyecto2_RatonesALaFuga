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
        // Imagen del aura (UI/AURAS GRANDES) por color, en el mismo orden que Palette.Colors; si falta se tiñe.
        public Texture[] AuraTextures;
        // Fuente del nombre sobre el ratón ("J1:LAURA"); la asigna Ratones/UI nueva en el prefab.
        public Font NameFont;
        public float NameHeight = 1.6f;
        // Piezas del modelo (nombres del FBX) que se animan: la llave de cuerda de la espalda y los bigotes.
        public string KeyName = "pCube3", WhiskersName = "pCylinder5";
        // Vueltas por segundo de la llave: quieto, caminando y con velocidad (detenido no gira).
        public float KeyIdle = .25f, KeyWalking = 1, KeyBoosted = 2;
        Material auraMaterial;
        MaterialPropertyBlock tint;
        TextMesh nameLabel;
        int shownKey = -1;
        bool first = true;
        // Estado de la animación, que actualiza Show (en Personalizar no se llama y queda en reposo).
        bool moving, frozen, boosted;
        int lastScore = -1;
        float keyAngle, hop, walkTime;
        Spinner key, whiskers;
        const float HopTime = .35f;

        // Gira una pieza del modelo alrededor de un eje que pasa por su centro, partiendo de su pose original.
        sealed class Spinner
        {
            readonly Transform part;
            readonly Vector3 position, pivot, axis;
            readonly Quaternion rotation;
            public Spinner(Transform part, Vector3 pivotWorld, Vector3 axisWorld)
            {
                this.part = part; position = part.localPosition; rotation = part.localRotation;
                pivot = part.parent.InverseTransformPoint(pivotWorld);
                axis = part.parent.InverseTransformDirection(axisWorld).normalized;
            }
            public void Set(float degrees)
            {
                Quaternion turn = Quaternion.AngleAxis(degrees, axis);
                part.localPosition = pivot + turn * (position - pivot); part.localRotation = turn * rotation;
            }
        }

        void Awake()
        {
            if (Aura != null) auraMaterial = Aura.material; tint = new MaterialPropertyBlock();
            if (Model == null || Visual == null) return;
            Renderer body = null, keyPart = null, whiskerPart = null;
            foreach (Renderer r in Model)
            {
                if (r == null) continue;
                if (r.name == KeyName) keyPart = r; else if (r.name == WhiskersName) whiskerPart = r;
                else if (body == null || r.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude) body = r;
            }
            // La llave gira sobre su eje, que sale del cuerpo hacia atrás (horizontal respecto al ratón).
            if (keyPart != null && body != null)
            {
                Vector3 outward = Visual.InverseTransformDirection(keyPart.bounds.center - body.bounds.center);
                outward.y = 0;
                if (outward.sqrMagnitude < 1e-6f) outward = Vector3.back;
                key = new Spinner(keyPart.transform, keyPart.bounds.center, Visual.TransformDirection(outward));
            }
            // Los bigotes se mueven de lado a lado (eje vertical del ratón).
            if (whiskerPart != null) whiskers = new Spinner(whiskerPart.transform, whiskerPart.bounds.center, Visual.up);
        }
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
            Animate(Time.unscaledDeltaTime);
            if (nameLabel == null) return;
            if (viewer == null) viewer = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (viewer != null) nameLabel.transform.rotation = viewer.transform.rotation;
        }
        // Animación por código (el modelo no tiene esqueleto): llave de cuerda, contoneo, respiración, bigotes,
        // temblor al estar detenido y un saltito al comer.
        void Animate(float dt)
        {
            float time = Time.time;
            float turns = frozen ? 0 : boosted ? KeyBoosted : moving ? KeyWalking : KeyIdle;
            keyAngle = (keyAngle + turns * 360 * dt) % 360;
            if (key != null) key.Set(keyAngle);
            if (whiskers != null) whiskers.Set(frozen ? 0 : Mathf.Sin(time * (moving ? 9 : 3)) * 6);
            if (Visual == null) return;

            bool walking = moving && !frozen;
            if (walking) walkTime += dt * (boosted ? 1.5f : 1);
            float step = walkTime * 14;
            float bounce = walking ? Mathf.Abs(Mathf.Sin(step)) * .12f : 0;
            float roll = walking ? Mathf.Sin(step) * 6 : 0, lean = walking ? 4 : 0;
            float shake = frozen ? Mathf.Sin(time * 60) * .04f : 0;
            float breathe = walking || frozen ? 1 : 1 + Mathf.Sin(time * 2.5f) * .02f;
            float hopHeight = 0;
            if (hop > 0)
            {
                hop = Mathf.Max(0, hop - dt);
                hopHeight = Mathf.Sin((1 - hop / HopTime) * Mathf.PI) * .5f;
            }
            Visual.localPosition = new Vector3(shake, bounce + hopHeight, 0);
            Visual.localRotation = Quaternion.Euler(lean, 0, roll);
            Visual.localScale = new Vector3(1, breathe, 1);
        }
        public void Show(Player player, Position? predicted = null)
        {
            Position position = predicted ?? player.Position;
            var target = new Vector3(position.X, ArenaLayout.HeightAt(position), position.Z);
            moving = Vector3.Distance(transform.position, target) > .07f;
            transform.position = first ? target : Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-(predicted.HasValue ? 45 : 25) * Time.unscaledDeltaTime));
            Vector3 grounded = transform.position;
            grounded.y = ArenaLayout.HeightAt(new Position(grounded.x, grounded.z));
            transform.position = grounded;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, player.Angle, 0), Time.unscaledDeltaTime * 16);
            first = false;
            frozen = player.FreezeLeft > 0; boosted = player.BoostLeft > 0;
            // Saltito al comer (los puntos suben); no al empezar ni al reiniciar la ronda.
            if (lastScore >= 0 && player.Score > lastScore) hop = HopTime;
            lastScore = player.Score;
            ShowColors(player.KeyColor, player.AuraColor, frozen, boosted);
            SetName("J" + (player.Slot + 1) + ":" + (player.Name ?? "").ToUpperInvariant(), Palette.Colors[player.AuraColor]);
            gameObject.SetActive(player.Connected);
        }
        public void ShowColors(int key, int aura, bool frozen = false, bool boosted = false)
        {
            if (tint == null) Awake();
            if (auraMaterial != null)
            {
                Texture image = AuraTextures != null && aura >= 0 && aura < AuraTextures.Length ? AuraTextures[aura] : null;
                if (image != null) { auraMaterial.mainTexture = image; auraMaterial.color = Color.white; }
                else auraMaterial.color = Palette.Colors[aura];
            }
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
