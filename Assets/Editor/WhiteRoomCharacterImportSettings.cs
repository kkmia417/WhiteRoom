using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor
{
    /// <summary>
    /// Keeps production character sprites on one deterministic import profile.
    /// </summary>
    public sealed class WhiteRoomCharacterImportSettings : AssetPostprocessor
    {
        public const string CharacterFolder = "Assets/Presentation/Characters/";
        public const int MaximumTextureSize = 2048;
        public const float PixelsPerUnit = 100f;

        public static bool IsCharacterAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith(CharacterFolder, System.StringComparison.Ordinal);
        }

        private void OnPreprocessTexture()
        {
            if (!IsCharacterAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(textureSettings);
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
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
