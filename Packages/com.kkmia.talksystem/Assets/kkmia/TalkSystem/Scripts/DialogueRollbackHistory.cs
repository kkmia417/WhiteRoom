using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 行ごとの状態スナップショット（<see cref="DialogueSaveData"/>）を積み上げ、
    /// 直前の行へ巻き戻す（ロールバック）ための容量上限つきスタック。Unity 型に依存しないためテスト可能。
    /// スタックの先頭（最後に Push したもの）が「現在の行」を表す。
    /// </summary>
    public sealed class DialogueRollbackHistory
    {
        public const int DefaultCapacity = 100;

        private readonly List<DialogueSaveData> _snapshots = new List<DialogueSaveData>();
        private readonly int _capacity;

        public DialogueRollbackHistory(int capacity = DefaultCapacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        /// <summary>積まれているスナップショット数。</summary>
        public int Count { get { return _snapshots.Count; } }

        /// <summary>巻き戻せる前の行が存在するか（現在行を含めて 2 つ以上必要）。</summary>
        public bool CanRollback { get { return _snapshots.Count > 1; } }

        /// <summary>新しい行のスナップショットを積む。容量を超えたら最古を破棄する。</summary>
        public void Push(DialogueSaveData snapshot)
        {
            if (snapshot == null) return;

            _snapshots.Add(snapshot);
            if (_snapshots.Count > _capacity)
                _snapshots.RemoveAt(0);
        }

        /// <summary>
        /// 現在行のスナップショットを捨て、直前の行のスナップショットを返す。
        /// 戻れる行が無い場合は null を返し、スタックは変化しない。
        /// </summary>
        public DialogueSaveData Rollback()
        {
            if (!CanRollback) return null;

            _snapshots.RemoveAt(_snapshots.Count - 1); // 現在行を捨てる
            return _snapshots[_snapshots.Count - 1];   // 直前行が新しい先頭
        }

        /// <summary>会話開始時などに履歴を破棄する。</summary>
        public void Clear()
        {
            _snapshots.Clear();
        }
    }
}
