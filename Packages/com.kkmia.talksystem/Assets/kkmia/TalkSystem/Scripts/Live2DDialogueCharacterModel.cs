// Live2D Cubism 連携。Cubism SDK をインポートし、Scripting Define Symbols に
// TALKSYSTEM_LIVE2D を追加すると有効化される。未定義時はコンパイル対象に含まれない。
#if TALKSYSTEM_LIVE2D
using Live2D.Cubism.Core;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// Live2D Cubism モデル用の立ち絵。リップシンクの口開きを Cubism パラメータへ反映する。
    /// 表情/アニメは Animator 連携（基底）か、必要に応じて本クラスを継承して拡張する。
    /// </summary>
    public class Live2DDialogueCharacterModel : DialogueCharacterModel
    {
        [Header("Live2D")]
        [SerializeField] private CubismModel cubismModel;
        [SerializeField] private string mouthOpenParameterId = "ParamMouthOpenY";
        [SerializeField] private float mouthScale = 1f;

        private CubismParameter _mouthParameter;

        protected override void Awake()
        {
            base.Awake();
            if (cubismModel == null)
                cubismModel = GetComponent<CubismModel>();
            CacheMouthParameter();
        }

        public override void SetMouthOpen(float openness)
        {
            if (_mouthParameter == null) return;
            _mouthParameter.Value = Mathf.Clamp01(openness) * mouthScale;
        }

        private void CacheMouthParameter()
        {
            _mouthParameter = null;
            if (cubismModel == null || cubismModel.Parameters == null || string.IsNullOrEmpty(mouthOpenParameterId))
                return;

            var parameters = cubismModel.Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] != null && parameters[i].Id == mouthOpenParameterId)
                {
                    _mouthParameter = parameters[i];
                    return;
                }
            }
        }
    }
}
#endif
