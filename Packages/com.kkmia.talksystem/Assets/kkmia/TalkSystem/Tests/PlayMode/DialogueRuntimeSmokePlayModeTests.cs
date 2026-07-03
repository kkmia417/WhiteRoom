using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueRuntimeSmokePlayModeTests
    {
        private sealed class RuntimeHarness
        {
            public GameObject Root;
            public DialogueManager Manager;
            public DialogueView View;
        }

        private sealed class ManualInputSource : MonoBehaviour, IDialogueInputSource
        {
            public event Action<DialogueInputAction> InputReceived;

            public void Raise(DialogueInputAction action)
            {
                if (InputReceived != null)
                    InputReceived(action);
            }
        }

        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            private readonly Dictionary<int, DialogueSaveSlot> _slots = new Dictionary<int, DialogueSaveSlot>();

            public bool TryLoad(int slot, out DialogueSaveSlot data)
            {
                return _slots.TryGetValue(slot, out data);
            }

            public void Save(DialogueSaveSlot slot)
            {
                _slots[slot.SlotIndex] = slot;
            }

            public void Delete(int slot)
            {
                _slots.Remove(slot);
            }

            public bool Exists(int slot)
            {
                return _slots.ContainsKey(slot);
            }

            public IEnumerable<int> ListSlots()
            {
                return new List<int>(_slots.Keys);
            }

            public byte[] LoadThumbnail(int slot)
            {
                return null;
            }

            public void SaveThumbnail(int slot, byte[] pngBytes)
            {
            }
        }

        [UnityTest]
        public IEnumerator ManagerViewInput_NextAndChoiceAdvance()
        {
            yield return DestroyExistingManager();

            var harness = CreateHarness(
                "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                "1,A,Start,2,,,,,\n" +
                "2,A,Pick,-1,,,,,Left->3|Right->4\n" +
                "3,A,Left,-1,,,,,\n" +
                "4,A,Right,-1,,,,,\n");

            var input = harness.View.gameObject.AddComponent<ManualInputSource>();
            var router = harness.View.gameObject.AddComponent<DialogueInputRouter>();
            SetField(router, "inputSourceComponent", input);
            SetField(router, "playbackController", null);
            SetField(router, "backlog", null);

            try
            {
                harness.Manager.StartDialogue(1);
                yield return null;
                Assert.AreEqual(1, harness.Manager.CurrentData.Id);

                input.Raise(DialogueInputAction.Next);
                yield return null;
                Assert.AreEqual(2, harness.Manager.CurrentData.Id);
                Assert.AreEqual(DialogueSessionState.ChoicePending, harness.Manager.State);

                input.Raise(DialogueInputAction.Confirm);
                yield return null;
                Assert.AreEqual(3, harness.Manager.CurrentData.Id);
            }
            finally
            {
                UnityEngine.Object.Destroy(harness.Root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ManagerView_AutoAdvanceOverrideAdvancesLine()
        {
            yield return DestroyExistingManager();

            var harness = CreateHarness(
                "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                "1,A,Auto,2,,,,,\n" +
                "2,A,Done,-1,,,,,Stay->2\n");

            try
            {
                harness.Manager.StartDialogue(1);
                yield return null;

                harness.Manager.SetAutoAdvanceOverride(true, 0.01f);
                yield return new WaitForSeconds(0.05f);

                Assert.AreEqual(2, harness.Manager.CurrentData.Id);
            }
            finally
            {
                UnityEngine.Object.Destroy(harness.Root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveSystem_SaveAndLoadRestoresCurrentLine()
        {
            yield return DestroyExistingManager();

            var harness = CreateHarness(
                "Id,Speaker,Text,NextId\n" +
                "1,A,One,2\n" +
                "2,A,Two,3\n" +
                "3,A,Three,-1\n");
            var saveSystem = harness.Root.AddComponent<DialogueSaveSystem>();
            saveSystem.SetStorage(new MemoryStorage());

            try
            {
                harness.Manager.StartDialogue(1);
                yield return null;
                harness.Manager.RequestNext();
                yield return null;
                Assert.AreEqual(2, harness.Manager.CurrentData.Id);

                Assert.IsNotNull(saveSystem.Save(10, false, "smoke"));

                harness.Manager.RequestNext();
                yield return null;
                Assert.AreEqual(3, harness.Manager.CurrentData.Id);

                Assert.IsTrue(saveSystem.Load(10));
                yield return null;
                Assert.AreEqual(2, harness.Manager.CurrentData.Id);
            }
            finally
            {
                UnityEngine.Object.Destroy(harness.Root);
            }

            yield return null;
        }

        private static RuntimeHarness CreateHarness(string csv)
        {
            var root = new GameObject("DialogueRuntimeSmoke");
            root.SetActive(false);

            var viewObject = new GameObject("DialogueView");
            viewObject.transform.SetParent(root.transform, false);
            var view = viewObject.AddComponent<DialogueView>();

            var manager = root.AddComponent<DialogueManager>();
            SetField(manager, "csvFile", new TextAsset(csv));
            SetField(manager, "view", view);

            root.SetActive(true);

            return new RuntimeHarness
            {
                Root = root,
                Manager = manager,
                View = view
            };
        }

        private static IEnumerator DestroyExistingManager()
        {
            if (DialogueManager.Instance != null)
            {
                UnityEngine.Object.Destroy(DialogueManager.Instance.gameObject);
                yield return null;
            }
        }

        private static void ConfigureRouter(
            DialogueInputRouter router,
            ManualInputSource input,
            DialoguePlaybackController controller,
            DialogueBacklogView backlog)
        {
            SetField(router, "inputSourceComponent", input);
            SetField(router, "playbackController", controller);
            SetField(router, "backlog", backlog);
            Invoke(router, "Awake");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }
    }
}
