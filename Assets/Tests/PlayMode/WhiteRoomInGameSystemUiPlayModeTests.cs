using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomInGameSystemUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator MessageHidePreservesStageAndReusesOneRecoveryDriver()
        {
            var viewObject = new GameObject("Issue42DialogueView", typeof(RectTransform), typeof(DialogueView));
            var stageObject = new GameObject("Issue42Stage");
            object commandBar = null;
            object controller = null;
            try
            {
                commandBar = CreateCommandBar();
                InjectCommandBarRoots(commandBar);
                Invoke(commandBar, "SetSceneVisible", true);
                var commandRoot = (GameObject)GetProperty(commandBar, "Root");

                var controllerType = RequireType("WhiteRoom.Novel.MessageWindowVisibilityController, Assembly-CSharp");
                controller = Activator.CreateInstance(
                    controllerType,
                    viewObject.GetComponent<DialogueView>(),
                    commandBar,
                    new Func<bool>(() => true));

                for (var index = 0; index < 4; index++)
                {
                    Assert.That((bool)Invoke(controller, "Hide"), Is.True);
                    Assert.That(viewObject.activeSelf, Is.False);
                    Assert.That(commandRoot.activeSelf, Is.False);
                    Assert.That(stageObject.activeSelf, Is.True, "Stage presentation must remain visible.");
                    Assert.That((bool)Invoke(controller, "Restore"), Is.True);
                    Assert.That(viewObject.activeSelf, Is.True);
                    Assert.That(commandRoot.activeSelf, Is.True);
                }

                var recoveryCount = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Count(item => item.name == "MessageWindowRecoveryInput");
                Assert.That(recoveryCount, Is.EqualTo(1));
            }
            finally
            {
                if (controller is IDisposable disposable)
                    disposable.Dispose();
                if (commandBar is IDisposable commandDisposable)
                    commandDisposable.Dispose();
                UnityEngine.Object.Destroy(viewObject);
                UnityEngine.Object.Destroy(stageObject);
                var canvas = GameObject.Find("NovelDialogueCanvas");
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirtyTitleReturnSupportsCancelConfirmAndRapidInputGuard()
        {
            var resets = 0;
            var returns = 0;
            var serviceType = RequireType("WhiteRoom.Novel.TitleReturnService, Assembly-CSharp");
            var service = Activator.CreateInstance(
                serviceType,
                new Action(() => resets++),
                new Action(() => returns++));
            var controllerType = RequireType("WhiteRoom.Novel.TitleReturnConfirmationController, Assembly-CSharp");
            var controller = Activator.CreateInstance(controllerType, service);

            try
            {
                Invoke(service, "MarkProgressChanged");
                Assert.That((bool)Invoke(controller, "Request"), Is.True);
                Assert.That((bool)GetProperty(controller, "IsOpen"), Is.True);
                var root = GameObject.Find("TitleReturnConfirmation");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));

                Invoke(controller, "Cancel");
                Assert.That((bool)GetProperty(controller, "IsOpen"), Is.False);
                Assert.That(resets, Is.Zero);
                Assert.That(returns, Is.Zero);

                Assert.That((bool)Invoke(controller, "Request"), Is.True);
                Assert.That((bool)Invoke(controller, "Confirm"), Is.True);
                Assert.That(resets, Is.EqualTo(1));
                Assert.That(returns, Is.EqualTo(1));
                Assert.That((bool)Invoke(controller, "Request"), Is.False,
                    "Repeated Title input during transition must be rejected.");
            }
            finally
            {
                var root = GameObject.Find("TitleReturnConfirmation");
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                var canvas = GameObject.Find("NovelDialogueCanvas");
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas);
            }
            yield return null;
        }

        private static object CreateCommandBar()
        {
            var bindingsType = RequireType("WhiteRoom.Novel.NovelCommandBarBindings, Assembly-CSharp");
            var catalogType = RequireType("WhiteRoom.Novel.NovelCommandCatalog, Assembly-CSharp");
            var controllerType = RequireType("WhiteRoom.Novel.NovelCommandBarController, Assembly-CSharp");
            var bindings = Activator.CreateInstance(bindingsType);
            var definitions = catalogType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, new[] { bindings });
            Assert.That(definitions, Is.Not.Null);
            return Activator.CreateInstance(controllerType, definitions);
        }

        private static void InjectCommandBarRoots(object commandBar)
        {
            var root = new GameObject("Issue42CommandBar", typeof(RectTransform));
            var reveal = new GameObject("Issue42RevealZone", typeof(RectTransform));
            SetField(commandBar, "_root", root);
            SetField(commandBar, "_revealZone", reveal);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
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
                .FirstOrDefault(candidate =>
                    candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
        }
    }
}
