using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public enum AutosaveCheckpointKind
    {
        ChapterStart,
        ChoiceConfirmed,
        EndingUnlocked
    }

    /// <summary>
    /// Converts explicit story events into product autosave checkpoints. A request
    /// remains pending while text is typing or choices are visible and is consumed
    /// exactly once when the dialogue reaches WaitingForInput.
    /// </summary>
    public sealed class AutosaveCheckpointService : IDisposable
    {
        private sealed class PendingCheckpoint
        {
            public AutosaveCheckpointKind Kind;
            public string Title;
        }

        private readonly Func<string, bool> _saveCheckpoint;
        private readonly Func<DialoguePlaybackMode> _getPlaybackMode;
        private readonly Action<DialoguePlaybackMode> _setPlaybackMode;
        private readonly List<PendingCheckpoint> _pending = new List<PendingCheckpoint>();
        private readonly HashSet<string> _queuedOrWritten = new HashSet<string>(StringComparer.Ordinal);

        private DialogueManager _manager;
        private bool _awaitingPostChoiceLine;

        public AutosaveCheckpointService(
            Func<string, bool> saveCheckpoint,
            Func<DialoguePlaybackMode> getPlaybackMode = null,
            Action<DialoguePlaybackMode> setPlaybackMode = null)
        {
            _saveCheckpoint = saveCheckpoint ?? throw new ArgumentNullException(nameof(saveCheckpoint));
            _getPlaybackMode = getPlaybackMode;
            _setPlaybackMode = setPlaybackMode;
        }

        public int PendingCount => _pending.Count;

        public void AttachTo(DialogueManager manager)
        {
            Detach();
            _manager = manager;
            if (_manager == null)
                return;

            _manager.LineStarted += HandleLineStarted;
            _manager.LineCompleted += HandleLineCompleted;
            _manager.ProgressMarkerReached += HandleProgressMarkerReached;
            _manager.DialogueEnded += HandleDialogueEnded;
        }

        /// <summary>
        /// Called from the application update loop. It never writes unless an
        /// explicit checkpoint event was queued, so it cannot become a frame timer.
        /// </summary>
        public bool TryFlush()
        {
            return _manager != null && TryFlush(_manager.State, _manager.HasCurrentChoices, true);
        }

        /// <summary>Public state overload keeps checkpoint policy independently testable.</summary>
        public bool TryFlush(DialogueSessionState state, bool hasChoices)
        {
            return TryFlush(state, hasChoices, true);
        }

        public void Dispose()
        {
            Detach();
            _pending.Clear();
            _queuedOrWritten.Clear();
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (!_awaitingPostChoiceLine || context == null || context.Data == null)
                return;

            _awaitingPostChoiceLine = false;
            Queue(
                AutosaveCheckpointKind.ChoiceConfirmed,
                $"choice:{context.Data.Id}",
                "Auto: Choice confirmed");
        }

        private void HandleLineCompleted(DialogueEventContext context)
        {
            if (context == null || context.Data == null)
                return;

            if (context.State == DialogueSessionState.ChoicePending)
                _awaitingPostChoiceLine = true;

            if (!context.Data.HasEndingKey)
                return;

            var key = context.Data.EndingKey.Trim();
            Queue(
                AutosaveCheckpointKind.EndingUnlocked,
                $"ending:{key}:{context.Data.Id}",
                $"Auto: Ending {key}");

            // LineCompleted is raised after the final text is confirmed but before
            // DialogueEnded clears CurrentData. Unlock persistence already ran when
            // the ending marker was reached, so this is the final coherent window.
            TryFlush(context.State, false, false);
        }

        private void HandleProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (context == null || context.Marker == null || context.Data == null)
                return;
            if (context.Marker.Type != DialogueProgressMarkerType.Chapter || !context.Marker.IsFirstReach)
                return;

            var key = context.Marker.Key.Trim();
            if (key.Length == 0)
                return;

            Queue(
                AutosaveCheckpointKind.ChapterStart,
                $"chapter:{key}:{context.Data.Id}",
                $"Auto: Chapter {key}");
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            _awaitingPostChoiceLine = false;
            _pending.Clear();
            _queuedOrWritten.Clear();
        }

        private bool TryFlush(DialogueSessionState state, bool hasChoices, bool restorePlayback)
        {
            if (_pending.Count == 0 || state != DialogueSessionState.WaitingForInput || hasChoices)
                return false;

            var selected = SelectCheckpoint();
            _pending.Clear();

            var previousMode = _getPlaybackMode != null
                ? _getPlaybackMode()
                : DialoguePlaybackMode.Normal;
            if (_setPlaybackMode != null && previousMode != DialoguePlaybackMode.Normal)
                _setPlaybackMode(DialoguePlaybackMode.Normal);

            bool saved;
            try
            {
                saved = _saveCheckpoint(selected.Title);
            }
            catch (Exception exception)
            {
                saved = false;
                Debug.LogWarning("AutosaveCheckpointService: checkpoint write failed; dialogue will continue. " + exception.Message);
            }
            finally
            {
                if (restorePlayback && _setPlaybackMode != null && previousMode != DialoguePlaybackMode.Normal)
                    _setPlaybackMode(previousMode);
            }

            return saved;
        }

        private PendingCheckpoint SelectCheckpoint()
        {
            var selected = _pending[0];
            for (var index = 1; index < _pending.Count; index++)
            {
                if (_pending[index].Kind > selected.Kind)
                    selected = _pending[index];
            }

            return selected;
        }

        private void Queue(AutosaveCheckpointKind kind, string signature, string title)
        {
            if (string.IsNullOrEmpty(signature) || !_queuedOrWritten.Add(signature))
                return;

            _pending.Add(new PendingCheckpoint
            {
                Kind = kind,
                Title = title ?? "Auto Save"
            });
        }

        private void Detach()
        {
            if (_manager == null)
                return;

            _manager.LineStarted -= HandleLineStarted;
            _manager.LineCompleted -= HandleLineCompleted;
            _manager.ProgressMarkerReached -= HandleProgressMarkerReached;
            _manager.DialogueEnded -= HandleDialogueEnded;
            _manager = null;
        }
    }
}
