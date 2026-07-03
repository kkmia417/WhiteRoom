namespace kkmia.TalkSystem
{
    public interface IDialogueUnlockStorage
    {
        bool TryLoad(out DialogueUnlockState state);
        void Save(DialogueUnlockState state);
        void Delete();
        bool Exists();
    }
}
