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

        public BacklogController(DialogueBacklogView backlogView, DialogueAutoAdvanceGate autoAdvanceGate)
        {
            _backlogView = backlogView;
            _autoAdvanceGate = autoAdvanceGate;
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
