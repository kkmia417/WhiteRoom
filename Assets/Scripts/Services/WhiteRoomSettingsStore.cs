using System;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public interface IWhiteRoomSettingsStorage
    {
        bool TryLoad(out string json);
        void Save(string json);
    }

    public sealed class PlayerPrefsWhiteRoomSettingsStorage : IWhiteRoomSettingsStorage
    {
        private const string Key = "WhiteRoom.Settings.Json";

        public bool TryLoad(out string json)
        {
            json = PlayerPrefs.GetString(Key, string.Empty);
            return !string.IsNullOrEmpty(json);
        }

        public void Save(string json)
        {
            PlayerPrefs.SetString(Key, json ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public sealed class WhiteRoomSettingsDocument
    {
        public int SchemaVersion = VersionedDialogueSettingsStore.CurrentSchemaVersion;
        public float BgmVolume = 1f;
        public float SeVolume = 1f;
        public float VoiceVolume = 1f;
        public float TextSpeed = 0.5f;
        public float AutoAdvanceDelay = 1.5f;
        public bool SkipReadOnly = true;
    }

    /// <summary>Product-owned, schema-versioned persistence adapter for dialogue settings.</summary>
    public sealed class VersionedDialogueSettingsStore : IDialogueSettingsStore
    {
        public const int CurrentSchemaVersion = 1;

        private readonly IWhiteRoomSettingsStorage _storage;
        private readonly IDialogueSettingsStore _legacyStore;

        public VersionedDialogueSettingsStore(
            IWhiteRoomSettingsStorage storage = null,
            IDialogueSettingsStore legacyStore = null)
        {
            _storage = storage ?? new PlayerPrefsWhiteRoomSettingsStorage();
            _legacyStore = legacyStore ?? new PlayerPrefsDialogueSettingsStore();
        }

        public string LastWarning { get; private set; } = string.Empty;

        public void Load(DialogueSettings settings)
        {
            if (settings == null)
                return;

            string json;
            if (!_storage.TryLoad(out json))
            {
                _legacyStore.Load(settings);
                Save(settings);
                return;
            }

            WhiteRoomSettingsDocument document;
            try
            {
                document = JsonUtility.FromJson<WhiteRoomSettingsDocument>(json);
            }
            catch (Exception exception)
            {
                LastWarning = "Settings data is corrupt: " + exception.Message;
                return;
            }

            if (document == null || document.SchemaVersion != CurrentSchemaVersion)
            {
                LastWarning = "Unsupported settings schema version: " +
                              (document != null ? document.SchemaVersion.ToString() : "null") + ".";
                return;
            }

            settings.BgmVolume = document.BgmVolume;
            settings.SeVolume = document.SeVolume;
            settings.VoiceVolume = document.VoiceVolume;
            settings.TextSpeed = document.TextSpeed;
            settings.AutoAdvanceDelay = document.AutoAdvanceDelay;
            settings.SkipReadOnly = document.SkipReadOnly;
            LastWarning = string.Empty;
        }

        public void Save(DialogueSettings settings)
        {
            if (settings == null)
                return;
            var document = new WhiteRoomSettingsDocument
            {
                BgmVolume = settings.BgmVolume,
                SeVolume = settings.SeVolume,
                VoiceVolume = settings.VoiceVolume,
                TextSpeed = settings.TextSpeed,
                AutoAdvanceDelay = settings.AutoAdvanceDelay,
                SkipReadOnly = settings.SkipReadOnly
            };
            _storage.Save(JsonUtility.ToJson(document));
            LastWarning = string.Empty;
        }
    }
}
