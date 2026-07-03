using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// セーブ/ロードのオーケストレーション（純ロジック）。スロットのメタ付与・contributor 実行・
    /// ストレージ呼び出しを担う。会話本体の capture/restore（DialogueManager）とは分離しているためテスト可能。
    /// </summary>
    public sealed class DialogueSaveService
    {
        private readonly IDialogueSaveStorage _storage;
        private readonly List<IDialogueSaveContributor> _contributors = new List<IDialogueSaveContributor>();
        private readonly List<IDialogueSaveDataMigration> _dataMigrations = new List<IDialogueSaveDataMigration>();
        private readonly List<IDialogueSaveSlotMigration> _slotMigrations = new List<IDialogueSaveSlotMigration>();
        private readonly DialogueSaveServiceOptions _options;

        public DialogueSaveService(
            IDialogueSaveStorage storage,
            IEnumerable<IDialogueSaveContributor> contributors = null,
            DialogueSaveServiceOptions options = null)
        {
            _storage = storage;
            _options = options != null ? options.Clone() : new DialogueSaveServiceOptions();
            if (contributors != null)
            {
                foreach (var contributor in contributors)
                {
                    if (contributor != null)
                        _contributors.Add(contributor);
                }
            }
        }

        public event Action<DialogueSaveOperationResult> OperationCompleted;
        public event Action<DialogueSaveOperationResult> OperationFailed;

        public DialogueSaveServiceOptions Options
        {
            get { return _options; }
        }

        public DialogueSaveOperationResult LastResult { get; private set; }

        public void AddContributor(IDialogueSaveContributor contributor)
        {
            if (contributor != null && !_contributors.Contains(contributor))
                _contributors.Add(contributor);
        }

        public void RemoveContributor(IDialogueSaveContributor contributor)
        {
            _contributors.Remove(contributor);
        }

        public void AddDataMigration(IDialogueSaveDataMigration migration)
        {
            if (migration != null && !_dataMigrations.Contains(migration))
                _dataMigrations.Add(migration);
        }

        public void AddSlotMigration(IDialogueSaveSlotMigration migration)
        {
            if (migration != null && !_slotMigrations.Contains(migration))
                _slotMigrations.Add(migration);
        }

        /// <summary>
        /// すでに capture 済みの本体データを、contributor で補強してスロットへ保存する。
        /// </summary>
        public DialogueSaveSlot Save(int slot, DialogueSaveData data, string title, bool isAutosave, long timestampUnix)
        {
            if (!EnsureStorage(DialogueSaveOperation.Save, slot))
                return null;

            if (data == null) data = new DialogueSaveData();

            try
            {
                ApplyCurrentMetadata(data);

                for (var i = 0; i < _contributors.Count; i++)
                    _contributors[i].Capture(data);

                ApplyCurrentMetadata(data);

                var saveSlot = new DialogueSaveSlot
                {
                    SchemaVersion = CurrentSchemaVersion,
                    ContentVersion = CurrentContentVersion,
                    ProductChannel = CurrentProductChannel,
                    SlotIndex = slot,
                    Title = title ?? string.Empty,
                    SavedAtUnix = timestampUnix,
                    IsAutosave = isAutosave,
                    Data = data
                };

                _storage.Save(saveSlot);
                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.Save, slot));
                return saveSlot;
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Save,
                    slot,
                    "Failed to save dialogue slot " + slot + ": " + e.Message,
                    e));
                return null;
            }
        }

        /// <summary>スロットを読み込む（contributor の復元は <see cref="ApplyRestore"/> で別途行う）。</summary>
        public DialogueSaveSlot QuickSave(DialogueSaveData data, string title, long timestampUnix)
        {
            return Save(DialogueSaveSlotConventions.QuickSaveSlot, data, title, false, timestampUnix);
        }

        public DialogueSaveSlot Load(int slot)
        {
            if (!EnsureStorage(DialogueSaveOperation.Load, slot))
                return null;

            try
            {
                DialogueSaveSlot data;
                if (!_storage.TryLoad(slot, out data))
                {
                    Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.Load, slot, "Slot was not found."));
                    return null;
                }

                string error;
                if (!PrepareLoadedSlot(data, out error))
                {
                    Report(DialogueSaveOperationResult.Failure(DialogueSaveOperation.Load, slot, error));
                    return null;
                }

                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.Load, slot));
                return data;
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Load,
                    slot,
                    "Failed to load dialogue slot " + slot + ": " + e.Message,
                    e));
                return null;
            }
        }

        /// <summary>本体復元後に呼び、contributor へサブシステム状態を反映させる。</summary>
        public DialogueSaveSlot QuickLoad()
        {
            return Load(DialogueSaveSlotConventions.QuickSaveSlot);
        }

        public void ApplyRestore(DialogueSaveData data)
        {
            if (data == null) return;

            try
            {
                for (var i = 0; i < _contributors.Count; i++)
                    _contributors[i].Restore(data);
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Load,
                    data.CurrentDialogueId,
                    "Failed to restore dialogue save contributors: " + e.Message,
                    e));
            }
        }

        public bool Exists(int slot)
        {
            if (!EnsureStorage(DialogueSaveOperation.Exists, slot))
                return false;

            try
            {
                var exists = _storage.Exists(slot);
                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.Exists, slot));
                return exists;
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Exists,
                    slot,
                    "Failed to check dialogue slot " + slot + ": " + e.Message,
                    e));
                return false;
            }
        }

        public void Delete(int slot)
        {
            if (!EnsureStorage(DialogueSaveOperation.Delete, slot))
                return;

            try
            {
                _storage.Delete(slot);
                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.Delete, slot));
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Delete,
                    slot,
                    "Failed to delete dialogue slot " + slot + ": " + e.Message,
                    e));
            }
        }

        public List<int> ListSlots()
        {
            var result = new List<int>();

            if (!EnsureStorage(DialogueSaveOperation.ListSlots, -1))
                return result;

            try
            {
                foreach (var slot in _storage.ListSlots())
                    result.Add(slot);
                result.Sort();
                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.ListSlots, -1));
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.ListSlots,
                    -1,
                    "Failed to list dialogue save slots: " + e.Message,
                    e));
            }

            return result;
        }

        public byte[] LoadThumbnail(int slot)
        {
            if (!EnsureStorage(DialogueSaveOperation.LoadThumbnail, slot))
                return null;

            try
            {
                var bytes = _storage.LoadThumbnail(slot);
                Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.LoadThumbnail, slot));
                return bytes;
            }
            catch (Exception e)
            {
                Report(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.LoadThumbnail,
                    slot,
                    "Failed to load dialogue thumbnail " + slot + ": " + e.Message,
                    e));
                return null;
            }
        }

        public void SaveThumbnail(int slot, byte[] pngBytes)
        {
            if (pngBytes != null && pngBytes.Length > 0)
            {
                if (!EnsureStorage(DialogueSaveOperation.SaveThumbnail, slot))
                    return;

                try
                {
                    _storage.SaveThumbnail(slot, pngBytes);
                    Report(DialogueSaveOperationResult.Success(DialogueSaveOperation.SaveThumbnail, slot));
                }
                catch (Exception e)
                {
                    Report(DialogueSaveOperationResult.Failure(
                        DialogueSaveOperation.SaveThumbnail,
                        slot,
                        "Failed to save dialogue thumbnail " + slot + ": " + e.Message,
                        e));
                }
            }
        }

        public DialogueSaveSlotViewModel GetSlotViewModel(int slot, bool includeThumbnail = true)
        {
            var saveSlot = Load(slot);
            if (saveSlot == null)
            {
                var error = LastResult != null && LastResult.Failed ? LastResult.Message : string.Empty;
                return DialogueSaveSlotViewModel.Empty(slot, error);
            }

            var thumbnail = includeThumbnail ? LoadThumbnail(slot) : null;
            return DialogueSaveSlotViewModel.FromSlot(saveSlot, thumbnail);
        }

        public List<DialogueSaveSlotViewModel> GetSlotViewModels(IEnumerable<int> slots, bool includeThumbnail = true)
        {
            var result = new List<DialogueSaveSlotViewModel>();
            if (slots == null)
                return result;

            foreach (var slot in slots)
                result.Add(GetSlotViewModel(slot, includeThumbnail));

            return result;
        }

        public List<DialogueSaveSlotViewModel> ListSlotViewModels(bool includeThumbnail = true)
        {
            return GetSlotViewModels(ListSlots(), includeThumbnail);
        }

        public DialogueSaveSlotViewModel GetLatestContinueCandidate(
            bool includeAutosaves = true,
            bool includeQuickSaves = true,
            bool includeThumbnail = true)
        {
            DialogueSaveSlotViewModel latest = null;
            var slots = ListSlotViewModels(includeThumbnail);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.CanLoad || slot.IsEmpty)
                    continue;
                if (!includeAutosaves && slot.Category == DialogueSaveSlotCategory.Autosave)
                    continue;
                if (!includeQuickSaves && slot.Category == DialogueSaveSlotCategory.QuickSave)
                    continue;

                if (latest == null ||
                    slot.SavedAtUnix > latest.SavedAtUnix ||
                    (slot.SavedAtUnix == latest.SavedAtUnix && slot.SlotIndex > latest.SlotIndex))
                    latest = slot;
            }

            return latest;
        }

        private bool PrepareLoadedSlot(DialogueSaveSlot slot, out string error)
        {
            error = null;
            if (slot == null)
            {
                error = "Save slot payload was empty.";
                return false;
            }

            if (slot.Data == null)
                slot.Data = new DialogueSaveData();

            if (!MigrateSlot(slot, out error))
                return false;

            if (!MigrateData(slot.Data, out error))
                return false;

            FillMissingMetadata(slot);
            FillMissingMetadata(slot.Data);

            if (!ResolveMissingDialogue(slot.Data, out error))
                return false;

            return true;
        }

        private bool MigrateSlot(DialogueSaveSlot slot, out string error)
        {
            error = null;
            var version = NormalizeLoadedSchemaVersion(slot.SchemaVersion);
            if (version > CurrentSchemaVersion)
            {
                error = "Save slot schema " + version + " is newer than supported schema " + CurrentSchemaVersion + ".";
                return false;
            }

            while (version < CurrentSchemaVersion)
            {
                var migration = FindSlotMigration(version);
                if (migration != null)
                {
                    if (migration.ToSchemaVersion <= version)
                    {
                        error = "Slot migration from schema " + version + " does not advance the schema version.";
                        return false;
                    }

                    migration.Migrate(slot, new DialogueSaveMigrationContext(_options, version, migration.ToSchemaVersion));
                    version = migration.ToSchemaVersion;
                    slot.SchemaVersion = version;
                    continue;
                }

                if (version == 0 && CurrentSchemaVersion >= 1)
                {
                    version = 1;
                    slot.SchemaVersion = version;
                    continue;
                }

                if (version == 1 && CurrentSchemaVersion >= 2)
                {
                    version = 2;
                    slot.SchemaVersion = version;
                    continue;
                }

                error = "No slot migration is registered from schema " + version + " to " + CurrentSchemaVersion + ".";
                return false;
            }

            slot.SchemaVersion = CurrentSchemaVersion;
            return true;
        }

        private bool MigrateData(DialogueSaveData data, out string error)
        {
            error = null;
            var version = NormalizeLoadedSchemaVersion(data.SchemaVersion);
            if (version > CurrentSchemaVersion)
            {
                error = "Save data schema " + version + " is newer than supported schema " + CurrentSchemaVersion + ".";
                return false;
            }

            while (version < CurrentSchemaVersion)
            {
                var migration = FindDataMigration(version);
                if (migration != null)
                {
                    if (migration.ToSchemaVersion <= version)
                    {
                        error = "Data migration from schema " + version + " does not advance the schema version.";
                        return false;
                    }

                    migration.Migrate(data, new DialogueSaveMigrationContext(_options, version, migration.ToSchemaVersion));
                    version = migration.ToSchemaVersion;
                    data.SchemaVersion = version;
                    continue;
                }

                if (version == 0 && CurrentSchemaVersion >= 1)
                {
                    version = 1;
                    data.SchemaVersion = version;
                    continue;
                }

                if (version == 1 && CurrentSchemaVersion >= 2)
                {
                    MigrateLegacyChoiceHistory(data);
                    version = 2;
                    data.SchemaVersion = version;
                    continue;
                }

                error = "No data migration is registered from schema " + version + " to " + CurrentSchemaVersion + ".";
                return false;
            }

            data.SchemaVersion = CurrentSchemaVersion;
            return true;
        }

        private bool ResolveMissingDialogue(DialogueSaveData data, out string error)
        {
            error = null;
            if (data == null || data.CurrentDialogueId < 0 || _options.Repository == null)
                return true;

            if (_options.Repository.Get(data.CurrentDialogueId) != null)
                return true;

            if (_options.MissingDialoguePolicy == DialogueMissingDialoguePolicy.RestoreEnded)
            {
                data.CurrentDialogueId = -1;
                data.State = DialogueSessionState.Ended;
                return true;
            }

            if (_options.MissingDialoguePolicy == DialogueMissingDialoguePolicy.UseFallbackDialogueId)
            {
                if (_options.FallbackDialogueId >= 0 && _options.Repository.Get(_options.FallbackDialogueId) != null)
                {
                    data.CurrentDialogueId = _options.FallbackDialogueId;
                    data.State = DialogueSessionState.ShowingLine;
                    return true;
                }

                error = "Saved dialogue ID " + data.CurrentDialogueId + " is missing and fallback ID " + _options.FallbackDialogueId + " was not found.";
                return false;
            }

            error = "Saved dialogue ID " + data.CurrentDialogueId + " was not found in the active repository.";
            return false;
        }

        private IDialogueSaveDataMigration FindDataMigration(int fromSchemaVersion)
        {
            for (var i = 0; i < _dataMigrations.Count; i++)
            {
                if (_dataMigrations[i].FromSchemaVersion == fromSchemaVersion)
                    return _dataMigrations[i];
            }

            return null;
        }

        private IDialogueSaveSlotMigration FindSlotMigration(int fromSchemaVersion)
        {
            for (var i = 0; i < _slotMigrations.Count; i++)
            {
                if (_slotMigrations[i].FromSchemaVersion == fromSchemaVersion)
                    return _slotMigrations[i];
            }

            return null;
        }

        private bool EnsureStorage(DialogueSaveOperation operation, int slot)
        {
            if (_storage != null)
                return true;

            Report(DialogueSaveOperationResult.Failure(operation, slot, "Dialogue save storage is not configured."));
            return false;
        }

        private void ApplyCurrentMetadata(DialogueSaveSlot slot)
        {
            if (slot == null) return;
            slot.SchemaVersion = CurrentSchemaVersion;
            slot.ContentVersion = CurrentContentVersion;
            slot.ProductChannel = CurrentProductChannel;

            if (slot.Data != null)
                ApplyCurrentMetadata(slot.Data);
        }

        private void ApplyCurrentMetadata(DialogueSaveData data)
        {
            if (data == null) return;
            data.SchemaVersion = CurrentSchemaVersion;
            data.ContentVersion = CurrentContentVersion;
            data.ProductChannel = CurrentProductChannel;
        }

        private void FillMissingMetadata(DialogueSaveSlot slot)
        {
            if (slot == null) return;
            if (slot.SchemaVersion <= 0)
                slot.SchemaVersion = CurrentSchemaVersion;
            if (slot.ContentVersion == null)
                slot.ContentVersion = string.Empty;
            if (slot.ProductChannel == null)
                slot.ProductChannel = string.Empty;
        }

        private void FillMissingMetadata(DialogueSaveData data)
        {
            if (data == null) return;
            if (data.SchemaVersion <= 0)
                data.SchemaVersion = CurrentSchemaVersion;
            if (data.ContentVersion == null)
                data.ContentVersion = string.Empty;
            if (data.ProductChannel == null)
                data.ProductChannel = string.Empty;
            if (data.SeenLineIds == null)
                data.SeenLineIds = new List<int>();
            if (data.ChoiceRecords == null)
                data.ChoiceRecords = new List<DialogueChoiceRecord>();
            if (data.ChoiceHistory == null)
                data.ChoiceHistory = new List<int>();
            if (data.History == null)
                data.History = new List<DialogueHistoryEntry>();
            if (data.Progress == null)
                data.Progress = new DialogueProgressState();
            if (data.ExtraState == null)
                data.ExtraState = new List<DialogueSaveValue>();
        }

        private static void MigrateLegacyChoiceHistory(DialogueSaveData data)
        {
            if (data == null)
                return;

            if (data.ChoiceRecords == null)
                data.ChoiceRecords = new List<DialogueChoiceRecord>();

            if (data.ChoiceRecords.Count > 0 || data.ChoiceHistory == null)
                return;

            for (var i = 0; i < data.ChoiceHistory.Count; i++)
            {
                data.ChoiceRecords.Add(new DialogueChoiceRecord(
                    -1,
                    data.ChoiceHistory[i],
                    -1,
                    string.Empty,
                    string.Empty));
            }
        }

        private void Report(DialogueSaveOperationResult result)
        {
            LastResult = result;
            if (OperationCompleted != null)
                OperationCompleted(result);
            if (result != null && result.Failed && OperationFailed != null)
                OperationFailed(result);
        }

        private int CurrentSchemaVersion
        {
            get { return _options.CurrentSchemaVersion > 0 ? _options.CurrentSchemaVersion : DialogueSaveSchema.CurrentVersion; }
        }

        private string CurrentContentVersion
        {
            get { return _options.ContentVersion ?? string.Empty; }
        }

        private string CurrentProductChannel
        {
            get { return _options.ProductChannel ?? string.Empty; }
        }

        private static int NormalizeLoadedSchemaVersion(int version)
        {
            return version > 0 ? version : 0;
        }
    }
}
