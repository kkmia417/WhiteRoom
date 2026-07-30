using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class DialogueViewFactoryTests
    {
        [SetUp]
        public void SetUp()
        {
            NovelUiFactory.EnsureFont(
                null,
                "Fonts/LogoTypeGothicCondense/LogoTypeGothicCondense");
            var resetWarnings = typeof(DialogueViewFactory).GetMethod(
                "ResetFallbackWarnings",
                BindingFlags.NonPublic | BindingFlags.Static);
            resetWarnings?.Invoke(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            DestroySceneObjects<DialogueView>();
            DestroySceneObjects<DialogueBacklogView>();
            DestroySceneObjects<Canvas>();
            DestroySceneObjects<EventSystem>();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DefaultConfigurationReferencesCheckedInPrefabsAndSkin()
        {
            var configuration = NovelUiConfiguration.LoadDefault();

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.DialogueViewPrefab, Is.Not.Null);
            Assert.That(configuration.DialogueViewPrefab.gameObject.name, Is.EqualTo("DialogueView"));
            Assert.That(configuration.DialogueBacklogViewPrefab, Is.Not.Null);
            Assert.That(configuration.DialogueBacklogViewPrefab.gameObject.name, Is.EqualTo("DialogueBacklogView"));
            Assert.That(configuration.DialogueWindowSprite, Is.Not.Null);
            Assert.That(configuration.DialogueWindowSprite.name, Is.EqualTo("WhiteRoom_comment_window_transparent"));

            var prefabImage = configuration.DialogueViewPrefab.GetComponent<Image>();
            Assert.That(prefabImage, Is.Not.Null);
            Assert.That(prefabImage.sprite, Is.SameAs(configuration.DialogueWindowSprite));
            Assert.That(prefabImage.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(prefabImage.color, Is.EqualTo(Color.white));
            Assert.That(configuration.DialogueWindowSprite.border.sqrMagnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void EnsureViewsInstantiatesConfiguredPrefabsOnceAndAppliesSkin()
        {
            var configuration = NovelUiConfiguration.LoadDefault();

            var dialogue = DialogueViewFactory.EnsureDialogueView(
                configuration.DialogueViewPrefab,
                configuration.DialogueWindowSprite);
            var backlog = DialogueViewFactory.EnsureBacklogView(configuration.DialogueBacklogViewPrefab);

            Assert.That(dialogue.gameObject.name, Does.StartWith("DialogueView"));
            Assert.That(backlog.gameObject.name, Does.StartWith("DialogueBacklogView"));
            Assert.That(dialogue.GetComponent<Image>().sprite, Is.SameAs(configuration.DialogueWindowSprite));
            Assert.That(dialogue.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(dialogue.GetComponentsInChildren<TMP_Text>(true), Is.All.Matches<TMP_Text>(text =>
                text.font != null && text.font.name.Contains("LogoTypeGothicCondense")));
            Assert.That(backlog.GetComponentsInChildren<TMP_Text>(true), Is.All.Matches<TMP_Text>(text =>
                text.font != null && text.font.name.Contains("LogoTypeGothicCondense")));

            Assert.That(
                DialogueViewFactory.EnsureDialogueView(configuration.DialogueViewPrefab, configuration.DialogueWindowSprite),
                Is.SameAs(dialogue));
            Assert.That(
                DialogueViewFactory.EnsureBacklogView(configuration.DialogueBacklogViewPrefab),
                Is.SameAs(backlog));
            Assert.That(FindSceneObjects<DialogueView>().Length, Is.EqualTo(1));
            Assert.That(FindSceneObjects<DialogueBacklogView>().Length, Is.EqualTo(1));
            Assert.That(FindSceneObjects<Canvas>().Length, Is.EqualTo(1));
        }

        [Test]
        public void MissingPrefabsUseFallbacksAndWarnOnlyOncePerViewType()
        {
            const string dialogueWarning =
                "DialogueViewFactory: DialogueView prefab was not configured; using the runtime fallback UI. " +
                "Check Resources/WhiteRoom/NovelUiConfiguration.";
            const string backlogWarning =
                "DialogueViewFactory: DialogueBacklogView prefab was not configured; using the runtime fallback UI. " +
                "Check Resources/WhiteRoom/NovelUiConfiguration.";
            LogAssert.Expect(LogType.Warning, dialogueWarning);
            LogAssert.Expect(LogType.Warning, backlogWarning);

            var dialogue = DialogueViewFactory.EnsureDialogueView(null);
            var backlog = DialogueViewFactory.EnsureBacklogView(null);

            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.gameObject.name, Is.EqualTo("DialogueWindow"));
            Assert.That(backlog, Is.Not.Null);
            Assert.That(backlog.gameObject.name, Is.EqualTo("DialogueBacklog"));

            Object.DestroyImmediate(dialogue.gameObject);
            Object.DestroyImmediate(backlog.gameObject);

            Assert.That(DialogueViewFactory.EnsureDialogueView(null), Is.Not.Null);
            Assert.That(DialogueViewFactory.EnsureBacklogView(null), Is.Not.Null);
        }

        private static T[] FindSceneObjects<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void DestroySceneObjects<T>() where T : Component
        {
            foreach (var component in FindSceneObjects<T>())
            {
                if (component != null)
                    Object.DestroyImmediate(component.gameObject);
            }
        }
    }
}
