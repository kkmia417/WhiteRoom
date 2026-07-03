using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueRollbackTests
    {
        [Test]
        public void History_CanRollback_RequiresAtLeastTwoSnapshots()
        {
            var history = new DialogueRollbackHistory();
            Assert.IsFalse(history.CanRollback);

            history.Push(Snapshot(1));
            Assert.IsFalse(history.CanRollback, "現在行だけでは戻れない");

            history.Push(Snapshot(2));
            Assert.IsTrue(history.CanRollback);
        }

        [Test]
        public void History_Rollback_DropsCurrentAndReturnsPrevious()
        {
            var history = new DialogueRollbackHistory();
            history.Push(Snapshot(1));
            history.Push(Snapshot(2));
            history.Push(Snapshot(3));

            var previous = history.Rollback();
            Assert.AreEqual(2, previous.CurrentDialogueId, "直前行のスナップショットを返す");
            Assert.AreEqual(2, history.Count, "現在行が捨てられる");

            var previous2 = history.Rollback();
            Assert.AreEqual(1, previous2.CurrentDialogueId);
            Assert.IsFalse(history.CanRollback, "先頭行ではこれ以上戻れない");
        }

        [Test]
        public void History_Rollback_ReturnsNullWhenCannot()
        {
            var history = new DialogueRollbackHistory();
            Assert.IsNull(history.Rollback());

            history.Push(Snapshot(1));
            Assert.IsNull(history.Rollback(), "現在行だけのときは null");
            Assert.AreEqual(1, history.Count, "戻れないときスタックは変化しない");
        }

        [Test]
        public void History_Push_DropsOldestBeyondCapacity()
        {
            var history = new DialogueRollbackHistory(capacity: 2);
            history.Push(Snapshot(1));
            history.Push(Snapshot(2));
            history.Push(Snapshot(3)); // 1 が破棄される

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(2, history.Rollback().CurrentDialogueId, "最古(1)が捨てられ、戻り先は 2");
        }

        [Test]
        public void History_Clear_EmptiesStack()
        {
            var history = new DialogueRollbackHistory();
            history.Push(Snapshot(1));
            history.Push(Snapshot(2));

            history.Clear();

            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.CanRollback);
        }

        private static DialogueSaveData Snapshot(int id)
        {
            return new DialogueSaveData { CurrentDialogueId = id };
        }
    }
}
