using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueInputRouterPlayModeTests
    {
        [UnityTest]
        public IEnumerator Router_RoutesSkipAndAutoToPlaybackController()
        {
            var root = new GameObject("DialogueRouting");
            root.AddComponent<DialogueView>();
            var controller = root.AddComponent<DialoguePlaybackController>();
            var input = root.AddComponent<ManualInputSource>();
            var router = root.AddComponent<DialogueInputRouter>();

            try
            {
                ConfigureRouter(router, input, controller, null);

                input.Raise(DialogueInputAction.Skip);
                yield return null;
                Assert.AreEqual(DialoguePlaybackMode.Skip, controller.Mode);

                input.Raise(DialogueInputAction.Auto);
                yield return null;
                Assert.AreEqual(DialoguePlaybackMode.Auto, controller.Mode);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator Router_BacklogBlocksPlaybackInputsUntilClosed()
        {
            var root = new GameObject("DialogueRoutingBacklog");
            root.AddComponent<DialogueView>();
            var controller = root.AddComponent<DialoguePlaybackController>();
            var input = root.AddComponent<ManualInputSource>();
            var router = root.AddComponent<DialogueInputRouter>();
            var backlogObject = new GameObject("Backlog");
            var panel = new GameObject("Panel");
            var backlog = backlogObject.AddComponent<DialogueBacklogView>();

            try
            {
                panel.transform.SetParent(backlogObject.transform, false);
                panel.SetActive(false);
                SetField(backlog, "panel", panel);
                ConfigureRouter(router, input, controller, backlog);

                controller.SetMode(DialoguePlaybackMode.Auto);
                input.Raise(DialogueInputAction.Backlog);
                yield return null;

                Assert.IsTrue(backlog.IsOpen);
                Assert.AreEqual(DialoguePlaybackMode.Normal, controller.Mode);

                input.Raise(DialogueInputAction.Skip);
                yield return null;
                Assert.AreEqual(DialoguePlaybackMode.Normal, controller.Mode);

                input.Raise(DialogueInputAction.Next);
                yield return null;
                Assert.IsFalse(backlog.IsOpen);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(backlogObject);
            }
        }

        [UnityTest]
        public IEnumerator Router_RollbackCancelsPlaybackMode()
        {
            var root = new GameObject("DialogueRoutingRollback");
            root.AddComponent<DialogueView>();
            var controller = root.AddComponent<DialoguePlaybackController>();
            var input = root.AddComponent<ManualInputSource>();
            var router = root.AddComponent<DialogueInputRouter>();

            try
            {
                ConfigureRouter(router, input, controller, null);

                controller.SetMode(DialoguePlaybackMode.Skip);
                input.Raise(DialogueInputAction.Rollback);
                yield return null;

                Assert.AreEqual(DialoguePlaybackMode.Normal, controller.Mode);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
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

        private sealed class ManualInputSource : MonoBehaviour, IDialogueInputSource
        {
            public event Action<DialogueInputAction> InputReceived;

            public void Raise(DialogueInputAction action)
            {
                if (InputReceived != null)
                    InputReceived(action);
            }
        }
    }
}
