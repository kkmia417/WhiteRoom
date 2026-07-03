using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 既読行 ID の永続レジストリ。<see cref="DialogueSession.SeenLineIds"/> が 1 プレイ内の既読なのに対し、
    /// こちらはセーブを跨いだ恒久的な既読（スキップの「既読のみ」判定や未読頭出しに使う）。
    /// 永続化は <see cref="IDialogueReadStore"/> に委ねる。Unity 型に依存しないためテスト可能。
    /// </summary>
    public sealed class DialogueReadRegistry
    {
        private readonly HashSet<int> _read = new HashSet<int>();
        private readonly IDialogueReadStore _store;

        public DialogueReadRegistry(IDialogueReadStore store = null)
        {
            _store = store;
            if (_store != null)
            {
                var loaded = _store.Load();
                if (loaded != null)
                {
                    foreach (var id in loaded)
                        _read.Add(id);
                }
            }
        }

        public IReadOnlyCollection<int> ReadIds
        {
            get { return _read; }
        }

        public bool IsRead(int lineId)
        {
            return _read.Contains(lineId);
        }

        /// <summary>既読として記録する。新規に追加された場合のみ true。</summary>
        public bool MarkRead(int lineId)
        {
            return _read.Add(lineId);
        }

        public void Clear()
        {
            _read.Clear();
        }

        public void Save()
        {
            if (_store != null)
                _store.Save(_read);
        }
    }

    /// <summary>既読 ID の永続化先（PlayerPrefs など）。</summary>
    public interface IDialogueReadStore
    {
        IEnumerable<int> Load();
        void Save(IEnumerable<int> readIds);
    }
}
