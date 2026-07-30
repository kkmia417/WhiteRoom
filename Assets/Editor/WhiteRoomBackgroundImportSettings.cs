using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor
{
    /// <summary>
    /// Keeps production visual-novel backgrounds on one deterministic import profile.
    /// </summary>
    public sealed class WhiteRoomBackgroundImportSettings : AssetPostprocessor
    {
        public const string BackgroundFolder = "Assets/Presentation/Backgrounds/";
        public const int MaximumTextureSize = 2048;

        public static bool IsBackgroundAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith(BackgroundFolder, System.StringComparison.Ordinal);
        }

        private void OnPreprocessTexture()
        {
            if (!IsBackgroundAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.maxTextureSize = MaximumTextureSize;
        }
    }
}
