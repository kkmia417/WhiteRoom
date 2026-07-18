using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueManagerTests
    {
        private const string Csv = "Id,Speaker,Text,NextId\n1,A,Hello,2\n2,A,End,-1\n";
        private const string ChoiceCsv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                                         "1,A,Choose,-1,,,,,Go->2|Stay->-1\n" +
                                         "2,A,End,-1,,,,,\n";

        [Test]
        public void Manager_NaturalEnd_HidesViewAndRaisesEndedOnce()
        {
            var view = CreateView("DialogueView");
            var manager = CreateManager(view);
            var endedCount = 0;

            try
            {
                manager.DialogueEnded += _ => endedCount++;

                manager.StartDialogue(1);
                view.RequestNext();
                view.RequestNext();

                Assert.AreEqual(DialogueSessionState.Ended, manager.State);
                Assert.IsFalse(view.gameObject.activeSelf);
                Assert.AreEqual(1, endedCount);
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [Test]
        public void Manager_SetView_PreservesSessionAndMovesInputToNewView()
        {
            var firstView = CreateView("FirstDialogueView");
            var secondView = CreateView("SecondDialogueView");
            var manager = CreateManager(firstView);

            try
            {
                manager.StartDialogue(1);
                Assert.AreEqual(1, manager.History.Count);

                manager.SetView(secondView);
                Assert.AreEqual(1, manager.History.Count);

                firstView.RequestNext();
                Assert.AreEqual(1, manager.History.Count);

                secondView.RequestNext();
                Assert.AreEqual(2, manager.History.Count);
                Assert.AreEqual(DialogueSessionState.WaitingForInput, manager.State);
            }
            finally
            {
                Destroy(manager);
                Destroy(firstView);
                Destroy(secondView);
            }
        }

        [Test]
        public void Manager_ExposesCurrentChoiceCount()
        {
            var view = CreateView("ChoiceDialogueView");
            var manager = CreateManager(view, ChoiceCsv);

            try
            {
                manager.StartDialogue(1);

                Assert.AreEqual(DialogueSessionState.ChoicePending, manager.State);
                Assert.AreEqual(2, manager.CurrentChoiceCount);
                Assert.IsTrue(manager.HasCurrentChoices);
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [UnityTest]
        public IEnumerator Manager_WithoutInspectorCsv_LoadsRepositoryAndPlaysThroughBoundView()
        {
            InvokeStatic("ResetStatics");
            var view = CreateView("RuntimeLoadedDialogueView");
            var binder = view.gameObject.AddComponent<DialogueViewBinder>();
            Invoke(binder, "Awake");
            Invoke(binder, "OnEnable");

            var manager = CreateManagerWithoutCsv(null);
            var eventKeys = new System.Collections.Generic.List<string>();
            manager.DialogueEventTriggered += context => eventKeys.Add(context.EventKey);

            try
            {
                Assert.AreSame(manager, DialogueManager.Instance);
                Assert.IsNull(manager.Repository);

                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey\n" +
                    "1,A,Runtime loaded,2,,,,reward\n" +
                    "2,A,End,-1,,,,\n")));

                yield return null;
                yield return null;

                Assert.IsNotNull(manager.Repository);

                manager.StartDialogue(1);

                Assert.AreEqual(1, manager.CurrentData.Id);
                CollectionAssert.AreEqual(new[] { "reward" }, eventKeys);

                view.RequestNext();

                Assert.AreEqual(2, manager.CurrentData.Id);
            }
            finally
            {
                Invoke(binder, "OnDisable");
                Destroy(manager);
                Destroy(view);
                InvokeStatic("ResetStatics");
            }
        }

        [UnityTest]
        public IEnumerator Manager_LoadRepository_IgnoresStaleCompletion()
        {
            var view = CreateView("RuntimeLoadedDialogueView");
            var manager = CreateManagerWithoutCsv(view);
            var slow = new ManualRepositoryLoader();
            var fast = new ManualRepositoryLoader();

            try
            {
                manager.LoadRepository(slow);
                manager.LoadRepository(fast);

                fast.Complete(CreateRepository("2", "Fast"));
                yield return null;

                slow.Complete(CreateRepository("1", "Slow"));
                yield return null;

                Assert.AreEqual("Fast", manager.Repository.Get(2).Text);
                Assert.IsNull(manager.Repository.Get(1));
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [UnityTest]
        public IEnumerator Manager_LoadRepository_IgnoresStaleFailure()
        {
            var view = CreateView("RuntimeLoadedDialogueView");
            var manager = CreateManagerWithoutCsv(view);
            var slow = new ManualRepositoryLoader();
            var fast = new ManualRepositoryLoader();
            var errors = new System.Collections.Generic.List<string>();
            manager.ErrorRaised += errors.Add;

            try
            {
                manager.LoadRepository(slow);
                manager.LoadRepository(fast);

                fast.Complete(CreateRepository("2", "Fast"));
                yield return null;

                slow.Fail("Slow loader failed.");
                yield return null;

                Assert.AreEqual("Fast", manager.Repository.Get(2).Text);
                CollectionAssert.IsEmpty(errors);
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [Test]
        public void Manager_LoadRepository_RejectsActiveSessionReplacement()
        {
            var view = CreateView("DialogueView");
            var manager = CreateManager(view);
            var loader = new ManualRepositoryLoader();
            var errors = new System.Collections.Generic.List<string>();
            manager.ErrorRaised += errors.Add;

            try
            {
                manager.StartDialogue(1);
                LogAssert.Expect(LogType.Error, "DialogueManager: Cannot load repository while dialogue is active.");

                manager.LoadRepository(loader);

                Assert.IsFalse(loader.Started);
                CollectionAssert.AreEqual(new[] { "Cannot load repository while dialogue is active." }, errors);
                Assert.AreEqual("Hello", manager.Repository.Get(1).Text);
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [Test]
        public void Manager_StartDialogueWithNullPredicate_LogsErrorWithoutThrowing()
        {
            var view = CreateView("DialogueView");
            var manager = CreateManager(view);

            try
            {
                LogAssert.Expect(LogType.Error, "DialogueManager: predicate が null です。");
                Assert.DoesNotThrow(() => manager.StartDialogue((Func<DialogueData, bool>)null));
                Assert.AreEqual(DialogueSessionState.Idle, manager.State, "会話は開始されない");
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
            }
        }

        [Test]
        public void Manager_WithoutInspectorCsv_StillRaisesInstanceChanged()
        {
            InvokeStatic("ResetStatics");
            var instances = new System.Collections.Generic.List<DialogueManager>();
            DialogueManager.InstanceChanged += instances.Add;
            var manager = CreateManagerWithoutCsv(null);

            try
            {
                Assert.AreSame(manager, DialogueManager.Instance);
                CollectionAssert.AreEqual(new[] { manager }, instances);
                Assert.IsNull(manager.Repository);
            }
            finally
            {
                Destroy(manager);
                InvokeStatic("ResetStatics");
            }
        }

        [Test]
        public void Manager_ResetStatics_ClearsInstanceAndSubscribers()
        {
            var view = CreateView("DialogueView");
            var manager = CreateManager(view);
            var invoked = false;
            DialogueManager.InstanceChanged += _ => invoked = true;

            try
            {
                InvokeStatic("ResetStatics");

                Assert.IsNull(DialogueManager.Instance);
                Assert.IsNull(GetStaticEventDelegate("InstanceChanged"));
                Assert.IsFalse(invoked);
            }
            finally
            {
                Destroy(manager);
                Destroy(view);
                InvokeStatic("ResetStatics");
            }
        }

        private static DialogueManager CreateManager(DialogueView view)
        {
            return CreateManager(view, Csv);
        }

        private static DialogueManager CreateManager(DialogueView view, string csv)
        {
            // EditMode テストでは Awake が自動で呼ばれないため、フィールド設定後に明示的に起動する。
            var gameObject = new GameObject("DialogueManager");
            var manager = gameObject.AddComponent<DialogueManager>();
            SetPrivateField(manager, "csvFile", new TextAsset(csv));
            SetPrivateField(manager, "view", view);
            Invoke(manager, "Awake");
            return manager;
        }

        private static DialogueManager CreateManagerWithoutCsv(DialogueView view)
        {
            var gameObject = new GameObject("DialogueManager");
            var manager = gameObject.AddComponent<DialogueManager>();
            if (view != null)
                SetPrivateField(manager, "view", view);
            Invoke(manager, "Awake");
            return manager;
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(target, null);
        }

        private static void InvokeStatic(string methodName)
        {
            var method = typeof(DialogueManager).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }

        private static object GetStaticEventDelegate(string eventName)
        {
            var field = typeof(DialogueManager).GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(null);
        }

        private static DialogueView CreateView(string name)
        {
            return new GameObject(name).AddComponent<DialogueView>();
        }

        private static DialogueRepository CreateRepository(string id, string text)
        {
            return new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n" + id + ",A," + text + ",-1\n").Values);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static void Destroy(Component component)
        {
            if (component != null)
                Object.DestroyImmediate(component.gameObject);
        }

        private sealed class ManualRepositoryLoader : IDialogueRepositoryLoader
        {
            private bool _completed;
            private IDialogueRepository _repository;
            private string _error;

            public bool Started { get; private set; }

            public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
            {
                Started = true;
                while (!_completed)
                    yield return null;

                if (!string.IsNullOrEmpty(_error))
                {
                    if (onError != null)
                        onError(_error);
                    yield break;
                }

                if (onCompleted != null)
                    onCompleted(_repository);
            }

            public void Complete(IDialogueRepository repository)
            {
                _repository = repository;
                _completed = true;
            }

            public void Fail(string error)
            {
                _error = error;
                _completed = true;
            }
        }
    }
}
