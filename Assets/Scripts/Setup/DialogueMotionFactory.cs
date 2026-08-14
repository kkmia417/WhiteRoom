using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Creates and wires the WhiteRoom-owned transient motion layer without changing
    /// Talk System's narrative or save contracts.
    /// </summary>
    public static class DialogueMotionFactory
    {
        public static NovelDialogueMotionController Ensure(
            DialogueManager manager,
            DialogueView view,
            DialogueStageView stageView)
        {
            if (view == null)
                return null;

            var controller = view.GetComponent<NovelDialogueMotionController>();
            if (controller == null)
                controller = view.gameObject.AddComponent<NovelDialogueMotionController>();
            controller.Configure(manager, view, stageView);
            return controller;
        }
    }
}
