using System;
using System.Reflection;
using kkmia.TalkSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhiteRoom.Novel.Editor
{
    public static class DialoguePlaybackControlsValidation
    {
        private const string MenuPath = "Tools/WhiteRoom/Validate Dialogue Playback Controls";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            ResetDialogueManager();
            var viewObject = new GameObject("PlaybackValidationView");
            var managerObject = new GameObject("PlaybackValidationManager");
            var view = viewObject.AddComponent<DialogueView>();
            var manager = managerObject.AddComponent<DialogueManager>();
            DialoguePlaybackController playback = null;

            try
            {
                SetPrivateField(manager, "csvFile", new TextAsset(
                    "Id,Speaker,Text,NextId\n" +
                    "1,A,First,2\n" +
                    "2,A,Second,3\n" +
                    "3,A,Third,4\n" +
                    "4,A,Last,-1\n"));
                SetPrivateField(manager, "view", view);
                Invoke(manager, "Awake");
                playback = managerObject.AddComponent<DialoguePlaybackController>();
                Invoke(playback, "Awake");
                Invoke(playback, "OnEnable");

                manager.StartDialogue(1);
                view.RequestNext();
                view.RequestNext();
                Require(manager.CurrentData.Id == 3 && manager.CanRollback, "Validation dialogue must have rollback history.");

                playback.SetMode(DialoguePlaybackMode.Auto);
                var backSkip = new DialogueBackSkipController(manager, playback, 0.12f);
                var stateChanges = 0;
                backSkip.StateChanged += _ => stateChanges++;
                Require(backSkip.Start(), "Back Skip must start when rollback history exists.");
                Require(playback.Mode == DialoguePlaybackMode.Normal, "Back Skip must cancel Auto and Skip modes.");

                backSkip.Tick(0f, false);
                Require(manager.CurrentData.Id == 2, "Back Skip must roll back one entry immediately.");
                backSkip.Tick(0.05f, false);
                Require(manager.CurrentData.Id == 2, "Back Skip speed must use elapsed time, not frame count.");
                backSkip.Tick(0.13f, false);
                Require(manager.CurrentData.Id == 1, "Back Skip must continue after its configured interval.");
                Require(!backSkip.IsActive, "Back Skip must stop at the rollback boundary.");

                view.RequestNext();
                Require(manager.CurrentData.Id == 2, "Dialogue must advance for cancellation validation.");
                Require(backSkip.Start(), "Back Skip must restart after advancing.");
                backSkip.Tick(1f, true);
                Require(!backSkip.IsActive && manager.CurrentData.Id == 2, "Arbitrary input must stop Back Skip before rollback.");
                Require(backSkip.Start(), "Back Skip must start for toggle cancellation.");
                backSkip.Toggle();
                Require(!backSkip.IsActive, "Pressing Back Skip again must stop it without restarting.");
                Require(stateChanges == 6, "Back Skip must report each start and stop transition.");

                Debug.Log("Dialogue playback controls validation passed.");
            }
            finally
            {
                if (playback != null)
                    Invoke(playback, "OnDisable");
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(viewObject);
                ResetDialogueManager();
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Required field was not found: " + name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "Required method was not found: " + methodName);
            method.Invoke(target, null);
        }

        private static void ResetDialogueManager()
        {
            var method = typeof(DialogueManager).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Dialogue playback validation failed: " + message);
        }
    }
}
