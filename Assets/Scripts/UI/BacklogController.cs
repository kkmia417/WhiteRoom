using System;
using kkmia.TalkSystem;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Opens and closes the backlog view, pausing dialogue auto-advance while it is open.
    /// </summary>
    public sealed class BacklogController
    {
        private readonly DialogueBacklogView _backlogView;
        private readonly DialogueAutoAdvanceGate _autoAdvanceGate;
        private readonly Action _beforeOpen;

        public BacklogController(
            DialogueBacklogView backlogView,
            DialogueAutoAdvanceGate autoAdvanceGate,
            Action beforeOpen = null)
        {
            _backlogView = backlogView;
            _autoAdvanceGate = autoAdvanceGate;
            _beforeOpen = beforeOpen;
        }

        public bool IsOpen => _backlogView != null && _backlogView.IsOpen;

        public void Toggle()
        {
            if (_backlogView == null)
                return;

            if (_backlogView.IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (_backlogView == null)
                return;

            _beforeOpen?.Invoke();
            _autoAdvanceGate.Suspend(this);
            _backlogView.Open();
        }

        public void Close()
        {
            if (_backlogView == null)
                return;

            _backlogView.Close();
            _autoAdvanceGate.Resume(this);
        }
    }
}
