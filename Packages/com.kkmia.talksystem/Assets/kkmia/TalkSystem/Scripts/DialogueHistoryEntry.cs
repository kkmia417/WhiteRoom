using System;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class DialogueHistoryEntry
    {
        public int Id;
        public string Speaker;
        public string Text;
        public string EmotionKey;
        public string TriggerKey;
        public string EventKey;
        public string Voice;
        public int Order;

        public DialogueHistoryEntry()
        {
        }

        public DialogueHistoryEntry(DialogueData data, int order)
        {
            Id = data != null ? data.Id : -1;
            Speaker = data != null ? data.Speaker : string.Empty;
            Text = data != null ? data.Text : string.Empty;
            EmotionKey = data != null ? data.EmotionKey : string.Empty;
            TriggerKey = data != null ? data.TriggerKey : string.Empty;
            EventKey = data != null ? data.EventKey : string.Empty;
            Voice = data != null ? data.Voice : string.Empty;
            Order = order;
        }
    }
}
