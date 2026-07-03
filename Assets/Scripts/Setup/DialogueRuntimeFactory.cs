using System;
using System.Reflection;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Locates or creates the TalkSystem runtime components (manager, save system,
    /// playback controller, keyboard input routing) at startup.
    /// </summary>
    public static class DialogueRuntimeFactory
    {
        public static DialogueManager EnsureManager()
        {
            if (DialogueManager.Instance != null)
                return DialogueManager.Instance;

            var managerObject = new GameObject("DialogueManager");
            UnityEngine.Object.DontDestroyOnLoad(managerObject);
            return managerObject.AddComponent<DialogueManager>();
        }

        public static DialogueSaveSystem EnsureSaveSystem(DialogueManager manager, string contentVersion, string productChannel)
        {
            if (manager == null)
                return null;

            var saveSystem = manager.GetComponent<DialogueSaveSystem>();
            if (saveSystem == null)
                saveSystem = manager.gameObject.AddComponent<DialogueSaveSystem>();

            saveSystem.SetSaveMetadata(contentVersion, productChannel);
            return saveSystem;
        }

        public static DialoguePlaybackController EnsurePlaybackController(DialogueManager manager)
        {
            if (manager == null)
                return null;

            var playbackController = manager.GetComponent<DialoguePlaybackController>();
            if (playbackController == null)
                playbackController = manager.gameObject.AddComponent<DialoguePlaybackController>();

            return playbackController;
        }

        public static void EnsureKeyboardInputRouting(DialogueView view, DialogueBacklogView backlog, DialoguePlaybackController playbackController)
        {
            if (view == null)
                return;

            var keyboardInput = view.GetComponent<DialogueKeyboardInput>();
            if (keyboardInput == null)
                keyboardInput = view.gameObject.AddComponent<DialogueKeyboardInput>();

            var inputRouter = view.GetComponent<DialogueInputRouter>();
            if (inputRouter == null)
                inputRouter = view.gameObject.AddComponent<DialogueInputRouter>();

            RuntimeFieldBinder.SetPrivateField(inputRouter, "inputSourceComponent", keyboardInput);
            RuntimeFieldBinder.SetPrivateField(inputRouter, "backlog", backlog);
            RuntimeFieldBinder.SetPrivateField(inputRouter, "playbackController", playbackController);

            if (view.gameObject.activeInHierarchy)
                ConnectInputRouter(inputRouter, view, keyboardInput);
        }

        private static void ConnectInputRouter(DialogueInputRouter router, DialogueView view, DialogueKeyboardInput input)
        {
            if (router == null || input == null)
                return;

            RuntimeFieldBinder.SetPrivateField(router, "_view", view);
            RuntimeFieldBinder.SetPrivateField(router, "_inputSource", input);

            var method = typeof(DialogueInputRouter).GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning("DialogueRuntimeFactory: DialogueInputRouter.HandleInput was not found.");
                return;
            }

            var handler = (Action<DialogueInputAction>)Delegate.CreateDelegate(typeof(Action<DialogueInputAction>), router, method);
            input.InputReceived -= handler;
            input.InputReceived += handler;
        }
    }
}
