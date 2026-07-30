using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public enum DialogueBoundaryKind
    {
        Scene,
        Choice
    }

    public enum DialogueBoundaryDirection
    {
        Previous,
        Next
    }

    public enum DialogueBoundaryJumpStatus
    {
        Success,
        NoTarget,
        Busy,
        MissingTarget,
        ConditionNotSatisfied,
        CycleDetected,
        InvalidSnapshot,
        RestoreFailed
    }

    public sealed class DialogueBoundaryJumpResult
    {
        public DialogueBoundaryJumpResult(
            DialogueBoundaryJumpStatus status,
            string boundaryId = null,
            string message = null)
        {
            Status = status;
            BoundaryId = boundaryId ?? string.Empty;
            Message = message ?? DefaultMessage(status);
        }

        public DialogueBoundaryJumpStatus Status { get; }
        public string BoundaryId { get; }
        public string Message { get; }
        public bool Succeeded => Status == DialogueBoundaryJumpStatus.Success;

        private static string DefaultMessage(DialogueBoundaryJumpStatus status)
        {
            switch (status)
            {
                case DialogueBoundaryJumpStatus.Success: return "Dialogue position restored.";
                case DialogueBoundaryJumpStatus.NoTarget: return "No reached boundary is available in that direction.";
                case DialogueBoundaryJumpStatus.Busy: return "A dialogue jump is already in progress.";
                case DialogueBoundaryJumpStatus.MissingTarget: return "The reached dialogue target no longer exists.";
                case DialogueBoundaryJumpStatus.ConditionNotSatisfied: return "The choice is no longer available under current conditions.";
                case DialogueBoundaryJumpStatus.CycleDetected: return "The reached boundary is part of a repeated cycle.";
                case DialogueBoundaryJumpStatus.InvalidSnapshot: return "The reached boundary snapshot is invalid.";
                default: return "The dialogue position could not be restored.";
            }
        }
    }

    /// <summary>
    /// Product policy for navigating only among scene and choice boundaries that
    /// were reached on the active journey. Talk System remains the source of row,
    /// choice, history, progress and presentation snapshot truth.
    /// </summary>
    public sealed class DialogueBoundaryNavigationService : IDisposable, IDialogueSaveContributor
    {
        private const string SaveKey = "whiteroom.boundary-navigation.v1";
        private const int PayloadVersion = 1;

        [Serializable]
        private sealed class BoundaryCheckpoint
        {
            public string BoundaryId = string.Empty;
            public DialogueBoundaryKind Kind;
            public int DialogueId = -1;
            public string ChapterKey = string.Empty;
            public DialogueSaveData Snapshot;
        }

        [Serializable]
        private sealed class NavigationPayload
        {
            public int Version = PayloadVersion;
            public int Cursor = -1;
            public bool AtBoundary;
            public List<BoundaryCheckpoint> Checkpoints = new List<BoundaryCheckpoint>();
            public List<string> CyclicBoundaryIds = new List<string>();
        }

        private readonly DialogueManager _manager;
        private readonly DialogueSaveSystem _saveSystem;
        private readonly Action<string> _warning;
        private readonly List<BoundaryCheckpoint> _checkpoints = new List<BoundaryCheckpoint>();
        private readonly HashSet<string> _cyclicBoundaryIds = new HashSet<string>(StringComparer.Ordinal);

        private int _cursor = -1;
        private bool _atBoundary;
        private bool _attached;
        private bool _isRestoring;

        public DialogueBoundaryNavigationService(
            DialogueManager manager,
            DialogueSaveSystem saveSystem,
            Action<string> warning = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _saveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
            _warning = warning;
        }

        public bool IsBusy { get; private set; }
        public int ReachedBoundaryCount => _checkpoints.Count;
        public string CurrentBoundaryId =>
            _atBoundary && _cursor >= 0 && _cursor < _checkpoints.Count
                ? _checkpoints[_cursor].BoundaryId
                : string.Empty;

        public event Action JumpStarted;
        public event Action<DialogueBoundaryJumpResult> JumpCompleted;

        public void Attach()
        {
            if (_attached) return;
            _manager.LineStarted += HandleLineStarted;
            _attached = true;
            RecordCurrentBoundaryIfNeeded();
        }

        public void Reset()
        {
            _checkpoints.Clear();
            _cyclicBoundaryIds.Clear();
            _cursor = -1;
            _atBoundary = false;
        }

        public bool CanJump(DialogueBoundaryKind kind, DialogueBoundaryDirection direction)
        {
            return !IsBusy && FindCandidateIndex(kind, direction) >= 0;
        }

        public string GetUnavailableReason(DialogueBoundaryKind kind, DialogueBoundaryDirection direction)
        {
            if (IsBusy)
                return "Dialogue navigation is busy";
            return FindCandidateIndex(kind, direction) >= 0
                ? string.Empty
                : direction == DialogueBoundaryDirection.Previous
                    ? "No previous reached " + KindLabel(kind)
                    : "No next reached " + KindLabel(kind);
        }

        public DialogueBoundaryJumpResult Jump(
            DialogueBoundaryKind kind,
            DialogueBoundaryDirection direction)
        {
            if (IsBusy)
                return Complete(new DialogueBoundaryJumpResult(DialogueBoundaryJumpStatus.Busy));

            var candidateIndex = FindCandidateIndex(kind, direction);
            if (candidateIndex < 0)
                return Complete(new DialogueBoundaryJumpResult(DialogueBoundaryJumpStatus.NoTarget));

            var checkpoint = _checkpoints[candidateIndex];
            if (_cyclicBoundaryIds.Contains(checkpoint.BoundaryId))
            {
                return Complete(new DialogueBoundaryJumpResult(
                    DialogueBoundaryJumpStatus.CycleDetected,
                    checkpoint.BoundaryId));
            }

            if (checkpoint.Snapshot == null ||
                checkpoint.DialogueId < 0 ||
                checkpoint.Snapshot.CurrentDialogueId != checkpoint.DialogueId)
            {
                return Complete(new DialogueBoundaryJumpResult(
                    DialogueBoundaryJumpStatus.InvalidSnapshot,
                    checkpoint.BoundaryId));
            }

            var repository = _manager.Repository;
            var target = repository != null ? repository.Get(checkpoint.DialogueId) : null;
            if (target == null)
            {
                return Complete(new DialogueBoundaryJumpResult(
                    DialogueBoundaryJumpStatus.MissingTarget,
                    checkpoint.BoundaryId));
            }

            IsBusy = true;
            JumpStarted?.Invoke();
            var before = _saveSystem.CaptureState(this);
            DialogueBoundaryJumpResult result;
            try
            {
                _isRestoring = true;
                if (before == null || !_saveSystem.RestoreState(checkpoint.Snapshot, this))
                {
                    result = new DialogueBoundaryJumpResult(
                        DialogueBoundaryJumpStatus.RestoreFailed,
                        checkpoint.BoundaryId);
                }
                else if (checkpoint.Kind == DialogueBoundaryKind.Choice && !_manager.HasCurrentChoices)
                {
                    _saveSystem.RestoreState(before, this);
                    result = new DialogueBoundaryJumpResult(
                        DialogueBoundaryJumpStatus.ConditionNotSatisfied,
                        checkpoint.BoundaryId);
                }
                else
                {
                    _cursor = candidateIndex;
                    _atBoundary = true;
                    result = new DialogueBoundaryJumpResult(
                        DialogueBoundaryJumpStatus.Success,
                        checkpoint.BoundaryId);
                }
            }
            catch (Exception exception)
            {
                if (before != null)
                    _saveSystem.RestoreState(before, this);
                _warning?.Invoke("Dialogue boundary restore failed: " + exception.Message);
                result = new DialogueBoundaryJumpResult(
                    DialogueBoundaryJumpStatus.RestoreFailed,
                    checkpoint.BoundaryId);
            }
            finally
            {
                _isRestoring = false;
                IsBusy = false;
            }

            return Complete(result);
        }

        void IDialogueSaveContributor.Capture(DialogueSaveData data)
        {
            if (data == null) return;
            var payload = new NavigationPayload
            {
                Cursor = _cursor,
                AtBoundary = _atBoundary,
                Checkpoints = new List<BoundaryCheckpoint>(_checkpoints),
                CyclicBoundaryIds = new List<string>(_cyclicBoundaryIds)
            };
            data.SetExtra(SaveKey, JsonUtility.ToJson(payload));
        }

        void IDialogueSaveContributor.Restore(DialogueSaveData data)
        {
            Reset();
            if (data == null)
                return;

            string json;
            if (!data.TryGetExtra(SaveKey, out json) || string.IsNullOrWhiteSpace(json))
            {
                RecordCurrentBoundaryIfNeeded();
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<NavigationPayload>(json);
                if (payload == null || payload.Version != PayloadVersion)
                {
                    WarnIgnoredPayload("unsupported payload version");
                    RecordCurrentBoundaryIfNeeded();
                    return;
                }

                if (payload.Checkpoints != null)
                {
                    for (var index = 0; index < payload.Checkpoints.Count; index++)
                    {
                        if (payload.Checkpoints[index] != null)
                            _checkpoints.Add(payload.Checkpoints[index]);
                    }
                }
                if (payload.CyclicBoundaryIds != null)
                {
                    for (var index = 0; index < payload.CyclicBoundaryIds.Count; index++)
                    {
                        if (!string.IsNullOrEmpty(payload.CyclicBoundaryIds[index]))
                            _cyclicBoundaryIds.Add(payload.CyclicBoundaryIds[index]);
                    }
                }

                _cursor = Mathf.Clamp(payload.Cursor, -1, _checkpoints.Count - 1);
                _atBoundary = payload.AtBoundary && _cursor >= 0;
                DetectDuplicateCycles();
            }
            catch (Exception exception)
            {
                Reset();
                WarnIgnoredPayload(exception.Message);
                RecordCurrentBoundaryIfNeeded();
            }
        }

        public void Dispose()
        {
            if (_attached)
                _manager.LineStarted -= HandleLineStarted;
            _attached = false;
        }

        public static string SceneBoundaryId(DialogueData data)
        {
            if (data == null || !data.HasChapterKey) return string.Empty;
            return "scene:" + data.ChapterKey.Trim() + ":" + data.Id;
        }

        public static string ChoiceBoundaryId(DialogueData data)
        {
            if (data == null || data.GetChoices().Count == 0) return string.Empty;
            return "choice:" + data.Id;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (_isRestoring || context == null || context.Data == null)
                return;

            if (_cursor < _checkpoints.Count - 1)
                _checkpoints.RemoveRange(_cursor + 1, _checkpoints.Count - _cursor - 1);

            _atBoundary = false;
            RecordBoundary(context.Data, DialogueBoundaryKind.Scene, SceneBoundaryId(context.Data));
            RecordBoundary(context.Data, DialogueBoundaryKind.Choice, ChoiceBoundaryId(context.Data));
        }

        private void RecordCurrentBoundaryIfNeeded()
        {
            if (_manager.CurrentData == null)
                return;

            var data = _manager.CurrentData;
            RecordBoundary(data, DialogueBoundaryKind.Scene, SceneBoundaryId(data));
            RecordBoundary(data, DialogueBoundaryKind.Choice, ChoiceBoundaryId(data));
        }

        private void RecordBoundary(DialogueData data, DialogueBoundaryKind kind, string boundaryId)
        {
            if (string.IsNullOrEmpty(boundaryId))
                return;

            for (var index = 0; index < _checkpoints.Count; index++)
            {
                if (!string.Equals(_checkpoints[index].BoundaryId, boundaryId, StringComparison.Ordinal))
                    continue;

                if (index == _checkpoints.Count - 1 && index == _cursor)
                {
                    var replacement = CreateCheckpoint(data, kind, boundaryId);
                    if (replacement != null)
                        _checkpoints[index] = replacement;
                    _atBoundary = true;
                    return;
                }

                _cyclicBoundaryIds.Add(boundaryId);
                return;
            }

            var checkpoint = CreateCheckpoint(data, kind, boundaryId);
            if (checkpoint == null)
                return;

            _checkpoints.Add(checkpoint);
            _cursor = _checkpoints.Count - 1;
            _atBoundary = true;
        }

        private BoundaryCheckpoint CreateCheckpoint(
            DialogueData data,
            DialogueBoundaryKind kind,
            string boundaryId)
        {
            var snapshot = _saveSystem.CaptureState(this);
            if (snapshot == null)
            {
                _warning?.Invoke("Dialogue boundary snapshot capture failed for " + boundaryId + ".");
                return null;
            }

            return new BoundaryCheckpoint
            {
                BoundaryId = boundaryId,
                Kind = kind,
                DialogueId = data.Id,
                ChapterKey = data.ChapterKey ?? string.Empty,
                Snapshot = snapshot
            };
        }

        private int FindCandidateIndex(DialogueBoundaryKind kind, DialogueBoundaryDirection direction)
        {
            if (_checkpoints.Count == 0)
                return -1;

            if (direction == DialogueBoundaryDirection.Previous)
            {
                var start = _atBoundary ? _cursor - 1 : _cursor;
                for (var index = start; index >= 0; index--)
                {
                    if (_checkpoints[index].Kind == kind)
                        return index;
                }
                return -1;
            }

            for (var index = _cursor + 1; index < _checkpoints.Count; index++)
            {
                if (_checkpoints[index].Kind == kind)
                    return index;
            }
            return -1;
        }

        private void DetectDuplicateCycles()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _checkpoints.Count; index++)
            {
                var id = _checkpoints[index].BoundaryId;
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    _cyclicBoundaryIds.Add(id ?? string.Empty);
            }
        }

        private DialogueBoundaryJumpResult Complete(DialogueBoundaryJumpResult result)
        {
            JumpCompleted?.Invoke(result);
            return result;
        }

        private void WarnIgnoredPayload(string reason)
        {
            _warning?.Invoke("Dialogue boundary navigation state was ignored: " + reason + ".");
        }

        private static string KindLabel(DialogueBoundaryKind kind)
        {
            return kind == DialogueBoundaryKind.Scene ? "scene" : "choice";
        }
    }
}
