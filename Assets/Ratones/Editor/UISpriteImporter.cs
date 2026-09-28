using UnityEditor;

namespace Ratones.Basic.Editor
{
    // Todo lo que se agregue a Assets/Ratones/UI entra como Sprite listo para Image.
    public sealed class UISpriteImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Ratones/UI/";
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            Configure(importer);
        }
        public static bool Configure(TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // Las barras de los sliders se estiran por el centro: los extremos redondos no se deforman.
            float radius = importer.assetPath.EndsWith("PILDORA_SLIDER.png") ? 32 : importer.assetPath.EndsWith("FONDO_SLIDER.png") ? 22 : 0;
            if (radius > 0)
            {
                var border = new UnityEngine.Vector4(radius, 0, radius, 0);
                changed |= importer.spriteBorder != border;
                importer.spriteBorder = border;
            }
            return changed;
        }
    }
}
