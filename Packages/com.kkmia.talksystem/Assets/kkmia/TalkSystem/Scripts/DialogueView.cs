using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    [Serializable]
    public struct CharacterSprite
    {
        public string key;
        public Sprite sprite;
    }

    public class DialogueView : MonoBehaviour, IDialogueView
    {
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image dialogWindow;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private TypewriterEffect typewriter;
        [SerializeField] private List<CharacterSprite> characterSprites = new List<CharacterSprite>();
        [SerializeField] private CharacterExpressionDatabase characterDatabase;

        [Header("Choices")]
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private Button choiceButtonPrefab;
        [SerializeField] private bool fallbackNextButtonSelectsFirstChoice = true;

        [Header("Auto Advance")]
        [SerializeField] private bool enableAutoNext;
        [SerializeField] private float defaultAutoNextSeconds = 1f;

        public event Action OnNextRequested;
        public event Action<int> OnChoiceSelected;

        event Action IDialogueView.NextRequested
        {
            add { OnNextRequested += value; }
            remove { OnNextRequested -= value; }
        }

        event Action<int> IDialogueView.ChoiceSelected
        {
            add { OnChoiceSelected += value; }
            remove { OnChoiceSelected -= value; }
        }

        public bool IsTyping
        {
            get { return typewriter != null && typewriter.IsTyping; }
        }

        // 「選択肢なし」を表す共有の空リスト。行送りのたびに空リストを確保しないための定数。
        private static readonly IReadOnlyList<DialogueChoice> EmptyChoices = new DialogueChoice[0];

        private Sprite initialSprite;
        private Coroutine autoNextCoroutine;
        private bool _autoOverrideActive;
        private float _autoOverrideSeconds;
        private bool _autoAdvanceSuspended;
        private bool _lineReady;
        private DialogueData _currentData;
        private Transform _runtimeChoicesContainer;
        // 表示中の選択肢ボタン。行送りのたびに Destroy/Instantiate せず、_choiceButtonPool と
        // の間で使い回して GC 圧とヒエラルキー再構築を避ける。
        private readonly List<Button> _choiceButtons = new List<Button>();
        private readonly List<Button> _choiceButtonPool = new List<Button>();
        private IReadOnlyList<DialogueChoice> _activeChoices = EmptyChoices;

        private void Awake()
        {
            if (nextButton != null)
                nextButton.onClick.AddListener(HandleNextButtonClicked);

            if (characterImage != null)
                initialSprite = defaultSprite != null ? defaultSprite : characterImage.sprite;
        }

        private void OnDestroy()
        {
            if (nextButton != null)
                nextButton.onClick.RemoveListener(HandleNextButtonClicked);
        }

        public void Show(DialogueData data, Action onComplete)
        {
            Show(data, EmptyChoices, onComplete);
        }

        public void Show(DialogueData data, IReadOnlyList<DialogueChoice> choices, Action onComplete)
        {
            ForceStop();
            CancelAutoNextTimer();
            ClearChoiceButtons();

            _currentData = data;
            _lineReady = false;
            _activeChoices = choices ?? EmptyChoices;

            // Show can be called while the view is still inactive, but it must not
            // force an OnDisable/OnEnable cycle because binders re-register on enable.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (dialogWindow != null)
                dialogWindow.gameObject.SetActive(true);

            ApplySpeaker(data);

            if (bodyText != null)
                bodyText.text = string.Empty;

            UpdateCharacterSprite(data);

            if (nextButton != null)
                nextButton.gameObject.SetActive(false);

            if (typewriter != null)
            {
                typewriter.PlayInline(data.Text, () => CompleteLine(data, onComplete));
            }
            else
            {
                if (bodyText != null)
                    bodyText.text = DialogueInlineText.Build(data.Text).DisplayText;

                CompleteLine(data, onComplete);
            }
        }

        public void CompleteTyping()
        {
            if (typewriter != null && typewriter.IsTyping)
                typewriter.Complete();
        }

        public void RequestNext()
        {
            HandleNextButtonClicked();
        }

        private void CompleteLine(DialogueData data, Action onComplete)
        {
            _lineReady = true;
            DrawChoices();

            if (nextButton != null)
            {
                var hasChoices = _activeChoices != null && _activeChoices.Count > 0;
                var canFallbackChoice = hasChoices && _choiceButtons.Count == 0 && fallbackNextButtonSelectsFirstChoice;
                nextButton.gameObject.SetActive(!hasChoices || canFallbackChoice);
            }

            StartAutoNextTimer(data);
            if (onComplete != null)
                onComplete();
        }

        private void ApplySpeaker(DialogueData data)
        {
            if (speakerText == null || data == null) return;

            CharacterDefinition character;
            if (characterDatabase != null && characterDatabase.TryGetCharacter(data.Speaker, out character))
            {
                speakerText.text = string.IsNullOrEmpty(character.displayName) ? data.Speaker : character.displayName;
                speakerText.color = character.nameColor;
                return;
            }

            speakerText.text = data.Speaker;
        }

        private void UpdateCharacterSprite(DialogueData data)
        {
            if (characterImage == null) return;

            characterImage.gameObject.SetActive(true);

            Sprite resolved;
            if (data != null && characterDatabase != null && characterDatabase.TryGetSprite(data.Speaker, data.EmotionKey, out resolved))
            {
                characterImage.sprite = resolved;
                return;
            }

            if (data == null || string.IsNullOrEmpty(data.EmotionKey))
            {
                characterImage.sprite = initialSprite;
                return;
            }

            foreach (var character in characterSprites)
            {
                if (character.key == data.EmotionKey)
                {
                    characterImage.sprite = character.sprite;
                    return;
                }
            }

            characterImage.sprite = initialSprite;
        }

        private void DrawChoices()
        {
            if (_activeChoices == null || _activeChoices.Count == 0)
                return;

            var targetContainer = EnsureChoicesContainer();
            if (targetContainer == null)
                return;

            for (var i = 0; i < _activeChoices.Count; i++)
            {
                var index = i;
                var button = RentChoiceButton(targetContainer);

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = _activeChoices[i].Text;

                button.onClick.AddListener(() => SelectChoice(index));
                button.transform.SetAsLastSibling();
                button.gameObject.SetActive(true);
                _choiceButtons.Add(button);
            }
        }

        private Button RentChoiceButton(Transform parent)
        {
            // シーン遷移などで破棄済みのボタンが混ざり得るため、有効なものが出るまで取り出す。
            while (_choiceButtonPool.Count > 0)
            {
                var last = _choiceButtonPool.Count - 1;
                var pooled = _choiceButtonPool[last];
                _choiceButtonPool.RemoveAt(last);
                if (pooled == null)
                    continue;

                if (pooled.transform.parent != parent)
                    pooled.transform.SetParent(parent, false);
                return pooled;
            }

            return choiceButtonPrefab != null
                ? Instantiate(choiceButtonPrefab, parent)
                : CreateDefaultChoiceButton(parent);
        }

        private Transform EnsureChoicesContainer()
        {
            if (choicesContainer != null)
                return choicesContainer;

            if (_runtimeChoicesContainer != null)
                return _runtimeChoicesContainer;

            var parent = transform as RectTransform;
            if (parent == null)
                return null;

            var container = new GameObject("RuntimeChoices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(transform, false);

            var rect = (RectTransform)container.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 190f);
            rect.sizeDelta = new Vector2(520f, 150f);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _runtimeChoicesContainer = container.transform;
            return _runtimeChoicesContainer;
        }

        private static Button CreateDefaultChoiceButton(Transform parent)
        {
            var buttonObject = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = new Vector2(480f, 42f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 42f;
            layout.minHeight = 42f;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 4f);
            labelRect.offsetMax = new Vector2(-14f, -4f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.fontSize = 18f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return buttonObject.GetComponent<Button>();
        }

        private void SelectChoice(int index)
        {
            CancelAutoNextTimer();
            ClearChoiceButtons();
            if (OnChoiceSelected != null)
                OnChoiceSelected(index);
        }

        private void HandleNextButtonClicked()
        {
            CancelAutoNextTimer();

            if (IsTyping)
            {
                CompleteTyping();
                return;
            }

            if (_activeChoices != null && _activeChoices.Count > 0 && _choiceButtons.Count == 0 && fallbackNextButtonSelectsFirstChoice)
            {
                SelectChoice(0);
                return;
            }

            if (OnNextRequested != null)
                OnNextRequested();
        }

        /// <summary>
        /// オート/スキップ進行のための外部オーバーライド。active の間は、選択肢が無い限り
        /// 行表示後に必ず <paramref name="seconds"/> 後の自動送りを行う（行データの AutoNextSeconds を無視）。
        /// </summary>
        public void SetAutoAdvanceOverride(bool active, float seconds)
        {
            _autoOverrideActive = active;
            _autoOverrideSeconds = seconds < 0f ? 0f : seconds;

            if (_lineReady && !IsTyping)
                StartAutoNextTimer(_currentData);
        }

        public void SetAutoAdvanceSuspended(bool suspended)
        {
            if (_autoAdvanceSuspended == suspended)
                return;

            _autoAdvanceSuspended = suspended;
            if (_autoAdvanceSuspended)
            {
                CancelAutoNextTimer();
                return;
            }

            if (_lineReady && !IsTyping)
                StartAutoNextTimer(_currentData);
        }

        private void StartAutoNextTimer(DialogueData data)
        {
            CancelAutoNextTimer();
            if (_autoAdvanceSuspended)
                return;

            if (_activeChoices != null && _activeChoices.Count > 0)
                return;

            if (_autoOverrideActive)
            {
                autoNextCoroutine = StartCoroutine(AutoNextCoroutine(_autoOverrideSeconds));
                return;
            }

            var seconds = data != null && data.AutoNextSeconds >= 0f ? data.AutoNextSeconds : defaultAutoNextSeconds;
            if (!enableAutoNext && (data == null || data.AutoNextSeconds < 0f))
                return;

            autoNextCoroutine = StartCoroutine(AutoNextCoroutine(Mathf.Max(0f, seconds)));
        }

        private void CancelAutoNextTimer()
        {
            if (autoNextCoroutine != null)
            {
                StopCoroutine(autoNextCoroutine);
                autoNextCoroutine = null;
            }
        }

        private IEnumerator AutoNextCoroutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            autoNextCoroutine = null;
            if (OnNextRequested != null)
                OnNextRequested();
        }

        public void StartDelay(float seconds, Action onCompleted)
        {
            if (seconds < 0f) seconds = 0f;
            StartCoroutine(DelayCoroutine(seconds, onCompleted));
        }

        private IEnumerator DelayCoroutine(float seconds, Action onCompleted)
        {
            yield return new WaitForSeconds(seconds);
            if (onCompleted != null)
                onCompleted();
        }

        public void Clear()
        {
            ForceStop();
            CancelAutoNextTimer();
            ClearChoiceButtons();
            _currentData = null;
            _lineReady = false;
            _activeChoices = EmptyChoices;

            if (speakerText != null)
                speakerText.text = string.Empty;

            if (bodyText != null)
                bodyText.text = string.Empty;

            if (characterImage != null)
            {
                characterImage.sprite = initialSprite;
                characterImage.gameObject.SetActive(false);
            }

            if (nextButton != null)
                nextButton.gameObject.SetActive(false);

            if (dialogWindow != null)
                dialogWindow.gameObject.SetActive(false);
        }

        public void ForceStop()
        {
            CancelAutoNextTimer();
            if (typewriter != null && typewriter.IsTyping)
                typewriter.Cancel();
        }

        public void SetTypewriterSpeed(float newInterval)
        {
            if (typewriter != null)
                typewriter.SetInterval(newInterval);
        }

        private void ClearChoiceButtons()
        {
            for (var i = 0; i < _choiceButtons.Count; i++)
            {
                var button = _choiceButtons[i];
                if (button == null)
                    continue;

                // 実行時に追加したリスナーだけを外す（プレハブ側の永続リスナーは保持される）。
                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
                _choiceButtonPool.Add(button);
            }

            _choiceButtons.Clear();
        }
    }
}
