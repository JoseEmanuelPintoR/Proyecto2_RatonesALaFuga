using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ratones.Basic.Editor
{
    // Ícono de Android en capas (adaptativo): los launchers recortan el ícono con su propia forma (círculo, gota,
    // cuadrado redondeado) y solo muestran seguro el centro (66 de 108 dp). Con solo el Default Icon algunos (Huawei)
    // lo achican dentro de un fondo blanco y otros le cortan las letras. El logo va aparte y más chico sobre la madera.
    public static class IconTools
    {
        const string Portada = UISpriteImporter.Folder + "PORTADA APK.png";
        const string Logo = UISpriteImporter.Folder + "MENU/LOGO.png";
        const string Carpeta = UISpriteImporter.Folder + "ICONO/";
        const int Lado = 432;
        // Ancho del logo respecto al ícono: en la capa del frente cabe en la zona segura; en el redondo, dentro del círculo.
        const float LogoAdaptativo = .58f, LogoRedondo = .7f;
        // Filas de PORTADA APK (contadas desde abajo, de 512) que solo tienen madera, sin el logo.
        const int MaderaAbajoHasta = 151, MaderaArribaDesde = 366;

        [MenuItem("Ratones/Preparar icono Android")]
        public static void PrepareIcon()
        {
            var target = NamedBuildTarget.Android;

            PlatformIconKind[] tipos =
                PlayerSettings.GetSupportedIconKinds(target);

            if (tipos == null || tipos.Length == 0)
            {
                Debug.LogWarning(
                    "Este editor no tiene disponibles los tipos de icono Android. " +
                    "Para preparar el APK, revisa que Android Build Support " +
                    "esté instalado para esta versión de Unity."
                );
                return;
            }

            Texture2D portada = Load(Portada);
            Texture2D logo = Load(Logo);

            if (portada == null || logo == null)
            {
                if (portada != null) Object.DestroyImmediate(portada);
                if (logo != null) Object.DestroyImmediate(logo);

                Debug.LogError("Faltan " + Portada + " o " + Logo);
                return;
            }

            Directory.CreateDirectory(Carpeta);

            Texture2D fondo = Save(
                Wood(portada),
                "ICONO_FONDO.png"
            );

            Texture2D frente = Save(
                WithLogo(
                    new Texture2D(Lado, Lado, TextureFormat.RGBA32, false),
                    logo,
                    LogoAdaptativo,
                    true
                ),
                "ICONO_FRENTE.png"
            );

            Texture2D redondo = Save(
                WithLogo(Wood(portada), logo, LogoRedondo, false),
                "ICONO_REDONDO.png"
            );

            Object.DestroyImmediate(portada);
            Object.DestroyImmediate(logo);

            Texture2D legacy =
                AssetDatabase.LoadAssetAtPath<Texture2D>(Portada);

            int configurados = 0;

            foreach (PlatformIconKind tipo in tipos)
            {
                string nombre = tipo.ToString();

                if (nombre.IndexOf(
                        "Adaptive",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PlatformIcon[] iconos =
                        PlayerSettings.GetPlatformIcons(target, tipo);

                    foreach (PlatformIcon icono in iconos)
                    {
                        // Capa 0: fondo. Capa 1: logo.
                        icono.SetTextures(fondo, frente);
                    }

                    PlayerSettings.SetPlatformIcons(target, tipo, iconos);
                }
                else if (nombre.IndexOf(
                             "Round",
                             System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetAll(target, tipo, redondo);
                }
                else if (nombre.IndexOf(
                             "Legacy",
                             System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetAll(target, tipo, legacy);
                }
                else
                {
                    continue;
                }

                configurados++;
            }

            AssetDatabase.SaveAssets();

            if (configurados > 0)
            {
                Debug.Log(
                    "Listo: se configuraron " + configurados +
                    " tipos de icono Android. Vuelve a generar el APK."
                );
            }
            else
            {
                Debug.LogWarning(
                    "No se reconocieron los tipos de icono Android disponibles."
                );
            }
        }

        static void SetAll(NamedBuildTarget target, PlatformIconKind kind, Texture2D texture)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (PlatformIcon icon in icons) icon.SetTexture(texture);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        // Madera de la portada sin el logo: la mitad de abajo y la de arriba salen de las franjas que no lo tocan.
        static Texture2D Wood(Texture2D portada)
        {
            var wood = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
            float h = portada.height;
            for (int y = 0; y < Lado; y++)
            {
                float t = (y + .5f) / Lado;
                float v = t < .5f ? t * 2 * MaderaAbajoHasta / h : (MaderaArribaDesde + (t - .5f) * 2 * (h - MaderaArribaDesde)) / h;
                for (int x = 0; x < Lado; x++)
                {
                    Color c = portada.GetPixelBilinear((x + .5f) / Lado, v); c.a = 1;
                    wood.SetPixel(x, y, c);
                }
            }
            return wood;
        }

        // Logo centrado con "width" del ancho del ícono, encima de lo que ya tenga la textura.
        static Texture2D WithLogo(Texture2D texture, Texture2D logo, float width, bool clear)
        {
            if (clear) texture.SetPixels(new Color[Lado * Lado]);
            int w = Mathf.RoundToInt(Lado * width), h = Mathf.RoundToInt(w * (float)logo.height / logo.width);
            int x0 = (Lado - w) / 2, y0 = (Lado - h) / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color top = logo.GetPixelBilinear((x + .5f) / w, (y + .5f) / h);
                    Color under = texture.GetPixel(x0 + x, y0 + y);
                    float a = top.a + under.a * (1 - top.a);
                    Color mixed = a > 0 ? (top * top.a + under * under.a * (1 - top.a)) / a : Color.clear;
                    mixed.a = a;
                    texture.SetPixel(x0 + x, y0 + y, mixed);
                }
            return texture;
        }

        static Texture2D Save(Texture2D texture, string file)
        {
            string path = Carpeta + file;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            // Sin compresión para que el ícono no se vea con manchas.
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Lee el PNG del disco (no hace falta que la textura sea legible en Unity).
        static Texture2D Load(string path)
        {
            if (!File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
