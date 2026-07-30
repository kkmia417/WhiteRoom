using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Tracks story progress: dialogue event keys reached in this session and persistent
    /// unlocks (chapters/routes/endings) fed by progress markers. Also evaluates the
    /// condition keys used by dialogue branching.
    /// </summary>
    public sealed class DialogueProgressService : IDialogueConditionEvaluator, IDisposable
    {
        private readonly bool _unlockProgressMarkers;
        private readonly HashSet<string> _reachedEventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly DialogueUnlockRegistry _unlockRegistry = new DialogueUnlockRegistry();
        private readonly DialogueUnlockSaveService _unlockSaveService = new DialogueUnlockSaveService(new FileDialogueUnlockStorage());

        private DialogueManager _manager;

        public DialogueProgressService(bool unlockProgressMarkers)
        {
            _unlockProgressMarkers = unlockProgressMarkers;

            _unlockRegistry.Unlocked += HandleUnlocked;

            if (!_unlockSaveService.LoadInto(_unlockRegistry)
                && !string.IsNullOrEmpty(_unlockSaveService.LastError))
            {
                Debug.LogWarning($"DialogueProgressService: {_unlockSaveService.LastError}");
            }
        }

        public void AttachTo(DialogueManager manager)
        {
            if (manager == null)
                return;

            _manager = manager;
            manager.ProgressMarkerReached -= HandleProgressMarkerReached;
            manager.ProgressMarkerReached += HandleProgressMarkerReached;
        }

        public void RecordEvent(string eventKey)
        {
            if (!string.IsNullOrWhiteSpace(eventKey))
                _reachedEventKeys.Add(eventKey.Trim());
        }

        public bool HasReachedEvent(string eventKey)
        {
            return !string.IsNullOrWhiteSpace(eventKey) && _reachedEventKeys.Contains(eventKey.Trim());
        }

        public bool IsUnlocked(string unlockId)
        {
            return _unlockRegistry.IsUnlocked(unlockId);
        }

        public List<string> ListUnlockedIds(string category)
        {
            return _unlockRegistry.ListUnlockedIds(category);
        }

        /// <summary>
        /// Synchronously persists the current unlock registry. Ending transitions use
        /// the result as a gate so Title is never loaded ahead of durable progress.
        /// </summary>
        public bool FlushUnlocks()
        {
            return !_unlockProgressMarkers || SaveUnlocks();
        }

        public void Dispose()
        {
            _unlockRegistry.Unlocked -= HandleUnlocked;

            if (_manager != null)
                _manager.ProgressMarkerReached -= HandleProgressMarkerReached;
        }

        bool IDialogueConditionEvaluator.Evaluate(string conditionKey, DialogueData data)
        {
            if (string.IsNullOrEmpty(conditionKey))
                return true;

            return EvaluateCondition(conditionKey);
        }

        private bool EvaluateCondition(string conditionKey)
        {
            var normalized = conditionKey.Trim();
            var invert = normalized.StartsWith("!", StringComparison.Ordinal);
            if (invert)
                normalized = normalized.Substring(1).Trim();

            var result = EvaluatePositiveCondition(normalized);
            return invert ? !result : result;
        }

        private bool EvaluatePositiveCondition(string conditionKey)
        {
            if (conditionKey.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                return HasReachedEvent(conditionKey.Substring("event:".Length));

            if (conditionKey.StartsWith("unlock:", StringComparison.OrdinalIgnoreCase))
                return IsUnlocked(conditionKey.Substring("unlock:".Length));

            if (conditionKey.StartsWith("chapter:", StringComparison.OrdinalIgnoreCase)
                || conditionKey.StartsWith("route:", StringComparison.OrdinalIgnoreCase)
                || conditionKey.StartsWith("ending:", StringComparison.OrdinalIgnoreCase))
            {
                return IsUnlocked(conditionKey);
            }

            return HasReachedEvent(conditionKey) || IsUnlocked(conditionKey);
        }

        private void HandleProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (!_unlockProgressMarkers || context == null || context.Marker == null)
                return;

            var marker = context.Marker;
            if (!marker.IsFirstReach || string.IsNullOrEmpty(marker.Key))
                return;

            var category = GetUnlockCategory(marker.Type);
            if (string.IsNullOrEmpty(category))
                return;

            var unlockId = category + ":" + marker.Key;
            if (!_unlockRegistry.MarkUnlocked(unlockId, category))
                return;

            SaveUnlocks();
        }

        private static string GetUnlockCategory(DialogueProgressMarkerType markerType)
        {
            switch (markerType)
            {
                case DialogueProgressMarkerType.Chapter:
                    return "chapter";
                case DialogueProgressMarkerType.Route:
                    return "route";
                case DialogueProgressMarkerType.Ending:
                    return "ending";
                default:
                    return string.Empty;
            }
        }

        private void HandleUnlocked(DialogueUnlockEventContext context)
        {
            if (context == null || context.Entry == null)
                return;

            Debug.Log($"DialogueProgressService: unlocked '{context.Entry.Id}'.");
        }

        private bool SaveUnlocks()
        {
            var saved = _unlockSaveService.Save(_unlockRegistry);
            if (!saved)
                Debug.LogWarning($"DialogueProgressService: {_unlockSaveService.LastError}");
            return saved;
        }
    }
}
