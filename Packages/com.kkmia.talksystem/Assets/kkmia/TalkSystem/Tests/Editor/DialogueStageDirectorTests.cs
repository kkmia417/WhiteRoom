using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueStageDirectorTests
    {
        private sealed class RecordingStageView : IDialogueStageView
        {
            public readonly List<string> Calls = new List<string>();

            public void SetBackground(string backgroundKey, bool clear, string transition, float duration)
            {
                Calls.Add(clear
                    ? "bg:clear:" + transition + ":" + duration
                    : "bg:" + backgroundKey + ":" + transition + ":" + duration);
            }

            public void SetCharacter(string slot, string characterKey, string expression, string animation)
            {
                Calls.Add("show:" + slot + ":" + characterKey + ":" + expression + ":" + animation);
            }

            public void RemoveCharacter(string slot, string characterKey, string animation)
            {
                Calls.Add("hide:" + slot + ":" + characterKey + ":" + animation);
            }

            public void ClearCharacters()
            {
                Calls.Add("clearAll");
            }
        }

        private static DialogueData BuildRow(string background, string characters)
        {
            // DialogueData の setter は internal のため、既存テストと同様に CSV 経由で構築する。
            var csv = "Id,Speaker,Text,NextId,Background,Characters\n" +
                      "1,A,Hi,-1," + Quote(background) + "," + Quote(characters) + "\n";
            return CsvLoader.ParseText<DialogueData>(csv)[1];
        }

        private static string Quote(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        [Test]
        public void StageState_DefaultSlot_UsedWhenSlotOmitted()
        {
            var state = new DialogueStageState();

            var ops = state.Apply(DialogueStageDirective.ParseList("Alice"));

            Assert.AreEqual(1, ops.Count);
            Assert.AreEqual(DialogueStageOperationKind.Show, ops[0].Kind);
            Assert.AreEqual(DialogueStageSlot.Center, ops[0].Slot);
            Assert.AreEqual("Alice", state.Occupancy[DialogueStageSlot.Center]);
        }

        [Test]
        public void StageState_Exit_RemovesFromOccupiedSlot()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left"));

            var ops = state.Apply(DialogueStageDirective.ParseList("-Alice"));

            Assert.AreEqual(DialogueStageOperationKind.Hide, ops[0].Kind);
            Assert.AreEqual("left", ops[0].Slot);
            Assert.IsFalse(state.Occupancy.ContainsKey("left"));
        }

        [Test]
        public void StageState_ClearAll_EmptiesOccupancy()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left|Bob@right"));

            var ops = state.Apply(DialogueStageDirective.ParseList("*"));

            Assert.AreEqual(1, ops.Count);
            Assert.AreEqual(DialogueStageOperationKind.ClearAll, ops[0].Kind);
            Assert.AreEqual(0, state.Occupancy.Count);
        }

        [Test]
        public void StageState_MovingCharacter_FreesPreviousSlot()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left"));

            state.Apply(DialogueStageDirective.ParseList("Alice@right"));

            Assert.IsFalse(state.Occupancy.ContainsKey("left"));
            Assert.AreEqual("Alice", state.Occupancy["right"]);
        }

        [Test]
        public void StageState_MovingCharacter_EmitsHideBeforeShowAndCarriesExpression()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left:smile"));

            var ops = state.Apply(DialogueStageDirective.ParseList("Alice@right#slide"));

            Assert.AreEqual(2, ops.Count);
            Assert.AreEqual(DialogueStageOperationKind.Hide, ops[0].Kind);
            Assert.AreEqual("left", ops[0].Slot);
            Assert.AreEqual("Alice", ops[0].CharacterKey);
            Assert.AreEqual(DialogueStageOperationKind.Show, ops[1].Kind);
            Assert.AreEqual("right", ops[1].Slot);
            Assert.AreEqual("smile", ops[1].Expression);
            Assert.AreEqual("slide", ops[1].Animation);
        }

        [Test]
        public void StageState_ReplacingOccupiedSlot_HidesDisplacedCharacter()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@left|Bob@right"));

            var ops = state.Apply(DialogueStageDirective.ParseList("Alice@right"));

            Assert.AreEqual(3, ops.Count);
            Assert.AreEqual("hide:left:Alice", Describe(ops[0]));
            Assert.AreEqual("hide:right:Bob", Describe(ops[1]));
            Assert.AreEqual("show:right:Alice", Describe(ops[2]));
            Assert.IsFalse(state.Occupancy.ContainsKey("left"));
            Assert.AreEqual("Alice", state.Occupancy["right"]);
        }

        [Test]
        public void StageState_ReShow_KeepsExistingSlotWhenOmitted()
        {
            var state = new DialogueStageState();
            state.Apply(DialogueStageDirective.ParseList("Alice@right"));

            var ops = state.Apply(DialogueStageDirective.ParseList("Alice:happy"));

            Assert.AreEqual("right", ops[0].Slot);
            Assert.AreEqual("happy", ops[0].Expression);
        }

        [Test]
        public void Director_AppliesBackgroundThenCharacters()
        {
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);

            director.Apply(BuildRow("forest#fade:1.0", "Alice@left:smile"));

            Assert.AreEqual(2, view.Calls.Count);
            Assert.AreEqual("bg:forest:fade:1", view.Calls[0]);
            Assert.AreEqual("show:left:Alice:smile:", view.Calls[1]);
        }

        [Test]
        public void Director_NoPresentationFields_DoesNothing()
        {
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);

            director.Apply(BuildRow(string.Empty, string.Empty));

            Assert.IsEmpty(view.Calls);
        }

        [Test]
        public void Director_Clear_ResetsStateAndView()
        {
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);
            director.Apply(BuildRow(string.Empty, "Alice@left"));
            view.Calls.Clear();

            director.Clear();

            Assert.IsTrue(view.Calls.Contains("clearAll"));
            Assert.IsTrue(view.Calls.Any(c => c.StartsWith("bg:clear")));
            Assert.AreEqual(0, director.State.Occupancy.Count);
        }

        [Test]
        public void Director_RestoreSnapshot_RestoresWithoutTransitionsInStableSlotOrder()
        {
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);
            director.Apply(BuildRow("forest#fade:1", "Bob@right:angry|Alice@left:smile"));
            var snapshot = director.CaptureSnapshot();
            view.Calls.Clear();

            director.RestoreSnapshot(snapshot);

            CollectionAssert.AreEqual(new[]
            {
                "clearAll",
                "bg:forest::0",
                "show:left:Alice:smile:",
                "show:right:Bob:angry:"
            }, view.Calls);
        }

        private static string Describe(DialogueStageOperation op)
        {
            return op.Kind == DialogueStageOperationKind.Show
                ? "show:" + op.Slot + ":" + op.CharacterKey
                : "hide:" + op.Slot + ":" + op.CharacterKey;
        }
    }
}
