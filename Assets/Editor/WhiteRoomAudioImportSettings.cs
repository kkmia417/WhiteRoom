using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor
{
    /// <summary>
    /// Keeps production BGM and sound effects on deterministic, category-specific import profiles.
    /// </summary>
    public sealed class WhiteRoomAudioImportSettings : AssetPostprocessor
    {
        public const string AudioFolder = "Assets/Presentation/Audio/";
        public const string BgmFolder = AudioFolder + "Bgm/";
        public const string SeFolder = AudioFolder + "Se/";

        public static bool IsBgmAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith(BgmFolder, System.StringComparison.Ordinal);
        }

        public static bool IsSeAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith(SeFolder, System.StringComparison.Ordinal);
        }

        private void OnPreprocessAudio()
        {
            var isBgm = IsBgmAsset(assetPath);
            if (!isBgm && !IsSeAsset(assetPath))
                return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.ambisonic = false;
            importer.loadInBackground = isBgm;

            var settings = importer.defaultSampleSettings;
            settings.loadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = isBgm ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = !isBgm;
            settings.quality = 0.65f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
