using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomBoundaryNavigationPlayModeTests
    {
        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            private readonly Dictionary<int, DialogueSaveSlot> _slots = new Dictionary<int, DialogueSaveSlot>();
            public bool TryLoad(int slot, out DialogueSaveSlot data) => _slots.TryGetValue(slot, out data);
            public void Save(DialogueSaveSlot slot) => _slots[slot.SlotIndex] = slot;
            public void Delete(int slot) => _slots.Remove(slot);
            public bool Exists(int slot) => _slots.ContainsKey(slot);
            public IEnumerable<int> ListSlots() => _slots.Keys;
            public byte[] LoadThumbnail(int slot) => null;
            public void SaveThumbnail(int slot, byte[] pngBytes) { }
        }

        private sealed class PresentationContributor : IDialogueSaveContributor
        {
            public int Cue;
            public void Capture(DialogueSaveData data) => data.SetExtra("test.presentation", Cue.ToString());
            public void Restore(DialogueSaveData data)
            {
                string value;
                Cue = data.TryGetExtra("test.presentation", out value) ? int.Parse(value) : -1;
            }
        }

        private sealed class ToggleConditionEvaluator : IDialogueConditionEvaluator
        {
            public bool Allowed = true;
            public bool Evaluate(string conditionKey, DialogueData data) => Allowed;
        }

        [UnityTest]
        public IEnumerator ReachedBranchSupportsAllFourJumpsAndSurvivesSaveLoad()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;

            var managerObject = new GameObject(
                "BoundaryNavigationManager",
                typeof(DialogueManager),
                typeof(DialogueSaveSystem));
            var viewObject = new GameObject(
                "BoundaryNavigationView",
                typeof(RectTransform),
                typeof(DialogueView));
            var manager = managerObject.GetComponent<DialogueManager>();
            var saveSystem = managerObject.GetComponent<DialogueSaveSystem>();
            var view = viewObject.GetComponent<DialogueView>();
            object navigation = null;

            try
            {
                manager.SetView(view);
                saveSystem.SetStorage(new MemoryStorage());
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,Choices,ChapterKey\n" +
                    "1,Narrator,Scene A,2,,chapter_a\n" +
                    "2,Narrator,Choice A,-1,Left->3|Right->30,\n" +
                    "3,Narrator,Scene B,4,,chapter_b\n" +
                    "4,Narrator,Bridge,5,,\n" +
                    "5,Narrator,Choice B,-1,Continue->6,\n" +
                    "6,Narrator,Scene C,-1,,chapter_c\n" +
                    "30,Narrator,Other branch,-1,,chapter_other\n")));
                yield return null;
                yield return null;

                var presentation = new PresentationContributor();
                manager.LineStarted += context => presentation.Cue = context.Data.Id * 10;
                saveSystem.RegisterContributor(presentation);

                var serviceType = RequireType("WhiteRoom.Novel.DialogueBoundaryNavigationService, Assembly-CSharp");
                navigation = Activator.CreateInstance(serviceType, manager, saveSystem, null);
                saveSystem.RegisterContributor((IDialogueSaveContributor)navigation);
                Invoke(navigation, "Attach");

                var lineStarted = 0;
                manager.LineStarted += _ => lineStarted++;
                manager.StartDialogue(1);
                yield return null;
                manager.RequestNext(); // choice 2
                yield return null;
                InvokePrivate(view, "SelectChoice", 0); // scene 3
                yield return null;
                manager.RequestNext(); // bridge 4
                yield return null;
                manager.RequestNext(); // choice 5
                yield return null;
                InvokePrivate(view, "SelectChoice", 0); // scene 6
                yield return null;

                Assert.That(manager.CurrentData.Id, Is.EqualTo(6));
                Assert.That(GetProperty<int>(navigation, "ReachedBoundaryCount"), Is.EqualTo(5));
                var startedBeforeJumps = lineStarted;

                AssertJump(navigation, "Choice", "Previous", 5, 50, manager, presentation);
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.ChoicePending));
                AssertJump(navigation, "Scene", "Previous", 3, 30, manager, presentation);
                AssertJump(navigation, "Choice", "Next", 5, 50, manager, presentation);
                AssertJump(navigation, "Scene", "Next", 6, 60, manager, presentation);

                Assert.That(lineStarted, Is.EqualTo(startedBeforeJumps),
                    "Snapshot restore must not replay LineStarted side effects.");
                Assert.That(manager.History.Count, Is.GreaterThan(0));

                Assert.That(saveSystem.Save(7, false, "navigation"), Is.Not.Null);
                Invoke(navigation, "Reset");
                Assert.That(GetProperty<int>(navigation, "ReachedBoundaryCount"), Is.Zero);
                Assert.That(saveSystem.Load(7), Is.True);
                Assert.That(GetProperty<int>(navigation, "ReachedBoundaryCount"), Is.EqualTo(5));
                Assert.That((bool)Invoke(
                    navigation,
                    "CanJump",
                    EnumValue("DialogueBoundaryKind", "Choice"),
                    EnumValue("DialogueBoundaryDirection", "Previous")), Is.True);

                AssertJump(navigation, "Scene", "Previous", 3, 30, manager, presentation);
                manager.RequestNext();
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(4));
                Assert.That((bool)Invoke(
                    navigation,
                    "CanJump",
                    EnumValue("DialogueBoundaryKind", "Scene"),
                    EnumValue("DialogueBoundaryDirection", "Next")), Is.False,
                    "normal progress after a backward jump must truncate the old forward tail");
            }
            finally
            {
                (navigation as IDisposable)?.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(viewObject);
            }
        }

        [UnityTest]
        public IEnumerator ConditionMissingTargetAndCycleReturnClassifiedFailures()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;

            var managerObject = new GameObject(
                "BoundaryFailureManager",
                typeof(DialogueManager),
                typeof(DialogueSaveSystem));
            var viewObject = new GameObject(
                "BoundaryFailureView",
                typeof(RectTransform),
                typeof(DialogueView));
            var manager = managerObject.GetComponent<DialogueManager>();
            var saveSystem = managerObject.GetComponent<DialogueSaveSystem>();
            var view = viewObject.GetComponent<DialogueView>();
            object navigation = null;

            try
            {
                manager.SetView(view);
                saveSystem.SetStorage(new MemoryStorage());
                var conditions = new ToggleConditionEvaluator();
                manager.SetConditionEvaluator(conditions);
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,Choices,ChapterKey\n" +
                    "1,Narrator,Conditional choice,-1,Continue->2 ?allowed,\n" +
                    "2,Narrator,Reached scene,-1,,chapter_reached\n")));
                yield return null;
                yield return null;

                var serviceType = RequireType("WhiteRoom.Novel.DialogueBoundaryNavigationService, Assembly-CSharp");
                navigation = Activator.CreateInstance(serviceType, manager, saveSystem, null);
                saveSystem.RegisterContributor((IDialogueSaveContributor)navigation);
                Invoke(navigation, "Attach");

                manager.StartDialogue(1);
                yield return null;
                AssertJumpStatus(navigation, "Choice", "Previous", "NoTarget");
                InvokePrivate(view, "SelectChoice", 0);
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(2));

                conditions.Allowed = false;
                AssertJumpStatus(navigation, "Choice", "Previous", "ConditionNotSatisfied");
                Assert.That(manager.CurrentData.Id, Is.EqualTo(2), "failed condition must roll back");

                manager.EndDialogue();
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,Choices,ChapterKey\n" +
                    "2,Narrator,Only remaining row,-1,,chapter_reached\n")));
                yield return null;
                yield return null;
                AssertJumpStatus(navigation, "Choice", "Previous", "MissingTarget");

                Invoke(navigation, "Reset");
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,Choices,ChapterKey\n" +
                    "10,Narrator,Cycle A,11,,cycle_a\n" +
                    "11,Narrator,Cycle B,10,,cycle_b\n")));
                yield return null;
                yield return null;
                manager.StartDialogue(10);
                yield return null;
                manager.RequestNext();
                yield return null;
                manager.RequestNext();
                yield return null;

                AssertJumpStatus(navigation, "Scene", "Previous", "Success");
                Assert.That(manager.CurrentData.Id, Is.EqualTo(11));
                AssertJumpStatus(navigation, "Scene", "Previous", "CycleDetected");
                Assert.That(manager.CurrentData.Id, Is.EqualTo(11));
            }
            finally
            {
                (navigation as IDisposable)?.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(viewObject);
            }
        }

        private static void AssertJump(
            object navigation,
            string kind,
            string direction,
            int expectedDialogueId,
            int expectedCue,
            DialogueManager manager,
            PresentationContributor presentation)
        {
            var result = Invoke(
                navigation,
                "Jump",
                EnumValue("DialogueBoundaryKind", kind),
                EnumValue("DialogueBoundaryDirection", direction));
            Assert.That(GetProperty<object>(result, "Status").ToString(), Is.EqualTo("Success"));
            Assert.That(manager.CurrentData.Id, Is.EqualTo(expectedDialogueId));
            Assert.That(presentation.Cue, Is.EqualTo(expectedCue));
        }

        private static void AssertJumpStatus(
            object navigation,
            string kind,
            string direction,
            string expectedStatus)
        {
            var result = Invoke(
                navigation,
                "Jump",
                EnumValue("DialogueBoundaryKind", kind),
                EnumValue("DialogueBoundaryDirection", direction));
            Assert.That(GetProperty<object>(result, "Status").ToString(), Is.EqualTo(expectedStatus));
        }

        private static object EnumValue(string typeName, string value)
        {
            var type = RequireType("WhiteRoom.Novel." + typeName + ", Assembly-CSharp");
            return Enum.Parse(type, value);
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Array.ConvertAll(arguments, argument => argument.GetType()),
                null);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }
    }
}
