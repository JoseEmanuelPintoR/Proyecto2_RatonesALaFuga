using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ratones.Basic.Editor
{
    // Cambia los primitivos de los items por sus modelos exportados de Substance (FBX + texturas).
    public static class ModelTools
    {
        const string Models = "Assets/Ratones/Arte/Modelos/";
        // Prefab, FBX, medida del lado más largo en metros e imagen del aura en el suelo (UI/AURAS GRANDES).
        // Chocolate (velocidad) y trampa (detener) usan el color del tinte que toma el ratón con ese poder.
        static readonly (string item, string fbx, float size, string aura)[] Items =
        {
            ("Queso", "UVS QUESO.fbx", 2.2f, "AMARILLA"),
            ("Fresa", "Fresa UVS.fbx", 2.4f, "ROJA"),
            ("Pie", "Ponque_UVS.fbx", 3, "ROSADA"),
            ("Chocolate", "Chocolate_UVS.fbx", 3, "TURQUESA"),
            ("Trampa", "Trampa_UVS.fbx", 3, "AZUL"),
        };
        // Altura del modelo sobre el suelo y diámetro del aura respecto al tamaño del item.
        const float ItemLift = .15f, AuraScale = .42f;
        // Diámetro del aura del ratón (antes un cilindro de 2.6 m).
        const float MouseAura = 1.9f;
        const string AuraImages = "Assets/Ratones/UI/AURAS GRANDES/AURA_";
        // Mismo orden que Palette.Colors.
        static readonly string[] AuraColors = { "ROJA", "AZUL", "VERDE", "ROSADA", "NARANJA", "AMARILLA", "NEGRA", "BLANCA", "MORADA", "CAFE", "TURQUESA", "MENTA" };

        [MenuItem("Ratones/Preparar modelos de items")]
        public static void PrepareAll()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar los modelos."); return; }
            foreach (var entry in Items) Prepare(entry.item, Models + entry.fbx, entry.size, entry.aura);
            AssetDatabase.SaveAssets();
        }

        // Giro de la cocina (grados) por si se quiere otra pared detrás de la cámara.
        const float CocinaYaw = 0;
        // Zona de juego inicial en coordenadas del FBX (X, Z): cuadrado sobre el piso de madera del centro.
        static readonly Vector2 ZonaCentro = new Vector2(-1.75f, .8f);
        const float ZonaLado = 8.4f;
        // Puntos de referencia del FBX (objetos hijos directos de la raíz) con su traslación (X, Z) conocida,
        // para pasar coordenadas del FBX a la escena sin depender de cómo Unity convierte los ejes.
        static readonly (string name, Vector2 position)[] Referencias =
        { ("pCube24", new Vector2(6.585f, -8.394f)), ("pCube23", new Vector2(-9.924f, 4.591f)), ("pCube49", new Vector2(4.665f, -.208f)) };
        // La zona mide un poco más que la arena para que los ratones del borde no queden justo en su orilla.
        const float CocinaMargen = 1.02f;
        const string CapaCocina = FollowCamera.KitchenLayer;
        // Mallas que cubren casi toda la planta (el cuarto: piso, paredes y techo); el resto son muebles.
        const float FraccionCuarto = .6f;

        // La cocina es decoración: la zona de juego (cuadro rosado) queda en la arena de 100 x 100 con la madera en y = 0.
        // Las colisiones siguen siendo las de ArenaLayout (RampasYObstaculos y los límites), con la textura de la madera.
        // Cocina nueva (cocina 2): sus materiales con las texturas de "cocina 2/textures".
        [MenuItem("Ratones/Preparar cocina")]
        public static void PrepareKitchen() { BuildKitchen(Models + "cocina 2/ModeloCocina.fbx", false); }
        // Cocina anterior (Substance): materiales armados con sus texturas BaseMap, Normal, etc.
        [MenuItem("Ratones/Preparar cocina anterior")]
        public static void PrepareOldKitchen() { BuildKitchen(Models + "Cocina/Cocina_UVS.fbx", true); }
        // Malla del piso de la cocina nueva ("nền nhà"): la zona de juego empieza en su centro.
        const string PisoCocina2 = "nen_nha";

        static void BuildKitchen(string fbx, bool substance)
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar la cocina."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Juego") { Debug.LogWarning("Abre la escena Juego antes de preparar la cocina."); return; }
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) { Debug.LogError("No se encontró " + fbx); return; }
            // Legible para medir el piso con rayos (posición y coordenadas de textura).
            if (importer.importCameras || importer.importLights || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None || !importer.isReadable)
            {
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None; importer.isReadable = true; importer.SaveAndReimport();
            }
            foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "Cocina").ToArray()) Undo.DestroyObjectImmediate(old);

            // Raíz sin giro (Cocina) con el modelo y la zona como hijos: escalar la raíz mueve los dos juntos.
            var root = new GameObject("Cocina");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Preparar cocina");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), scene);
            model.name = "Modelo"; model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0, CocinaYaw, 0) * model.transform.localRotation;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogError(Path.GetFileName(fbx) + " no tiene mallas."); return; }
            foreach (Renderer r in renderers) r.gameObject.isStatic = true;
            if (substance) SubstanceKitchenMaterials(renderers); else TexturedKitchenMaterials(fbx, renderers);
            // Primero a un tamaño cómodo para medir (lado más largo = arena); el ajuste final lo hace la zona.
            Bounds bounds = WorldBounds(renderers);
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest > 0) root.transform.localScale *= Rules.ArenaSize / longest;
            Renderer floor = renderers.FirstOrDefault(r => r.name == PisoCocina2);
            if (!substance) OrientKitchen(model.transform, renderers, floor);
            bounds = WorldBounds(renderers);

            var zone = new GameObject("ZonaDeJuego", typeof(ZonaDeJuego)).transform;
            zone.SetParent(root.transform, false);
            Vector2 center, axisX, axisZ;
            Vector3 freeCenter; float freeSide;
            if (substance && FbxToScene(model.transform, out center, out axisX, out axisZ))
            {
                Vector2 world = center + axisX * ZonaCentro.x + axisZ * ZonaCentro.y;
                float side = ZonaLado * Mathf.Sqrt(Mathf.Abs(axisX.x * axisZ.y - axisX.y * axisZ.x));
                zone.position = new Vector3(world.x, bounds.min.y, world.y);
                zone.localScale = new Vector3(side, 1, side) / root.transform.localScale.x;
            }
            else if (FreeSquare(floor != null ? floor.bounds : bounds, Furniture(renderers, floor), out freeCenter, out freeSide))
            {
                // El cuadrado libre de muebles más grande, lo más lejos posible de ellos: así los muebles quedan
                // alrededor, de lejos, y no tapan la pantalla.
                zone.position = freeCenter;
                zone.localScale = new Vector3(freeSide, 1, freeSide) * ZonaLibre / root.transform.localScale.x;
            }
            else
            {
                // Sin puntos de referencia: centro del piso (o del cuarto) y la mitad de su lado más corto.
                Bounds area = floor != null ? floor.bounds : bounds;
                Debug.LogWarning("La zona de juego quedó en el centro del " + (floor != null ? "piso" : "cuarto") +
                    ". Si no cae donde quieres, mueve o escala Cocina/ZonaDeJuego y usa Ratones/Ajustar cocina a la zona de juego.");
                zone.position = new Vector3(area.center.x, bounds.min.y, area.center.z);
                zone.localScale = Vector3.one * Mathf.Min(area.size.x, area.size.z) * .5f / root.transform.localScale.x;
            }
            FitKitchen(root.transform);
            Selection.activeGameObject = zone.gameObject;
        }

        // La cámara mira desde +X/-Z: arriba en la pantalla es (-X, +Z) y a la izquierda (-X, -Z). El rincón de las dos
        // paredes (donde se juntan los muebles) va arriba, un poco a la izquierda, y el piso abierto hacia la cámara.
        static readonly Vector3 EsquinaMuebles = new Vector3(-.92f, 0, .38f);
        // Copias del piso de la cocina 2 hacia los lados abiertos (sin paredes) para que no se vea el fondo de la cámara.
        const string PisoExtra = "PisoExtra";
        const int PisoExtraVueltas = 2;
        // Parte del cuadrado libre que se usa como zona (deja un margen con los muebles; FitKitchen agrega CocinaMargen).
        const float ZonaLibre = .92f;

        // Gira la cocina en pasos de 90° para que los muebles queden hacia la esquina superior derecha de la pantalla.
        static void OrientKitchen(Transform model, Renderer[] renderers, Renderer floor)
        {
            Renderer[] furniture = Furniture(renderers, floor);
            Bounds area = floor != null ? floor.bounds : WorldBounds(renderers);
            Vector3 sum = Vector3.zero; float weight = 0;
            foreach (Renderer r in furniture)
            { float w = r.bounds.size.x * r.bounds.size.z; sum += (r.bounds.center - area.center) * w; weight += w; }
            if (weight <= 0) return;
            Vector3 toFurniture = sum / weight; toFurniture.y = 0;
            int best = 0; float bestDot = float.MinValue;
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                float dot = Vector3.Dot(Quaternion.Euler(0, yaw, 0) * toFurniture, EsquinaMuebles);
                if (dot > bestDot) { bestDot = dot; best = yaw; }
            }
            // Gira alrededor del centro del piso para que siga donde estaba.
            model.RotateAround(area.center, Vector3.up, best);
        }
        // Repite el piso (nen_nha) hacia los lados sin paredes, lejos del rincón de los muebles.
        static void ExtendFloor(Renderer[] renderers)
        {
            Renderer floor = renderers.FirstOrDefault(r => r.name == PisoCocina2);
            if (floor == null) return;
            Renderer[] furniture = Furniture(renderers, floor);
            if (furniture.Length == 0) return;
            Bounds area = floor.bounds;
            Vector3 corner = furniture.Aggregate(Vector3.zero, (acc, r) => acc + r.bounds.center) / furniture.Length - area.center;
            float sx = corner.x > 0 ? -1 : 1, sz = corner.z > 0 ? -1 : 1;
            for (int i = 0; i <= PisoExtraVueltas; i++)
                for (int j = 0; j <= PisoExtraVueltas; j++)
                {
                    if (i == 0 && j == 0) continue;
                    var copy = Object.Instantiate(floor.gameObject, floor.transform.parent, true);
                    copy.name = PisoExtra;
                    foreach (Transform child in copy.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    // Un poco más abajo que el original para que no parpadee donde se tocan.
                    copy.transform.position = floor.transform.position + new Vector3(sx * i * area.size.x, -.02f, sz * j * area.size.z);
                    copy.layer = floor.gameObject.layer; copy.isStatic = true;
                }
        }

        // Muebles y paredes: todo lo que sobresale del piso y no es el cuarto completo (mismo criterio que FitKitchen).
        static Renderer[] Furniture(Renderer[] renderers, Renderer floor)
        {
            Bounds all = WorldBounds(renderers);
            float footprint = all.size.x * all.size.z;
            float floorTop = floor != null ? floor.bounds.max.y : all.min.y;
            float lift = all.size.y * .01f;
            return renderers.Where(r => r != floor && r.bounds.size.x * r.bounds.size.z < footprint * FraccionCuarto && r.bounds.max.y > floorTop + lift).ToArray();
        }
        // Busca en el piso el cuadrado más grande sin muebles; entre los del mismo tamaño, el más lejano a los muebles.
        static bool FreeSquare(Bounds floor, Renderer[] furniture, out Vector3 center, out float side)
        {
            var blocks = furniture.Select(r => r.bounds).ToArray();
            Vector3 away = blocks.Length > 0 ? blocks.Aggregate(Vector3.zero, (acc, b) => acc + b.center) / blocks.Length : floor.center;
            float step = Mathf.Min(floor.size.x, floor.size.z) / 60f;
            for (side = Mathf.Min(floor.size.x, floor.size.z); side > step * 6; side -= step)
            {
                float half = side / 2, bestDistance = -1;
                center = Vector3.zero;
                for (float x = floor.min.x + half; x <= floor.max.x - half + 1e-3f; x += step)
                    for (float z = floor.min.z + half; z <= floor.max.z - half + 1e-3f; z += step)
                    {
                        bool blocked = false;
                        foreach (Bounds b in blocks)
                            if (b.min.x < x + half && b.max.x > x - half && b.min.z < z + half && b.max.z > z - half) { blocked = true; break; }
                        if (blocked) continue;
                        float distance = (x - away.x) * (x - away.x) + (z - away.z) * (z - away.z);
                        if (distance > bestDistance) { bestDistance = distance; center = new Vector3(x, floor.max.y, z); }
                    }
                if (bestDistance >= 0) return true;
            }
            center = floor.center; side = 0;
            return false;
        }

        // Cocina 2: un material propio por cada material del FBX (copia de su color) con la textura
        // "textures/<material>_baseColor" si existe (blinn5 = la paleta de colores que usa casi todo el modelo).
        static void TexturedKitchenMaterials(string fbx, Renderer[] renderers)
        {
            string folder = Path.GetDirectoryName(fbx).Replace('\\', '/') + "/textures/";
            var materials = new Dictionary<string, Material>();
            foreach (Material source in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null))
            {
                if (materials.ContainsKey(source.name)) continue;
                string path = "Assets/Ratones/Materiales/Cocina2_" + Clean(source.name).Trim('_') + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
                material.shader = Shader.Find("Standard");
                material.color = source.HasProperty("_Color") ? source.color : Color.white;
                Texture2D texture = new[] { ".jpeg", ".jpg", ".png" }
                    .Select(ext => AssetDatabase.LoadAssetAtPath<Texture2D>(folder + source.name + "_baseColor" + ext)).FirstOrDefault(t => t != null);
                if (texture != null) { material.mainTexture = texture; material.color = Color.white; }
                else Debug.Log("Cocina 2: el material " + source.name + " no tiene textura; se deja su color.");
                material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .15f);
                EditorUtility.SetDirty(material);
                materials[source.name] = material;
            }
            foreach (Renderer r in renderers)
            {
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                { Material m; if (shared[i] != null && materials.TryGetValue(shared[i].name, out m)) shared[i] = m; }
                r.sharedMaterials = shared;
            }
            FloorMaterial(folder, renderers);
            AssetDatabase.SaveAssets();
        }
        // El piso usa su propia copia de la paleta (textures/piso_baseColor.png): en blinn5 el cuadro de las baldosas
        // oscuras es rosado (en la paleta original "color panel.jpg" era negro) y las claras caen en blanco puro.
        static void FloorMaterial(string folder, Renderer[] renderers)
        {
            Renderer floor = renderers.FirstOrDefault(r => r.name == PisoCocina2);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "piso_baseColor.png");
            if (floor == null || texture == null) return;
            const string path = "Assets/Ratones/Materiales/Cocina2_piso.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Standard"); material.mainTexture = texture; material.color = Color.white;
            material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .15f);
            EditorUtility.SetDirty(material);
            Material[] shared = floor.sharedMaterials;
            for (int i = 0; i < shared.Length; i++) shared[i] = material;
            floor.sharedMaterials = shared;
        }

        static void SubstanceKitchenMaterials(Renderer[] renderers)
        {
            string[] slots = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToArray();
            var materials = new Dictionary<string, Material>();
            foreach (string slot in slots)
                materials[slot] = CreateMaterial(slots.Length == 1 ? "CocinaModelo" : "CocinaModelo_" + Clean(slot).Trim('_'), "Cocina/Cocina_UVS_" + Clean(slot) + "_", 4096);
            foreach (Renderer r in renderers)
            {
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                { Material m; if (shared[i] != null && materials.TryGetValue(shared[i].name, out m) && m != null) shared[i] = m; }
                r.sharedMaterials = shared;
            }
            // Sin metal: el MaskMap marca partes del piso como metálicas y sin reflejos se ven negras.
            foreach (Material m in materials.Values.Where(m => m != null))
            {
                m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICGLOSSMAP");
                m.SetFloat("_Metallic", 0); m.SetFloat("_Glossiness", .15f); EditorUtility.SetDirty(m);
            }
        }

        [MenuItem("Ratones/Ajustar cocina a la zona de juego")]
        public static void FitKitchenMenu()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de ajustar la cocina."); return; }
            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Cocina");
            if (root == null || root.GetComponentInChildren<ZonaDeJuego>() == null) { Debug.LogWarning("Primero usa Ratones/Preparar cocina en la escena Juego."); return; }
            Undo.RecordObject(root.transform, "Ajustar cocina");
            Undo.RecordObject(root.GetComponentInChildren<ZonaDeJuego>().transform, "Ajustar cocina");
            FitKitchen(root.transform);
        }

        // Escala la cocina para que la zona de juego sea la arena (piso en y = 0) y la deja de fondo: va en la capa
        // Cocina, que FollowCamera dibuja detrás de todo, así los muebles solo se ven fuera de los límites del mapa.
        static void FitKitchen(Transform root)
        {
            Scene scene = root.gameObject.scene;
            Transform zone = root.GetComponentInChildren<ZonaDeJuego>().transform;
            Transform model = root.Find("Modelo");
            if (model == null) { Debug.LogError("Falta Cocina/Modelo: usa Ratones/Preparar cocina."); return; }
            model.gameObject.SetActive(true);
            foreach (Transform extra in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == PisoExtra).ToArray())
                Object.DestroyImmediate(extra.gameObject);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = WorldBounds(renderers);
            float footprint = bounds.size.x * bounds.size.z;
            Renderer[] room = renderers.Where(r => r.bounds.size.x * r.bounds.size.z >= footprint * FraccionCuarto).ToArray();
            var colliders = new List<MeshCollider>();
            try
            {
                foreach (Renderer r in room)
                {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    MeshCollider c = r.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = filter.sharedMesh; colliders.Add(c);
                }
                Physics.SyncTransforms();
                RaycastHit floorHit;
                if (!FloorHit(colliders, new Vector2(zone.position.x, zone.position.z), bounds.max.y + 1, out floorHit))
                { Debug.LogError("No hay piso de la cocina bajo la zona de juego: muévela sobre la madera."); return; }
                float side = Mathf.Min(zone.lossyScale.x, zone.lossyScale.z);
                float k = Rules.ArenaSize * CocinaMargen / side;
                var pivot = new Vector3(zone.position.x, floorHit.point.y, zone.position.z);
                root.localScale *= k;
                root.position = k * (root.position - pivot);
                Vector3 onFloor = zone.position; onFloor.y = .05f; zone.position = onFloor;
            }
            finally { foreach (MeshCollider c in colliders) if (c != null) Object.DestroyImmediate(c); }

            // La cocina 3D queda de fondo (capa propia) y sin la foto de una versión anterior.
            int layer = KitchenLayer();
            if (layer >= 0) foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            else Debug.LogWarning("No hay capas libres para " + CapaCocina + "; la cocina puede tapar el juego.");
            foreach (Renderer r in room) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ExtendFloor(renderers);
            GameObject photo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CocinaPlano");
            if (photo != null) Undo.DestroyObjectImmediate(photo);
            FloorTextures(scene);

            // Rampas y bloques de nuevo (UV para las texturas).
            ArenaTools.Prepare();
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log("Listo: cocina 3D de fondo alrededor de la arena. Si la zona no cae donde quieres, mueve o escala " +
                "Cocina/ZonaDeJuego (cuadro rosado) y usa Ratones/Ajustar cocina a la zona de juego. Guarda con Ctrl+S.");
        }

        // Texturas del juego: madera en el piso jugable (Plano100x100) y en plataformas y rampas (Suelo),
        // mármol en los bloques y los límites (Borde). Se repiten cada MetrosMadera / MetrosMarmol metros.
        const string Texturas = "Assets/Ratones/Arte/Texturas/";
        const float MetrosMadera = 8, MetrosMarmol = 6;
        [MenuItem("Ratones/Aplicar texturas del piso")]
        public static void FloorTexturesMenu()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de aplicar las texturas."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Juego") { Debug.LogWarning("Abre la escena Juego antes de aplicar las texturas."); return; }
            FloorTextures(scene);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log("Listo: piso de madera y bloques de mármol. Guarda Juego con Ctrl+S.");
        }
        static void FloorTextures(Scene scene)
        {
            Texture2D wood = TiledTexture(Texturas + "PisoMadera.png"), marble = TiledTexture(Texturas + "Marmol.png");
            if (wood == null || marble == null) { Debug.LogError("Faltan PisoMadera.png o Marmol.png en " + Texturas); return; }
            // Cada cara de los cubos y rampas va de 0 a 1 en UV: el piso mide 100 m y las caras de los bloques unos 6–12 m.
            Material floorMaterial = TiledMaterial("PisoMadera", wood, Rules.ArenaSize / MetrosMadera);
            TiledMaterial("Suelo", wood, 12 / MetrosMadera);
            TiledMaterial("Borde", marble, 8 / MetrosMarmol);
            Transform plane = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Plano100x100");
            MeshRenderer floor = plane != null ? plane.GetComponent<MeshRenderer>() : null;
            if (floor == null) { Debug.LogWarning("No se encontró Plano100x100 para ponerle la madera."); return; }
            Undo.RecordObject(floor, "Piso de madera");
            floor.enabled = true; floor.sharedMaterial = floorMaterial;
        }
        static Texture2D TiledTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Default || importer.wrapMode != TextureWrapMode.Repeat || !importer.sRGBTexture)
            { importer.textureType = TextureImporterType.Default; importer.wrapMode = TextureWrapMode.Repeat; importer.sRGBTexture = true; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        // Material Standard con la textura repetida "repeats" veces a lo ancho, respetando la proporción de la imagen.
        static Material TiledMaterial(string name, Texture2D texture, float repeats)
        {
            string path = "Assets/Ratones/Materiales/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = Color.white; material.mainTexture = texture;
            material.mainTextureOffset = Vector2.zero;
            material.mainTextureScale = new Vector2(repeats, repeats * texture.width / texture.height);
            material.SetTexture("_BumpMap", null); material.DisableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .15f);
            EditorUtility.SetDirty(material);
            return material;
        }
        // Crea la capa de la cocina en Project Settings > Tags and Layers si aún no existe.
        static int KitchenLayer()
        {
            int existing = LayerMask.NameToLayer(CapaCocina);
            if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = CapaCocina; tags.ApplyModifiedProperties();
                return i;
            }
            return -1;
        }
        // Transformación afín 2D de coordenadas del FBX (X, Z) a la escena, a partir de tres objetos de referencia.
        static bool FbxToScene(Transform model, out Vector2 origin, out Vector2 axisX, out Vector2 axisZ)
        {
            origin = axisX = axisZ = Vector2.zero;
            var points = new List<(Vector2 fbx, Vector2 scene)>();
            foreach (var reference in Referencias)
            {
                Transform t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == reference.name);
                if (t == null) return false;
                points.Add((reference.position, new Vector2(t.position.x, t.position.z)));
            }
            Vector2 n1 = points[1].fbx - points[0].fbx, n2 = points[2].fbx - points[0].fbx;
            Vector2 w1 = points[1].scene - points[0].scene, w2 = points[2].scene - points[0].scene;
            float det = n1.x * n2.y - n2.x * n1.y;
            if (Mathf.Abs(det) < 1e-6f) return false;
            // A = W · N⁻¹; sus columnas son hacia dónde va una unidad de X y de Z del FBX.
            axisX = (w1 * n2.y - w2 * n1.y) / det;
            axisZ = (w2 * n1.x - w1 * n2.x) / det;
            origin = points[0].scene - axisX * points[0].fbx.x - axisZ * points[0].fbx.y;
            return true;
        }
        // Superficie hacia arriba más baja bajo el punto (solo contra las mallas del cuarto).
        static bool FloorHit(List<MeshCollider> colliders, Vector2 point, float top, out RaycastHit floor)
        {
            floor = default(RaycastHit); bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, top, point.y), Vector3.down, Mathf.Abs(top) * 4 + 1000))
            {
                if (!colliders.Contains(hit.collider as MeshCollider) || hit.normal.y < .9f) continue;
                if (!found || hit.point.y < floor.point.y) { floor = hit; found = true; }
            }
            return found;
        }
        // Sufijo de la textura del ratón para cada color de Palette.Colors (rojo, azul, verde, rosa, naranja,
        // amarillo, negro, celeste, morado, café, cian, lima). Se sacó muestreando el color de acento de cada PNG.
        static readonly string[] MouseKeySuffix = { " 4", "", " 6", " 5", " 8", " 9", " 2", " 1", " 11", " 3", " 10", " 7" };
        const string MousePrefix = "Ratones/Raton_UVS_lambert1_";
        const float MouseHeight = 1.8f;
        // Giro extra del modelo si no mira hacia +Z (hacia donde camina el jugador).
        const float RatonYaw = 0;

        [MenuItem("Ratones/Preparar ratón")]
        public static void PrepareMouse()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar el ratón."); return; }
            string fbx = Models + "Ratones/Raton_UVS.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) { Debug.LogError("No se encontró " + fbx); return; }
            if (importer.importCameras || importer.importLights || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None; importer.SaveAndReimport();
            }
            // Normal y MetallicSmoothness son iguales en todas las variantes: se comparten.
            Texture2D gloss = LoadTexture(Find(MousePrefix, "MetallicSmoothness"), TextureImporterType.Default, false);
            Texture2D normal = LoadTexture(Find(MousePrefix, "Normal"), TextureImporterType.NormalMap, false);
            var keys = new Material[MouseKeySuffix.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                string albedo = Find(MousePrefix, "AlbedoTransparency" + MouseKeySuffix[i]);
                if (albedo == null) { Debug.LogError("Falta la textura " + Models + MousePrefix + "AlbedoTransparency" + MouseKeySuffix[i] + ".png"); return; }
                keys[i] = BuildMaterial("RatonLlave" + i, LoadTexture(albedo, TextureImporterType.Default, true), gloss, false, normal);
            }

            const string prefabPath = "Assets/Ratones/Prefabs/JugadorBasico.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PlayerView view = root.GetComponent<PlayerView>();
                Transform visual = view.Visual != null ? view.Visual : root.transform;
                foreach (string old in new[] { "Cuerpo", "Llave", "Modelo" })
                { Transform t = visual.Find(old); if (t != null) Object.DestroyImmediate(t.gameObject); }
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                model.name = "Modelo"; model.transform.SetParent(visual, false);
                model.transform.localRotation = Quaternion.Euler(0, RatonYaw, 0) * model.transform.localRotation;
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    var materials = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = keys[0];
                    r.sharedMaterials = materials;
                }
                // Escala pareja hasta la altura del jugador, apoyado en el suelo y centrado.
                Bounds bounds = WorldBounds(renderers);
                if (bounds.size.y > 0) model.transform.localScale *= MouseHeight / bounds.size.y;
                bounds = WorldBounds(renderers);
                model.transform.position -= new Vector3(bounds.center.x - root.transform.position.x, bounds.min.y - root.transform.position.y, bounds.center.z - root.transform.position.z);
                view.Model = renderers; view.KeyMaterials = keys;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                bounds = WorldBounds(renderers);
                Debug.Log("Listo: JugadorBasico usa el ratón con 12 texturas de llave (" + bounds.size.x.ToString("0.00") + " x " + bounds.size.y.ToString("0.00") + " x " + bounds.size.z.ToString("0.00") + " m). Si no mira hacia donde camina, cambia RatonYaw en ModelTools.cs.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        // Cambia los discos de color (cilindros) del ratón y de los items por las imágenes de AURAS GRANDES.
        [MenuItem("Ratones/Aplicar auras con imagen")]
        public static void ApplyAuras()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de aplicar las auras."); return; }
            foreach (var entry in Items)
            {
                string prefabPath = "Assets/Ratones/Prefabs/" + entry.item + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { Debug.LogWarning("No se encontró " + prefabPath); continue; }
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try { Aura(root.transform, entry.item, entry.size, entry.aura); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            MouseAuras();
            AssetDatabase.SaveAssets();
            Debug.Log("Auras con imagen aplicadas al ratón y a los items.");
        }
        static void MouseAuras()
        {
            const string prefabPath = "Assets/Ratones/Prefabs/JugadorBasico.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { Debug.LogWarning("No se encontró " + prefabPath); return; }
            Texture[] textures = AuraColors.Select(AuraTexture).ToArray();
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PlayerView view = root.GetComponent<PlayerView>();
                Transform aura = view.Aura != null ? view.Aura.transform : root.transform.Find("Aura");
                if (aura == null) { Debug.LogWarning("JugadorBasico no tiene Aura."); return; }
                ImageAura(aura, MouseAura, AuraMaterial("Plataforma", (Texture2D)textures[0]));
                view.Aura = aura.GetComponent<MeshRenderer>(); view.AuraTextures = textures;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Texture2D AuraTexture(string color)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AuraImages + color + ".png");
            if (texture == null) Debug.LogWarning("No se encontró " + AuraImages + color + ".png");
            return texture;
        }

        static void Prepare(string item, string fbx, float size, string auraColor)
        {
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) { Debug.LogError("No se encontró " + fbx); return; }
            if (importer.importCameras || importer.importLights || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None; importer.SaveAndReimport();
            }

            string prefabPath = "Assets/Ratones/Prefabs/" + item + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>()); Object.DestroyImmediate(root.GetComponent<MeshFilter>());
                root.transform.localScale = Vector3.one;
                Transform previous = root.transform.Find("Modelo");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                model.name = "Modelo"; model.transform.SetParent(root.transform, false);

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                string[] slots = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToArray();
                var materials = new Dictionary<string, Material>();
                foreach (string slot in slots)
                    materials[slot] = CreateMaterial(slots.Length == 1 ? item + "Modelo" : item + "Modelo_" + Clean(slot).Trim('_'),
                        Path.GetFileNameWithoutExtension(fbx) + "_" + Clean(slot) + "_");
                foreach (Renderer r in renderers)
                {
                    Material[] shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++)
                    { Material m; if (shared[i] != null && materials.TryGetValue(shared[i].name, out m) && m != null) shared[i] = m; }
                    r.sharedMaterials = shared; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                }

                // Escala uniforme, centrado en X/Z para que gire sobre su eje y apoyado sobre la raíz (el suelo).
                Bounds bounds = WorldBounds(renderers);
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (longest > 0) model.transform.localScale *= size / longest;
                bounds = WorldBounds(renderers);
                Vector3 origin = root.transform.position;
                model.transform.localPosition -= new Vector3(bounds.center.x - origin.x, bounds.min.y - origin.y - ItemLift, bounds.center.z - origin.z);
                Aura(root.transform, item, size, auraColor);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Listo: " + item + " usa su modelo (" + bounds.size.x.ToString("0.00") + " x " + bounds.size.y.ToString("0.00") + " x " + bounds.size.z.ToString("0.00") + " m).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Imagen del aura acostada en el suelo bajo el item, como el aura del ratón.
        static void Aura(Transform root, string item, float size, string color)
        {
            Transform aura = root.Find("Aura");
            if (aura == null)
            {
                aura = new GameObject("Aura", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                aura.SetParent(root, false);
            }
            ImageAura(aura, size * AuraScale, AuraMaterial(item, AuraTexture(color)));
        }
        // Quad acostado (mira hacia arriba) con la imagen; reemplaza el cilindro aplastado de antes.
        static void ImageAura(Transform aura, float diameter, Material material)
        {
            aura.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            aura.localPosition = new Vector3(0, .04f, 0); aura.localRotation = Quaternion.Euler(90, 0, 0);
            aura.localScale = new Vector3(diameter, diameter, 1);
            var renderer = aura.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        // Sin luz para que se vea igual a la sombra; la imagen ya trae el color.
        static Material AuraMaterial(string item, Texture2D texture)
        {
            string path = "Assets/Ratones/Materiales/Aura" + item + ".mat";
            Shader shader = Shader.Find("Unlit/Transparent");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.mainTexture = texture;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Painter nombra las texturas "<FBX>_<material>_<mapa>.png" cambiando los caracteres raros por "_".
        static string Clean(string name) { return Regex.Replace(name, "[^A-Za-z0-9_ ]", "_"); }
        static string Find(string prefix, params string[] maps)
        {
            foreach (string map in maps)
            { string path = Models + prefix + map + ".png"; if (File.Exists(path)) return path; }
            return null;
        }

        static Material CreateMaterial(string name, string prefix, int maxSize = 1024)
        {
            string albedoPath = Find(prefix, "BaseMap", "AlbedoTransparency", "BaseColor");
            if (albedoPath == null) { Debug.LogWarning("No se encontraron texturas " + Models + prefix + "*.png"); return null; }
            string maskPath = Find(prefix, "MaskMap");
            string glossPath = maskPath ?? Find(prefix, "MetallicSmoothness") ?? CombineMetallicRoughness(prefix);
            Texture2D albedo = LoadTexture(albedoPath, TextureImporterType.Default, true, maxSize);
            Texture2D gloss = LoadTexture(glossPath, TextureImporterType.Default, false, maxSize);
            Texture2D normal = LoadTexture(Find(prefix, "Normal"), TextureImporterType.NormalMap, false, maxSize);
            return BuildMaterial(name, albedo, gloss, maskPath != null, normal);
        }

        static Material BuildMaterial(string name, Texture2D albedo, Texture2D gloss, bool glossHasOcclusion, Texture2D normal)
        {
            string path = "Assets/Ratones/Materiales/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = Color.white; material.mainTexture = albedo;
            // Metal en R y suavidad en A; el MaskMap además trae la oclusión en G, que es el canal que lee el Standard.
            material.SetTexture("_MetallicGlossMap", gloss); material.SetFloat("_GlossMapScale", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetTexture("_OcclusionMap", glossHasOcclusion ? gloss : null); material.SetFloat("_OcclusionStrength", 1);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1);
            if (gloss != null) material.EnableKeyword("_METALLICGLOSSMAP"); else material.DisableKeyword("_METALLICGLOSSMAP");
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Para exports con Metallic y Roughness separados: arma el MetallicSmoothness que usa el Standard.
        static string CombineMetallicRoughness(string prefix)
        {
            string metallicPath = Find(prefix, "Metallic"), roughnessPath = Find(prefix, "Roughness");
            if (roughnessPath == null) return null;
            var roughness = new Texture2D(2, 2); roughness.LoadImage(File.ReadAllBytes(roughnessPath));
            Color32[] rough = roughness.GetPixels32(), metal = null;
            if (metallicPath != null)
            {
                var metallic = new Texture2D(2, 2); metallic.LoadImage(File.ReadAllBytes(metallicPath));
                if (metallic.width == roughness.width && metallic.height == roughness.height) metal = metallic.GetPixels32();
                Object.DestroyImmediate(metallic);
            }
            var pixels = new Color32[rough.Length];
            for (int i = 0; i < pixels.Length; i++)
            { byte m = metal != null ? metal[i].r : (byte)0; pixels[i] = new Color32(m, m, m, (byte)(255 - rough[i].r)); }
            var combined = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false);
            combined.SetPixels32(pixels);
            string path = Models + prefix + "MetallicSmoothness.png";
            File.WriteAllBytes(path, combined.EncodeToPNG());
            Object.DestroyImmediate(roughness); Object.DestroyImmediate(combined);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        static Texture2D LoadTexture(string path, TextureImporterType type, bool srgb, int maxSize = 1024)
        {
            if (path == null) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != maxSize))
            { importer.textureType = type; importer.sRGBTexture = srgb; importer.maxTextureSize = maxSize; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Bounds WorldBounds(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
