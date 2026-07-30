using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomScreenshotPlayModeTests
    {
        [UnityTest]
        public IEnumerator FullResolutionPngsUseUniqueNamesAndReleaseSourceTextures()
        {
            var directory = Path.Combine(Path.GetTempPath(), "WhiteRoomScreenshotPlay_" + Guid.NewGuid().ToString("N"));
            var textures = new List<Texture2D>();
            var storageType = RequireType("WhiteRoom.Novel.FileScreenshotStorage, Assembly-CSharp");
            var serviceType = RequireType("WhiteRoom.Novel.ScreenshotCaptureService, Assembly-CSharp");
            var storage = Activator.CreateInstance(storageType, directory);
            var service = Activator.CreateInstance(
                serviceType,
                storage,
                new Func<DateTime>(() => new DateTime(2026, 7, 30, 12, 1, 2, 345, DateTimeKind.Utc)),
                new Func<Texture2D>(() =>
                {
                    var texture = new Texture2D(96, 54, TextureFormat.RGB24, false);
                    texture.Apply(false, false);
                    textures.Add(texture);
                    return texture;
                }));

            try
            {
                for (var index = 0; index < 3; index++)
                {
                    var routine = BeginCapture(service);
                    while (routine.MoveNext())
                        yield return routine.Current;
                    yield return null;
                    Assert.That(textures[index] == null, Is.True, "Captured Texture2D must be destroyed.");
                }

                var files = Directory.GetFiles(directory, "*.png")
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(files, Is.EqualTo(new[]
                {
                    "WhiteRoom_20260730_120102_345Z.png",
                    "WhiteRoom_20260730_120102_345Z_001.png",
                    "WhiteRoom_20260730_120102_345Z_002.png"
                }));

                var bytes = File.ReadAllBytes(Path.Combine(directory, files[0]));
                var decoded = new Texture2D(2, 2);
                try
                {
                    Assert.That(decoded.LoadImage(bytes), Is.True);
                    Assert.That(decoded.width, Is.EqualTo(96));
                    Assert.That(decoded.height, Is.EqualTo(54));
                }
                finally
                {
                    UnityEngine.Object.Destroy(decoded);
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator CaptureUiExcludesCommandsTooltipAndNotificationAndHonorsHiddenMessage()
        {
            object commandBar = null;
            object notifications = null;
            object captureUi = null;
            var messageHidden = false;
            try
            {
                commandBar = CreateCommandBar();
                InjectCommandBarRoots(commandBar);
                Invoke(commandBar, "SetSceneVisible", true);
                var commandRoot = (GameObject)GetProperty(commandBar, "Root");

                var notificationType = RequireType("WhiteRoom.Novel.NovelNotificationController, Assembly-CSharp");
                notifications = Activator.CreateInstance(notificationType);
                Invoke(notifications, "ShowInfo", "Ready");
                Assert.That((bool)GetProperty(notifications, "IsVisible"), Is.True);

                var uiType = RequireType("WhiteRoom.Novel.ScreenshotCaptureUiController, Assembly-CSharp");
                captureUi = Activator.CreateInstance(
                    uiType,
                    commandBar,
                    notifications,
                    new Func<bool>(() => true),
                    new Func<bool>(() => messageHidden));

                Invoke(captureUi, "HideForCapture");
                Assert.That(commandRoot.activeSelf, Is.False);
                Assert.That((bool)GetProperty(notifications, "IsVisible"), Is.False);
                Assert.That((bool)GetProperty(captureUi, "IsCaptureUiHidden"), Is.True);

                Invoke(captureUi, "RestoreAfterCapture");
                Assert.That(commandRoot.activeSelf, Is.True);
                Assert.That((bool)GetProperty(notifications, "IsVisible"), Is.True);

                messageHidden = true;
                Invoke(captureUi, "HideForCapture");
                Invoke(captureUi, "RestoreAfterCapture");
                Assert.That(commandRoot.activeSelf, Is.False,
                    "A screenshot requested while messages are hidden must preserve that state.");
            }
            finally
            {
                if (commandBar is IDisposable disposable)
                    disposable.Dispose();
                var canvas = GameObject.Find("NovelDialogueCanvas");
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas);
            }
            yield return null;
        }

        private static IEnumerator BeginCapture(object service)
        {
            var method = service.GetType().GetMethod("TryBegin", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { null };
            Assert.That((bool)method.Invoke(service, arguments), Is.True);
            Assert.That(arguments[0], Is.InstanceOf<IEnumerator>());
            return (IEnumerator)arguments[0];
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
            SetField(commandBar, "_root", new GameObject("Issue44CommandBar", typeof(RectTransform)));
            SetField(commandBar, "_revealZone", new GameObject("Issue44RevealZone", typeof(RectTransform)));
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

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
        }
    }
}
