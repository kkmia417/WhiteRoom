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
    public sealed class WhiteRoomAutosavePlayModeTests
    {
        private const string ServiceTypeName = "WhiteRoom.Novel.AutosaveCheckpointService, Assembly-CSharp";

        [UnityTest]
        public IEnumerator StoryCheckpointsCaptureConfirmedStateAndFailureDoesNotStopProgress()
        {
            yield return DestroyExistingManager();
            var managerObject = new GameObject("AutosaveManager", typeof(DialogueManager));
            var viewObject = new GameObject("AutosaveView", typeof(RectTransform), typeof(DialogueView));
            var manager = managerObject.GetComponent<DialogueManager>();
            var view = viewObject.GetComponent<DialogueView>();
            var titles = new List<string>();
            var snapshots = new List<DialogueSaveData>();
            object service = null;

            try
            {
                manager.SetView(view);
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,Choices,ChapterKey,EndingKey\n" +
                    "1,Narrator,Chapter start,2,,chapter_a,\n" +
                    "2,Narrator,Choose,-1,Go->3,,\n" +
                    "3,Narrator,Choice destination,4,,,\n" +
                    "4,Narrator,Ending,-1,,,ending_a\n")));
                yield return null;
                yield return null;

                var serviceType = RequireType(ServiceTypeName);
                service = Activator.CreateInstance(serviceType, new object[]
                {
                    new Func<string, bool>(title =>
                    {
                        titles.Add(title);
                        snapshots.Add(manager.CaptureState());
                        return false; // A failed write must not stop the story.
                    }),
                    null,
                    null,
                    null
                });
                Invoke(service, "AttachTo", manager);

                manager.StartDialogue(1);
                yield return null;
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.WaitingForInput));
                Invoke(service, "TryFlush");
                Assert.That(titles.Count, Is.EqualTo(1));
                Assert.That(snapshots[0].CurrentDialogueId, Is.EqualTo(1));
                Assert.That(snapshots[0].Progress.CurrentChapterKey, Is.EqualTo("chapter_a"));

                manager.RequestNext();
                yield return null;
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.ChoicePending));
                InvokePrivate(view, "SelectChoice", 0);
                yield return null;
                Invoke(service, "TryFlush");
                Assert.That(titles.Count, Is.EqualTo(2));
                Assert.That(snapshots[1].CurrentDialogueId, Is.EqualTo(3));
                Assert.That(snapshots[1].ChoiceRecords.Count, Is.EqualTo(1));
                Assert.That(snapshots[1].ChoiceRecords[0].NextId, Is.EqualTo(3));

                manager.RequestNext();
                yield return null;
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.WaitingForInput));
                manager.RequestNext();
                yield return null;
                Assert.That(titles.Count, Is.EqualTo(3));
                Assert.That(snapshots[2].CurrentDialogueId, Is.EqualTo(4));
                Assert.That(snapshots[2].Progress.CurrentEndingKey, Is.EqualTo("ending_a"));
                Assert.That(manager.State, Is.EqualTo(DialogueSessionState.Ended),
                    "Autosave failure must not interrupt DialogueEnded.");
                CollectionAssert.AreEqual(
                    new[] { "Auto: Chapter chapter_a", "Auto: Choice confirmed", "Auto: Ending ending_a" },
                    titles);
            }
            finally
            {
                if (service is IDisposable disposable)
                    disposable.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(viewObject);
            }
        }

        private static IEnumerator DestroyExistingManager()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(manager.gameObject);
            yield return null;
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = null;
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (var index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == methodName && methods[index].GetParameters().Length == arguments.Length)
                {
                    method = methods[index];
                    break;
                }
            }
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }
    }
}
