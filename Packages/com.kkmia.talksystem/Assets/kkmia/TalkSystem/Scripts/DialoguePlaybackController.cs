using System;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// オート/スキップ進行・文字速度・既読記録を、設定（<see cref="DialogueSettings"/>）と
    /// 既読レジストリ（<see cref="DialogueReadRegistry"/>）に基づいて <see cref="DialogueManager"/> へ反映する。
    /// 判断ロジックは純粋な <see cref="DialoguePlaybackPlanner"/> に委譲している。
    /// </summary>
    public class DialoguePlaybackController : MonoBehaviour
    {
        [Tooltip("未設定なら DialogueManager.Instance を使う。")]
        [SerializeField] private DialogueManager manager;

        private readonly DialoguePlaybackPlanner _planner = new DialoguePlaybackPlanner();
        private DialogueSettings _settings;
        private DialogueReadRegistry _readRegistry;
        private IDialogueSettingsStore _settingsStore;
        private DialogueManager _bound;
        private bool _hasCurrentLine;
        private bool _currentLineWasRead;

        /// <summary>共有設定。コンフィグ画面や音量バインダーから参照する。</summary>
        public DialogueSettings Settings
        {
            get { return _settings; }
        }

        public DialogueReadRegistry ReadRegistry
        {
            get { return _readRegistry; }
        }

        public DialoguePlaybackMode Mode { get; private set; }

        public DialoguePlaybackState PlaybackState
        {
            get { return BuildPlaybackState(); }
        }

        public event Action<DialoguePlaybackMode> ModeChanged;
        public event Action<DialoguePlaybackState> StateChanged;

        private void Awake()
        {
            if (_settings == null)
            {
                _settings = new DialogueSettings();
                _settingsStore = new PlayerPrefsDialogueSettingsStore();
                _settings.Load(_settingsStore);
            }

            if (_readRegistry == null)
                _readRegistry = new DialogueReadRegistry(new PlayerPrefsDialogueReadStore());
        }

        private void OnEnable()
        {
            Bind(manager != null ? manager : DialogueManager.Instance);
        }

        private void Start()
        {
            if (_bound == null)
                Bind(manager != null ? manager : DialogueManager.Instance);
        }

        private void OnDisable()
        {
            if (_readRegistry != null)
                _readRegistry.Save();
            Unbind();
        }

        public void SetMode(DialoguePlaybackMode mode)
        {
            var previous = Mode;
            Mode = mode;
            ApplyTextSpeed();
            ApplyCurrentPlan();

            if (previous != Mode)
            {
                RaiseModeChanged();
                RaiseStateChanged();
            }
        }

        public void ToggleAuto()
        {
            SetMode(Mode == DialoguePlaybackMode.Auto ? DialoguePlaybackMode.Normal : DialoguePlaybackMode.Auto);
        }

        public void ToggleSkip()
        {
            SetMode(Mode == DialoguePlaybackMode.Skip ? DialoguePlaybackMode.Normal : DialoguePlaybackMode.Skip);
        }

        private void Bind(DialogueManager target)
        {
            if (target == null || _bound == target) return;

            Unbind();
            _bound = target;
            _bound.LineStarted += HandleLineStarted;
            _bound.DialogueEnded += HandleDialogueEnded;

            if (_bound.CurrentData != null)
            {
                _hasCurrentLine = true;
                _currentLineWasRead = _readRegistry == null || _readRegistry.IsRead(_bound.CurrentData.Id);
                ApplyCurrentPlan();
            }
        }

        private void Unbind()
        {
            if (_bound == null) return;
            _bound.LineStarted -= HandleLineStarted;
            _bound.DialogueEnded -= HandleDialogueEnded;
            _bound = null;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (context == null || context.Data == null) return;

            var id = context.Data.Id;
            // 既読判定はマーク前に行う（スキップの既読限定判定のため）。
            var wasRead = _readRegistry.IsRead(id);
            _readRegistry.MarkRead(id);
            _hasCurrentLine = true;
            _currentLineWasRead = wasRead;

            ApplyTextSpeed();
            var previousMode = Mode;
            ApplyCurrentPlan();
            if (previousMode == Mode)
                RaiseStateChanged();

            // Apply the playback plan after the session has exposed choice state.
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            _hasCurrentLine = false;
            _currentLineWasRead = false;

            if (_readRegistry != null)
                _readRegistry.Save();

            if (_bound != null)
                _bound.SetAutoAdvanceOverride(false, 0f);

            var previousMode = Mode;
            SetMode(DialoguePlaybackMode.Normal);
            if (previousMode == Mode)
                RaiseStateChanged();
        }

        private void ApplyCurrentPlan()
        {
            if (_bound == null)
                return;

            if (Mode == DialoguePlaybackMode.Normal || !_hasCurrentLine)
            {
                _bound.SetAutoAdvanceOverride(false, 0f);
                return;
            }

            var hasChoices = _bound.State == DialogueSessionState.ChoicePending || _bound.CurrentChoiceCount > 0;
            var plan = _planner.Plan(Mode, hasChoices, _currentLineWasRead, _settings);

            if (plan.CancelSkip)
            {
                SetMode(DialoguePlaybackMode.Normal);
                return;
            }

            _bound.SetAutoAdvanceOverride(plan.ShouldAdvance, plan.Delay);
        }

        private void ApplyTextSpeed()
        {
            if (_bound == null) return;

            var interval = Mode == DialoguePlaybackMode.Skip
                ? DialogueTextSpeed.DefaultFastestInterval
                : DialogueTextSpeed.ToInterval(_settings != null ? _settings.TextSpeed : 0.5f);

            _bound.SetTypewriterSpeed(interval);
        }

        private void RaiseModeChanged()
        {
            var handler = ModeChanged;
            if (handler != null)
                handler(Mode);
        }

        private void RaiseStateChanged()
        {
            var handler = StateChanged;
            if (handler != null)
                handler(BuildPlaybackState());
        }

        private DialoguePlaybackState BuildPlaybackState()
        {
            var hasChoices = _bound != null &&
                             (_bound.State == DialogueSessionState.ChoicePending ||
                              _bound.CurrentChoiceCount > 0);
            return new DialoguePlaybackState(Mode, _hasCurrentLine, hasChoices, _currentLineWasRead);
        }
    }
}
