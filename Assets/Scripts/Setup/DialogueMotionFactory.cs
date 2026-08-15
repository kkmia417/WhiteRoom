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
            var canvas = view.GetComponentInParent<Canvas>(true);
            var chapterTitleView = NovelChapterTitleView.Ensure(
                canvas != null ? canvas.transform : view.transform.parent);
            controller.Configure(manager, view, stageView, chapterTitleView);
            return controller;
        }
    }
}
