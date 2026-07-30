using System;
using kkmia.TalkSystem;

namespace WhiteRoom.Novel
{
    public sealed class EndingResultInfo
    {
        private EndingResultInfo(string endingKey, string type, string displayName, bool isFirstReach)
        {
            EndingKey = endingKey;
            Type = type;
            DisplayName = displayName;
            IsFirstReach = isFirstReach;
        }

        public string EndingKey { get; }
        public string Type { get; }
        public string DisplayName { get; }
        public bool IsFirstReach { get; }

        public static EndingResultInfo Create(string endingKey, string text, bool isFirstReach)
        {
            var normalizedKey = string.IsNullOrWhiteSpace(endingKey) ? string.Empty : endingKey.Trim();
            var normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            var type = "ENDING";
            var displayName = normalizedText;

            if (normalizedText.StartsWith("【", StringComparison.Ordinal))
            {
                var close = normalizedText.IndexOf('】');
                if (close > 1)
                {
                    type = normalizedText.Substring(1, close - 1).Trim();
                    displayName = normalizedText.Substring(close + 1).Trim();
                }
            }

            if (displayName.Length == 0)
                displayName = normalizedKey;

            return new EndingResultInfo(normalizedKey, type, displayName, isFirstReach);
        }
    }

    /// <summary>
    /// Converts Talk System ending markers into the WhiteRoom result-screen flow.
    /// A marker is remembered when its final row starts, but is not presented until
    /// DialogueEnded confirms that the final text has been completed by the player.
    /// </summary>
    public sealed class EndingFlowService : IDisposable
    {
        private readonly Func<bool> _persistUnlocks;
        private readonly Func<string, bool> _isEndingUnlocked;
        private readonly Action _resetForTitle;
        private readonly Action _returnToTitle;

        private DialogueManager _manager;
        private EndingResultInfo _pendingResult;
        private EndingResultInfo _currentResult;
        private bool _transitionInProgress;

        public EndingFlowService(Func<bool> persistUnlocks, Action resetForTitle, Action returnToTitle)
            : this(persistUnlocks, null, resetForTitle, returnToTitle)
        {
        }

        public EndingFlowService(
            Func<bool> persistUnlocks,
            Func<string, bool> isEndingUnlocked,
            Action resetForTitle,
            Action returnToTitle)
        {
            _persistUnlocks = persistUnlocks ?? throw new ArgumentNullException(nameof(persistUnlocks));
            _isEndingUnlocked = isEndingUnlocked;
            _resetForTitle = resetForTitle ?? throw new ArgumentNullException(nameof(resetForTitle));
            _returnToTitle = returnToTitle ?? throw new ArgumentNullException(nameof(returnToTitle));
        }

        public event Action<EndingResultInfo> ResultReady;
        public event Action<string> TransitionFailed;

        public EndingResultInfo CurrentResult => _currentResult;
        public bool IsAwaitingConfirmation => _currentResult != null;
        public bool IsTransitionInProgress => _transitionInProgress;
        public bool IsInputBlocked => IsAwaitingConfirmation || IsTransitionInProgress;

        public void AttachTo(DialogueManager manager)
        {
            if (_manager == manager)
                return;

            Detach();
            _manager = manager;
            if (_manager == null)
                return;

            _manager.ProgressMarkerReached += HandleProgressMarkerReached;
            _manager.DialogueEnded += HandleDialogueEnded;
        }

        public bool ConfirmAndReturnToTitle()
        {
            if (_currentResult == null || _transitionInProgress)
                return false;

            if (!_persistUnlocks())
            {
                TransitionFailed?.Invoke("エンディング解放情報を保存できませんでした。もう一度お試しください。");
                return false;
            }

            _transitionInProgress = true;
            _resetForTitle();
            _currentResult = null;
            _returnToTitle();
            return true;
        }

        public void NotifySceneLoaded()
        {
            _transitionInProgress = false;
        }

        public void Dispose()
        {
            Detach();
            _pendingResult = null;
            _currentResult = null;
        }

        private void HandleProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (context == null || context.Marker == null || context.Data == null)
                return;
            if (context.Marker.Type != DialogueProgressMarkerType.Ending
                || string.IsNullOrWhiteSpace(context.Marker.Key))
                return;

            var isFirstReach = _isEndingUnlocked != null
                ? !_isEndingUnlocked(context.Marker.Key)
                : context.Marker.IsFirstReach;
            _pendingResult = EndingResultInfo.Create(context.Marker.Key, context.Data.Text, isFirstReach);
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            if (_pendingResult == null || _transitionInProgress)
                return;

            _currentResult = _pendingResult;
            _pendingResult = null;
            ResultReady?.Invoke(_currentResult);
        }

        private void Detach()
        {
            if (_manager == null)
                return;

            _manager.ProgressMarkerReached -= HandleProgressMarkerReached;
            _manager.DialogueEnded -= HandleDialogueEnded;
            _manager = null;
        }
    }
}
