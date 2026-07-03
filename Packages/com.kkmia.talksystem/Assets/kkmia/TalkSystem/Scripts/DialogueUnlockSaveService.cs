using System;

namespace kkmia.TalkSystem
{
    public sealed class DialogueUnlockSaveService
    {
        private readonly IDialogueUnlockStorage _storage;

        public DialogueUnlockSaveService(IDialogueUnlockStorage storage)
        {
            _storage = storage;
        }

        public string LastError { get; private set; }
        public Exception LastException { get; private set; }

        public bool Save(DialogueUnlockRegistry registry)
        {
            return Save(registry != null ? registry.CaptureState() : null);
        }

        public bool Save(DialogueUnlockState state)
        {
            if (!EnsureStorage())
                return false;

            try
            {
                _storage.Save(state != null ? state.Clone() : new DialogueUnlockState());
                ClearError();
                return true;
            }
            catch (Exception e)
            {
                SetError("Failed to save dialogue unlock state: " + e.Message, e);
                return false;
            }
        }

        public bool LoadInto(DialogueUnlockRegistry registry)
        {
            if (registry == null)
            {
                SetError("Dialogue unlock registry is null.", null);
                return false;
            }

            DialogueUnlockState state;
            if (!TryLoad(out state))
                return false;

            registry.RestoreState(state);
            return true;
        }

        public bool TryLoad(out DialogueUnlockState state)
        {
            state = new DialogueUnlockState();
            if (!EnsureStorage())
                return false;

            try
            {
                if (!_storage.TryLoad(out state))
                {
                    state = new DialogueUnlockState();
                    ClearError();
                    return false;
                }

                state = state != null ? state.Clone() : new DialogueUnlockState();
                ClearError();
                return true;
            }
            catch (Exception e)
            {
                state = new DialogueUnlockState();
                SetError("Failed to load dialogue unlock state: " + e.Message, e);
                return false;
            }
        }

        public bool Reset()
        {
            if (!EnsureStorage())
                return false;

            try
            {
                _storage.Delete();
                ClearError();
                return true;
            }
            catch (Exception e)
            {
                SetError("Failed to delete dialogue unlock state: " + e.Message, e);
                return false;
            }
        }

        public bool Exists()
        {
            if (!EnsureStorage())
                return false;

            try
            {
                var exists = _storage.Exists();
                ClearError();
                return exists;
            }
            catch (Exception e)
            {
                SetError("Failed to check dialogue unlock state: " + e.Message, e);
                return false;
            }
        }

        private bool EnsureStorage()
        {
            if (_storage != null)
                return true;

            SetError("Dialogue unlock storage is not configured.", null);
            return false;
        }

        private void ClearError()
        {
            LastError = string.Empty;
            LastException = null;
        }

        private void SetError(string message, Exception exception)
        {
            LastError = message ?? string.Empty;
            LastException = exception;
        }
    }
}
