using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor
{
    public static class NovelCommandBarValidation
    {
        private const string MenuPath = "Tools/WhiteRoom/Validate Novel Command Bar";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var bindings = new NovelCommandBarBindings
            {
                OpenSave = NoOp,
                OpenLoad = NoOp,
                QuickSave = NoOp,
                QuickLoad = NoOp,
                PreviousText = NoOp,
                ToggleBacklog = NoOp,
                ToggleAuto = NoOp,
                ToggleSkip = NoOp,
                CanSave = Always,
                CanQuickLoad = Always,
                HasDialogue = Always
            };

            var definitions = NovelCommandCatalog.Create(bindings);
            var controller = new NovelCommandBarController(definitions);
            try
            {
                controller.EnsureCreated();
                controller.SetSceneVisible(true);

                Require(definitions.Count == NovelCommandBarController.ExpectedCommandCount, "Expected 23 command definitions.");
                for (var i = 0; i < definitions.Count; i++)
                {
                    Require((int)definitions[i].Id == i, $"Command order differs at index {i}.");
                    Require(controller.GetButton(definitions[i].Id) != null, $"Missing button for {definitions[i].Id}.");
                }

                var rect = controller.Root.GetComponent<RectTransform>();
                Require(rect != null, "Command bar RectTransform is missing.");
                Require(rect.anchorMin == new Vector2(0.5f, 0f), "Command bar must be bottom-center anchored.");
                Require(rect.anchorMax == new Vector2(0.5f, 0f), "Command bar must use a fixed bottom-center anchor.");
                Require(controller.Root.GetComponentsInChildren<Button>(true).Length == 23, "Command bar must render 23 buttons.");

                var directSave = controller.GetButton(NovelCommandId.DirectSave);
                Require(directSave != null && !directSave.interactable, "Unbound commands must remain disabled.");

                var lockButton = controller.GetButton(NovelCommandId.ToolbarLock);
                Require(lockButton != null && controller.IsLocked, "Command bar must start locked.");
                lockButton.onClick.Invoke();
                Require(!controller.IsLocked, "Lock command must unlock the command bar.");

                controller.NotifyPointerExited();
                controller.Tick(float.MaxValue, false);
                Require(!controller.IsBarShown, "Unlocked command bar must auto-hide.");
                controller.Tick(float.MaxValue, true);
                Require(controller.IsBarShown, "Keyboard navigation must reveal the command bar.");
                controller.DismissKeyboardFocus();
                Require(!controller.IsBarShown, "Keyboard cancel must dismiss an unlocked command bar.");

                Debug.Log("Novel command bar validation passed.");
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void NoOp()
        {
        }

        private static bool Always()
        {
            return true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Novel command bar validation failed: " + message);
        }
    }
}
