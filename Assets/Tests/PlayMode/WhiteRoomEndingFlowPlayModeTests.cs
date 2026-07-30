using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomEndingFlowPlayModeTests
    {
        private const string FlowTypeName = "WhiteRoom.Novel.EndingFlowService, Assembly-CSharp";
        private const string BootstrapTypeName = "WhiteRoom.Novel.NovelGameBootstrap, Assembly-CSharp";

        [UnityTest]
        public IEnumerator TwoDifferentEndingsShowOnlyAfterTheirFinalTextCompletes()
        {
            yield return DestroyExistingManager();
            var managerObject = new GameObject("EndingFlowManager", typeof(DialogueManager));
            var viewObject = new GameObject("EndingFlowView", typeof(RectTransform), typeof(DialogueView));
            var manager = managerObject.GetComponent<DialogueManager>();
            var view = viewObject.GetComponent<DialogueView>();
            object flow = null;

            try
            {
                manager.SetView(view);
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId,EndingKey\n" +
                    "1,地の文,【BAD END】試験の部屋,-1,bad_test\n" +
                    "2,地の文,【TRUE END】自由の名前,-1,true_test\n")));
                yield return null;
                yield return null;

                var flowType = RequireType(FlowTypeName);
                flow = Activator.CreateInstance(
                    flowType,
                    new Func<bool>(() => true),
                    new Action(() => { }),
                    new Action(() => { }));
                Invoke(flow, "AttachTo", manager);

                yield return ReachEnding(manager, flow, 1, "bad_test", "BAD END", "試験の部屋");
                Assert.That((bool)GetProperty(flow, "ConfirmAndReturnToTitle"), Is.True);
                Invoke(flow, "NotifySceneLoaded");
                yield return ReachEnding(manager, flow, 2, "true_test", "TRUE END", "自由の名前");
            }
            finally
            {
                if (flow is IDisposable disposable)
                    disposable.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(viewObject);
            }
        }

        [UnityTest]
        public IEnumerator BootstrapDontDestroySingletonRemovesDuplicateInstance()
        {
            var bootstrapType = RequireType(BootstrapTypeName);
            foreach (var existing in UnityEngine.Object.FindObjectsByType(
                         bootstrapType,
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(existing);
            yield return null;

            var first = new GameObject("BootstrapFirst");
            var second = new GameObject("BootstrapSecond");
            try
            {
                var firstComponent = first.AddComponent(bootstrapType) as Behaviour;
                if (firstComponent != null)
                    firstComponent.enabled = false;
                var secondComponent = second.AddComponent(bootstrapType) as Behaviour;
                if (secondComponent != null)
                    secondComponent.enabled = false;
                yield return null;

                var instances = UnityEngine.Object.FindObjectsByType(
                    bootstrapType,
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                Assert.That(instances.Length, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.Destroy(first);
                UnityEngine.Object.Destroy(second);
            }
        }

        private static IEnumerator ReachEnding(
            DialogueManager manager,
            object flow,
            int id,
            string expectedKey,
            string expectedType,
            string expectedName)
        {
            manager.StartDialogue(id);
            yield return null;
            Assert.That(Get(flow, "CurrentResult"), Is.Null, "Marker alone must not open the result.");

            manager.RequestNext();
            yield return null;
            if (Get(flow, "CurrentResult") == null)
            {
                manager.RequestNext();
                yield return null;
            }

            var result = Get(flow, "CurrentResult");
            Assert.That(result, Is.Not.Null);
            Assert.That(Get(result, "EndingKey"), Is.EqualTo(expectedKey));
            Assert.That(Get(result, "Type"), Is.EqualTo(expectedType));
            Assert.That(Get(result, "DisplayName"), Is.EqualTo(expectedName));
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

        private static object Get(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static object GetProperty(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, null);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }
    }
}
