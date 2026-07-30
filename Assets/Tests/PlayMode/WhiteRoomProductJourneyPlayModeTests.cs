using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomProductJourneyPlayModeTests
    {
        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            private readonly Dictionary<int, DialogueSaveSlot> _slots = new Dictionary<int, DialogueSaveSlot>();

            public bool TryLoad(int slot, out DialogueSaveSlot data) => _slots.TryGetValue(slot, out data);
            public void Save(DialogueSaveSlot slot) => _slots[slot.SlotIndex] = slot;
            public void Delete(int slot) => _slots.Remove(slot);
            public bool Exists(int slot) => _slots.ContainsKey(slot);
            public IEnumerable<int> ListSlots() => new List<int>(_slots.Keys);
            public byte[] LoadThumbnail(int slot) => null;
            public void SaveThumbnail(int slot, byte[] pngBytes) { }
        }

        [UnityTest]
        public IEnumerator TitleMainEndingTitleJourneyRestoresSaveAndKeepsRuntimeSingletonsCoherent()
        {
            DestroyPersistentFixtureObjects();
            yield return null;
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Title"));
            EnsureSharedUiTwice();
            AssertSingletonUi();

            var managerObject = new GameObject("WhiteRoomProductJourneyManager", typeof(DialogueManager), typeof(DialogueSaveSystem));
            var viewObject = new GameObject("WhiteRoomProductJourneyView", typeof(RectTransform), typeof(DialogueView));
            UnityEngine.Object.DontDestroyOnLoad(viewObject);
            var manager = managerObject.GetComponent<DialogueManager>();
            var view = viewObject.GetComponent<DialogueView>();
            var saveSystem = managerObject.GetComponent<DialogueSaveSystem>();
            object saveService = null;
            object titleMenu = null;
            object endingFlow = null;

            try
            {
                saveSystem.SetStorage(new MemoryStorage());
                var saveServiceType = RequireProductType("WhiteRoom.Novel.NovelSaveService");
                saveService = Activator.CreateInstance(
                    saveServiceType,
                    manager,
                    saveSystem,
                    DialogueSaveSlotConventions.FirstManualSlot,
                    false,
                    null);
                var newGameClicked = false;
                var titleMenuType = RequireProductType("WhiteRoom.Novel.TitleMenuController");
                titleMenu = Activator.CreateInstance(
                    titleMenuType,
                    saveService,
                    new Action(() =>
                    {
                        newGameClicked = true;
                        Invoke(titleMenu, "Hide");
                        SceneManager.LoadScene("Main");
                    }),
                    new Action(() => { }),
                    null,
                    null,
                    null,
                    null);
                Invoke(titleMenu, "Show");
                var newGameButton = GameObject.Find("NewGameButton")?.GetComponent<Button>();
                Assert.That(newGameButton, Is.Not.Null);
                newGameButton.onClick.Invoke();
                yield return null;
                Assert.That(newGameClicked, Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
                AssertSingletonUi();

                var scenario = Resources.Load<TextAsset>("Dialogue/r00_escape_talksystem");
                Assert.That(scenario, Is.Not.Null);
                manager.SetView(view);
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(scenario));
                yield return null;
                yield return null;

                var endingFlowType = RequireProductType("WhiteRoom.Novel.EndingFlowService");
                endingFlow = Activator.CreateInstance(
                    endingFlowType,
                    new Func<bool>(() => true),
                    new Action(() => { }),
                    new Action(() => SceneManager.LoadScene("Title")));
                Invoke(endingFlow, "AttachTo", manager);

                manager.StartDialogue(1);
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(1));
                manager.RequestNext();
                yield return null;
                manager.RequestNext();
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(3));

                Assert.That(saveSystem.Save(10, false, "journey-checkpoint"), Is.Not.Null);
                manager.RequestNext();
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(4));
                Assert.That(saveSystem.Load(10), Is.True);
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(3));

                AssertOverlaySuspendsAndRestoresAutomation();

                manager.RequestNext();
                yield return null;
                manager.RequestNext();
                yield return null;
                Assert.That(manager.CurrentData.Id, Is.EqualTo(5));
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.ChoicePending));

                var firstEndingTarget = 100;
                var selectedIndex = manager.CurrentData.GetChoices()
                    .Select((choice, index) => new { choice, index })
                    .Single(item => item.choice.NextId == firstEndingTarget)
                    .index;
                InvokePrivate(view, "SelectChoice", selectedIndex);
                yield return null;

                for (var guard = 0; guard < 10 && GetProperty(endingFlow, "CurrentResult") == null; guard++)
                {
                    manager.RequestNext();
                    yield return null;
                }

                var result = GetProperty(endingFlow, "CurrentResult");
                Assert.That(result, Is.Not.Null);
                Assert.That(GetProperty(result, "EndingKey"), Is.EqualTo("bad_too_good"));
                Assert.That(manager.History.Select(entry => entry.Id), Does.Contain(104));
                Assert.That(UnityEngine.Object.FindObjectsByType<DialogueManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.EqualTo(1));

                Assert.That((bool)Invoke(endingFlow, "ConfirmAndReturnToTitle"), Is.True);
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Title"));
                Invoke(endingFlow, "NotifySceneLoaded");
                Assert.That((bool)GetProperty(endingFlow, "IsInputBlocked"), Is.False);
                Invoke(titleMenu, "Show");
                Assert.That(GameObject.Find("WhiteRoomTitleMenu"), Is.Not.Null);
                AssertSingletonUi();
                Assert.That(UnityEngine.Object.FindObjectsByType<DialogueManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            finally
            {
                (endingFlow as IDisposable)?.Dispose();
                (saveService as IDisposable)?.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                if (viewObject != null)
                    UnityEngine.Object.Destroy(viewObject);
                DestroyPersistentFixtureObjects();
            }
        }

        private static void AssertOverlaySuspendsAndRestoresAutomation()
        {
            var mode = DialoguePlaybackMode.Auto;
            var inputEnabled = true;
            var secondaryStops = 0;
            var coordinatorType = RequireProductType("WhiteRoom.Novel.GameplayOverlayCoordinator");
            var coordinator = Activator.CreateInstance(
                coordinatorType,
                new Func<DialoguePlaybackMode>(() => mode),
                new Action<DialoguePlaybackMode>(value => mode = value),
                new Action(() => secondaryStops++),
                new Action<bool>(value => inputEnabled = value));

            Invoke(coordinator, "Suspend");
            Assert.That(mode, Is.EqualTo(DialoguePlaybackMode.Normal));
            Assert.That(inputEnabled, Is.False);
            Assert.That(secondaryStops, Is.EqualTo(1));
            Invoke(coordinator, "Resume");
            Assert.That(mode, Is.EqualTo(DialoguePlaybackMode.Auto));
            Assert.That(inputEnabled, Is.True);
        }

        private static void EnsureSharedUiTwice()
        {
            var factory = RequireProductType("WhiteRoom.Novel.NovelUiFactory");
            for (var index = 0; index < 2; index++)
            {
                factory.GetMethod("EnsureCanvas", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
                factory.GetMethod("EnsureEventSystem", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
            }
        }

        private static void AssertSingletonUi()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Count(canvas => canvas.name == "NovelDialogueCanvas"), Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private static void DestroyPersistentFixtureObjects()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(manager.gameObject);
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (canvas.name == "NovelDialogueCanvas")
                    UnityEngine.Object.Destroy(canvas.gameObject);
            }
            foreach (var eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(eventSystem.gameObject);
        }

        private static Type RequireProductType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
        }

        private static void InvokePrivate(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }
    }
}
