using UnityEngine;
using UnityEngine.Audio;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="DialogueSettings"/> の音量を AudioMixer の公開パラメータ（dB）へ反映する。
    /// BGM/SE/Voice の各 AudioSource を対応する Mixer グループへ出力しておくこと。
    /// Phase 3 の <see cref="DialogueAudioPlayer"/> には依存しない（ミキサー経由で分離）。
    /// </summary>
    public class DialogueAudioVolumeBinder : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private string masterParameter = "MasterVolume";
        [SerializeField] private string bgmParameter = "BgmVolume";
        [SerializeField] private string seParameter = "SeVolume";
        [SerializeField] private string voiceParameter = "VoiceVolume";

        [Header("Source")]
        [Tooltip("設定の供給元。未設定なら同一/親の DialoguePlaybackController を探す。")]
        [SerializeField] private DialoguePlaybackController playbackController;

        private DialogueSettings _settings;

        private void OnEnable()
        {
            if (_settings == null)
            {
                if (playbackController == null)
                    playbackController = GetComponentInParent<DialoguePlaybackController>();
                if (playbackController != null)
                    Bind(playbackController.Settings);
            }
            else
            {
                Apply();
            }
        }

        private void Start()
        {
            // PlaybackController.Awake で生成された Settings を確実に取得するため Start でも試行。
            if (_settings == null && playbackController != null)
                Bind(playbackController.Settings);
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>設定を明示的に接続する。</summary>
        public void Bind(DialogueSettings settings)
        {
            if (settings == null || _settings == settings) return;

            Unbind();
            _settings = settings;
            _settings.Changed += Apply;
            Apply();
        }

        private void Unbind()
        {
            if (_settings == null) return;
            _settings.Changed -= Apply;
            _settings = null;
        }

        private void Apply()
        {
            if (mixer == null || _settings == null) return;

            SetParam(masterParameter, _settings.MasterVolume);
            SetParam(bgmParameter, _settings.BgmVolume);
            SetParam(seParameter, _settings.SeVolume);
            SetParam(voiceParameter, _settings.VoiceVolume);
        }

        private void SetParam(string parameter, float linear)
        {
            if (!string.IsNullOrEmpty(parameter))
                mixer.SetFloat(parameter, DialogueVolume.LinearToDecibels(linear));
        }
    }
}
