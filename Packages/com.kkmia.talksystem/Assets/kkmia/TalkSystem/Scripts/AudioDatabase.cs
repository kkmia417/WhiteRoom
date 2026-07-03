using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class AudioEntry
    {
        public string audioKey;
        public AudioClip clip;
    }

    /// <summary>
    /// 音声キー -> AudioClip の対応表。BGM / SE / ボイスを分けて保持する。
    /// カテゴリ分けは Phase 4 の音量バス（BGM/SE/Voice 個別音量）と整合させるため。
    /// </summary>
    [CreateAssetMenu(menuName = "kkmia/Talk System/Audio Database")]
    public sealed class AudioDatabase : ScriptableObject
    {
        [SerializeField] private List<AudioEntry> bgm = new List<AudioEntry>();
        [SerializeField] private List<AudioEntry> se = new List<AudioEntry>();
        [SerializeField] private List<AudioEntry> voice = new List<AudioEntry>();

        public bool TryGetBgm(string key, out AudioClip clip)
        {
            return TryGet(bgm, key, out clip);
        }

        public bool TryGetSe(string key, out AudioClip clip)
        {
            return TryGet(se, key, out clip);
        }

        public bool TryGetVoice(string key, out AudioClip clip)
        {
            return TryGet(voice, key, out clip);
        }

        private static bool TryGet(List<AudioEntry> entries, string key, out AudioClip clip)
        {
            if (!string.IsNullOrEmpty(key))
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry != null && entry.audioKey == key)
                    {
                        clip = entry.clip;
                        return clip != null;
                    }
                }
            }

            clip = null;
            return false;
        }
    }
}
