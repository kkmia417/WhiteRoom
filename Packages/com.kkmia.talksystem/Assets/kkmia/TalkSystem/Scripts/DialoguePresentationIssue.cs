using System;

namespace kkmia.TalkSystem
{
    public enum DialoguePresentationIssueKind
    {
        Background,
        Bgm,
        Se,
        Voice,
        StageSlot,
        Character,
        CharacterModel
    }

    public sealed class DialoguePresentationIssueContext
    {
        public DialoguePresentationIssueContext(DialoguePresentationIssueKind kind, string key, string message)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DialoguePresentationIssueKind Kind { get; private set; }
        public string Key { get; private set; }
        public string Message { get; private set; }
    }

    public interface IDialoguePresentationIssueSource
    {
        event Action<DialoguePresentationIssueContext> PresentationIssueRaised;
    }
}
