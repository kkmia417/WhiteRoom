using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueSessionTests
    {
        [Test]
        public void Session_StartAdvanceEnd_TracksStateAndSeenLines()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,2\n2,A,End,-1\n").Values);
            var session = new DialogueSession(repo);

            Assert.IsTrue(session.Start(1));
            Assert.AreEqual(DialogueSessionState.ShowingLine, session.State);
            session.MarkLineReady();
            Assert.AreEqual(DialogueSessionState.WaitingForInput, session.State);

            Assert.IsTrue(session.Advance());
            Assert.AreEqual(2, session.CurrentData.Id);
            Assert.AreEqual(2, session.SeenLineIds.Count);

            session.MarkLineReady();
            Assert.IsFalse(session.Advance());
            Assert.AreEqual(DialogueSessionState.Ended, session.State);
        }

        [Test]
        public void Session_RevisitsConditionFailingNode_ContinuesWithoutEnding()
        {
            // 2 は条件不成立で常にスキップされ、3 から再び 2 へ戻る構造。
            // _skipGuard がセッション全体で共有されていると、2 回目の再訪で
            // 会話が誤って終了してしまう（regression: issue #37）。
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Hello,2,,,,,\n" +
                      "2,A,Gated,3,,,gate,,\n" +
                      "3,A,Middle,2,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo)
            {
                ConditionEvaluator = new BlockKeyConditionEvaluator("gate")
            };

            Assert.IsTrue(session.Start(1));

            // 1 回目: 2 をスキップして 3 へ。
            session.MarkLineReady();
            Assert.IsTrue(session.Advance());
            Assert.AreEqual(3, session.CurrentData.Id);

            // 2 回目: 再び 2 をスキップして 3 へ戻る。終了してはならない。
            session.MarkLineReady();
            Assert.IsTrue(session.Advance());
            Assert.AreEqual(3, session.CurrentData.Id);
            Assert.AreNotEqual(DialogueSessionState.Ended, session.State);
        }

        [Test]
        public void Session_SkipsLongConditionFailingChain_WithoutRecursion()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices");

            const int skippedLines = 5000;
            for (var i = 1; i <= skippedLines; i++)
                builder.Append(i).Append(",A,Skip,").Append(i + 1).Append(",,,gate,,").AppendLine();

            builder.Append(skippedLines + 1).AppendLine(",A,Visible,-1,,,,,");

            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(builder.ToString()).Values);
            var session = new DialogueSession(repo)
            {
                ConditionEvaluator = new BlockKeyConditionEvaluator("gate")
            };

            Assert.IsTrue(session.Start(1));
            Assert.AreEqual(skippedLines + 1, session.CurrentData.Id);
            CollectionAssert.AreEqual(new[] { skippedLines + 1 }, session.SeenLineIds);
        }

        [Test]
        public void Session_SelectChoice_AdvancesToSelectedTarget()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Choose,-1,,,,,Left->2|Right->3\n" +
                      "2,A,Left,-1,,,,,\n" +
                      "3,A,Right,-1,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo);

            session.Start(1);
            session.MarkLineReady();
            Assert.AreEqual(DialogueSessionState.ChoicePending, session.State);

            Assert.IsTrue(session.SelectChoice(1));
            Assert.AreEqual(3, session.CurrentData.Id);
        }

        [Test]
        public void Session_Advance_RejectsTypingChoicePendingAndEndedStates()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Hello,2,,,,,\n" +
                      "2,A,End,-1,,,,,\n" +
                      "3,A,Choose,-1,,,,,Go->2\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo);

            Assert.IsTrue(session.Start(1));
            Assert.IsFalse(session.Advance());
            Assert.AreEqual(1, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.ShowingLine, session.State);

            session.MarkTyping();
            Assert.IsFalse(session.Advance());
            Assert.AreEqual(1, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.Typing, session.State);

            Assert.IsTrue(session.Start(3));
            session.MarkLineReady();
            Assert.AreEqual(DialogueSessionState.ChoicePending, session.State);
            Assert.IsFalse(session.Advance());
            Assert.AreEqual(3, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.ChoicePending, session.State);

            session.End();
            Assert.IsFalse(session.Advance());
            Assert.IsNull(session.CurrentData);
            Assert.AreEqual(DialogueSessionState.Ended, session.State);
        }

        [Test]
        public void Session_SelectChoice_RejectsNonChoiceStates()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                "1,A,Choose,-1,,,,,Go->2\n" +
                "2,A,End,-1,,,,,\n").Values);
            var session = new DialogueSession(repo);

            Assert.IsTrue(session.Start(1));
            Assert.IsFalse(session.SelectChoice(0));
            Assert.AreEqual(1, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.ShowingLine, session.State);

            session.MarkTyping();
            Assert.IsFalse(session.SelectChoice(0));
            Assert.AreEqual(1, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.Typing, session.State);

            Assert.IsTrue(session.Start(2));
            session.MarkLineReady();
            Assert.IsFalse(session.SelectChoice(0));
            Assert.AreEqual(2, session.CurrentData.Id);
            Assert.AreEqual(DialogueSessionState.WaitingForInput, session.State);

            session.End();
            Assert.IsFalse(session.SelectChoice(0));
            Assert.IsNull(session.CurrentData);
            Assert.AreEqual(DialogueSessionState.Ended, session.State);
        }

        [Test]
        public void Session_SelectChoice_RecordsStableRawChoiceAndDestination()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Choose,-1,,,,,Hidden->2 ?hidden|Shown->3\n" +
                      "2,A,Hidden,-1,,,,,\n" +
                      "3,A,Shown,-1,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo)
            {
                ConditionEvaluator = new BlockKeyConditionEvaluator("hidden")
            };

            session.Start(1);
            session.MarkLineReady();

            Assert.AreEqual(1, session.CurrentChoices.Count, "条件で非表示の選択肢は UI index に含まれない");
            Assert.IsTrue(session.SelectChoice(0));

            Assert.AreEqual(3, session.CurrentData.Id);
            Assert.AreEqual(1, session.ChoiceRecords.Count);
            Assert.AreEqual(1, session.ChoiceRecords[0].RawChoiceIndex, "フィルタ後 index ではなく CSV 上の raw index を保存する");
            Assert.AreEqual(3, session.ChoiceRecords[0].NextId);
            Assert.AreEqual("Shown", session.ChoiceRecords[0].Text);
        }

        [Test]
        public void Session_Capture_StoresChoiceRecordsWithoutPersistingFilteredIndexes()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Choose,-1,,,,,Left->2|Right->3\n" +
                      "2,A,Left,-1,,,,,\n" +
                      "3,A,Right,-1,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo);

            session.Start(1);
            session.MarkLineReady();
            Assert.IsTrue(session.SelectChoice(1));

            var save = session.Capture();

            Assert.AreEqual(1, save.ChoiceRecords.Count);
            Assert.AreEqual(1, save.ChoiceRecords[0].RawChoiceIndex);
            Assert.AreEqual(3, save.ChoiceRecords[0].NextId);
            CollectionAssert.IsEmpty(save.ChoiceHistory, "schema 2 saves do not persist legacy filtered indexes");
        }

        [Test]
        public void Session_Restore_ChoiceRecordKeepsSelectedDestinationAfterConditionsChange()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Choose,-1,,,,,Gate->2 ?gate|Free->3\n" +
                      "2,A,Gate,-1,,,,,\n" +
                      "3,A,Free,-1,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var source = new DialogueSession(repo);
            source.Start(1);
            source.MarkLineReady();
            Assert.IsTrue(source.SelectChoice(1));

            var save = source.Capture();
            var restored = new DialogueSession(repo)
            {
                ConditionEvaluator = new BlockKeyConditionEvaluator("gate")
            };

            Assert.IsTrue(restored.Restore(save));
            Assert.AreEqual(1, restored.ChoiceRecords.Count);
            Assert.AreEqual(1, restored.ChoiceRecords[0].RawChoiceIndex);
            Assert.AreEqual(3, restored.ChoiceRecords[0].NextId);
            Assert.AreEqual("Free", restored.ChoiceRecords[0].Text);
        }

        [Test]
        public void Session_Restore_PreservesHistoryAndSavedState()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,2\n2,A,End,-1\n").Values);

            var source = new DialogueSession(repo);
            source.Start(1);
            source.RecordDisplayedLine(source.CurrentData);
            source.MarkLineReady();
            Assert.AreEqual(DialogueSessionState.WaitingForInput, source.State);

            var save = source.Capture();
            Assert.AreEqual(1, save.History.Count);
            Assert.AreEqual(DialogueSessionState.WaitingForInput, save.State);

            var restored = new DialogueSession(repo);
            Assert.IsTrue(restored.Restore(save));

            Assert.AreEqual(1, restored.CurrentData.Id, "保存時の現在行が復元される");
            Assert.AreEqual(1, restored.History.Count, "復元で履歴が増えない");
            Assert.AreEqual(DialogueSessionState.WaitingForInput, restored.State,
                "保存時の State が ShowingLine に潰されず維持される");
        }

        [Test]
        public void Session_Restore_LegacyChoiceHistoryAsLossyRecords()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,-1\n").Values);
            var save = new DialogueSaveData
            {
                CurrentDialogueId = 1,
                State = DialogueSessionState.WaitingForInput,
                ChoiceRecords = null,
                ChoiceHistory = new System.Collections.Generic.List<int> { 2 }
            };

            var restored = new DialogueSession(repo);

            Assert.IsTrue(restored.Restore(save));
            Assert.AreEqual(1, restored.ChoiceRecords.Count);
            Assert.AreEqual(2, restored.ChoiceRecords[0].RawChoiceIndex);
            Assert.AreEqual(-1, restored.ChoiceRecords[0].LineId);
            Assert.AreEqual(-1, restored.ChoiceRecords[0].NextId);
        }

        [Test]
        public void Session_Restore_DeduplicatesSeenLines()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,2\n2,A,End,-1\n").Values);
            var save = new DialogueSaveData
            {
                CurrentDialogueId = 2,
                State = DialogueSessionState.WaitingForInput,
                SeenLineIds = new List<int> { 1, 1, 2, 2 }
            };

            var restored = new DialogueSession(repo);

            Assert.IsTrue(restored.Restore(save));
            CollectionAssert.AreEqual(new[] { 1, 2 }, restored.SeenLineIds);

            var captured = restored.Capture();
            CollectionAssert.AreEqual(new[] { 1, 2 }, captured.SeenLineIds);
        }

        [Test]
        public void Session_StartAgain_ResetsHistorySeenAndChoiceHistory()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Choose,-1,,,,,Left->2|Right->3\n" +
                      "2,A,Left,-1,,,,,\n" +
                      "3,A,Right,-1,,,,,\n";
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
            var session = new DialogueSession(repo);

            // 1 回目の会話: 行を表示・既読化し、選択肢を選ぶ。
            session.Start(1);
            session.RecordDisplayedLine(session.CurrentData);
            session.MarkLineReady();
            Assert.IsTrue(session.SelectChoice(0)); // 1 -> 2
            session.RecordDisplayedLine(session.CurrentData);

            Assert.AreEqual(1, session.ChoiceRecords.Count);
            Assert.AreEqual(2, session.History.Count);
            Assert.AreEqual(2, session.SeenLineIds.Count);

            // 2 回目の Start で前回会話の状態が残ってはならない。
            session.Start(1);
            session.RecordDisplayedLine(session.CurrentData);

            Assert.AreEqual(0, session.ChoiceRecords.Count, "選択履歴は新会話で空から始まる");
            Assert.AreEqual(1, session.History.Count, "履歴は新会話の表示行だけを含む");
            CollectionAssert.AreEqual(new[] { 1 }, session.SeenLineIds, "既読は新会話の表示済み行だけ");
        }

        private sealed class BlockKeyConditionEvaluator : IDialogueConditionEvaluator
        {
            private readonly string _blockedKey;

            public BlockKeyConditionEvaluator(string blockedKey)
            {
                _blockedKey = blockedKey;
            }

            public bool Evaluate(string conditionKey, DialogueData data)
            {
                return conditionKey != _blockedKey;
            }
        }
    }
}
