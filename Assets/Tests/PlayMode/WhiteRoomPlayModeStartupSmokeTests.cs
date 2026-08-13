using System;
using System.Collections;
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
        public IEnumerator TitleStartupBuildsNovelRuntimeWithoutUnexpectedLogs()
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

            Assert.That(Object.FindObjectsByType<DialogueManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Count(canvas => canvas.name == "NovelDialogueCanvas"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.EqualTo(1));
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
