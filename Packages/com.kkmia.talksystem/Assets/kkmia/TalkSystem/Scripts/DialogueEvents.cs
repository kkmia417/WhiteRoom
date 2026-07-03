using System;

namespace kkmia.TalkSystem
{
    public sealed class DialogueEventContext
    {
        public DialogueEventContext(DialogueData data, string eventKey, DialogueSessionState state)
        {
            Data = data;
            EventKey = eventKey ?? string.Empty;
            State = state;
        }

        public DialogueData Data { get; private set; }
        public string EventKey { get; private set; }
        public DialogueSessionState State { get; private set; }
    }

    public interface IDialogueEventDispatcher
    {
        void Dispatch(DialogueEventContext context);
    }

    public sealed class DelegateDialogueEventDispatcher : IDialogueEventDispatcher
    {
        private readonly Action<DialogueEventContext> _handler;

        public DelegateDialogueEventDispatcher(Action<DialogueEventContext> handler)
        {
            _handler = handler;
        }

        public void Dispatch(DialogueEventContext context)
        {
            if (_handler != null)
                _handler(context);
        }
    }
}
