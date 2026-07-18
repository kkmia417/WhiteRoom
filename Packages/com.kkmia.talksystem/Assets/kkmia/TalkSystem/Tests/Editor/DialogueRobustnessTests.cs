using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    /// <summary>
    /// 破損・改竄され得るセーブデータからの復元と、既知スキーマ外の CSV カラム
    /// （拡張カラム）の取り込みに関する堅牢性テスト。
    /// </summary>
    public sealed class DialogueRobustnessTests
    {
        private const string ChoiceCsv =
            "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
            "1,A,Pick,-1,,,,,Left->2?can_left|Right->3\n" +
            "2,A,Left,-1,,,,,\n" +
            "3,A,Right,-1,,,,,\n";

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

        private static DialogueRepository CreateRepository(string csv)
        {
            return new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
        }

        [Test]
        public void Restore_ChoicePendingWithoutChoices_CoercesToWaitingForInput()
        {
            // 選択肢の無い行に ChoicePending を持つセーブ（破損 or 条件変化）を復元すると、
            // Advance も SelectChoice も拒否される進行不能状態になる。休止状態へ補正されること。
            var repo = CreateRepository("Id,Speaker,Text,NextId\n1,A,Hello,-1\n");
            var session = new DialogueSession(repo);

            var restored = session.Restore(new DialogueSaveData
            {
                CurrentDialogueId = 1,
                State = DialogueSessionState.ChoicePending
            });

            Assert.IsTrue(restored);
            Assert.AreEqual(DialogueSessionState.WaitingForInput, session.State);
        }

        [Test]
        public void Restore_WaitingForInputWithChoices_CoercesToChoicePending()
        {
            // 選択肢のある行に WaitingForInput を持つセーブを復元すると、選択肢を無視して
            // NextId(-1) へ進み、誤った分岐/終了になる。ChoicePending へ補正されること。
            var repo = CreateRepository(ChoiceCsv);
            var session = new DialogueSession(repo);

            var restored = session.Restore(new DialogueSaveData
            {
                CurrentDialogueId = 1,
                State = DialogueSessionState.WaitingForInput
            });

            Assert.IsTrue(restored);
            Assert.AreEqual(DialogueSessionState.ChoicePending, session.State);
            Assert.AreEqual(2, session.CurrentChoices.Count);
        }

        [Test]
        public void Restore_UndefinedStateValue_CoercesToConsistentState()
        {
            var repo = CreateRepository("Id,Speaker,Text,NextId\n1,A,Hello,-1\n");
            var session = new DialogueSession(repo);

            var restored = session.Restore(new DialogueSaveData
            {
                CurrentDialogueId = 1,
                State = (DialogueSessionState)999
            });

            Assert.IsTrue(restored);
            Assert.AreEqual(DialogueSessionState.WaitingForInput, session.State);
        }

        [Test]
        public void Restore_NoCurrentLine_AllowsOnlyIdleOrEnded()
        {
            var repo = CreateRepository("Id,Speaker,Text,NextId\n1,A,Hello,-1\n");

            var idle = new DialogueSession(repo);
            Assert.IsTrue(idle.Restore(new DialogueSaveData
            {
                CurrentDialogueId = -1,
                State = DialogueSessionState.Idle
            }));
            Assert.AreEqual(DialogueSessionState.Idle, idle.State);

            var corrupted = new DialogueSession(repo);
            Assert.IsTrue(corrupted.Restore(new DialogueSaveData
            {
                CurrentDialogueId = -1,
                State = DialogueSessionState.WaitingForInput
            }));
            Assert.AreEqual(DialogueSessionState.Ended, corrupted.State);
        }

        [Test]
        public void Restore_ConditionsChanged_FiltersChoicesAndKeepsChoicePending()
        {
            // 保存後に条件が変わって一部の選択肢が非表示になっても、残りの選択肢で
            // ChoicePending が維持されること。
            var repo = CreateRepository(ChoiceCsv);
            var session = new DialogueSession(repo)
            {
                ConditionEvaluator = new BlockKeyConditionEvaluator("can_left")
            };

            Assert.IsTrue(session.Restore(new DialogueSaveData
            {
                CurrentDialogueId = 1,
                State = DialogueSessionState.ChoicePending
            }));

            Assert.AreEqual(DialogueSessionState.ChoicePending, session.State);
            Assert.AreEqual(1, session.CurrentChoices.Count);
            Assert.AreEqual("Right", session.CurrentChoices[0].Text);
        }

        [Test]
        public void CsvLoader_ExtraColumns_AreCapturedCaseInsensitively()
        {
            var csv =
                "Id,Speaker,Text,NextId,CameraShot,Mood\n" +
                "1,A,Hello,2,close_up,tense\n" +
                "2,A,End,-1,,relaxed\n";

            var dict = CsvLoader.ParseText<DialogueData>(csv);

            var first = dict[1];
            Assert.IsTrue(first.HasExtraColumns);
            string shot;
            Assert.IsTrue(first.TryGetExtra("camerashot", out shot), "ヘッダー名は大文字小文字を区別しない");
            Assert.AreEqual("close_up", shot);
            Assert.AreEqual("tense", first.ExtraColumns["Mood"]);

            // 空セルは取り込まれない。
            var second = dict[2];
            string missing;
            Assert.IsFalse(second.TryGetExtra("CameraShot", out missing));
            Assert.AreEqual("relaxed", second.ExtraColumns["Mood"]);
        }

        [Test]
        public void CsvLoader_KnownColumns_AreNotTreatedAsExtra()
        {
            var dict = CsvLoader.ParseText<DialogueData>(ChoiceCsv);
            var data = dict[1];

            Assert.IsFalse(data.HasExtraColumns);
            Assert.AreEqual(0, data.ExtraColumns.Count);
            string value;
            Assert.IsFalse(data.TryGetExtra(DialogueSchema.Speaker, out value));
        }

        [Test]
        public void ExtraColumns_SurviveWithResolvedText()
        {
            var csv = "Id,Speaker,Text,NextId,CameraShot\n1,A,Hello {p},-1,wide\n";
            var data = CsvLoader.ParseText<DialogueData>(csv)[1];

            var resolved = data.WithResolvedText("Hello World");

            Assert.AreEqual("Hello World", resolved.Text);
            string shot;
            Assert.IsTrue(resolved.TryGetExtra("CameraShot", out shot));
            Assert.AreEqual("wide", shot);
        }

        [Test]
        public void GetChoices_IsCachedPerData()
        {
            var data = CsvLoader.ParseText<DialogueData>(ChoiceCsv)[1];

            var first = data.GetChoices();
            var second = data.GetChoices();

            Assert.AreSame(first, second, "パース結果は行データ単位でキャッシュされる");
            Assert.AreEqual(2, first.Count);
        }

        [Test]
        public void Session_EndAndEmptyMarkers_ShareAllocationFreeResults()
        {
            var repo = CreateRepository("Id,Speaker,Text,NextId\n1,A,Hello,-1\n");
            var session = new DialogueSession(repo);
            session.Start(1);

            var markers = session.MarkProgress(session.CurrentData);
            Assert.AreEqual(0, markers.Count, "マーカー列が無い行では空リストが返る");

            session.End();
            Assert.AreEqual(0, session.CurrentChoices.Count);
        }
    }
}
