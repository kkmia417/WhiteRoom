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
    }
}
