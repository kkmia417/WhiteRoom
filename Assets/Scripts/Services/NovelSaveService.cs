using System;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
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

        public NovelSaveService(DialogueManager manager, DialogueSaveSystem saveSystem, int defaultManualSlot, bool saveThumbnails)
        {
            _manager = manager;
            _saveSystem = saveSystem;
            _defaultManualSlot = defaultManualSlot;
            _saveThumbnails = saveThumbnails;

            _saveSystem.OperationFailed += HandleOperationFailed;
        }

        public event Action Saved;
        public event Action Loaded;

        public bool CanSaveNow => _manager != null && _manager.CurrentData != null;

        public bool Save()
        {
            return Save(_defaultManualSlot);
        }

        public bool Save(int slot)
        {
            var title = BuildSaveTitle(slot);
            var saved = _saveThumbnails
                ? SaveWithThumbnail(slot, false, title) != null
                : _saveSystem.Save(slot, false, title) != null;

            if (saved)
                Saved?.Invoke();

            return saved;
        }

        public bool Load()
        {
            return Load(_defaultManualSlot);
        }

        public bool Load(int slot)
        {
            var loaded = _saveSystem.Load(slot);
            if (loaded)
                Loaded?.Invoke();

            return loaded;
        }

        public bool QuickSave()
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

            return saved;
        }

        public bool QuickLoad()
        {
            var loaded = _saveSystem.QuickLoad();
            if (loaded)
                Loaded?.Invoke();

            return loaded;
        }

        public bool ContinueLatest()
        {
            var candidate = _saveSystem.GetLatestContinueCandidate(true, true, false);
            var loaded = candidate != null && candidate.CanLoad && _saveSystem.Load(candidate.SlotIndex);
            if (loaded)
                Loaded?.Invoke();

            return loaded;
        }

        public bool HasSave(int slot)
        {
            return _saveSystem.Exists(slot);
        }

        public bool HasContinueSave()
        {
            var candidate = _saveSystem.GetLatestContinueCandidate(true, true, false);
            return candidate != null && candidate.CanLoad;
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
