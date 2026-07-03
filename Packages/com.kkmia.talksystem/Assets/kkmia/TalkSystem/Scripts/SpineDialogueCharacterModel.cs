// Spine 連携。spine-unity ランタイムをインポートし、Scripting Define Symbols に
// TALKSYSTEM_SPINE を追加すると有効化される。未定義時はコンパイル対象に含まれない。
#if TALKSYSTEM_SPINE
using Spine.Unity;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// Spine（SkeletonAnimation）用の立ち絵。表情・アニメを Spine アニメーションへマッピングする。
    /// 表情はループ再生、入退場アニメは別トラックで 1 回再生する。
    /// </summary>
    public class SpineDialogueCharacterModel : DialogueCharacterModel
    {
        [Header("Spine")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [Tooltip("表情を流すトラック番号。")]
        [SerializeField] private int expressionTrack;
        [Tooltip("入退場アニメを流すトラック番号。")]
        [SerializeField] private int animationTrack = 1;

        protected override void Awake()
        {
            base.Awake();
            if (skeletonAnimation == null)
                skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        public override void SetExpression(string expression)
        {
            if (skeletonAnimation != null && !string.IsNullOrEmpty(expression))
                skeletonAnimation.AnimationState.SetAnimation(expressionTrack, expression, true);
        }

        public override void PlayAnimation(string animation)
        {
            if (skeletonAnimation != null && !string.IsNullOrEmpty(animation))
                skeletonAnimation.AnimationState.SetAnimation(animationTrack, animation, false);
        }
    }
}
#endif
