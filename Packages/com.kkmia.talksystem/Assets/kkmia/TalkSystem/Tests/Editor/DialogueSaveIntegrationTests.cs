using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueSaveIntegrationTests
    {
        private sealed class RecordingStageView : IDialogueStageView
        {
            public readonly List<string> Calls = new List<string>();

            public void SetBackground(string backgroundKey, bool clear, string transition, float duration)
            {
                Calls.Add(clear ? "bg:clear" : "bg:" + backgroundKey);
            }

            public void SetCharacter(string slot, string characterKey, string expression, string animation)
            {
                Calls.Add("show:" + slot + ":" + characterKey + ":" + expression);
            }

            public void RemoveCharacter(string slot, string characterKey, string animation)
            {
                Calls.Add("hide:" + slot);
            }

            public void ClearCharacters()
            {
                Calls.Add("clear");
            }
        }

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public void PlayBgm(string bgmKey, bool stop, string transition, float duration) { }
            public void PlaySe(string seKey) { }
            public void PlayVoice(string voiceKey) { }
            public void StopVoice() { }
            public void StopAll() { }
        }

        private static DialogueData Row(string columns, string values)
        {
            var csv = "Id,Speaker,Text,NextId," + columns + "\n1,A,Hi,-1," + values + "\n";
            return CsvLoader.ParseText<DialogueData>(csv)[1];
        }

        [Test]
        public void StageState_Snapshot_CapturesSlotKeyAndExpression()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left:smile|Bob@right:angry"));

            var snapshot = state.Snapshot().OrderBy(c => c.slot).ToList();

            Assert.AreEqual(2, snapshot.Count);
            var left = snapshot.First(c => c.slot == "left");
            Assert.AreEqual("Alice", left.characterKey);
            Assert.AreEqual("smile", left.expression);
        }

        [Test]
        public void StageState_ReShowWithoutExpression_KeepsExpression()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left:smile"));
            state.Apply(DialogueStageDirective.ParseList("Alice"));

            var entry = state.Snapshot().Single();
            Assert.AreEqual("left", entry.slot);
            Assert.AreEqual("smile", entry.expression);
        }

        [Test]
        public void StageState_RestoreSnapshot_RebuildsOccupancy()
        {
            var state = new DialogueStageState();
            var snapshot = new List<DialogueStageCharacterSnapshot>
            {
                new DialogueStageCharacterSnapshot("center", "Carol", "happy")
            };

            state.RestoreSnapshot(snapshot);

            Assert.AreEqual("Carol", state.Occupancy["center"]);
            Assert.AreEqual("happy", state.Snapshot().Single().expression);
        }

        [Test]
        public void StageDirector_CaptureThenRestore_ReappliesBackgroundAndCharacters()
        {
            var captureView = new RecordingStageView();
            var director = new DialogueStageDirector(captureView);
            director.Apply(Row("Background,Characters", "forest,Alice@left:smile"));

            var snapshot = director.CaptureSnapshot();
            Assert.AreEqual("forest", snapshot.backgroundKey);
            Assert.AreEqual(1, snapshot.characters.Count);

            var restoreView = new RecordingStageView();
            var director2 = new DialogueStageDirector(restoreView);
            director2.RestoreSnapshot(snapshot);

            Assert.IsTrue(restoreView.Calls.Contains("bg:forest"));
            Assert.IsTrue(restoreView.Calls.Contains("show:left:Alice:smile"));
        }

        [Test]
        public void StageDirector_CaptureAfterClear_HasNoBackground()
        {
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);
            director.Apply(Row("Background,Characters", "forest,Alice@left"));
            director.Clear();

            var snapshot = director.CaptureSnapshot();
            Assert.AreEqual(string.Empty, snapshot.backgroundKey);
            Assert.AreEqual(0, snapshot.characters.Count);
        }

        [Test]
        public void AudioDirector_TracksCurrentBgm_AndStopResets()
        {
            var director = new DialogueAudioDirector(new RecordingAudioPlayer());

            director.Apply(Row("Bgm", "theme"));
            Assert.AreEqual("theme", director.CurrentBgmKey);

            director.Apply(Row("Bgm", "stop"));
            Assert.AreEqual(string.Empty, director.CurrentBgmKey);

            director.Apply(Row("Bgm", "battle"));
            director.StopAll();
            Assert.AreEqual(string.Empty, director.CurrentBgmKey);
        }
    }
}
