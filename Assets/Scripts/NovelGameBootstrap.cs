using System;
using System.Reflection;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    [DefaultExecutionOrder(-500)]
    public sealed class NovelGameBootstrap : MonoBehaviour, IDialogueVariableResolver, IDialogueConditionEvaluator
    {
        private const string DefaultDialogueResourcePath = "Dialogue/r00_escape_talksystem";
        private const string DefaultStartTriggerKey = "R00EscapeStart";

        private static NovelGameBootstrap _instance;

        [SerializeField] private string dialogueResourcePath = DefaultDialogueResourcePath;
        [SerializeField] private string startTriggerKey = DefaultStartTriggerKey;
        [SerializeField] private bool startOnLaunch = true;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private float typewriterInterval = 0.025f;
        [SerializeField] private DialogueView dialogueViewPrefab;

        private DialogueManager _manager;
        private DialogueView _view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeBootstrap()
        {
            if (FindFirstObjectByType<NovelGameBootstrap>() != null)
                return;

            var bootstrap = new GameObject(nameof(NovelGameBootstrap));
            bootstrap.AddComponent<NovelGameBootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BuildRuntime();
        }

        public void StartDialogue(int id)
        {
            if (_manager == null)
                return;

            _manager.StartDialogue(id);
        }

        public void StartDialogueForTrigger(string triggerKey)
        {
            if (_manager == null)
                return;

            _manager.StartDialogueForState(triggerKey);
        }

        public void RequestNext()
        {
            if (_manager != null)
                _manager.RequestNext();
        }

        public void Rollback()
        {
            if (_manager != null)
                _manager.Rollback();
        }

        bool IDialogueVariableResolver.TryResolve(string variableName, DialogueData data, out string value)
        {
            if (string.Equals(variableName, "playerName", StringComparison.OrdinalIgnoreCase))
            {
                value = playerName;
                return true;
            }

            value = null;
            return false;
        }

        bool IDialogueConditionEvaluator.Evaluate(string conditionKey, DialogueData data)
        {
            if (string.IsNullOrEmpty(conditionKey))
                return true;

            return true;
        }

        private void BuildRuntime()
        {
            EnsureEventSystem();
            _view = EnsureDialogueView();
            _manager = EnsureDialogueManager();

            _manager.SetView(_view);
            _manager.SetVariableResolver(this);
            _manager.SetConditionEvaluator(this);
            _manager.SetTypewriterSpeed(typewriterInterval);
            _manager.SetEventDispatcher(new DelegateDialogueEventDispatcher(HandleDialogueEvent));

            var csv = Resources.Load<TextAsset>(dialogueResourcePath);
            if (csv == null)
            {
                Debug.LogError($"NovelGameBootstrap: dialogue CSV was not found at Resources/{dialogueResourcePath}.");
                return;
            }

            StartCoroutine(LoadDialogueAndStart(csv));
        }

        private System.Collections.IEnumerator LoadDialogueAndStart(TextAsset csv)
        {
            _manager.LoadRepository(new TextAssetDialogueRepositoryLoader(csv));
            yield return null;
            yield return null;

            if (startOnLaunch && !string.IsNullOrEmpty(startTriggerKey))
                _manager.StartDialogueForState(startTriggerKey);
        }

        private DialogueManager EnsureDialogueManager()
        {
            if (DialogueManager.Instance != null)
                return DialogueManager.Instance;

            var managerObject = new GameObject("DialogueManager");
            DontDestroyOnLoad(managerObject);
            return managerObject.AddComponent<DialogueManager>();
        }

        private DialogueView EnsureDialogueView()
        {
            var existingView = FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            if (existingView != null)
            {
                EnsureDialogueViewBinder(existingView);
                return existingView;
            }

            if (dialogueViewPrefab != null)
            {
                var prefabCanvas = EnsureDialogueCanvas();
                var prefabView = Instantiate(dialogueViewPrefab, prefabCanvas.transform);
                prefabView.gameObject.SetActive(false);
                EnsureDialogueViewBinder(prefabView);
                return prefabView;
            }

            var fallbackCanvas = EnsureDialogueCanvas();
            return CreateFallbackDialogueView(fallbackCanvas.transform);
        }

        private static DialogueView CreateFallbackDialogueView(Transform parent)
        {
            var root = CreateDialogueRoot(parent);
            root.SetActive(false);

            var speaker = CreateText("SpeakerText", root.transform, new Vector2(28f, -18f), new Vector2(-28f, -56f), 24f, FontStyles.Bold);
            var body = CreateText("BodyText", root.transform, new Vector2(28f, -70f), new Vector2(-148f, 26f), 26f, FontStyles.Normal);
            var nextButton = CreateNextButton(root.transform);
            var typewriter = body.gameObject.AddComponent<TypewriterEffect>();
            var view = root.AddComponent<DialogueView>();

            ConfigureFallbackDialogueView(view, speaker, body, nextButton, root.GetComponent<Image>(), typewriter);
            EnsureDialogueViewBinder(view);

            return view;
        }

        private static Canvas EnsureDialogueCanvas()
        {
            var existingCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (existingCanvas != null && string.Equals(existingCanvas.name, "NovelDialogueCanvas", StringComparison.Ordinal))
                return existingCanvas;

            return CreateCanvas();
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("NovelDialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static GameObject CreateDialogueRoot(Transform parent)
        {
            var root = new GameObject("DialogueWindow", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.04f, 0.04f);
            rect.anchorMax = new Vector2(0.96f, 0.34f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

            return root;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.TopLeft;

            return text;
        }

        private static Button CreateNextButton(Transform parent)
        {
            var buttonObject = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 22f);
            rect.sizeDelta = new Vector2(108f, 44f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.24f, 0.30f, 0.95f);

            var label = CreateText("Label", buttonObject.transform, new Vector2(12f, 6f), new Vector2(-12f, -6f), 20f, FontStyles.Bold);
            label.text = "Next";
            label.alignment = TextAlignmentOptions.Center;

            return buttonObject.GetComponent<Button>();
        }

        private static void EnsureDialogueViewBinder(DialogueView view)
        {
            if (view != null && view.GetComponent<DialogueViewBinder>() == null)
                view.gameObject.AddComponent<DialogueViewBinder>();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static void ConfigureFallbackDialogueView(
            DialogueView view,
            TMP_Text speaker,
            TMP_Text body,
            Button nextButton,
            Image dialogWindow,
            TypewriterEffect typewriter)
        {
            SetPrivateField(view, "speakerText", speaker);
            SetPrivateField(view, "bodyText", body);
            SetPrivateField(view, "nextButton", nextButton);
            SetPrivateField(view, "dialogWindow", dialogWindow);
            SetPrivateField(view, "typewriter", typewriter);
        }

        private static void SetPrivateField<T>(DialogueView view, string fieldName, T value)
        {
            var field = typeof(DialogueView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"NovelGameBootstrap: DialogueView field '{fieldName}' was not found.");
                return;
            }

            field.SetValue(view, value);
        }

        private void HandleDialogueEvent(DialogueEventContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.EventKey))
                return;

            switch (context.EventKey)
            {
                case "load_main":
                    SceneManager.LoadScene("Main");
                    break;
                default:
                    Debug.Log($"NovelGameBootstrap: dialogue event '{context.EventKey}' was raised.");
                    break;
            }
        }
    }
}
