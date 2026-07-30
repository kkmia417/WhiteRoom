using System;
using kkmia.TalkSystem;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Suspends gameplay automation and input while a system overlay owns focus,
    /// then restores the exact playback mode when the overlay is cancelled/closed.
    /// </summary>
    public sealed class GameplayOverlayCoordinator
    {
        private readonly Func<DialoguePlaybackMode> _getPlaybackMode;
        private readonly Action<DialoguePlaybackMode> _setPlaybackMode;
        private readonly Action _stopSecondaryAutomation;
        private readonly Action<bool> _setGameplayInputEnabled;
        private DialoguePlaybackMode? _previousMode;

        public GameplayOverlayCoordinator(
            Func<DialoguePlaybackMode> getPlaybackMode,
            Action<DialoguePlaybackMode> setPlaybackMode,
            Action stopSecondaryAutomation,
            Action<bool> setGameplayInputEnabled)
        {
            _getPlaybackMode = getPlaybackMode ?? throw new ArgumentNullException(nameof(getPlaybackMode));
            _setPlaybackMode = setPlaybackMode ?? throw new ArgumentNullException(nameof(setPlaybackMode));
            _stopSecondaryAutomation = stopSecondaryAutomation;
            _setGameplayInputEnabled = setGameplayInputEnabled ??
                                       throw new ArgumentNullException(nameof(setGameplayInputEnabled));
        }

        public bool IsSuspended => _previousMode.HasValue;
        public DialoguePlaybackMode? SuspendedMode => _previousMode;

        public void Suspend()
        {
            if (!_previousMode.HasValue)
                _previousMode = _getPlaybackMode();

            _stopSecondaryAutomation?.Invoke();
            _setPlaybackMode(DialoguePlaybackMode.Normal);
            _setGameplayInputEnabled(false);
        }

        public void Resume()
        {
            if (!_previousMode.HasValue)
                return;

            var mode = _previousMode.Value;
            _previousMode = null;
            _setPlaybackMode(mode);
            _setGameplayInputEnabled(true);
        }

        public void ResetForTransition()
        {
            _previousMode = null;
            _stopSecondaryAutomation?.Invoke();
            _setPlaybackMode(DialoguePlaybackMode.Normal);
            _setGameplayInputEnabled(false);
        }
    }
}
