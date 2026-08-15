using System.Linq;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class NovelDialogueMotionControllerTests
    {
        private const string ScenarioPath =
            "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        [TearDown]
        public void TearDown()
        {
            DestroySceneObjects<DialogueManager>();
            DestroySceneObjects<DialogueView>();
            DestroySceneObjects<DialogueStageView>();
            DestroySceneObjects<DialogueAudioPlayer>();
            DestroySceneObjects<Canvas>();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ActiveSlotFollowsSpeakerAndNarrationReturnsToNeutral()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values.ToDictionary(row => row.Id);

            Assert.That(NovelDialogueMotionController.ResolveActiveSlot(rows[1000019]), Is.EqualTo(DialogueStageSlot.Left));
            Assert.That(NovelDialogueMotionController.ResolveActiveSlot(rows[1000020]), Is.EqualTo(DialogueStageSlot.Right));
            Assert.That(NovelDialogueMotionController.ResolveActiveSlot(rows[1000077]), Is.EqualTo(DialogueStageSlot.Left));
            Assert.That(NovelDialogueMotionController.ResolveActiveSlot(rows[1000078]), Is.Empty);
            Assert.That(NovelDialogueMotionController.ResolveActiveSlot(rows[1000717]), Is.EqualTo(DialogueStageSlot.Right));
        }

        [Test]
        public void StageTransitionPolicyUsesBackgroundSemanticsAndChapterWeight()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values.ToDictionary(row => row.Id);

            NovelDialogueMotionController.StageTransitionProfile opening;
            Assert.That(
                NovelDialogueMotionController.TryResolveStageTransition(rows[1000001], out opening),
                Is.True);
            Assert.That(opening.Mood, Is.EqualTo(NovelDialogueMotionController.StageTransitionMood.Cold));
            Assert.That(opening.Duration, Is.EqualTo(1f).Within(0.001f));
            Assert.That(opening.StartAlpha, Is.GreaterThan(0.8f));
            Assert.That(opening.AlertPulse, Is.False);

            NovelDialogueMotionController.StageTransitionProfile sterileCut;
            Assert.That(
                NovelDialogueMotionController.TryResolveStageTransition(rows[1000062], out sterileCut),
                Is.True);
            Assert.That(sterileCut.Mood, Is.EqualTo(NovelDialogueMotionController.StageTransitionMood.Sterile));
            Assert.That(sterileCut.Duration, Is.EqualTo(0.16f).Within(0.001f));

            NovelDialogueMotionController.StageTransitionProfile alarmChapter;
            Assert.That(
                NovelDialogueMotionController.TryResolveStageTransition(rows[1008625], out alarmChapter),
                Is.True);
            Assert.That(alarmChapter.Mood, Is.EqualTo(NovelDialogueMotionController.StageTransitionMood.Alarm));
            Assert.That(alarmChapter.Duration, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(alarmChapter.AlertPulse, Is.True);

            NovelDialogueMotionController.StageTransitionProfile unused;
            Assert.That(
                NovelDialogueMotionController.TryResolveStageTransition(rows[1000019], out unused),
                Is.False);
        }

        [Test]
        public void ChapterTitlePolicySeparatesOrdinalAndTitle()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values.ToDictionary(row => row.Id);

            NovelDialogueMotionController.ChapterTitleContent opening;
            Assert.That(
                NovelDialogueMotionController.TryResolveChapterTitle(rows[1000001], out opening),
                Is.True);
            Assert.That(opening.Ordinal, Is.EqualTo("第一章"));
            Assert.That(opening.Title, Is.EqualTo("答えのない問い"));

            NovelDialogueMotionController.ChapterTitleContent alarm;
            Assert.That(
                NovelDialogueMotionController.TryResolveChapterTitle(rows[1008625], out alarm),
                Is.True);
            Assert.That(alarm.Ordinal, Is.EqualTo("第十三章"));
            Assert.That(alarm.Title, Is.EqualTo("正解の外側"));

            NovelDialogueMotionController.ChapterTitleContent unused;
            Assert.That(
                NovelDialogueMotionController.TryResolveChapterTitle(rows[1000019], out unused),
                Is.False);
        }

        [Test]
        public void MotionFactoryConfiguresProductionPrefabAndRuntimeFallbackOnce()
        {
            var managerObject = new GameObject("MotionTestManager", typeof(DialogueManager));
            var manager = managerObject.GetComponent<DialogueManager>();
            var presentationConfiguration = NovelPresentationConfiguration.LoadDefault();
            var presentation = DialoguePresentationFactory.Ensure(
                presentationConfiguration.BackgroundDatabase,
                presentationConfiguration.CharacterDatabase,
                presentationConfiguration.AudioDatabase);
            var uiConfiguration = NovelUiConfiguration.LoadDefault();
            var production = DialogueViewFactory.EnsureDialogueView(uiConfiguration.DialogueViewPrefab);

            var productionMotion = DialogueMotionFactory.Ensure(manager, production, presentation.StageView);
            var repeated = DialogueMotionFactory.Ensure(manager, production, presentation.StageView);

            Assert.That(productionMotion, Is.Not.Null);
            Assert.That(productionMotion.IsConfigured, Is.True);
            Assert.That(productionMotion.BoundManager, Is.SameAs(manager));
            Assert.That(repeated, Is.SameAs(productionMotion));
            Assert.That(production.GetComponents<NovelDialogueMotionController>(), Has.Length.EqualTo(1));
            var chapterOverlays = production.GetComponentInParent<Canvas>(true)
                .GetComponentsInChildren<NovelChapterTitleView>(true);
            Assert.That(chapterOverlays, Has.Length.EqualTo(1));
            Assert.That(productionMotion.ChapterTitleView, Is.SameAs(chapterOverlays[0]));
            Assert.That(chapterOverlays[0].Group.blocksRaycasts, Is.False);
            Assert.That(chapterOverlays[0].Group.interactable, Is.False);
            Assert.That(chapterOverlays[0].PanelRect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(chapterOverlays[0].PanelRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(chapterOverlays[0].transform.Find(NovelChapterTitleView.SafeAreaName), Is.Not.Null);
            var overlays = presentation.StageView.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == "NovelStageTransitionOverlay")
                .ToArray();
            Assert.That(overlays, Has.Length.EqualTo(1));
            var overlayImage = overlays[0].GetComponent<Image>();
            var overlayGroup = overlays[0].GetComponent<CanvasGroup>();
            Assert.That(overlayImage, Is.Not.Null);
            Assert.That(overlayImage.raycastTarget, Is.False);
            Assert.That(overlayGroup, Is.Not.Null);
            Assert.That(overlayGroup.blocksRaycasts, Is.False);
            Assert.That(overlayGroup.interactable, Is.False);

            Object.DestroyImmediate(production.gameObject);
            var fallback = DialogueViewFactory.CreateDefaultDialogueView(
                NovelUiFactory.EnsureCanvas().transform);
            var fallbackMotion = DialogueMotionFactory.Ensure(manager, fallback, presentation.StageView);

            Assert.That(fallbackMotion, Is.Not.Null);
            Assert.That(fallbackMotion.IsConfigured, Is.True);
            Assert.That(fallbackMotion.BoundManager, Is.SameAs(manager));
            Assert.That(fallback.GetComponents<NovelDialogueMotionController>(), Has.Length.EqualTo(1));
            Assert.That(fallbackMotion.ChapterTitleView, Is.SameAs(chapterOverlays[0]));
        }

        [Test]
        public void ChoiceFeedbackTreatsPointerAndControllerSelectionEqually()
        {
            var choiceObject = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var feedback = choiceObject.AddComponent<NovelChoiceMotionFeedback>();
                feedback.Configure();

                Assert.That(feedback.DesiredScaleMultiplier, Is.EqualTo(1f));
                feedback.OnPointerEnter(null);
                Assert.That(feedback.DesiredScaleMultiplier, Is.GreaterThan(1f));
                feedback.OnPointerExit(null);
                Assert.That(feedback.DesiredScaleMultiplier, Is.EqualTo(1f));
                feedback.OnSelect((BaseEventData)null);
                Assert.That(feedback.DesiredScaleMultiplier, Is.GreaterThan(1f));
                feedback.OnPointerDown(null);
                Assert.That(feedback.DesiredScaleMultiplier, Is.LessThan(1f));
                feedback.OnPointerUp(null);
                feedback.OnDeselect((BaseEventData)null);
                Assert.That(feedback.DesiredScaleMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(choiceObject);
            }
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
