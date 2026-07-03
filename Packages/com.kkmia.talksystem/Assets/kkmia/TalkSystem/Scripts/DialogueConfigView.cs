using UnityEngine;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// コンフィグ画面。スライダー/トグルを <see cref="DialogueSettings"/> に双方向バインドし、
    /// 変更を <see cref="IDialogueSettingsStore"/> に保存する。各 UI 参照は任意（未設定なら無視）。
    /// </summary>
    public class DialogueConfigView : MonoBehaviour
    {
        [Header("Volume (0..1)")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("Text / Auto")]
        [SerializeField] private Slider textSpeedSlider;
        [SerializeField] private Slider autoDelaySlider;
        [SerializeField] private Toggle skipReadOnlyToggle;

        [Header("Source")]
        [Tooltip("設定の供給元。未設定なら同一/親の DialoguePlaybackController を探す。")]
        [SerializeField] private DialoguePlaybackController playbackController;

        private DialogueSettings _settings;
        private IDialogueSettingsStore _store;
        private bool _suppress;

        private void OnEnable()
        {
            if (_settings == null)
            {
                if (playbackController == null)
                    playbackController = GetComponentInParent<DialoguePlaybackController>();
                if (playbackController != null)
                    Bind(playbackController.Settings);
            }

            HookUi();
            SyncFromSettings();
        }

        private void Start()
        {
            if (_settings == null && playbackController != null)
            {
                Bind(playbackController.Settings);
                SyncFromSettings();
            }
        }

        private void OnDisable()
        {
            UnhookUi();
            if (_settings != null)
                _settings.Changed -= SyncFromSettings;
            if (_settings != null && _store != null)
                _settings.Save(_store);
        }

        /// <summary>設定と保存先を明示的に接続する。</summary>
        public void Bind(DialogueSettings settings, IDialogueSettingsStore store = null)
        {
            if (_settings == settings) return;

            if (_settings != null)
                _settings.Changed -= SyncFromSettings;

            _settings = settings;
            _store = store ?? _store ?? new PlayerPrefsDialogueSettingsStore();

            if (_settings != null)
                _settings.Changed += SyncFromSettings;
        }

        private void HookUi()
        {
            AddListener(masterSlider, v => Apply(() => _settings.MasterVolume = v));
            AddListener(bgmSlider, v => Apply(() => _settings.BgmVolume = v));
            AddListener(seSlider, v => Apply(() => _settings.SeVolume = v));
            AddListener(voiceSlider, v => Apply(() => _settings.VoiceVolume = v));
            AddListener(textSpeedSlider, v => Apply(() => _settings.TextSpeed = v));
            AddListener(autoDelaySlider, v => Apply(() => _settings.AutoAdvanceDelay = v));

            if (skipReadOnlyToggle != null)
                skipReadOnlyToggle.onValueChanged.AddListener(v => Apply(() => _settings.SkipReadOnly = v));
        }

        private void UnhookUi()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveAllListeners();
            if (seSlider != null) seSlider.onValueChanged.RemoveAllListeners();
            if (voiceSlider != null) voiceSlider.onValueChanged.RemoveAllListeners();
            if (textSpeedSlider != null) textSpeedSlider.onValueChanged.RemoveAllListeners();
            if (autoDelaySlider != null) autoDelaySlider.onValueChanged.RemoveAllListeners();
            if (skipReadOnlyToggle != null) skipReadOnlyToggle.onValueChanged.RemoveAllListeners();
        }

        private static void AddListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
        {
            if (slider != null)
                slider.onValueChanged.AddListener(action);
        }

        private void Apply(System.Action change)
        {
            if (_settings == null || _suppress) return;
            change();
            if (_store != null)
                _settings.Save(_store);
        }

        private void SyncFromSettings()
        {
            if (_settings == null) return;

            _suppress = true;
            SetSlider(masterSlider, _settings.MasterVolume);
            SetSlider(bgmSlider, _settings.BgmVolume);
            SetSlider(seSlider, _settings.SeVolume);
            SetSlider(voiceSlider, _settings.VoiceVolume);
            SetSlider(textSpeedSlider, _settings.TextSpeed);
            SetSlider(autoDelaySlider, _settings.AutoAdvanceDelay);
            if (skipReadOnlyToggle != null)
                skipReadOnlyToggle.isOn = _settings.SkipReadOnly;
            _suppress = false;
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }
    }
}
