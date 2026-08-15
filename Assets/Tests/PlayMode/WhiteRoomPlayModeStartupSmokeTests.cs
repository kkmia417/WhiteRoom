using System;
using System.Collections;
using System.IO;
using System.Linq;
using kkmia.TalkSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomPlayModeStartupSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyRuntimeObjects();
            yield return null;
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyRuntimeObjects();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TitleNewGameLoadsMainThenStartsThePublishedScenarioWithoutUnexpectedLogs()
        {
            var bootstrapType = Type.GetType("WhiteRoom.Novel.NovelGameBootstrap, Assembly-CSharp");
            Assert.That(bootstrapType, Is.Not.Null);
            var bootstrapObject = new GameObject("PlayModeStartupSmokeBootstrap");
            bootstrapObject.AddComponent(bootstrapType);

            yield return null;
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Title"));
            Assert.That(GameObject.Find("WhiteRoomTitleMenu"), Is.Not.Null);

            var commandBar = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Select(transform => transform.gameObject)
                .SingleOrDefault(gameObject => gameObject.name == "NovelCommandBar");
            Assert.That(commandBar, Is.Not.Null);
            Assert.That(commandBar.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(23));
            Assert.That(commandBar.GetComponentsInChildren<Image>(true), Is.All.Matches<Image>(image =>
                image.sprite == null && image.type == Image.Type.Simple));

            var manager = Object.FindObjectsByType<DialogueManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Single();
            var saveSystem = Object.FindFirstObjectByType<DialogueSaveSystem>();
            Assert.That(saveSystem, Is.Not.Null);
            saveSystem.SetThumbnailCaptureProvider(() =>
            {
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = Enumerable.Repeat(Color.black, 16).ToArray();
                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            });
            Assert.That(Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Count(canvas => canvas.name == "NovelDialogueCanvas"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.EqualTo(1));

            var newGameButton = GameObject.Find("NewGameButton")?.GetComponent<Button>();
            Assert.That(newGameButton, Is.Not.Null);
            newGameButton.onClick.Invoke();

            for (var guard = 0; guard < 60 && manager.CurrentData == null; guard++)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
            Assert.That(manager.CurrentData, Is.Not.Null);
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000001));
            Assert.That(commandBar.activeInHierarchy, Is.True);
            Assert.That(GameObject.Find("WhiteRoomTitleMenu"), Is.Null);

            var dialogueView = Object.FindFirstObjectByType<DialogueView>();
            var motionType = Type.GetType("WhiteRoom.Novel.NovelDialogueMotionController, Assembly-CSharp");
            var choiceMotionType = Type.GetType("WhiteRoom.Novel.NovelChoiceMotionFeedback, Assembly-CSharp");
            Assert.That(motionType, Is.Not.Null);
            Assert.That(choiceMotionType, Is.Not.Null);
            var motion = Object.FindFirstObjectByType(
                motionType,
                FindObjectsInactive.Include) as Component;
            var left = GameObject.Find("LeftCharacter")?.GetComponent<Image>();
            var right = GameObject.Find("RightCharacter")?.GetComponent<Image>();
            Assert.That(dialogueView, Is.Not.Null);
            Assert.That(motion, Is.Not.Null);
            Assert.That(GetMotionProperty<bool>(motion, "IsConfigured"), Is.True);
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            var backgroundDepthLayer = GameObject.Find("NovelBackgroundDepthLayer")?.GetComponent<RectTransform>();
            var portraitDepthLayer = GameObject.Find("NovelPortraitDepthLayer")?.GetComponent<RectTransform>();
            Assert.That(backgroundDepthLayer, Is.Not.Null);
            Assert.That(portraitDepthLayer, Is.Not.Null);

            var chapterTitle = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(transform => transform.name == "NovelChapterTitleOverlay");
            var chapterGroup = chapterTitle.GetComponent<CanvasGroup>();
            var chapterPanel = chapterTitle.Find("SafeArea/ChapterTitlePanel") as RectTransform;
            var chapterOrdinal = chapterPanel.Find("ChapterOrdinal").GetComponent<TMP_Text>();
            var chapterName = chapterPanel.Find("ChapterTitle").GetComponent<TMP_Text>();
            var dialogueWindowImage = dialogueView.GetComponent<Image>();
            var speakerGroup = dialogueView.transform.Find("SpeakerText").GetComponent<CanvasGroup>();
            var bodyGroup = dialogueView.transform.Find("BodyText").GetComponent<CanvasGroup>();
            Assert.That(chapterGroup, Is.Not.Null);
            Assert.That(chapterGroup.blocksRaycasts, Is.False);
            Assert.That(chapterPanel.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(chapterPanel.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(GetMotionProperty<bool>(motion, "IsChapterTitleActive"), Is.True);
            Assert.That(chapterOrdinal.text, Is.EqualTo("第一章"));
            Assert.That(chapterName.text, Is.EqualTo("答えのない問い"));
            Assert.That(dialogueWindowImage.enabled, Is.False);
            Assert.That(speakerGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bodyGroup.alpha, Is.EqualTo(0f).Within(0.001f));

            var transitionOverlay = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(transform => transform.name == "NovelStageTransitionOverlay");
            var transitionImage = transitionOverlay.GetComponent<Image>();
            var transitionGroup = transitionOverlay.GetComponent<CanvasGroup>();
            Assert.That(transitionImage, Is.Not.Null);
            Assert.That(transitionImage.raycastTarget, Is.False);
            Assert.That(transitionGroup, Is.Not.Null);
            Assert.That(transitionGroup.blocksRaycasts, Is.False);
            Assert.That(transitionGroup.alpha, Is.GreaterThan(0f));
            var irisOverlay = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(transform => transform.name == "NovelIrisTransitionOverlay");
            var irisGroup = irisOverlay.GetComponent<CanvasGroup>();
            Assert.That(irisGroup, Is.Not.Null);
            Assert.That(irisGroup.blocksRaycasts, Is.False);
            var irisImages = irisOverlay.GetComponentsInChildren<Image>(true);
            Assert.That(irisImages, Has.Length.EqualTo(4));
            Assert.That(irisImages, Is.All.Matches<Image>(image => !image.raycastTarget));
            var screenEffectOverlay = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(transform => transform.name == "NovelScreenEffectOverlay");
            var screenEffectImage = screenEffectOverlay.GetComponent<Image>();
            var screenEffectGroup = screenEffectOverlay.GetComponent<CanvasGroup>();
            Assert.That(screenEffectImage, Is.Not.Null);
            Assert.That(screenEffectImage.raycastTarget, Is.False);
            Assert.That(screenEffectGroup, Is.Not.Null);
            Assert.That(screenEffectGroup.blocksRaycasts, Is.False);

            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.65f);
            Assert.That(chapterTitle.gameObject.activeSelf, Is.True);
            Assert.That(chapterGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            var backgroundDepth = GetMotionProperty<Vector2>(motion, "BackgroundDepthOffset");
            var portraitDepth = GetMotionProperty<Vector2>(motion, "PortraitDepthOffset");
            Assert.That(GetMotionProperty<object>(motion, "ActiveDepthStyle").ToString(), Is.EqualTo("Drift"));
            Assert.That(backgroundDepth.magnitude, Is.GreaterThan(0.05f));
            Assert.That(portraitDepth.magnitude, Is.GreaterThan(0.01f));
            Assert.That(Vector2.Dot(backgroundDepth, portraitDepth), Is.LessThan(0f));

            dialogueView.RequestNext();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000002));
            Assert.That(GetMotionProperty<bool>(motion, "IsChapterTitleActive"), Is.False);
            Assert.That(chapterTitle.gameObject.activeSelf, Is.False);
            Assert.That(dialogueWindowImage.enabled, Is.True);
            Assert.That(speakerGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(bodyGroup.alpha, Is.EqualTo(1f).Within(0.001f));

            manager.EndDialogue();
            manager.StartDialogue(1000062);
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.That(transitionOverlay.gameObject.activeSelf, Is.False);
            Assert.That(transitionGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(irisOverlay.gameObject.activeSelf, Is.True);
            Assert.That(irisGroup.alpha, Is.GreaterThan(0.5f));
            Assert.That(GetMotionProperty<bool>(motion, "IsScreenEffectPlaying"), Is.True);
            Assert.That(GetMotionProperty<string>(motion, "ActiveScreenEffectCue"), Is.EqualTo("zoom_in"));
            Assert.That(GetMotionProperty<float>(motion, "StageEffectScale"), Is.GreaterThan(1.005f));
            Assert.That(GetMotionProperty<object>(motion, "ActiveDepthStyle").ToString(), Is.EqualTo("Still"));
            Assert.That(GetMotionProperty<Vector2>(motion, "BackgroundDepthOffset"), Is.EqualTo(Vector2.zero));
            Assert.That(GetMotionProperty<Vector2>(motion, "PortraitDepthOffset"), Is.EqualTo(Vector2.zero));
            yield return new WaitForSecondsRealtime(0.56f);
            Assert.That(irisOverlay.gameObject.activeSelf, Is.False);
            Assert.That(irisGroup.alpha, Is.EqualTo(0f).Within(0.001f));

            manager.EndDialogue();
            manager.StartDialogue(1000019);
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(
                GetMotionProperty<object>(motion, "ActiveCharacterMotion").ToString(),
                Is.EqualTo("ReactSharp"));
            Assert.That(GetMotionProperty<Vector2>(motion, "ActiveCharacterMotionOffset").magnitude, Is.GreaterThan(0.1f));
            Assert.That(GetMotionProperty<float>(motion, "ActiveCharacterMotionScale"), Is.GreaterThan(1f));
            yield return new WaitForSecondsRealtime(0.27f);
            Assert.That(GetMotionProperty<string>(motion, "ActiveSlot"), Is.EqualTo(DialogueStageSlot.Left));
            Assert.That(ColorEnergy(left.color), Is.GreaterThan(ColorEnergy(right.color)));

            manager.RequestNext();
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000020));
            Assert.That(GetMotionProperty<string>(motion, "ActiveSlot"), Is.EqualTo(DialogueStageSlot.Right));
            Assert.That(ColorEnergy(right.color), Is.GreaterThan(ColorEnergy(left.color)));

            var girlState = manager.CaptureState();
            manager.RequestNext();
            yield return null;
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000021));
            Assert.That(manager.RestoreState(girlState), Is.True);
            yield return null;
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000020));
            Assert.That(GetMotionProperty<string>(motion, "ActiveSlot"), Is.EqualTo(DialogueStageSlot.Right));

            manager.EndDialogue();
            manager.StartDialogue(1000001);
            dialogueView.CompleteTyping();
            yield return null;
            Assert.That(transitionOverlay.gameObject.activeSelf, Is.True);
            Assert.That(manager.RestoreState(girlState), Is.True);
            yield return null;
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000020));
            Assert.That(transitionOverlay.gameObject.activeSelf, Is.False);
            Assert.That(transitionGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(chapterTitle.gameObject.activeSelf, Is.False);
            Assert.That(dialogueWindowImage.enabled, Is.True);

            manager.EndDialogue();
            manager.StartDialogue(1200046);
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(GetMotionProperty<bool>(motion, "IsScreenEffectPlaying"), Is.True);
            Assert.That(
                GetMotionProperty<string>(motion, "ActiveScreenEffectCue"),
                Is.EqualTo("shake_impact|flash_white"));
            Assert.That(GetMotionProperty<Vector2>(motion, "StageEffectOffset").magnitude, Is.GreaterThan(0.05f));
            Assert.That(GetMotionProperty<float>(motion, "ScreenEffectOverlayAlpha"), Is.GreaterThan(0.05f));
            Assert.That(manager.RestoreState(girlState), Is.True);
            yield return null;
            Assert.That(GetMotionProperty<bool>(motion, "IsScreenEffectPlaying"), Is.False);
            Assert.That(GetMotionProperty<Vector2>(motion, "StageEffectOffset"), Is.EqualTo(Vector2.zero));
            Assert.That(GetMotionProperty<float>(motion, "StageEffectScale"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetMotionProperty<float>(motion, "ScreenEffectOverlayAlpha"), Is.EqualTo(0f).Within(0.001f));

            manager.EndDialogue();
            manager.StartDialogue(1000077);
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(left.enabled, Is.True);
            Assert.That(right.enabled, Is.True);
            Assert.That(GetMotionProperty<string>(motion, "ActiveSlot"), Is.EqualTo(DialogueStageSlot.Left));
            Assert.That(ColorEnergy(left.color), Is.GreaterThan(ColorEnergy(right.color)));

            manager.RequestNext();
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(manager.CurrentData.Id, Is.EqualTo(1000078));
            Assert.That(GetMotionProperty<string>(motion, "ActiveSlot"), Is.Empty);
            Assert.That(ColorEnergy(left.color), Is.EqualTo(ColorEnergy(right.color)).Within(0.02f));

            manager.EndDialogue();
            manager.StartDialogue(1000717);
            dialogueView.CompleteTyping();
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.That(
                GetMotionProperty<object>(motion, "ActiveCharacterMotion").ToString(),
                Is.EqualTo("IdleBreathe"));
            Assert.That(GetMotionProperty<float>(motion, "ActiveCharacterMotionScale"), Is.InRange(1f, 1.0121f));
            var choices = dialogueView.transform.Find("Choices");
            var choiceButtons = choices.GetComponentsInChildren<Button>(false);
            Assert.That(choiceButtons, Has.Length.EqualTo(2));
            Assert.That(choiceButtons, Is.All.Matches<Button>(button =>
                button.GetComponent(choiceMotionType) != null &&
                Mathf.Approximately(button.GetComponent<CanvasGroup>().alpha, 1f)));

            var captureDirectory = Environment.GetEnvironmentVariable("WHITE_ROOM_CAPTURE_DIR");
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-motion-choice.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000019);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.08f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-character-react-sharp.png"));
                yield return new WaitForSecondsRealtime(0.27f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-motion-rei-focus.png"));

                manager.RequestNext();
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.35f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-motion-girl-focus.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000077);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.35f);
                yield return new WaitForEndOfFrame();
                WriteGeometry(
                    Path.Combine(captureDirectory, "dialogue-geometry.txt"),
                    dialogueView);
                WriteCapture(Path.Combine(captureDirectory, "dialogue-motion-two-placeholders.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000001);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.42f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-transition-cold-chapter.png"));

                manager.EndDialogue();
                manager.StartDialogue(1008625);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.26f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-transition-alarm-chapter.png"));

                manager.EndDialogue();
                manager.StartDialogue(1200046);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.06f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-screen-effect-impact.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000356);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.055f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-screen-effect-alarm.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000062);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.20f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-screen-effect-zoom.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000062);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.08f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-transition-iris.png"));

                manager.EndDialogue();
                manager.StartDialogue(1004397);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.25f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-transition-wipe-left.png"));

                manager.EndDialogue();
                manager.StartDialogue(1002628);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.25f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-transition-wipe-right.png"));

                manager.EndDialogue();
                manager.StartDialogue(1008625);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.70f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-depth-tense.png"));

                manager.EndDialogue();
                manager.StartDialogue(1004397);
                dialogueView.CompleteTyping();
                yield return new WaitForSecondsRealtime(0.70f);
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-depth-intimate.png"));
            }
        }

        private static float ColorEnergy(Color color)
        {
            return color.r + color.g + color.b;
        }

        private static T GetMotionProperty<T>(Component motion, string propertyName)
        {
            Assert.That(motion, Is.Not.Null);
            var property = motion.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(motion);
        }

        private static void WriteGeometry(string path, DialogueView view)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var names = new[] { "DialogueView(Clone)", "SpeakerText", "BodyText" };
            var lines = new[] { $"screen={Screen.width}x{Screen.height}" }
                .Concat(names.Select(name =>
                {
                    var target = name == "DialogueView(Clone)"
                        ? view.transform as RectTransform
                        : view.transform.Find(name) as RectTransform;
                    var corners = new Vector3[4];
                    target.GetWorldCorners(corners);
                    return $"{name}: rect={target.rect} anchored={target.anchoredPosition} " +
                           $"size={target.sizeDelta} min={corners[0]} max={corners[2]}";
                }));
            File.WriteAllLines(path, lines);
        }

        private static void WriteCapture(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(texture, Is.Not.Null, "The gameplay frame could not be captured.");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
        }

        private static void DestroyRuntimeObjects()
        {
            foreach (var manager in Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Object.Destroy(manager.gameObject);

            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (canvas.name == "NovelDialogueCanvas")
                    Object.Destroy(canvas.gameObject);
            }

            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Object.Destroy(eventSystem.gameObject);

            var bootstrapType = Type.GetType("WhiteRoom.Novel.NovelGameBootstrap, Assembly-CSharp");
            if (bootstrapType == null)
                return;

            foreach (var bootstrap in Object.FindObjectsByType(
                         bootstrapType,
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Object.Destroy(((Component)bootstrap).gameObject);
        }
    }
}
