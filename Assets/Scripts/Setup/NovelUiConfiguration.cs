using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Project-owned entry point for the checked-in novel UI assets used by the
    /// runtime-created bootstrap. Keeping the references in one Resources asset
    /// avoids relying on inspector fields on a dynamically created component.
    /// </summary>
    [CreateAssetMenu(fileName = "NovelUiConfiguration", menuName = "WhiteRoom/Novel UI Configuration")]
    public sealed class NovelUiConfiguration : ScriptableObject
    {
        public const string DefaultResourcePath = "WhiteRoom/NovelUiConfiguration";

        [SerializeField] private DialogueView dialogueViewPrefab;
        [SerializeField] private DialogueBacklogView dialogueBacklogViewPrefab;
        [SerializeField] private Sprite dialogueWindowSprite;

        public DialogueView DialogueViewPrefab => dialogueViewPrefab;
        public DialogueBacklogView DialogueBacklogViewPrefab => dialogueBacklogViewPrefab;
        public Sprite DialogueWindowSprite => dialogueWindowSprite;

        public static NovelUiConfiguration LoadDefault()
        {
            return Resources.Load<NovelUiConfiguration>(DefaultResourcePath);
        }
    }
}
