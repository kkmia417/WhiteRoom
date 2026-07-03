namespace kkmia.TalkSystem
{
    public enum DialogueSessionState
    {
        Idle,
        ShowingLine,
        Typing,
        WaitingForInput,
        ChoicePending,
        Delaying,
        Ended
    }
}
