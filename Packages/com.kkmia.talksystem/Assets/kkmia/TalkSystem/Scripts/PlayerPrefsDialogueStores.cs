using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="DialogueSettings"/> を Unity の PlayerPrefs に保存する既定実装。
    /// </summary>
    public sealed class PlayerPrefsDialogueSettingsStore : IDialogueSettingsStore
    {
        private const string Prefix = "kkmia.TalkSystem.Settings.";

        public void Load(DialogueSettings settings)
        {
            if (settings == null) return;

            settings.MasterVolume = PlayerPrefs.GetFloat(Prefix + "Master", settings.MasterVolume);
            settings.BgmVolume = PlayerPrefs.GetFloat(Prefix + "Bgm", settings.BgmVolume);
            settings.SeVolume = PlayerPrefs.GetFloat(Prefix + "Se", settings.SeVolume);
            settings.VoiceVolume = PlayerPrefs.GetFloat(Prefix + "Voice", settings.VoiceVolume);
            settings.TextSpeed = PlayerPrefs.GetFloat(Prefix + "TextSpeed", settings.TextSpeed);
            settings.AutoAdvanceDelay = PlayerPrefs.GetFloat(Prefix + "AutoDelay", settings.AutoAdvanceDelay);
            settings.SkipReadOnly = PlayerPrefs.GetInt(Prefix + "SkipReadOnly", settings.SkipReadOnly ? 1 : 0) != 0;
        }

        public void Save(DialogueSettings settings)
        {
            if (settings == null) return;

            PlayerPrefs.SetFloat(Prefix + "Master", settings.MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "Bgm", settings.BgmVolume);
            PlayerPrefs.SetFloat(Prefix + "Se", settings.SeVolume);
            PlayerPrefs.SetFloat(Prefix + "Voice", settings.VoiceVolume);
            PlayerPrefs.SetFloat(Prefix + "TextSpeed", settings.TextSpeed);
            PlayerPrefs.SetFloat(Prefix + "AutoDelay", settings.AutoAdvanceDelay);
            PlayerPrefs.SetInt(Prefix + "SkipReadOnly", settings.SkipReadOnly ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 既読 ID を PlayerPrefs に保存する既定実装。CSV 文字列として 1 キーにまとめる。
    /// </summary>
    public sealed class PlayerPrefsDialogueReadStore : IDialogueReadStore
    {
        private const string Key = "kkmia.TalkSystem.ReadIds";

        public IEnumerable<int> Load()
        {
            var raw = PlayerPrefs.GetString(Key, string.Empty);
            var result = new List<int>();
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var token in raw.Split(','))
            {
                int id;
                if (int.TryParse(token, out id))
                    result.Add(id);
            }

            return result;
        }

        public void Save(IEnumerable<int> readIds)
        {
            if (readIds == null)
            {
                PlayerPrefs.SetString(Key, string.Empty);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(Key, string.Join(",", readIds));
            PlayerPrefs.Save();
        }
    }
}
