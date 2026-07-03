using System.Collections.Generic;
using kkmia.TalkSystem;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Suspends dialogue auto-advance while any overlay (backlog, save/load screen, ...)
    /// holds the gate, and resumes it once every holder has released.
    /// </summary>
    public sealed class DialogueAutoAdvanceGate
    {
        private readonly DialogueView _view;
        private readonly HashSet<object> _holders = new HashSet<object>();

        public DialogueAutoAdvanceGate(DialogueView view)
        {
            _view = view;
        }

        public void Suspend(object holder)
        {
            if (_holders.Add(holder))
                Apply();
        }

        public void Resume(object holder)
        {
            if (_holders.Remove(holder))
                Apply();
        }

        private void Apply()
        {
            if (_view != null)
                _view.SetAutoAdvanceSuspended(_holders.Count > 0);
        }
    }
}
