using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// ボイス用 AudioSource の出力振幅をサンプリングし、口の開き具合（0..1）へ変換するリップシンク。
    /// 開き具合は <see cref="Openness"/> / <see cref="onOpennessChanged"/> で取得でき、
    /// 任意で口画像（閉じ／開き）の差し替えにも使える。Live2D 連携は Phase 6 で本値を流用する。
    /// 信号処理は <see cref="DialogueLipSyncMath"/>（純関数）に委譲している。
    /// </summary>
    public class DialogueLipSync : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("ボイスを再生する AudioSource。未設定なら同一GameObjectの DialogueAudioPlayer から取得する。")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private DialogueAudioPlayer audioPlayer;

        [Header("Sampling")]
        [SerializeField, Range(64, 1024)] private int sampleWindow = 256;
        [Tooltip("これ未満の振幅は無音とみなす。")]
        [SerializeField] private float silenceThreshold = 0.02f;
        [Tooltip("振幅から開き具合への倍率。")]
        [SerializeField] private float sensitivity = 12f;
        [Tooltip("開き具合の急変を抑える追従速度（大きいほど速い）。")]
        [SerializeField] private float smoothing = 18f;

        [Header("Optional Mouth Image")]
        [SerializeField] private Image mouthImage;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [Tooltip("この開き具合以上で口を開いた表示にする。")]
        [SerializeField, Range(0f, 1f)] private float openAtOpenness = 0.5f;

        [System.Serializable]
        public sealed class OpennessEvent : UnityEvent<float> { }

        [Header("Output")]
        public OpennessEvent onOpennessChanged = new OpennessEvent();

        private float[] _buffer;
        private float _openness;
        private bool _lastMouthOpen;

        /// <summary>現在の口の開き具合（0..1）。</summary>
        public float Openness
        {
            get { return _openness; }
        }

        private void Awake()
        {
            _buffer = new float[sampleWindow];
            if (voiceSource == null)
            {
                if (audioPlayer == null)
                    audioPlayer = GetComponent<DialogueAudioPlayer>();
                if (audioPlayer != null)
                    voiceSource = audioPlayer.VoiceSource;
            }
        }

        private void Update()
        {
            var target = 0f;

            if (voiceSource != null && voiceSource.isPlaying && voiceSource.clip != null)
            {
                voiceSource.GetOutputData(_buffer, 0);
                var rms = DialogueLipSyncMath.Rms(_buffer, _buffer.Length);
                target = DialogueLipSyncMath.Openness(rms, silenceThreshold, sensitivity);
            }

            var t = smoothing > 0f ? Mathf.Clamp01(smoothing * Time.deltaTime) : 1f;
            _openness = Mathf.Lerp(_openness, target, t);

            onOpennessChanged.Invoke(_openness);
            UpdateMouthImage();
        }

        private void UpdateMouthImage()
        {
            if (mouthImage == null) return;

            var open = _openness >= openAtOpenness;
            if (open == _lastMouthOpen) return;

            _lastMouthOpen = open;
            var sprite = open ? openSprite : closedSprite;
            if (sprite != null)
                mouthImage.sprite = sprite;
        }
    }
}
