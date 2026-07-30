using System;

namespace WhiteRoom.Novel
{
    public enum TitleReturnRequestResult
    {
        Started,
        ConfirmationRequired,
        Rejected
    }

    /// <summary>
    /// Owns the unsaved-progress and transition guard policy for returning to Title.
    /// Scene loading and UI reset remain injected application boundaries.
    /// </summary>
    public sealed class TitleReturnService
    {
        private readonly Action _resetForTitle;
        private readonly Action _returnToTitle;
        private bool _hasUnsavedProgress;
        private bool _transitionInProgress;

        public TitleReturnService(Action resetForTitle, Action returnToTitle)
        {
            _resetForTitle = resetForTitle ?? throw new ArgumentNullException(nameof(resetForTitle));
            _returnToTitle = returnToTitle ?? throw new ArgumentNullException(nameof(returnToTitle));
        }

        public bool HasUnsavedProgress => _hasUnsavedProgress;
        public bool IsTransitionInProgress => _transitionInProgress;

        public void MarkProgressChanged()
        {
            if (!_transitionInProgress)
                _hasUnsavedProgress = true;
        }

        public void MarkProgressSaved()
        {
            _hasUnsavedProgress = false;
        }

        public TitleReturnRequestResult RequestReturnToTitle()
        {
            if (_transitionInProgress)
                return TitleReturnRequestResult.Rejected;
            if (_hasUnsavedProgress)
                return TitleReturnRequestResult.ConfirmationRequired;

            return StartTransition();
        }

        public TitleReturnRequestResult ConfirmReturnToTitle()
        {
            if (_transitionInProgress)
                return TitleReturnRequestResult.Rejected;

            return StartTransition();
        }

        public void NotifySceneLoaded()
        {
            _transitionInProgress = false;
            _hasUnsavedProgress = false;
        }

        private TitleReturnRequestResult StartTransition()
        {
            _transitionInProgress = true;
            _hasUnsavedProgress = false;
            _resetForTitle();
            _returnToTitle();
            return TitleReturnRequestResult.Started;
        }
    }
}
