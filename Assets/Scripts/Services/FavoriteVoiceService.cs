using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public interface IFavoriteVoiceStorage
    {
        bool TryLoad(out string json);
        void Save(string json);
    }

    public sealed class PlayerPrefsFavoriteVoiceStorage : IFavoriteVoiceStorage
    {
        private const string Key = "WhiteRoom.FavoriteVoices.Json";

        public bool TryLoad(out string json)
        {
            json = PlayerPrefs.GetString(Key, string.Empty);
            return !string.IsNullOrWhiteSpace(json);
        }

        public void Save(string json)
        {
            PlayerPrefs.SetString(Key, json ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public sealed class FavoriteVoiceRecord
    {
        public int DialogueId = -1;
        public string VoiceKey = string.Empty;
        public int Order;

        public string StableId => DialogueId + ":" + (VoiceKey ?? string.Empty);
    }

    [Serializable]
    public sealed class FavoriteVoiceDocument
    {
        public int SchemaVersion = FavoriteVoiceService.CurrentSchemaVersion;
        public List<FavoriteVoiceRecord> Entries = new List<FavoriteVoiceRecord>();
    }

    public sealed class FavoriteVoiceViewModel
    {
        public FavoriteVoiceViewModel(
            FavoriteVoiceRecord record,
            string speaker,
            string text,
            bool isVoiceAvailable)
        {
            Record = record;
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            IsVoiceAvailable = isVoiceAvailable;
        }

        public FavoriteVoiceRecord Record { get; }
        public string Speaker { get; }
        public string Text { get; }
        public bool IsVoiceAvailable { get; }
    }

    public enum FavoriteVoiceStatus
    {
        Success,
        NoCurrentVoice,
        VoiceUnavailable,
        AlreadyRegistered,
        NotFound,
        PersistenceFailed
    }

    public sealed class FavoriteVoiceResult
    {
        public FavoriteVoiceResult(FavoriteVoiceStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public FavoriteVoiceStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == FavoriteVoiceStatus.Success;
    }

    /// <summary>
    /// Product-owned current-voice replay and versioned favorite policy. Durable
    /// identity is the dialogue ID plus voice key; display text is resolved from
    /// the current localized repository each time the list is built.
    /// </summary>
    public sealed class FavoriteVoiceService
    {
        public const int CurrentSchemaVersion = 1;

        private readonly Func<DialogueData> _getCurrentLine;
        private readonly Func<int, DialogueData> _getDialogue;
        private readonly Func<string, bool> _canResolveVoice;
        private readonly IDialogueAudioPlayer _audioPlayer;
        private readonly IFavoriteVoiceStorage _storage;
        private readonly Action<string> _warning;
        private readonly List<FavoriteVoiceRecord> _entries = new List<FavoriteVoiceRecord>();
        private int _nextOrder;

        public FavoriteVoiceService(
            Func<DialogueData> getCurrentLine,
            Func<int, DialogueData> getDialogue,
            Func<string, bool> canResolveVoice,
            IDialogueAudioPlayer audioPlayer,
            IFavoriteVoiceStorage storage = null,
            Action<string> warning = null)
        {
            _getCurrentLine = getCurrentLine ?? (() => null);
            _getDialogue = getDialogue ?? (_ => null);
            _canResolveVoice = canResolveVoice ?? (_ => false);
            _audioPlayer = audioPlayer;
            _storage = storage ?? new PlayerPrefsFavoriteVoiceStorage();
            _warning = warning;
            Load();
        }

        public int Count => _entries.Count;
        public bool HasFavorites => _entries.Count > 0;
        public string LastWarning { get; private set; } = string.Empty;

        public bool CanUseCurrentVoice
        {
            get
            {
                var line = _getCurrentLine();
                return line != null &&
                       line.HasVoice &&
                       _audioPlayer != null &&
                       _canResolveVoice(Normalize(line.Voice));
            }
        }

        public FavoriteVoiceResult ReplayCurrent()
        {
            var line = _getCurrentLine();
            if (line == null || !line.HasVoice)
                return Result(FavoriteVoiceStatus.NoCurrentVoice, "The current line has no voice.");

            var key = Normalize(line.Voice);
            if (_audioPlayer == null || !_canResolveVoice(key))
                return Result(FavoriteVoiceStatus.VoiceUnavailable, "The current voice is unavailable.");

            _audioPlayer.StopVoice();
            _audioPlayer.PlayVoice(key);
            return Result(FavoriteVoiceStatus.Success, "Voice replayed.");
        }

        public FavoriteVoiceResult AddCurrent()
        {
            var line = _getCurrentLine();
            if (line == null || !line.HasVoice)
                return Result(FavoriteVoiceStatus.NoCurrentVoice, "The current line has no voice.");

            var key = Normalize(line.Voice);
            if (_audioPlayer == null || !_canResolveVoice(key))
                return Result(FavoriteVoiceStatus.VoiceUnavailable, "The current voice is unavailable.");

            var stableId = StableId(line.Id, key);
            if (_entries.Exists(entry => entry.StableId == stableId))
                return Result(FavoriteVoiceStatus.AlreadyRegistered, "This voice is already a favorite.");

            var record = new FavoriteVoiceRecord
            {
                DialogueId = line.Id,
                VoiceKey = key,
                Order = _nextOrder++
            };
            _entries.Add(record);
            var result = Save("Voice added to favorites.");
            if (!result.Succeeded)
            {
                _entries.Remove(record);
                _nextOrder--;
            }
            return result;
        }

        public List<FavoriteVoiceViewModel> BuildList()
        {
            var result = new List<FavoriteVoiceViewModel>();
            for (var index = 0; index < _entries.Count; index++)
            {
                var record = _entries[index];
                var line = _getDialogue(record.DialogueId);
                if (line == null || Normalize(line.Voice) != Normalize(record.VoiceKey))
                    continue;

                result.Add(new FavoriteVoiceViewModel(
                    record,
                    line.Speaker,
                    line.Text,
                    _audioPlayer != null && _canResolveVoice(record.VoiceKey)));
            }
            return result;
        }

        public FavoriteVoiceResult Play(FavoriteVoiceRecord record)
        {
            var existing = Find(record);
            if (existing == null)
                return Result(FavoriteVoiceStatus.NotFound, "The favorite voice was not found.");
            if (_audioPlayer == null || !_canResolveVoice(existing.VoiceKey))
                return Result(FavoriteVoiceStatus.VoiceUnavailable, "The favorite voice is unavailable.");

            _audioPlayer.StopVoice();
            _audioPlayer.PlayVoice(existing.VoiceKey);
            return Result(FavoriteVoiceStatus.Success, "Favorite voice replayed.");
        }

        public FavoriteVoiceResult Remove(FavoriteVoiceRecord record)
        {
            var existing = Find(record);
            if (existing == null)
                return Result(FavoriteVoiceStatus.NotFound, "The favorite voice was not found.");

            var index = _entries.IndexOf(existing);
            _entries.RemoveAt(index);
            var result = Save("Favorite voice removed.");
            if (!result.Succeeded)
                _entries.Insert(index, existing);
            return result;
        }

        public void Stop()
        {
            _audioPlayer?.StopVoice();
        }

        private void Load()
        {
            _entries.Clear();
            _nextOrder = 0;
            string json;
            try
            {
                if (!_storage.TryLoad(out json) || string.IsNullOrWhiteSpace(json))
                    return;
            }
            catch (Exception exception)
            {
                Warn("Favorite voice persistence could not be read: " + exception.Message);
                return;
            }

            FavoriteVoiceDocument document;
            try
            {
                document = JsonUtility.FromJson<FavoriteVoiceDocument>(json);
            }
            catch (Exception exception)
            {
                Warn("Favorite voice data is corrupt: " + exception.Message);
                return;
            }

            if (document == null || (document.SchemaVersion != 0 && document.SchemaVersion != CurrentSchemaVersion))
            {
                Warn("Unsupported favorite voice schema version: " +
                     (document != null ? document.SchemaVersion.ToString() : "null") + ".");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (document.Entries != null)
            {
                for (var index = 0; index < document.Entries.Count; index++)
                {
                    var source = document.Entries[index];
                    if (source == null || source.DialogueId < 0 || string.IsNullOrWhiteSpace(source.VoiceKey))
                        continue;

                    var key = Normalize(source.VoiceKey);
                    var line = _getDialogue(source.DialogueId);
                    if (line == null || Normalize(line.Voice) != key)
                    {
                        Warn("Ignored unknown favorite voice '" + StableId(source.DialogueId, key) + "'.");
                        continue;
                    }

                    var id = StableId(source.DialogueId, key);
                    if (!seen.Add(id))
                        continue;

                    var order = document.SchemaVersion == 0 ? index : Math.Max(0, source.Order);
                    _entries.Add(new FavoriteVoiceRecord
                    {
                        DialogueId = source.DialogueId,
                        VoiceKey = key,
                        Order = order
                    });
                    _nextOrder = Math.Max(_nextOrder, order + 1);
                }
            }

            _entries.Sort((left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                if (order != 0) return order;
                var dialogue = left.DialogueId.CompareTo(right.DialogueId);
                return dialogue != 0 ? dialogue : string.CompareOrdinal(left.VoiceKey, right.VoiceKey);
            });

            if (document.SchemaVersion == 0)
                Save("Favorite voice data migrated.");
        }

        private FavoriteVoiceResult Save(string successMessage)
        {
            try
            {
                _storage.Save(JsonUtility.ToJson(new FavoriteVoiceDocument
                {
                    Entries = new List<FavoriteVoiceRecord>(_entries)
                }));
                LastWarning = string.Empty;
                return Result(FavoriteVoiceStatus.Success, successMessage);
            }
            catch (Exception exception)
            {
                Warn("Favorite voice persistence failed: " + exception.Message);
                return Result(FavoriteVoiceStatus.PersistenceFailed, "Favorite voice data could not be saved.");
            }
        }

        private FavoriteVoiceRecord Find(FavoriteVoiceRecord record)
        {
            if (record == null) return null;
            var id = StableId(record.DialogueId, Normalize(record.VoiceKey));
            return _entries.Find(entry => entry.StableId == id);
        }

        private void Warn(string message)
        {
            LastWarning = message ?? string.Empty;
            _warning?.Invoke(LastWarning);
        }

        private static FavoriteVoiceResult Result(FavoriteVoiceStatus status, string message)
        {
            return new FavoriteVoiceResult(status, message);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string StableId(int dialogueId, string voiceKey)
        {
            return dialogueId + ":" + (voiceKey ?? string.Empty);
        }
    }
}
