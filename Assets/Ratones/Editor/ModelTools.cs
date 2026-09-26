using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ratones.Basic.Editor
{
    // Cambia los primitivos de los items por sus modelos exportados de Substance (FBX + texturas).
    public static class ModelTools
    {
        const string Models = "Assets/Ratones/Arte/Modelos/";
        // Prefab, FBX y medida del lado más largo en metros (igual que el primitivo anterior).
        static readonly (string item, string fbx, float size)[] Items =
        {
            ("Queso", "UVS QUESO.fbx", 1.5f),
            ("Fresa", "Fresa UVS.fbx", 1.6f),
            ("Pie", "Ponque_UVS.fbx", 2),
            ("Chocolate", "Chocolate_UVS.fbx", 2),
            ("Trampa", "Trampa_UVS.fbx", 2),
        };

        [MenuItem("Ratones/Preparar modelos de items")]
        public static void PrepareAll()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar los modelos."); return; }
            foreach (var entry in Items) Prepare(entry.item, Models + entry.fbx, entry.size);
            AssetDatabase.SaveAssets();
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

        static void Prepare(string item, string fbx, float size)
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

                // Escala uniforme y centrado para que gire sobre su centro sin deformarse.
                Bounds bounds = WorldBounds(renderers);
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (longest > 0) model.transform.localScale *= size / longest;
                bounds = WorldBounds(renderers);
                model.transform.localPosition -= bounds.center - root.transform.position;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Listo: " + item + " usa su modelo (" + bounds.size.x.ToString("0.00") + " x " + bounds.size.y.ToString("0.00") + " x " + bounds.size.z.ToString("0.00") + " m).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Painter nombra las texturas "<FBX>_<material>_<mapa>.png" cambiando los caracteres raros por "_".
        static string Clean(string name) { return Regex.Replace(name, "[^A-Za-z0-9_ ]", "_"); }
        static string Find(string prefix, params string[] maps)
        {
            foreach (string map in maps)
            { string path = Models + prefix + map + ".png"; if (File.Exists(path)) return path; }
            return null;
        }

        static Material CreateMaterial(string name, string prefix)
        {
            string albedoPath = Find(prefix, "BaseMap", "AlbedoTransparency", "BaseColor");
            if (albedoPath == null) { Debug.LogWarning("No se encontraron texturas " + Models + prefix + "*.png"); return null; }
            string maskPath = Find(prefix, "MaskMap");
            string glossPath = maskPath ?? Find(prefix, "MetallicSmoothness") ?? CombineMetallicRoughness(prefix);
            Texture2D albedo = LoadTexture(albedoPath, TextureImporterType.Default, true);
            Texture2D gloss = LoadTexture(glossPath, TextureImporterType.Default, false);
            Texture2D normal = LoadTexture(Find(prefix, "Normal"), TextureImporterType.NormalMap, false);
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

        static Texture2D LoadTexture(string path, TextureImporterType type, bool srgb)
        {
            if (path == null) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 1024))
            { importer.textureType = type; importer.sRGBTexture = srgb; importer.maxTextureSize = 1024; importer.SaveAndReimport(); }
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
