using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Project-owned entry point for presentation databases used by the
    /// runtime-created bootstrap.
    /// </summary>
    [CreateAssetMenu(fileName = "NovelPresentationConfiguration", menuName = "WhiteRoom/Novel Presentation Configuration")]
    public sealed class NovelPresentationConfiguration : ScriptableObject
    {
        public const string DefaultResourcePath = "WhiteRoom/NovelPresentationConfiguration";

        [SerializeField] private BackgroundDatabase backgroundDatabase;
        [SerializeField] private CharacterExpressionDatabase characterDatabase;
        [SerializeField] private AudioDatabase audioDatabase;

        public BackgroundDatabase BackgroundDatabase => backgroundDatabase;
        public CharacterExpressionDatabase CharacterDatabase => characterDatabase;
        public AudioDatabase AudioDatabase => audioDatabase;

        public static NovelPresentationConfiguration LoadDefault()
        {
            return Resources.Load<NovelPresentationConfiguration>(DefaultResourcePath);
        }
    }
}
