using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialoguePlaybackControllerTests
    {
        [Test]
        public void Controller_ModeChangesRaiseEventForUiIndicators()
        {
            var gameObject = new GameObject("DialoguePlaybackController");
            var controller = gameObject.AddComponent<DialoguePlaybackController>();
            var modes = new List<DialoguePlaybackMode>();
            var states = new List<DialoguePlaybackState>();

            try
            {
                controller.ModeChanged += modes.Add;
                controller.StateChanged += states.Add;

                controller.SetMode(DialoguePlaybackMode.Skip);
                controller.SetMode(DialoguePlaybackMode.Skip);
                controller.ToggleAuto();

                CollectionAssert.AreEqual(
                    new[] { DialoguePlaybackMode.Skip, DialoguePlaybackMode.Auto },
                    modes);
                Assert.AreEqual(DialoguePlaybackMode.Auto, controller.Mode);
                Assert.AreEqual(2, states.Count);
                Assert.IsTrue(states[0].IsSkip);
                Assert.IsTrue(states[1].IsAuto);
                Assert.AreEqual(DialoguePlaybackMode.Auto, controller.PlaybackState.Mode);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Controller_SettingsChangesRefreshPlaybackStateImmediately()
        {
            var gameObject = new GameObject("DialoguePlaybackSettings");
            var controller = gameObject.AddComponent<DialoguePlaybackController>();
            typeof(DialoguePlaybackController)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
            var changes = 0;
            try
            {
                controller.StateChanged += _ => changes++;
                controller.Settings.TextSpeed = 0.9f;
                controller.Settings.AutoAdvanceDelay = 3f;
                Assert.That(changes, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }
    }
}
