namespace kkmia.TalkSystem
{
    public sealed class DialogueUnlockEventDispatcher : IDialogueEventDispatcher
    {
        public const string DefaultPrefix = "unlock:";

        private readonly DialogueUnlockRegistry _registry;
        private readonly IDialogueEventDispatcher _innerDispatcher;
        private readonly string _prefix;

        public DialogueUnlockEventDispatcher(DialogueUnlockRegistry registry)
            : this(registry, null, DefaultPrefix)
        {
        }

        public DialogueUnlockEventDispatcher(
            DialogueUnlockRegistry registry,
            IDialogueEventDispatcher innerDispatcher,
            string prefix = DefaultPrefix)
        {
            _registry = registry;
            _innerDispatcher = innerDispatcher;
            _prefix = prefix ?? string.Empty;
        }

        public void Dispatch(DialogueEventContext context)
        {
            if (context != null)
                TryMarkFromEventKey(context.EventKey);

            if (_innerDispatcher != null)
                _innerDispatcher.Dispatch(context);
        }

        public bool TryMarkFromEventKey(string eventKey)
        {
            return TryMarkFromEventKey(eventKey, 0);
        }

        public bool TryMarkFromEventKey(string eventKey, long unlockedAtUnix)
        {
            if (_registry == null || string.IsNullOrWhiteSpace(eventKey))
                return false;

            var id = eventKey.Trim();
            if (_prefix.Length > 0)
            {
                if (!id.StartsWith(_prefix, System.StringComparison.Ordinal))
                    return false;

                id = id.Substring(_prefix.Length);
            }

            if (id.Length == 0)
                return false;

            return unlockedAtUnix > 0
                ? _registry.MarkUnlocked(id, unlockedAtUnix)
                : _registry.MarkUnlocked(id);
        }
    }
}
