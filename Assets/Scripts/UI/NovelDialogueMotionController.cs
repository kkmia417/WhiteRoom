using System;
using System.Collections;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Product-owned, non-blocking polish for dialogue, portraits, background and choices.
    /// Talk System remains authoritative; every transient animation is generation-cancelled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NovelDialogueMotionController : MonoBehaviour
    {
        private const float LineDuration = 0.22f;
        private const float ChoiceDuration = 0.20f;
        private const float ChoiceStagger = 0.055f;
        private const float WindowOffset = -10f;
        private const float SpeakerStartScale = 0.94f;
        private const float FocusScale = 1.025f;
        private const float ListenerScale = 0.985f;
        private const float FocusLift = 8f;
        private const float BackgroundScale = 1.08f;
        private const float ChapterRevealDelay = 0.10f;
        private const float ChapterRevealDuration = 0.48f;
        private const float ChapterExitDuration = 0.18f;
        private const string ScreenEffectColumn = "ScreenEffect";

        private static readonly Color FocusColor = Color.white;
        private static readonly Color ListenerColor = new Color(0.58f, 0.64f, 0.72f, 1f);
        private static readonly Color NeutralColor = new Color(0.86f, 0.89f, 0.94f, 1f);

        public enum StageTransitionMood
        {
            Neutral,
            Cold,
            Sterile,
            Alarm
        }

        [Flags]
        public enum ScreenEffectKind
        {
            None = 0,
            ShakeSoft = 1 << 0,
            ShakeImpact = 1 << 1,
            FlashWhite = 1 << 2,
            FlashAlarm = 1 << 3,
            ZoomIn = 1 << 4
        }

        public readonly struct ScreenEffectProfile
        {
            public ScreenEffectProfile(
                ScreenEffectKind kind,
                string cue,
                float shakeAmplitude,
                float shakeFrequency,
                float shakeDuration,
                Color flashColor,
                float flashAlpha,
                float flashDuration,
                float zoomScale,
                float zoomDuration,
                float phase)
            {
                Kind = kind;
                Cue = cue ?? string.Empty;
                ShakeAmplitude = shakeAmplitude;
                ShakeFrequency = shakeFrequency;
                ShakeDuration = shakeDuration;
                FlashColor = flashColor;
                FlashAlpha = flashAlpha;
                FlashDuration = flashDuration;
                ZoomScale = zoomScale;
                ZoomDuration = zoomDuration;
                Phase = phase;
                Duration = Mathf.Max(shakeDuration, Mathf.Max(flashDuration, zoomDuration));
            }

            public ScreenEffectKind Kind { get; }
            public string Cue { get; }
            public float ShakeAmplitude { get; }
            public float ShakeFrequency { get; }
            public float ShakeDuration { get; }
            public Color FlashColor { get; }
            public float FlashAlpha { get; }
            public float FlashDuration { get; }
            public float ZoomScale { get; }
            public float ZoomDuration { get; }
            public float Phase { get; }
            public float Duration { get; }
        }

        public readonly struct StageTransitionProfile
        {
            public StageTransitionProfile(
                StageTransitionMood mood,
                Color veilColor,
                float startAlpha,
                float duration,
                bool alertPulse)
            {
                Mood = mood;
                VeilColor = veilColor;
                StartAlpha = startAlpha;
                Duration = duration;
                AlertPulse = alertPulse;
            }

            public StageTransitionMood Mood { get; }
            public Color VeilColor { get; }
            public float StartAlpha { get; }
            public float Duration { get; }
            public bool AlertPulse { get; }
        }

        public readonly struct ChapterTitleContent
        {
            public ChapterTitleContent(string ordinal, string title)
            {
                Ordinal = ordinal ?? string.Empty;
                Title = title ?? string.Empty;
            }

            public string Ordinal { get; }
            public string Title { get; }
        }

        private DialogueManager _manager;
        private DialogueView _view;
        private DialogueStageView _stageView;
        private NovelChapterTitleView _chapterTitleView;
        private RectTransform _windowRect;
        private CanvasGroup _windowGroup;
        private Image _dialogueWindowImage;
        private RectTransform _speakerRect;
        private CanvasGroup _speakerGroup;
        private RectTransform _bodyRect;
        private CanvasGroup _bodyGroup;
        private RectTransform _choicesContainer;
        private RectTransform _nextRect;
        private RectTransform _stageRect;
        private RectTransform _backgroundRect;
        private Image _backgroundImage;
        private Image _transitionImage;
        private CanvasGroup _transitionGroup;
        private Image _screenEffectImage;
        private CanvasGroup _screenEffectGroup;
        private readonly List<PortraitVisual> _portraits = new List<PortraitVisual>();
        private readonly List<ChoiceReveal> _choiceReveals = new List<ChoiceReveal>();
        private Coroutine _lineMotion;
        private Coroutine _transitionMotion;
        private Coroutine _chapterMotion;
        private Coroutine _screenEffectMotion;
        private int _generation;
        private bool _bound;
        private bool _configured;
        private bool _dialogueActive;
        private bool _chapterTitleActive;
        private int _observedDialogueId = -1;
        private float _backgroundPhase;
        private Vector2 _windowBasePosition;
        private Vector3 _windowBaseScale = Vector3.one;
        private Vector3 _speakerBaseScale = Vector3.one;
        private Vector3 _nextBaseScale = Vector3.one;
        private Vector2 _backgroundBasePosition;
        private Vector3 _backgroundBaseScale = Vector3.one;
        private Vector2 _stageBasePosition;
        private Vector3 _stageBaseScale = Vector3.one;
        private string _activeScreenEffectCue = string.Empty;
        private bool _dialogueWindowEnabledAtBaseline = true;

        public bool IsConfigured => _configured;
        public DialogueManager BoundManager => _manager;
        public string ActiveSlot { get; private set; } = string.Empty;
        public int AnimationGeneration => _generation;
        public float TransitionOverlayAlpha => _transitionGroup != null ? _transitionGroup.alpha : 0f;
        public bool IsTransitionPlaying => _transitionMotion != null;
        public bool IsChapterTitleActive => _chapterTitleActive;
        public NovelChapterTitleView ChapterTitleView => _chapterTitleView;
        public bool IsScreenEffectPlaying => _screenEffectMotion != null;
        public string ActiveScreenEffectCue => _activeScreenEffectCue;
        public float ScreenEffectOverlayAlpha => _screenEffectGroup != null ? _screenEffectGroup.alpha : 0f;
        public Vector2 StageEffectOffset => _stageRect != null
            ? _stageRect.anchoredPosition - _stageBasePosition
            : Vector2.zero;
        public float StageEffectScale => _stageRect != null && !Mathf.Approximately(_stageBaseScale.x, 0f)
            ? _stageRect.localScale.x / _stageBaseScale.x
            : 1f;

        public void Configure(
            DialogueManager manager,
            DialogueView view,
            DialogueStageView stageView,
            NovelChapterTitleView chapterTitleView)
        {
            Unbind();
            _manager = manager;
            _view = view;
            _stageView = stageView;
            _chapterTitleView = chapterTitleView;

            _windowRect = view != null ? view.transform as RectTransform : null;
            _windowGroup = EnsureCanvasGroup(view != null ? view.gameObject : null);
            _dialogueWindowImage = view != null ? view.GetComponent<Image>() : null;
            _speakerRect = FindDescendant(view != null ? view.transform : null, "SpeakerText") as RectTransform;
            _speakerGroup = EnsureCanvasGroup(_speakerRect != null ? _speakerRect.gameObject : null);
            _bodyRect = FindDescendant(view != null ? view.transform : null, "BodyText") as RectTransform;
            _bodyGroup = EnsureCanvasGroup(_bodyRect != null ? _bodyRect.gameObject : null);
            _choicesContainer = FindDescendant(view != null ? view.transform : null, "Choices") as RectTransform;
            _nextRect = FindDescendant(view != null ? view.transform : null, "NextButton") as RectTransform;
            _stageRect = stageView != null ? stageView.transform as RectTransform : null;
            _backgroundRect = FindDescendant(stageView != null ? stageView.transform : null, "Background") as RectTransform;
            _backgroundImage = _backgroundRect != null ? _backgroundRect.GetComponent<Image>() : null;
            EnsureTransitionOverlay();
            EnsureScreenEffectOverlay();

            _portraits.Clear();
            AddPortrait(DialogueStageSlot.Left, "LeftCharacter");
            AddPortrait(DialogueStageSlot.Center, "CenterCharacter");
            AddPortrait(DialogueStageSlot.Right, "RightCharacter");
            CaptureBaselines();

            _configured = _manager != null && _view != null && _stageView != null &&
                          _chapterTitleView != null && _windowRect != null && _speakerRect != null &&
                          _bodyRect != null && _choicesContainer != null &&
                          _stageRect != null && _backgroundRect != null && _screenEffectGroup != null &&
                          _portraits.Count == 3;
            if (isActiveAndEnabled)
                Bind();
            ResetTransientState();
        }

        public void ResetTransientState()
        {
            CancelLineMotion();
            SnapUiToRest();
            ResetChoiceReveals();
            ResetBackground();
            ResetTransitionOverlay();
            ResetScreenEffect();

            var current = _manager != null ? _manager.CurrentData : null;
            _dialogueActive = current != null && _view != null && _view.gameObject.activeInHierarchy;
            _observedDialogueId = current != null ? current.Id : -1;
            ActiveSlot = _dialogueActive ? ResolveActiveSlot(current) : string.Empty;
            ApplyPortraitStateImmediate(ActiveSlot);
            ApplyChapterStateImmediate(current);
        }

        public static bool TryResolveChapterTitle(DialogueData data, out ChapterTitleContent content)
        {
            content = default;
            if (data == null || !data.HasChapterKey)
                return false;

            var text = (data.Text ?? string.Empty).Trim();
            var chapterEnd = text.IndexOf('章');
            if (chapterEnd >= 0)
            {
                var ordinal = text.Substring(0, chapterEnd + 1).Trim();
                var title = text.Substring(chapterEnd + 1).Trim(' ', '\t', '\r', '\n', '　');
                content = new ChapterTitleContent(ordinal, title);
                return true;
            }

            var fallbackOrdinal = (data.ChapterKey ?? string.Empty)
                .Replace('_', ' ')
                .ToUpperInvariant();
            content = new ChapterTitleContent(fallbackOrdinal, text);
            return true;
        }

        public static bool TryResolveScreenEffect(DialogueData data, out ScreenEffectProfile profile)
        {
            profile = default;
            if (data == null || !data.TryGetExtra(ScreenEffectColumn, out var rawCue) ||
                string.IsNullOrWhiteSpace(rawCue))
                return false;

            var kind = ScreenEffectKind.None;
            var knownCues = new List<string>();
            var shakeAmplitude = 0f;
            var shakeFrequency = 0f;
            var shakeDuration = 0f;
            var flashColor = Color.white;
            var flashAlpha = 0f;
            var flashDuration = 0f;
            var zoomScale = 1f;
            var zoomDuration = 0f;

            var tokens = rawCue.Split(new[] { '|', '+', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var cue = tokens[i].Trim().ToLowerInvariant();
                switch (cue)
                {
                    case "shake_soft":
                        kind |= ScreenEffectKind.ShakeSoft;
                        knownCues.Add(cue);
                        shakeAmplitude = Mathf.Max(shakeAmplitude, 6f);
                        shakeFrequency = Mathf.Max(shakeFrequency, 24f);
                        shakeDuration = Mathf.Max(shakeDuration, 0.32f);
                        break;
                    case "shake_impact":
                        kind |= ScreenEffectKind.ShakeImpact;
                        knownCues.Add(cue);
                        shakeAmplitude = Mathf.Max(shakeAmplitude, 18f);
                        shakeFrequency = Mathf.Max(shakeFrequency, 32f);
                        shakeDuration = Mathf.Max(shakeDuration, 0.42f);
                        break;
                    case "flash_white":
                        kind |= ScreenEffectKind.FlashWhite;
                        knownCues.Add(cue);
                        flashColor = new Color(0.92f, 0.97f, 1f, 1f);
                        flashAlpha = Mathf.Max(flashAlpha, 0.72f);
                        flashDuration = Mathf.Max(flashDuration, 0.26f);
                        break;
                    case "flash_alarm":
                        kind |= ScreenEffectKind.FlashAlarm;
                        knownCues.Add(cue);
                        flashColor = new Color(0.78f, 0.035f, 0.055f, 1f);
                        flashAlpha = Mathf.Max(flashAlpha, 0.48f);
                        flashDuration = Mathf.Max(flashDuration, 0.34f);
                        break;
                    case "zoom_in":
                        kind |= ScreenEffectKind.ZoomIn;
                        knownCues.Add(cue);
                        zoomScale = Mathf.Max(zoomScale, 1.035f);
                        zoomDuration = Mathf.Max(zoomDuration, 0.50f);
                        break;
                }
            }

            if (kind == ScreenEffectKind.None)
                return false;

            var normalizedCue = string.Join("|", knownCues);
            profile = new ScreenEffectProfile(
                kind,
                normalizedCue,
                Mathf.Clamp(shakeAmplitude, 0f, 18f),
                Mathf.Clamp(shakeFrequency, 0f, 32f),
                Mathf.Clamp(shakeDuration, 0f, 0.42f),
                flashColor,
                Mathf.Clamp(flashAlpha, 0f, 0.72f),
                Mathf.Clamp(flashDuration, 0f, 0.34f),
                Mathf.Clamp(zoomScale, 1f, 1.035f),
                Mathf.Clamp(zoomDuration, 0f, 0.50f),
                StablePhase(data.Id + ":" + normalizedCue));
            return true;
        }

        public static string ResolveActiveSlot(DialogueData data)
        {
            if (data == null || string.Equals(data.Speaker, "地の文", StringComparison.Ordinal))
                return string.Empty;

            var directives = data.GetStageDirectives();
            if (directives == null || directives.Count == 0)
                return string.Empty;

            var expectedCharacter = ResolveCharacterKey(data.Speaker);
            DialogueStageDirective firstVisible = null;
            for (var i = 0; i < directives.Count; i++)
            {
                var directive = directives[i];
                if (directive == null || directive.IsClearAll || directive.IsExit)
                    continue;

                if (firstVisible == null)
                    firstVisible = directive;
                if (!string.IsNullOrEmpty(expectedCharacter) &&
                    string.Equals(directive.CharacterKey, expectedCharacter, StringComparison.Ordinal))
                    return ResolveSlot(directive);
            }

            return firstVisible != null ? ResolveSlot(firstVisible) : string.Empty;
        }

        private static string ResolveCharacterKey(string speaker)
        {
            switch (speaker)
            {
                case "レイ": return "Rei";
                case "ナギ": return "Nagi";
                case "研究員":
                case "若い研究員": return "Researcher";
                default: return string.Empty;
            }
        }

        private static string ResolveSlot(DialogueStageDirective directive)
        {
            return directive != null && directive.HasSlot ? directive.Slot : DialogueStageSlot.Center;
        }

        private void OnEnable()
        {
            if (_configured)
            {
                Bind();
                ResetTransientState();
            }
        }

        private void OnDisable()
        {
            Unbind();
            CancelLineMotion();
            _dialogueActive = false;
            _chapterTitleActive = false;
            _observedDialogueId = -1;
            ActiveSlot = string.Empty;
            SnapUiToRest();
            ResetChoiceReveals();
            ResetBackground();
            ResetTransitionOverlay();
            ResetScreenEffect();
            _chapterTitleView?.HideImmediate();
            ApplyPortraitStateImmediate(string.Empty);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            ObserveManagerState();
            if (!_dialogueActive)
                return;

            AnimateBackgroundDepth();
            AnimateNextIndicator();
        }

        private void Bind()
        {
            if (_bound || _manager == null)
                return;
            _manager.LineStarted += HandleLineStarted;
            _manager.DialogueEnded += HandleDialogueEnded;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _manager == null)
                return;
            _manager.LineStarted -= HandleLineStarted;
            _manager.DialogueEnded -= HandleDialogueEnded;
            _bound = false;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (context == null || context.Data == null)
                return;

            _dialogueActive = true;
            _observedDialogueId = context.Data.Id;
            ActiveSlot = ResolveActiveSlot(context.Data);
            if (!string.IsNullOrEmpty(context.Data.Background))
                _backgroundPhase = StablePhase(context.Data.Background);

            var leavingChapter = _chapterTitleActive && !context.Data.HasChapterKey;
            CancelLineMotion();
            SnapUiToRest();
            var generation = _generation;
            StageTransitionProfile transitionProfile;
            var hasTransition = TryResolveStageTransition(context.Data, out transitionProfile);
            if (hasTransition)
                _transitionMotion = StartCoroutine(AnimateStageTransition(transitionProfile, generation));

            ChapterTitleContent chapterTitle;
            _chapterTitleActive = TryResolveChapterTitle(context.Data, out chapterTitle);
            if (_chapterTitleActive)
            {
                SetChapterContentSuppressed(true);
                ApplyPortraitStateImmediate(string.Empty);
                var mood = hasTransition ? transitionProfile.Mood : StageTransitionMood.Neutral;
                _chapterTitleView.Prepare(chapterTitle.Ordinal, chapterTitle.Title, mood);
                _chapterMotion = StartCoroutine(AnimateChapterTitleReveal(generation));
            }
            else
            {
                SetChapterContentSuppressed(false);
                if (leavingChapter)
                {
                    _chapterTitleView.BeginExit();
                    _chapterMotion = StartCoroutine(AnimateChapterTitleExit(generation));
                }
                _lineMotion = StartCoroutine(AnimateLine(context.Data, generation));
            }

            ScreenEffectProfile screenEffect;
            if (TryResolveScreenEffect(context.Data, out screenEffect))
                _screenEffectMotion = StartCoroutine(AnimateScreenEffect(screenEffect, generation));
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            _dialogueActive = false;
            _chapterTitleActive = false;
            _observedDialogueId = -1;
            ActiveSlot = string.Empty;
            CancelLineMotion();
            SnapUiToRest();
            ResetChoiceReveals();
            ResetBackground();
            ResetTransitionOverlay();
            ResetScreenEffect();
            _chapterTitleView?.HideImmediate();
            ApplyPortraitStateImmediate(string.Empty);
        }

        private void ObserveManagerState()
        {
            var current = _manager != null ? _manager.CurrentData : null;
            var currentId = current != null ? current.Id : -1;
            if (currentId == _observedDialogueId)
                return;

            CancelLineMotion();
            SnapUiToRest();
            ResetBackground();
            ResetTransitionOverlay();
            ResetScreenEffect();
            _observedDialogueId = currentId;
            _dialogueActive = current != null && _view != null && _view.gameObject.activeInHierarchy;
            ActiveSlot = _dialogueActive ? ResolveActiveSlot(current) : string.Empty;
            ApplyPortraitStateImmediate(ActiveSlot);
            ApplyChapterStateImmediate(current);
        }

        private IEnumerator AnimateLine(DialogueData data, int generation)
        {
            // DialoguePresenter raises LineStarted before DialogueView.Show. Wait one frame so
            // text and pooled choice buttons are present before applying presentation motion.
            yield return null;
            if (generation != _generation || !_dialogueActive)
                yield break;

            PrepareUiEntrance();
            PrepareChoiceReveals();
            var portraitStarts = CapturePortraitStarts();
            var totalChoiceDuration = _choiceReveals.Count == 0
                ? 0f
                : ChoiceDuration + ChoiceStagger * (_choiceReveals.Count - 1);
            var totalDuration = Mathf.Max(LineDuration, totalChoiceDuration);
            var elapsed = 0f;

            while (elapsed < totalDuration)
            {
                if (generation != _generation)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                var lineT = EaseOutCubic(Mathf.Clamp01(elapsed / LineDuration));
                ApplyUiEntrance(lineT);
                ApplyPortraitTween(portraitStarts, lineT, ActiveSlot);
                ApplyChoiceReveal(elapsed);
                yield return null;
            }

            ApplyUiEntrance(1f);
            ApplyPortraitTween(portraitStarts, 1f, ActiveSlot);
            ApplyChoiceReveal(totalDuration + ChoiceDuration);
            _lineMotion = null;
        }

        private IEnumerator AnimateChapterTitleReveal(int generation)
        {
            if (_chapterTitleView == null)
                yield break;

            var elapsed = 0f;
            while (elapsed < ChapterRevealDelay + ChapterRevealDuration)
            {
                if (generation != _generation)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                var local = Mathf.Clamp01((elapsed - ChapterRevealDelay) / ChapterRevealDuration);
                _chapterTitleView.SetReveal(EaseOutCubic(local));
                yield return null;
            }

            if (generation == _generation)
            {
                _chapterTitleView.SetReveal(1f);
                _chapterMotion = null;
            }
        }

        private IEnumerator AnimateChapterTitleExit(int generation)
        {
            if (_chapterTitleView == null)
                yield break;

            var elapsed = 0f;
            while (elapsed < ChapterExitDuration)
            {
                if (generation != _generation)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                _chapterTitleView.SetExit(EaseOutCubic(elapsed / ChapterExitDuration));
                yield return null;
            }

            if (generation == _generation)
            {
                _chapterTitleView.HideImmediate();
                _chapterMotion = null;
            }
        }

        private void ApplyChapterStateImmediate(DialogueData data)
        {
            ChapterTitleContent content = default;
            _chapterTitleActive = _dialogueActive && TryResolveChapterTitle(data, out content);
            if (!_chapterTitleActive)
            {
                SetChapterContentSuppressed(false);
                _chapterTitleView?.HideImmediate();
                return;
            }

            SetChapterContentSuppressed(true);
            StageTransitionProfile profile;
            var mood = TryResolveStageTransition(data, out profile)
                ? profile.Mood
                : StageTransitionMood.Neutral;
            _chapterTitleView?.ShowImmediate(content.Ordinal, content.Title, mood);
        }

        private void SetChapterContentSuppressed(bool suppressed)
        {
            if (_dialogueWindowImage != null)
                _dialogueWindowImage.enabled = suppressed ? false : _dialogueWindowEnabledAtBaseline;
            if (_speakerGroup != null)
                _speakerGroup.alpha = suppressed ? 0f : 1f;
            if (_bodyGroup != null)
                _bodyGroup.alpha = suppressed ? 0f : 1f;
            if (_windowGroup != null)
                _windowGroup.alpha = 1f;
        }

        private IEnumerator AnimateStageTransition(StageTransitionProfile profile, int generation)
        {
            if (_transitionImage == null || _transitionGroup == null)
                yield break;

            _transitionImage.color = profile.VeilColor;
            _transitionImage.gameObject.SetActive(true);
            _transitionImage.transform.SetAsLastSibling();
            _transitionGroup.alpha = profile.StartAlpha;

            var elapsed = 0f;
            while (elapsed < profile.Duration)
            {
                if (generation != _generation)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / profile.Duration);
                var reveal = EaseInOutSine(normalized);
                var alpha = profile.StartAlpha * (1f - reveal);
                if (profile.AlertPulse)
                {
                    var pulse = Mathf.Sin(normalized * Mathf.PI * 3f);
                    alpha += pulse * pulse * 0.10f * (1f - normalized);
                }
                _transitionGroup.alpha = Mathf.Clamp01(alpha);
                yield return null;
            }

            if (generation == _generation)
            {
                ResetTransitionOverlay();
                _transitionMotion = null;
            }
        }

        private IEnumerator AnimateScreenEffect(ScreenEffectProfile profile, int generation)
        {
            if (_stageRect == null || profile.Duration <= 0f)
                yield break;

            _activeScreenEffectCue = profile.Cue;
            if (profile.FlashDuration > 0f && _screenEffectImage != null && _screenEffectGroup != null)
            {
                _screenEffectImage.color = profile.FlashColor;
                _screenEffectImage.gameObject.SetActive(true);
                _screenEffectImage.transform.SetAsLastSibling();
                _screenEffectGroup.alpha = 0f;
            }

            var elapsed = 0f;
            while (elapsed < profile.Duration)
            {
                if (generation != _generation)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                ApplyScreenEffectFrame(profile, elapsed);
                yield return null;
            }

            if (generation == _generation)
            {
                ResetScreenEffect();
                _screenEffectMotion = null;
            }
        }

        private void ApplyScreenEffectFrame(ScreenEffectProfile profile, float elapsed)
        {
            var offset = Vector2.zero;
            if (profile.ShakeDuration > 0f && elapsed < profile.ShakeDuration)
            {
                var t = Mathf.Clamp01(elapsed / profile.ShakeDuration);
                var attack = EaseOutCubic(Mathf.Clamp01(t / 0.08f));
                var damping = 1f - EaseOutCubic(t);
                var angle = (elapsed * profile.ShakeFrequency + profile.Phase) * Mathf.PI * 2f;
                var x = Mathf.Sin(angle) + Mathf.Sin(angle * 1.91f + 0.7f) * 0.24f;
                var y = Mathf.Sin(angle * 1.37f + 1.1f) + Mathf.Sin(angle * 2.23f) * 0.18f;
                offset = new Vector2(x, y) * (profile.ShakeAmplitude * attack * damping);
            }

            var scale = 1f;
            if (profile.ZoomDuration > 0f && elapsed < profile.ZoomDuration)
            {
                var t = Mathf.Clamp01(elapsed / profile.ZoomDuration);
                var envelope = t < 0.22f
                    ? EaseOutCubic(t / 0.22f)
                    : 1f - EaseInOutSine((t - 0.22f) / 0.78f);
                scale += (profile.ZoomScale - 1f) * envelope;
            }

            _stageRect.anchoredPosition = _stageBasePosition + offset;
            _stageRect.localScale = _stageBaseScale * scale;

            if (_screenEffectGroup != null)
            {
                var alpha = 0f;
                if (profile.FlashDuration > 0f && elapsed < profile.FlashDuration)
                {
                    var attackDuration = Mathf.Min(0.045f, profile.FlashDuration * 0.20f);
                    if (elapsed < attackDuration)
                        alpha = profile.FlashAlpha * EaseOutCubic(elapsed / attackDuration);
                    else
                    {
                        var decay = Mathf.Clamp01(
                            (elapsed - attackDuration) / (profile.FlashDuration - attackDuration));
                        alpha = profile.FlashAlpha * (1f - EaseOutCubic(decay));
                    }
                }
                _screenEffectGroup.alpha = Mathf.Clamp(alpha, 0f, 0.72f);
            }
        }

        public static bool TryResolveStageTransition(
            DialogueData data,
            out StageTransitionProfile profile)
        {
            profile = default;
            if (data == null)
                return false;

            var cue = data.GetBackgroundCue();
            var isChapter = data.HasChapterKey;
            if (!cue.HasValue && !isChapter)
                return false;

            var key = cue.Key ?? string.Empty;
            var mood = ResolveTransitionMood(key);
            var color = ResolveTransitionColor(mood);
            var transition = cue.Transition ?? string.Empty;
            var isCut = transition.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0;
            var isFade = transition.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0;

            float duration;
            float alpha;
            if (isChapter)
            {
                duration = cue.HasDuration
                    ? Mathf.Clamp(cue.Duration, 0.72f, 1.40f)
                    : isCut ? 0.48f : 0.82f;
                alpha = mood == StageTransitionMood.Alarm ? 0.62f : 0.88f;
            }
            else if (isCut)
            {
                duration = 0.16f;
                alpha = mood == StageTransitionMood.Alarm ? 0.48f : 0.34f;
            }
            else if (isFade)
            {
                duration = cue.HasDuration
                    ? Mathf.Clamp(cue.Duration, 0.25f, 1.40f)
                    : 0.55f;
                alpha = mood == StageTransitionMood.Alarm ? 0.55f : 0.72f;
            }
            else
            {
                duration = 0.28f;
                alpha = mood == StageTransitionMood.Alarm ? 0.44f : 0.38f;
            }

            profile = new StageTransitionProfile(
                mood,
                color,
                alpha,
                duration,
                mood == StageTransitionMood.Alarm);
            return true;
        }

        private static StageTransitionMood ResolveTransitionMood(string backgroundKey)
        {
            if (backgroundKey.IndexOf("alarm", StringComparison.OrdinalIgnoreCase) >= 0)
                return StageTransitionMood.Alarm;
            if (backgroundKey.IndexOf("white", StringComparison.OrdinalIgnoreCase) >= 0 ||
                backgroundKey.IndexOf("soft_cell", StringComparison.OrdinalIgnoreCase) >= 0)
                return StageTransitionMood.Sterile;
            if (backgroundKey.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0 ||
                backgroundKey.IndexOf("outside", StringComparison.OrdinalIgnoreCase) >= 0 ||
                backgroundKey.IndexOf("corridor", StringComparison.OrdinalIgnoreCase) >= 0)
                return StageTransitionMood.Cold;
            return StageTransitionMood.Neutral;
        }

        private static Color ResolveTransitionColor(StageTransitionMood mood)
        {
            switch (mood)
            {
                case StageTransitionMood.Alarm:
                    return new Color(0.30f, 0.015f, 0.025f, 1f);
                case StageTransitionMood.Sterile:
                    return new Color(0.78f, 0.89f, 1f, 1f);
                case StageTransitionMood.Cold:
                    return new Color(0.025f, 0.055f, 0.12f, 1f);
                default:
                    return new Color(0.025f, 0.035f, 0.055f, 1f);
            }
        }

        private void PrepareUiEntrance()
        {
            if (_windowGroup != null)
                _windowGroup.alpha = 0.88f;
            if (_windowRect != null)
            {
                _windowRect.anchoredPosition = _windowBasePosition + new Vector2(0f, WindowOffset);
                _windowRect.localScale = _windowBaseScale;
            }
            if (_speakerGroup != null)
                _speakerGroup.alpha = 0f;
            if (_speakerRect != null)
                _speakerRect.localScale = _speakerBaseScale * SpeakerStartScale;
        }

        private void ApplyUiEntrance(float t)
        {
            if (_windowGroup != null)
                _windowGroup.alpha = Mathf.LerpUnclamped(0.88f, 1f, t);
            if (_windowRect != null)
                _windowRect.anchoredPosition = Vector2.LerpUnclamped(
                    _windowBasePosition + new Vector2(0f, WindowOffset),
                    _windowBasePosition,
                    t);
            if (_speakerGroup != null)
                _speakerGroup.alpha = Mathf.Clamp01(t * 1.35f);
            if (_speakerRect != null)
                _speakerRect.localScale = Vector3.LerpUnclamped(
                    _speakerBaseScale * SpeakerStartScale,
                    _speakerBaseScale,
                    EaseOutBack(t));
        }

        private PortraitStart[] CapturePortraitStarts()
        {
            var starts = new PortraitStart[_portraits.Count];
            for (var i = 0; i < _portraits.Count; i++)
            {
                var portrait = _portraits[i];
                starts[i] = new PortraitStart(
                    portrait.Image != null ? portrait.Image.color : Color.white,
                    portrait.Rect != null ? portrait.Rect.anchoredPosition : Vector2.zero,
                    portrait.Rect != null ? portrait.Rect.localScale : Vector3.one);
            }
            return starts;
        }

        private void ApplyPortraitTween(PortraitStart[] starts, float t, string activeSlot)
        {
            for (var i = 0; i < _portraits.Count && i < starts.Length; i++)
            {
                var portrait = _portraits[i];
                if (portrait.Image == null || portrait.Rect == null || !portrait.Image.enabled)
                    continue;

                Color targetColor;
                Vector3 targetScale;
                var targetPosition = portrait.BasePosition;
                if (string.IsNullOrEmpty(activeSlot))
                {
                    targetColor = WithAlpha(NeutralColor, starts[i].Color.a);
                    targetScale = portrait.BaseScale;
                }
                else if (string.Equals(portrait.Slot, activeSlot, StringComparison.Ordinal))
                {
                    targetColor = WithAlpha(FocusColor, starts[i].Color.a);
                    targetScale = portrait.BaseScale * FocusScale;
                    targetPosition += new Vector2(0f, FocusLift);
                }
                else
                {
                    targetColor = WithAlpha(ListenerColor, starts[i].Color.a);
                    targetScale = portrait.BaseScale * ListenerScale;
                }

                portrait.Image.color = Color.LerpUnclamped(starts[i].Color, targetColor, t);
                portrait.Rect.anchoredPosition = Vector2.LerpUnclamped(starts[i].Position, targetPosition, t);
                portrait.Rect.localScale = Vector3.LerpUnclamped(starts[i].Scale, targetScale, t);
            }
        }

        private void ApplyPortraitStateImmediate(string activeSlot)
        {
            for (var i = 0; i < _portraits.Count; i++)
            {
                var portrait = _portraits[i];
                if (portrait.Image == null || portrait.Rect == null)
                    continue;

                var alpha = portrait.Image.color.a;
                if (string.IsNullOrEmpty(activeSlot))
                {
                    portrait.Image.color = WithAlpha(NeutralColor, alpha);
                    portrait.Rect.localScale = portrait.BaseScale;
                    portrait.Rect.anchoredPosition = portrait.BasePosition;
                }
                else if (string.Equals(portrait.Slot, activeSlot, StringComparison.Ordinal))
                {
                    portrait.Image.color = WithAlpha(FocusColor, alpha);
                    portrait.Rect.localScale = portrait.BaseScale * FocusScale;
                    portrait.Rect.anchoredPosition = portrait.BasePosition + new Vector2(0f, FocusLift);
                }
                else
                {
                    portrait.Image.color = WithAlpha(ListenerColor, alpha);
                    portrait.Rect.localScale = portrait.BaseScale * ListenerScale;
                    portrait.Rect.anchoredPosition = portrait.BasePosition;
                }
            }
        }

        private void PrepareChoiceReveals()
        {
            ResetChoiceReveals();
            if (_choicesContainer == null)
                return;

            var buttons = _choicesContainer.GetComponentsInChildren<Button>(false);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;

                var rect = button.transform as RectTransform;
                if (rect == null)
                    continue;
                var group = EnsureCanvasGroup(button.gameObject);
                var feedback = button.GetComponent<NovelChoiceMotionFeedback>();
                if (feedback == null)
                    feedback = button.gameObject.AddComponent<NovelChoiceMotionFeedback>();
                feedback.Configure();

                var basePosition = rect.anchoredPosition;
                group.alpha = 0f;
                rect.anchoredPosition = basePosition + new Vector2(0f, -12f);
                _choiceReveals.Add(new ChoiceReveal(rect, group, basePosition));
            }
        }

        private void ApplyChoiceReveal(float elapsed)
        {
            for (var i = 0; i < _choiceReveals.Count; i++)
            {
                var reveal = _choiceReveals[i];
                if (reveal.Rect == null || reveal.Group == null)
                    continue;
                var local = Mathf.Clamp01((elapsed - ChoiceStagger * i) / ChoiceDuration);
                var eased = EaseOutCubic(local);
                reveal.Group.alpha = eased;
                reveal.Rect.anchoredPosition = Vector2.LerpUnclamped(
                    reveal.BasePosition + new Vector2(0f, -12f),
                    reveal.BasePosition,
                    eased);
            }
        }

        private void ResetChoiceReveals()
        {
            for (var i = 0; i < _choiceReveals.Count; i++)
            {
                var reveal = _choiceReveals[i];
                if (reveal.Rect != null)
                    reveal.Rect.anchoredPosition = reveal.BasePosition;
                if (reveal.Group != null)
                    reveal.Group.alpha = 1f;
            }
            _choiceReveals.Clear();
        }

        private void AnimateBackgroundDepth()
        {
            if (_backgroundRect == null || _backgroundImage == null || !_backgroundImage.enabled)
                return;

            _backgroundPhase += Time.unscaledDeltaTime * 0.10f;
            _backgroundRect.localScale = _backgroundBaseScale * BackgroundScale;
            _backgroundRect.anchoredPosition = _backgroundBasePosition + new Vector2(
                Mathf.Sin(_backgroundPhase) * 5f,
                Mathf.Cos(_backgroundPhase * 0.73f) * 3f);
        }

        private void AnimateNextIndicator()
        {
            if (_nextRect == null || !_nextRect.gameObject.activeInHierarchy)
                return;
            var pulse = 1f + (Mathf.Sin(Time.unscaledTime * 4.2f) * 0.5f + 0.5f) * 0.025f;
            _nextRect.localScale = _nextBaseScale * pulse;
        }

        private void AddPortrait(string slot, string objectName)
        {
            var target = FindDescendant(_stageView != null ? _stageView.transform : null, objectName) as RectTransform;
            var image = target != null ? target.GetComponent<Image>() : null;
            if (target != null && image != null)
                _portraits.Add(new PortraitVisual(slot, target, image));
        }

        private void EnsureTransitionOverlay()
        {
            _transitionImage = null;
            _transitionGroup = null;
            if (_stageView == null)
                return;

            var existing = FindDescendant(_stageView.transform, "NovelStageTransitionOverlay");
            GameObject overlayObject;
            if (existing != null)
            {
                overlayObject = existing.gameObject;
            }
            else
            {
                overlayObject = new GameObject(
                    "NovelStageTransitionOverlay",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                overlayObject.transform.SetParent(_stageView.transform, false);
            }

            var rect = overlayObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            _transitionImage = overlayObject.GetComponent<Image>();
            if (_transitionImage == null)
                _transitionImage = overlayObject.AddComponent<Image>();
            _transitionImage.sprite = null;
            _transitionImage.type = Image.Type.Simple;
            _transitionImage.raycastTarget = false;

            _transitionGroup = EnsureCanvasGroup(overlayObject);
            _transitionGroup.interactable = false;
            _transitionGroup.blocksRaycasts = false;
            overlayObject.transform.SetAsLastSibling();
        }

        private void EnsureScreenEffectOverlay()
        {
            _screenEffectImage = null;
            _screenEffectGroup = null;
            if (_stageView == null)
                return;

            var existing = FindDescendant(_stageView.transform, "NovelScreenEffectOverlay");
            GameObject overlayObject;
            if (existing != null)
            {
                overlayObject = existing.gameObject;
            }
            else
            {
                overlayObject = new GameObject(
                    "NovelScreenEffectOverlay",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                overlayObject.transform.SetParent(_stageView.transform, false);
            }

            var rect = overlayObject.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-48f, -48f);
                rect.offsetMax = new Vector2(48f, 48f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            _screenEffectImage = overlayObject.GetComponent<Image>();
            if (_screenEffectImage == null)
                _screenEffectImage = overlayObject.AddComponent<Image>();
            _screenEffectImage.sprite = null;
            _screenEffectImage.type = Image.Type.Simple;
            _screenEffectImage.raycastTarget = false;

            _screenEffectGroup = EnsureCanvasGroup(overlayObject);
            _screenEffectGroup.interactable = false;
            _screenEffectGroup.blocksRaycasts = false;
            overlayObject.transform.SetAsLastSibling();
            ResetScreenEffectOverlay();
        }

        private void CaptureBaselines()
        {
            if (_dialogueWindowImage != null)
                _dialogueWindowEnabledAtBaseline = _dialogueWindowImage.enabled;
            if (_windowRect != null)
            {
                _windowBasePosition = _windowRect.anchoredPosition;
                _windowBaseScale = _windowRect.localScale;
            }
            if (_speakerRect != null)
                _speakerBaseScale = _speakerRect.localScale;
            if (_nextRect != null)
                _nextBaseScale = _nextRect.localScale;
            if (_stageRect != null)
            {
                _stageBasePosition = _stageRect.anchoredPosition;
                _stageBaseScale = _stageRect.localScale;
            }
            if (_backgroundRect != null)
            {
                _backgroundBasePosition = _backgroundRect.anchoredPosition;
                _backgroundBaseScale = _backgroundRect.localScale;
            }
            for (var i = 0; i < _portraits.Count; i++)
                _portraits[i].CaptureBaseline();
        }

        private void CancelLineMotion()
        {
            _generation++;
            if (_lineMotion != null)
            {
                StopCoroutine(_lineMotion);
                _lineMotion = null;
            }
            if (_transitionMotion != null)
            {
                StopCoroutine(_transitionMotion);
                _transitionMotion = null;
            }
            if (_chapterMotion != null)
            {
                StopCoroutine(_chapterMotion);
                _chapterMotion = null;
            }
            if (_screenEffectMotion != null)
            {
                StopCoroutine(_screenEffectMotion);
                _screenEffectMotion = null;
            }
            ResetChoiceReveals();
            ResetTransitionOverlay();
            ResetScreenEffect();
            _chapterTitleView?.HideImmediate();
        }

        private void SnapUiToRest()
        {
            if (_windowRect != null)
            {
                _windowRect.anchoredPosition = _windowBasePosition;
                _windowRect.localScale = _windowBaseScale;
            }
            if (_windowGroup != null)
                _windowGroup.alpha = 1f;
            if (_speakerRect != null)
                _speakerRect.localScale = _speakerBaseScale;
            if (_speakerGroup != null)
                _speakerGroup.alpha = 1f;
            if (_bodyGroup != null)
                _bodyGroup.alpha = 1f;
            if (_dialogueWindowImage != null)
                _dialogueWindowImage.enabled = _dialogueWindowEnabledAtBaseline;
            if (_nextRect != null)
                _nextRect.localScale = _nextBaseScale;
        }

        private void ResetBackground()
        {
            if (_backgroundRect == null)
                return;
            _backgroundRect.anchoredPosition = _backgroundBasePosition;
            _backgroundRect.localScale = _backgroundBaseScale;
        }

        private void ResetTransitionOverlay()
        {
            if (_transitionGroup != null)
                _transitionGroup.alpha = 0f;
            if (_transitionImage != null)
                _transitionImage.gameObject.SetActive(false);
        }

        private void ResetScreenEffect()
        {
            _activeScreenEffectCue = string.Empty;
            if (_stageRect != null)
            {
                _stageRect.anchoredPosition = _stageBasePosition;
                _stageRect.localScale = _stageBaseScale;
            }
            ResetScreenEffectOverlay();
        }

        private void ResetScreenEffectOverlay()
        {
            if (_screenEffectGroup != null)
                _screenEffectGroup.alpha = 0f;
            if (_screenEffectImage != null)
                _screenEffectImage.gameObject.SetActive(false);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null)
                return null;
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float overshoot = 1.70158f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }

        private static float EaseInOutSine(float t)
        {
            return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;
        }

        private static float StablePhase(string value)
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return Mathf.Abs(hash % 6283) / 1000f;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private sealed class PortraitVisual
        {
            public PortraitVisual(string slot, RectTransform rect, Image image)
            {
                Slot = slot;
                Rect = rect;
                Image = image;
                CaptureBaseline();
            }

            public string Slot { get; }
            public RectTransform Rect { get; }
            public Image Image { get; }
            public Vector2 BasePosition { get; private set; }
            public Vector3 BaseScale { get; private set; }

            public void CaptureBaseline()
            {
                if (Rect == null)
                    return;
                BasePosition = Rect.anchoredPosition;
                BaseScale = Rect.localScale;
            }
        }

        private readonly struct PortraitStart
        {
            public PortraitStart(Color color, Vector2 position, Vector3 scale)
            {
                Color = color;
                Position = position;
                Scale = scale;
            }

            public Color Color { get; }
            public Vector2 Position { get; }
            public Vector3 Scale { get; }
        }

        private sealed class ChoiceReveal
        {
            public ChoiceReveal(RectTransform rect, CanvasGroup group, Vector2 basePosition)
            {
                Rect = rect;
                Group = group;
                BasePosition = basePosition;
            }

            public RectTransform Rect { get; }
            public CanvasGroup Group { get; }
            public Vector2 BasePosition { get; }
        }
    }
}
