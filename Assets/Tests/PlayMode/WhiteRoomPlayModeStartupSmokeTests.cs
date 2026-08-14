using System;
using System.Collections;
using System.IO;
using System.Linq;
using kkmia.TalkSystem;
using NUnit.Framework;
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

            var captureDirectory = Environment.GetEnvironmentVariable("WHITE_ROOM_CAPTURE_DIR");
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                var dialogueView = Object.FindFirstObjectByType<DialogueView>();
                dialogueView.CompleteTyping();
                yield return new WaitForEndOfFrame();
                WriteCapture(Path.Combine(captureDirectory, "dialogue-opening.png"));

                manager.EndDialogue();
                manager.StartDialogue(1000077);
                yield return null;
                dialogueView.CompleteTyping();
                yield return new WaitForEndOfFrame();
                WriteGeometry(
                    Path.Combine(captureDirectory, "dialogue-geometry.txt"),
                    dialogueView);
                WriteCapture(Path.Combine(captureDirectory, "dialogue-two-placeholders.png"));
            }
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
