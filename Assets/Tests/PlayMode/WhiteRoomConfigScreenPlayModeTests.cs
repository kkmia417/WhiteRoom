using System;
using System.Collections;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomConfigScreenPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConfigScreenProvidesAllControlsAndCancelClose()
        {
            var type = Type.GetType("WhiteRoom.Novel.ConfigScreenController, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var settings = new DialogueSettings();
            var controller = Activator.CreateInstance(type, settings, new PlayerPrefsDialogueSettingsStore());
            try
            {
                Invoke(controller, "Open");
                yield return null;
                var root = GameObject.Find("WhiteRoomConfigScreen");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(5));
                Assert.That(root.GetComponentsInChildren<Toggle>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentInChildren<RectTransform>(true), Is.Not.Null);

                type.GetMethod("SetTextSpeed").Invoke(controller, new object[] { 0.85f });
                Assert.That(settings.TextSpeed, Is.EqualTo(0.85f));
                Invoke(controller, "Close");
                yield return null;
                Assert.That(type.GetProperty("IsOpen").GetValue(controller), Is.EqualTo(false));
            }
            finally
            {
                var root = GameObject.Find("WhiteRoomConfigScreen");
                if (root != null) UnityEngine.Object.Destroy(root);
            }
        }

        private static void Invoke(object target, string name)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(target, null);
        }
    }
}
