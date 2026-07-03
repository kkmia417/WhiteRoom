using System;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed partial class NovelGameBootstrap
    {
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

        private void EnsureDialogueUnlocks()
        {
            if (_unlockRegistry == null)
                _unlockRegistry = new DialogueUnlockRegistry();

            if (_unlockSaveService == null)
                _unlockSaveService = new DialogueUnlockSaveService(new FileDialogueUnlockStorage());

            _unlockRegistry.Unlocked -= HandleDialogueUnlocked;
            _unlockRegistry.Unlocked += HandleDialogueUnlocked;

            if (!_unlockSaveService.LoadInto(_unlockRegistry)
                && !string.IsNullOrEmpty(_unlockSaveService.LastError))
            {
                Debug.LogWarning($"NovelGameBootstrap: {_unlockSaveService.LastError}");
            }
        }

        private void ConnectProgressMarkers(DialogueManager manager)
        {
            if (manager == null)
                return;

            manager.ProgressMarkerReached -= HandleProgressMarkerReached;
            manager.ProgressMarkerReached += HandleProgressMarkerReached;
        }

        private void HandleProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (!unlockProgressMarkers || context == null || context.Marker == null)
                return;

            var marker = context.Marker;
            if (!marker.IsFirstReach || string.IsNullOrEmpty(marker.Key))
                return;

            var category = GetProgressUnlockCategory(marker.Type);
            if (string.IsNullOrEmpty(category))
                return;

            var unlockId = category + ":" + marker.Key;
            if (_unlockRegistry == null || !_unlockRegistry.MarkUnlocked(unlockId, category))
                return;

            SaveDialogueUnlocks();
        }

        private static string GetProgressUnlockCategory(DialogueProgressMarkerType markerType)
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

        private void HandleDialogueUnlocked(DialogueUnlockEventContext context)
        {
            if (context == null || context.Entry == null)
                return;

            Debug.Log($"NovelGameBootstrap: unlocked '{context.Entry.Id}'.");
        }

        private void SaveDialogueUnlocks()
        {
            if (_unlockSaveService == null || _unlockRegistry == null)
                return;

            if (!_unlockSaveService.Save(_unlockRegistry))
                Debug.LogWarning($"NovelGameBootstrap: {_unlockSaveService.LastError}");
        }
    }
}

