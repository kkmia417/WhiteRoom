using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 1 キャラクターの立ち絵モデルを表す。既定実装は GameObject の表示切替と Animator パラメータ駆動で、
    /// SDK に依存しない（Live2D Cubism / Spine の SkeletonMecanim など Animator 連携モデルでそのまま動く）。
    /// Live2D/Spine 固有 API が必要な場合は本クラスを継承して各メソッドを override する。
    /// </summary>
    public class DialogueCharacterModel : MonoBehaviour, IDialogueLipSyncTarget
    {
        [Tooltip("CSV の Characters 列・Speaker と対応するキー。")]
        [SerializeField] private string characterKey;

        [Header("Animator (任意)")]
        [SerializeField] private Animator animator;
        [Tooltip("口の開き(0..1)を流す Animator の float パラメータ名。")]
        [SerializeField] private string mouthOpenParameter = "MouthOpen";

        public string CharacterKey
        {
            get { return characterKey; }
        }

        protected Animator Animator
        {
            get { return animator; }
        }

        protected virtual void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>このキャラクターを表示する。</summary>
        public virtual void Show(string expression, string animation)
        {
            gameObject.SetActive(true);
            SetExpression(expression);
            PlayAnimation(animation);
        }

        /// <summary>このキャラクターを退場させる。</summary>
        public virtual void Hide(string animation)
        {
            PlayAnimation(animation);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 表情を切り替える。既定は表情名と同名の Animator トリガーを発火する
        /// （例: 表情 "smile" → トリガー "smile"）。
        /// </summary>
        public virtual void SetExpression(string expression)
        {
            if (animator != null && !string.IsNullOrEmpty(expression))
                animator.SetTrigger(expression);
        }

        /// <summary>
        /// 入退場等のアニメを再生する。既定はアニメ名と同名の Animator トリガーを発火する。
        /// </summary>
        public virtual void PlayAnimation(string animation)
        {
            if (animator != null && !string.IsNullOrEmpty(animation))
                animator.SetTrigger(animation);
        }

        /// <summary>口の開き具合(0..1)を反映する。既定は Animator の float パラメータ。</summary>
        public virtual void SetMouthOpen(float openness)
        {
            if (animator != null && !string.IsNullOrEmpty(mouthOpenParameter))
                animator.SetFloat(mouthOpenParameter, Mathf.Clamp01(openness));
        }
    }
}
