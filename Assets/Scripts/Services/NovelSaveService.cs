using System;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public enum NovelSaveFeedbackKind
    {
        Save,
        DirectSave,
        Load,
        QuickSave,
        QuickLoad,
        Autosave
    }

    public sealed class NovelSaveFeedback
    {
        public NovelSaveFeedback(NovelSaveFeedbackKind kind, int slot, bool succeeded, string message)
        {
            Kind = kind;
            Slot = slot;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public NovelSaveFeedbackKind Kind { get; }
        public int Slot { get; }
        public bool Succeeded { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Orchestrates every save/load operation (manual slots, quick save, continue),
    /// builds slot titles from the current line, and raises <see cref="Saved"/> /
    /// <see cref="Loaded"/> so the UI can react without knowing about the save system.
    /// </summary>
    public sealed class NovelSaveService : IDisposable
    {
        private readonly DialogueManager _manager;
        private readonly DialogueSaveSystem _saveSystem;
        private readonly int _defaultManualSlot;
        private readonly bool _saveThumbnails;
        private readonly NovelDirectSaveTarget _directSaveTarget;
        private bool _operationInProgress;

        public NovelSaveService(
            DialogueManager manager,
            DialogueSaveSystem saveSystem,
            int defaultManualSlot,
            bool saveThumbnails,
            INovelSavePreferenceStore preferenceStore = null)
        {
            _manager = manager;
            _saveSystem = saveSystem;
            _defaultManualSlot = defaultManualSlot;
            _saveThumbnails = saveThumbnails;
            _directSaveTarget = new NovelDirectSaveTarget(
                preferenceStore ?? new PlayerPrefsNovelSavePreferenceStore());

            _saveSystem.OperationFailed += HandleOperationFailed;
        }

        public event Action Saved;
        public event Action Loaded;
        public event Action<NovelSaveFeedback> Feedback;

        public bool CanSaveNow => _manager != null && _manager.CurrentData != null;
        public bool IsBusy => _operationInProgress;
        public bool HasDirectSaveTarget => _directSaveTarget.HasValue;
        public int DirectSaveSlot => _directSaveTarget.Slot;

        public bool Save()
        {
            return Save(_defaultManualSlot);
        }

        public bool Save(int slot)
        {
            return SaveInternal(slot, NovelSaveFeedbackKind.Save);
        }

        public bool DirectSave()
        {
            if (!_directSaveTarget.HasValue)
            {
                Publish(NovelSaveFeedbackKind.DirectSave, -1, false, "Select a manual save slot first.");
                return false;
            }

            return SaveInternal(_directSaveTarget.Slot, NovelSaveFeedbackKind.DirectSave);
        }

        public bool Load()
        {
            return Load(_defaultManualSlot);
        }

        public bool Load(int slot)
        {
            if (!TryBegin(NovelSaveFeedbackKind.Load, slot))
                return false;

            try
            {
                var loaded = _saveSystem.Load(slot);
                if (loaded)
                {
                    RememberManualSlot(slot);
                    Loaded?.Invoke();
                }

                Publish(
                    NovelSaveFeedbackKind.Load,
                    slot,
                    loaded,
                    loaded ? $"Loaded Slot {slot}." : FailureMessage("No loadable save data."));
                return loaded;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public bool QuickSave()
        {
            if (!TryBegin(NovelSaveFeedbackKind.QuickSave, DialogueSaveSystem.QuickSaveSlot))
                return false;

            try
            {
                bool saved;
                if (_saveThumbnails)
                {
                    _saveSystem.QuickSaveWithThumbnail(BuildSaveTitle(DialogueSaveSystem.QuickSaveSlot));
                    saved = _saveSystem.LastOperationResult == null || _saveSystem.LastOperationResult.Succeeded;
                }
                else
                {
                    saved = _saveSystem.QuickSave(BuildSaveTitle(DialogueSaveSystem.QuickSaveSlot)) != null;
                }

                if (saved)
                    Saved?.Invoke();

                Publish(
                    NovelSaveFeedbackKind.QuickSave,
                    DialogueSaveSystem.QuickSaveSlot,
                    saved,
                    saved ? "Quick Save complete." : FailureMessage("Quick Save failed."));
                return saved;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public bool QuickLoad()
        {
            if (!TryBegin(NovelSaveFeedbackKind.QuickLoad, DialogueSaveSystem.QuickSaveSlot))
                return false;

            try
            {
                var loaded = _saveSystem.QuickLoad();
                if (loaded)
                    Loaded?.Invoke();

                Publish(
                    NovelSaveFeedbackKind.QuickLoad,
                    DialogueSaveSystem.QuickSaveSlot,
                    loaded,
                    loaded ? "Quick Load complete." : FailureMessage("No Quick Save data."));
                return loaded;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        /// <summary>
        /// Replaces the single product autosave slot with a coherent checkpoint.
        /// Autosaves deliberately omit thumbnails so the narrative and registered
        /// presentation contributors are committed in one short synchronous write.
        /// </summary>
        public bool Autosave(string checkpointTitle)
        {
            var slot = DialogueSaveSystem.AutosaveSlot;
            if (!CanSaveNow)
            {
                Publish(NovelSaveFeedbackKind.Autosave, slot, false, "Autosave is not available at this point.");
                return false;
            }

            if (!TryBegin(NovelSaveFeedbackKind.Autosave, slot))
                return false;

            try
            {
                var title = string.IsNullOrWhiteSpace(checkpointTitle)
                    ? BuildSaveTitle(slot)
                    : checkpointTitle.Trim();
                var saved = _saveSystem.Save(slot, true, title) != null;
                if (saved)
                    Saved?.Invoke();

                Publish(
                    NovelSaveFeedbackKind.Autosave,
                    slot,
                    saved,
                    saved ? "Autosave complete." : FailureMessage("Autosave failed; dialogue will continue."));
                return saved;
            }
            catch (Exception exception)
            {
                var message = "Autosave failed; dialogue will continue. " + exception.Message;
                Debug.LogWarning("NovelSaveService: " + message);
                Publish(NovelSaveFeedbackKind.Autosave, slot, false, message);
                return false;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public bool ContinueLatest()
        {
            var candidate = GetContinueCandidate();
            var loaded = candidate != null && candidate.CanLoad && _saveSystem.Load(candidate.SlotIndex);
            if (loaded)
            {
                RememberManualSlot(candidate.SlotIndex);
                Loaded?.Invoke();
            }

            return loaded;
        }

        public bool HasSave(int slot)
        {
            return _saveSystem.Exists(slot);
        }

        public bool HasContinueSave()
        {
            var candidate = GetContinueCandidate();
            return candidate != null && candidate.CanLoad;
        }

        /// <summary>
        /// Continue uses the newest loadable manual, quick, or autosave. Talk System
        /// resolves equal-second timestamps by descending slot index, which gives
        /// manual slots precedence over autosave, then quick save.
        /// </summary>
        public DialogueSaveSlotViewModel GetContinueCandidate()
        {
            return _saveSystem.GetLatestContinueCandidate(true, true, false);
        }

        public DialogueSaveSlotViewModel GetSlotViewModel(int slot)
        {
            return _saveSystem.GetSlotViewModel(slot, false);
        }

        public void Dispose()
        {
            if (_saveSystem != null)
                _saveSystem.OperationFailed -= HandleOperationFailed;
        }

        private DialogueSaveSlot SaveWithThumbnail(int slot, bool isAutosave, string title)
        {
            _saveSystem.SaveWithThumbnail(slot, isAutosave, title);
            var result = _saveSystem.LastOperationResult;
            return result != null && result.Failed ? null : _saveSystem.Peek(slot);
        }

        private bool SaveInternal(int slot, NovelSaveFeedbackKind kind)
        {
            if (!CanSaveNow)
            {
                Publish(kind, slot, false, "Saving is not available at this point.");
                return false;
            }

            if (!TryBegin(kind, slot))
                return false;

            try
            {
                var title = BuildSaveTitle(slot);
                var saved = _saveThumbnails
                    ? SaveWithThumbnail(slot, false, title) != null
                    : _saveSystem.Save(slot, false, title) != null;

                if (saved)
                {
                    RememberManualSlot(slot);
                    Saved?.Invoke();
                }

                var verb = kind == NovelSaveFeedbackKind.DirectSave ? "Direct Save" : "Saved";
                Publish(kind, slot, saved, saved ? $"{verb}: Slot {slot}." : FailureMessage("Save failed."));
                return saved;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private bool TryBegin(NovelSaveFeedbackKind kind, int slot)
        {
            if (_operationInProgress)
            {
                Publish(kind, slot, false, "Another save operation is in progress.");
                return false;
            }

            _operationInProgress = true;
            return true;
        }

        private void RememberManualSlot(int slot)
        {
            if (slot >= DialogueSaveSlotConventions.FirstManualSlot)
                _directSaveTarget.Remember(slot);
        }

        private string FailureMessage(string fallback)
        {
            var result = _saveSystem != null ? _saveSystem.LastOperationResult : null;
            return result != null && !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : fallback;
        }

        private void Publish(NovelSaveFeedbackKind kind, int slot, bool succeeded, string message)
        {
            Feedback?.Invoke(new NovelSaveFeedback(kind, slot, succeeded, message));
        }

        private string BuildSaveTitle(int slot)
        {
            if (_manager != null && _manager.CurrentData != null)
            {
                var speaker = _manager.CurrentData.Speaker ?? string.Empty;
                var text = _manager.CurrentData.Text ?? string.Empty;
                var prefix = string.IsNullOrEmpty(speaker) ? string.Empty : speaker + ": ";
                var title = prefix + text;
                return title.Length > 40 ? title.Substring(0, 40) + "..." : title;
            }

            return slot == DialogueSaveSystem.QuickSaveSlot ? "Quick Save" : $"Save {slot}";
        }

        private void HandleOperationFailed(DialogueSaveOperationResult result)
        {
            if (result == null)
                return;

            Debug.LogWarning($"NovelSaveService: dialogue save {result.Operation} failed for slot {result.SlotIndex}: {result.Message}");
        }
    }
}
