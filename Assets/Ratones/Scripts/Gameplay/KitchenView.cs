using System.Collections.Generic;
using UnityEngine;
using Ratones.Core;

namespace Ratones.Gameplay
{
    public sealed class KitchenView : MonoBehaviour
    {
        public static readonly Color[] PlayerColors = {
            new Color(1f, .27f, .25f), new Color(.18f, .65f, 1f),
            new Color(.3f, .9f, .56f), new Color(1f, .76f, .18f)
        };
        public Transform MouseVisualPrefab, CheeseVisualPrefab;
        readonly Dictionary<int, MouseView> mice = new Dictionary<int, MouseView>();
        readonly Dictionary<int, Transform> items = new Dictionary<int, Transform>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<GameObject> previews = new List<GameObject>();
        readonly List<Mesh> cheeseMeshes = new List<Mesh>();
        Camera cam;
        Light sun;
        Transform dynamicRoot;
        int shownRound = -1;
        Vector3 cameraVelocity;
        bool wasPlaying;

        public Material Material(string key, Color color, bool glow = false)
        {
            Material result;
            if (materials.TryGetValue(key, out result)) return result;
            result = new Material(Resources.Load<Shader>("Ratones/KitchenColor")); result.color = color;
            result.SetFloat("_Glossiness", .22f);
            if (glow) { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", color * .35f); }
            materials.Add(key, result); return result;
        }

        public static GameObject Shape(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        { return Shape(name, PrimitiveType.Cube, parent, pos, size, mat); }

        public void Build()
        {
            var floorA = Material("floorA", new Color(.16f, .28f, .32f));
            var floorB = Material("floorB", new Color(.21f, .35f, .38f));
            var grout = Material("grout", new Color(.075f, .15f, .19f));
            var teal = Material("teal", new Color(.13f, .38f, .42f));
            var tealDark = Material("tealDark", new Color(.07f, .21f, .25f));
            var brass = Material("brass", new Color(.94f, .65f, .24f));
            var wood = Material("wood", new Color(.53f, .28f, .15f));
            var cream = Material("cream", new Color(.86f, .88f, .80f));
            var cardboard = Material("cardboard", new Color(.66f, .42f, .23f));
            var blue = Material("fridge", new Color(.42f, .66f, .70f));
            Box("Suelo 100 x 100", transform, new Vector3(0, -.35f, 0), new Vector3(100, .6f, 100), grout);
            for (int x = 0; x < 10; x++) for (int z = 0; z < 10; z++)
                Box("Baldosa", transform, new Vector3(-45 + x * 10, -.015f, -45 + z * 10),
                    new Vector3(9.88f, .08f, 9.88f), (x + z) % 2 == 0 ? floorA : floorB);
            Box("Pared del fondo", transform, new Vector3(0, 11, 50.5f), new Vector3(102, 22, 1), tealDark);
            Box("Zócalo izquierdo", transform, new Vector3(-50.5f, 1.6f, 0), new Vector3(1, 3.2f, 100), tealDark);
            Box("Zócalo derecho", transform, new Vector3(50.5f, 1.6f, 0), new Vector3(1, 3.2f, 100), tealDark);
            Box("Zócalo frontal", transform, new Vector3(0, .7f, -50.5f), new Vector3(100, 1.4f, 1), tealDark);
            foreach (Block b in KitchenMap.Blocks)
            {
                var group = new GameObject("Mueble " + b.Kind).transform;
                group.SetParent(transform, false); group.localPosition = new Vector3(b.X, 0, b.Z);
                if (b.Kind == 0)
                {
                    Box("Gabinete", group, new Vector3(0, b.Height / 2, 0), new Vector3(b.Width, b.Height, b.Depth), teal);
                    Box("Encimera", group, new Vector3(0, b.Height, 0), new Vector3(b.Width, .65f, b.Depth), cream);
                    for (int i = 0; i < 2; i++)
                    {
                        float x = (i == 0 ? -.25f : .25f) * b.Width;
                        Box("Puerta", group, new Vector3(x, b.Height / 2, -b.Depth / 2 - .03f),
                            new Vector3(b.Width * .44f, b.Height * .74f, .14f), tealDark);
                        Box("Tirador", group, new Vector3(x, b.Height * .62f, -b.Depth / 2 - .25f), new Vector3(2.5f, .25f, .3f), brass);
                    }
                }
                else if (b.Kind == 1)
                {
                    Box("Nevera", group, new Vector3(0, b.Height / 2, 0), new Vector3(b.Width, b.Height, b.Depth), blue);
                    Box("Separación de puertas", group, new Vector3(0, b.Height * .65f, -b.Depth / 2 - .04f), new Vector3(b.Width, .22f, .12f), tealDark);
                    Box("Manija superior", group, new Vector3(-b.Width * .33f, b.Height * .78f, -b.Depth / 2 - .35f), new Vector3(.4f, 3.3f, .5f), cream);
                    Box("Manija inferior", group, new Vector3(-b.Width * .33f, b.Height * .44f, -b.Depth / 2 - .35f), new Vector3(.4f, 4.4f, .5f), cream);
                }
                else if (b.Kind == 2)
                {
                    Box("Caja de despensa", group, new Vector3(0, b.Height / 2, 0), new Vector3(b.Width, b.Height, b.Depth), cardboard);
                    Box("Cinta de empaque", group, new Vector3(0, b.Height + .04f, 0), new Vector3(2, .1f, b.Depth), brass);
                    Box("Etiqueta", group, new Vector3(0, b.Height * .55f, -b.Depth / 2 - .06f), new Vector3(b.Width * .48f, b.Height * .38f, .14f), cream);
                }
                else Box(b.Kind == 3 ? "Pata de mesa" : "Tabla de cocina", group,
                    new Vector3(0, b.Height / 2, 0), new Vector3(b.Width, b.Height, b.Depth), wood);
            }
            // Bastidor abierto: comunica la mesa gigante y permite ver a los ratones.
            Box("Bastidor de mesa trasero", transform, new Vector3(0, 13, 12), new Vector3(28, 1.2f, 2.6f), wood);
            Box("Bastidor de mesa izquierdo", transform, new Vector3(-12, 13, 1), new Vector3(2.6f, 1.2f, 26), wood);
            Box("Bastidor de mesa derecho", transform, new Vector3(12, 13, 1), new Vector3(2.6f, 1.2f, 26), wood);
            // Detalles de cocina fuera de las rutas transitables.
            for (int i = 0; i < 3; i++)
                Shape("Plato apilado", PrimitiveType.Cylinder, transform, new Vector3(-36, 15.65f + i * .38f, 40), new Vector3(7, .15f, 7), cream);
            var pot = Shape("Olla", PrimitiveType.Cylinder, transform, new Vector3(-8, 17.5f, 40), new Vector3(7, 2.1f, 7), tealDark);
            Shape("Tapa de olla", PrimitiveType.Cylinder, pot.transform, new Vector3(0, 1.05f, 0), new Vector3(1.1f, .05f, 1.1f), brass);
            var lightObject = new GameObject("Luz de cocina"); lightObject.transform.SetParent(transform);
            sun = lightObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.25f;
            sun.color = new Color(1f, .90f, .76f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.60f, .69f, .75f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 115;
            cam = new GameObject("Camara principal").AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.transform.SetParent(transform); cam.orthographic = true; cam.orthographicSize = 60;
            cam.nearClipPlane = .1f; cam.farClipPlane = 220;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f, .075f, .11f);
            cam.gameObject.AddComponent<AudioListener>();
            dynamicRoot = new GameObject("Ratones y quesos").transform; dynamicRoot.SetParent(transform);
            for (int i = 0; i < 4; i++)
            {
                MouseView m = MakeMouse(i, "Ratón " + (i + 1));
                m.transform.position = new Vector3(-8 + i * 5, 0, -6 + (i % 2) * 3);
                m.transform.rotation = Quaternion.Euler(0, 160 + i * 15, 0);
                m.transform.localScale = Vector3.one * 1.8f; previews.Add(m.gameObject);
            }
        }

        MouseView MakeMouse(int id, string label)
        {
            GameObject root = new GameObject(label); root.transform.SetParent(dynamicRoot, false);
            var mouse = root.AddComponent<MouseView>();
            mouse.Build(this, PlayerColors[id % 4], MouseVisualPrefab);
            return mouse;
        }

        Transform MakeItem(ItemState item)
        {
            Transform root = new GameObject(item.Kind.ToString() + " " + item.Id).transform; root.SetParent(dynamicRoot, false);
            if (item.Kind == ItemKind.Cheese)
            {
                if (CheeseVisualPrefab != null) Instantiate(CheeseVisualPrefab, root, false);
                else
                {
                    var cheese = new GameObject("Cuña de queso"); cheese.transform.SetParent(root, false);
                    var mesh = new Mesh();
                    cheeseMeshes.Add(mesh);
                    // Prisma triangular, tamaño legible desde cámara elevada.
                    Vector3[] corners = { new Vector3(-1, 0, -.8f), new Vector3(1, 0, -.8f), new Vector3(0, 0, 1.3f),
                        new Vector3(-1, 1.2f, -.8f), new Vector3(1, 1.2f, -.8f), new Vector3(0, 1.2f, 1.3f) };
                    int[] faces = { 0,1,2, 3,5,4, 0,3,4, 0,4,1, 1,4,5, 1,5,2, 2,5,3, 2,3,0 };
                    var verts = new Vector3[faces.Length]; var tris = new int[faces.Length];
                    for (int i = 0; i < faces.Length; i++) { verts[i] = corners[faces[i]]; tris[i] = i; }
                    mesh.vertices = verts; mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    cheese.AddComponent<MeshFilter>().sharedMesh = mesh;
                    cheese.AddComponent<MeshRenderer>().sharedMaterial = Material("cheese", new Color(1, .72f, .10f), true);
                    var hole = Material("holes", new Color(.78f, .36f, .035f));
                    Shape("Agujero", PrimitiveType.Sphere, root, new Vector3(-.35f, 1.205f, -.25f), new Vector3(.42f, .035f, .42f), hole);
                    Shape("Agujero", PrimitiveType.Sphere, root, new Vector3(.25f, 1.205f, .28f), new Vector3(.3f, .035f, .3f), hole);
                    Shape("Agujero", PrimitiveType.Sphere, root, new Vector3(.3f, .52f, -.805f), new Vector3(.4f, .4f, .035f), hole);
                }
            }
            else if (item.Kind == ItemKind.Sugar)
            {
                var mat = Material("sugar", new Color(.1f, .8f, 1f), true);
                Shape("Caramelo", PrimitiveType.Sphere, root, new Vector3(0, .5f, 0), new Vector3(1.9f, 1.3f, 1.3f), mat);
                for (int s = -1; s <= 1; s += 2)
                {
                    var wrap = Box("Envoltura", root, new Vector3(s * 1.05f, .5f, 0), new Vector3(.65f, .7f, .7f), mat);
                    wrap.transform.localRotation = Quaternion.Euler(45, 0, 45);
                }
            }
            else
            {
                Shape("Frasco pegajoso", PrimitiveType.Cylinder, root, new Vector3(0, .5f, 0), new Vector3(1.4f, .65f, 1.4f),
                    Material("sticky", new Color(.68f, .3f, 1f), true));
                Shape("Tapa", PrimitiveType.Cylinder, root, new Vector3(0, 1.25f, 0), new Vector3(1.55f, .12f, 1.55f), Material("cream", Color.white));
            }
            return root;
        }

        public void Show(MatchState state, int ownId, float dt)
        {
            bool playing = state != null && state.Phase != MatchPhase.Lobby;
            foreach (GameObject p in previews) p.SetActive(!playing);
            if (!playing)
            {
                if (shownRound >= 0) ClearRound();
                cam.transform.position = new Vector3(10, 76, -47);
                cam.transform.LookAt(new Vector3(0, 0, 1));
                cam.orthographicSize = 55;
                wasPlaying = false; return;
            }
            if (shownRound != state.Round)
            {
                ClearRound(); shownRound = state.Round;
                foreach (ItemState item in state.Items) items[item.Id] = MakeItem(item);
            }
            foreach (PlayerState p in state.Players)
            {
                MouseView mouse;
                if (!mice.TryGetValue(p.Id, out mouse)) { mouse = MakeMouse(p.ColorIndex, p.Name); mice[p.Id] = mouse; mouse.transform.position = new Vector3(p.Position.X, 0, p.Position.Z); }
                mouse.gameObject.SetActive(p.Connected);
                mouse.Apply(p, state.Winners.Contains(p.Id), dt);
            }
            foreach (ItemState item in state.Items)
            {
                Transform model;
                if (!items.TryGetValue(item.Id, out model)) continue;
                model.gameObject.SetActive(item.Active);
                model.position = new Vector3(item.Position.X, .55f + Mathf.Sin(Time.time * 2.5f + item.Id) * .22f, item.Position.Z);
                model.rotation = Quaternion.Euler(0, Time.time * 32 + item.Id * 29, 0);
            }
            PlayerState own = state.Player(ownId);
            Vector3 focus = own == null ? Vector3.zero : new Vector3(own.Position.X, 0, own.Position.Z);
            Vector3 target = focus + new Vector3(0, 70, -30);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, cam.aspect < 1.4f ? 31 : 27, dt * 6);
            cam.transform.position = !wasPlaying ? target : Vector3.SmoothDamp(cam.transform.position, target, ref cameraVelocity, .15f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, -70, 30));
            wasPlaying = true;
        }

        public Vector3 ScreenPoint(Point p) { return cam.WorldToScreenPoint(new Vector3(p.X, 3.9f, p.Z)); }
        void ClearRound()
        {
            foreach (MouseView m in mice.Values) if (m != null) Destroy(m.gameObject);
            foreach (Transform t in items.Values) if (t != null) Destroy(t.gameObject);
            foreach (Mesh mesh in cheeseMeshes) if (mesh != null) Destroy(mesh);
            cheeseMeshes.Clear();
            mice.Clear(); items.Clear(); shownRound = -1;
        }
        public void SetShadows(bool on) { if (sun != null) sun.shadows = on ? LightShadows.Soft : LightShadows.None; }
        void OnDestroy() { foreach (Material m in materials.Values) if (m != null) Destroy(m); }
    }
}
