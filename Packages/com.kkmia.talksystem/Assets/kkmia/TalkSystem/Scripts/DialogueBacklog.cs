using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>バックログ 1 行分の表示モデル。</summary>
    public sealed class DialogueBacklogEntry
    {
        public string Speaker;
        public string Text;
        public string VoiceKey;
        public int Order;

        public bool HasVoice { get { return !string.IsNullOrEmpty(VoiceKey); } }
    }

    /// <summary>
    /// 会話履歴（<see cref="DialogueHistoryEntry"/>）をバックログ表示モデルへ変換する純粋ロジック。
    /// Unity 型に依存しないためテスト可能。表示順（新しい順／古い順）を選べる。
    /// </summary>
    public static class DialogueBacklog
    {
        public static IReadOnlyList<DialogueBacklogEntry> Build(
            IReadOnlyList<DialogueHistoryEntry> history, bool newestFirst = false)
        {
            var entries = new List<DialogueBacklogEntry>();
            if (history == null) return entries;

            for (var i = 0; i < history.Count; i++)
            {
                var source = history[i];
                if (source == null) continue;

                entries.Add(new DialogueBacklogEntry
                {
                    Speaker = source.Speaker,
                    Text = source.Text,
                    VoiceKey = source.Voice,
                    Order = source.Order
                });
            }

            if (newestFirst)
                entries.Reverse();

            return entries;
        }

        public static bool ReplayVoice(DialogueBacklogEntry entry, IDialogueAudioPlayer player)
        {
            return entry != null && ReplayVoice(entry.VoiceKey, player);
        }

        public static bool ReplayVoice(string voiceKey, IDialogueAudioPlayer player)
        {
            if (player == null || string.IsNullOrEmpty(voiceKey))
                return false;

            player.PlayVoice(voiceKey);
            return true;
        }
    }
}
