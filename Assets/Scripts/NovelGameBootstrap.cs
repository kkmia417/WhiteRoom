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
        [SerializeField] private DialogueBacklogView dialogueBacklogViewPrefab;
        [SerializeField] private bool enableDebugSaveHotkeys = true;
        [SerializeField] private bool enableDebugBacklogHotkey = true;
        [SerializeField] private int defaultManualSaveSlot = DialogueSaveSlotConventions.FirstManualSlot;
        [SerializeField] private bool saveThumbnails;
        [SerializeField] private string saveContentVersion = "r00_escape_talksystem";
        [SerializeField] private string saveProductChannel = string.Empty;

        private DialogueManager _manager;
        private DialogueView _view;
        private DialogueBacklogView _backlogView;
        private DialogueSaveSystem _saveSystem;

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

        private void Update()
        {
            if (enableDebugSaveHotkeys)
            {
                if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.F5))
                    QuickSave();
                if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.F9))
                    QuickLoad();
            }

            if (enableDebugBacklogHotkey && DialogueKeyboard.GetKeyDown(DialogueKeyCode.B))
                ToggleBacklog();
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

        public bool SaveDialogue()
        {
            return SaveDialogue(defaultManualSaveSlot);
        }

        public bool SaveDialogue(int slot)
        {
            if (!EnsureSaveSystemReady())
                return false;

            var title = BuildSaveTitle(slot);
            var saved = saveThumbnails
                ? SaveWithThumbnail(slot, false, title)
                : _saveSystem.Save(slot, false, title);

            return saved != null;
        }

        public bool LoadDialogue()
        {
            return LoadDialogue(defaultManualSaveSlot);
        }

        public bool LoadDialogue(int slot)
        {
            return EnsureSaveSystemReady() && _saveSystem.Load(slot);
        }

        public bool QuickSave()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var title = BuildSaveTitle(DialogueSaveSystem.QuickSaveSlot);
            if (saveThumbnails)
            {
                _saveSystem.QuickSaveWithThumbnail(title);
                return _saveSystem.LastOperationResult == null || _saveSystem.LastOperationResult.Succeeded;
            }

            return _saveSystem.QuickSave(title) != null;
        }

        public bool QuickLoad()
        {
            return EnsureSaveSystemReady() && _saveSystem.QuickLoad();
        }

        public bool ContinueLatest()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var candidate = _saveSystem.GetLatestContinueCandidate(true, true, false);
            return candidate != null && candidate.CanLoad && _saveSystem.Load(candidate.SlotIndex);
        }

        public bool HasSave(int slot)
        {
            return EnsureSaveSystemReady() && _saveSystem.Exists(slot);
        }

        public void ToggleBacklog()
        {
            if (!EnsureBacklogViewReady())
                return;

            if (_backlogView.IsOpen)
                CloseBacklog();
            else
                OpenBacklog();
        }

        public void OpenBacklog()
        {
            if (!EnsureBacklogViewReady())
                return;

            if (_view != null)
                _view.SetAutoAdvanceSuspended(true);

            _backlogView.Open();
        }

        public void CloseBacklog()
        {
            if (_backlogView == null)
                return;

            _backlogView.Close();

            if (_view != null)
                _view.SetAutoAdvanceSuspended(false);
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
            _backlogView = EnsureDialogueBacklogView();
            _manager = EnsureDialogueManager();
            _saveSystem = EnsureDialogueSaveSystem(_manager);

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

        private DialogueSaveSystem EnsureDialogueSaveSystem(DialogueManager manager)
        {
            if (manager == null)
                return null;

            var saveSystem = manager.GetComponent<DialogueSaveSystem>();
            if (saveSystem == null)
                saveSystem = manager.gameObject.AddComponent<DialogueSaveSystem>();

            saveSystem.SetSaveMetadata(saveContentVersion, saveProductChannel);
            saveSystem.OperationFailed -= HandleSaveOperationFailed;
            saveSystem.OperationFailed += HandleSaveOperationFailed;

            return saveSystem;
        }

        private bool EnsureSaveSystemReady()
        {
            if (_saveSystem != null)
                return true;

            _manager = _manager != null ? _manager : DialogueManager.Instance;
            _saveSystem = EnsureDialogueSaveSystem(_manager);
            return _saveSystem != null;
        }

        private bool EnsureBacklogViewReady()
        {
            if (_backlogView != null)
                return true;

            _view = _view != null ? _view : FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            _backlogView = EnsureDialogueBacklogView();
            return _backlogView != null;
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

        private DialogueBacklogView EnsureDialogueBacklogView()
        {
            var existingBacklog = FindFirstObjectByType<DialogueBacklogView>(FindObjectsInactive.Include);
            if (existingBacklog != null)
                return existingBacklog;

            var canvas = EnsureDialogueCanvas();
            if (dialogueBacklogViewPrefab != null)
            {
                var prefabBacklog = Instantiate(dialogueBacklogViewPrefab, canvas.transform);
                prefabBacklog.gameObject.SetActive(true);
                prefabBacklog.Close();
                return prefabBacklog;
            }

            return CreateFallbackBacklogView(canvas.transform);
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

        private static DialogueBacklogView CreateFallbackBacklogView(Transform parent)
        {
            var backlogObject = new GameObject("DialogueBacklog", typeof(RectTransform), typeof(DialogueBacklogView));
            backlogObject.transform.SetParent(parent, false);

            var rootRect = (RectTransform)backlogObject.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panel = CreateBacklogPanel(backlogObject.transform);
            var content = CreateBacklogScrollContent(panel.transform);
            var rowPrefab = CreateBacklogRowPrefab(backlogObject.transform);
            var backlogView = backlogObject.GetComponent<DialogueBacklogView>();

            SetPrivateField(backlogView, "panel", panel);
            SetPrivateField(backlogView, "contentContainer", content);
            SetPrivateField(backlogView, "rowPrefab", rowPrefab);

            panel.SetActive(false);
            return backlogView;
        }

        private static GameObject CreateBacklogPanel(Transform parent)
        {
            var panel = new GameObject("BacklogPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.12f, 0.12f);
            rect.anchorMax = new Vector2(0.88f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panel.GetComponent<Image>();
            image.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);

            var title = CreateText("Title", panel.transform, new Vector2(26f, -50f), new Vector2(-26f, 12f), 24f, FontStyles.Bold);
            title.text = "Backlog";

            return panel;
        }

        private static Transform CreateBacklogScrollContent(Transform parent)
        {
            var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);

            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(24f, 24f);
            scrollRectTransform.offsetMax = new Vector2(-24f, -72f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);

            var viewportRect = (RectTransform)viewportObject.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            var mask = viewportObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);

            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            return contentObject.transform;
        }

        private static DialogueBacklogRow CreateBacklogRowPrefab(Transform parent)
        {
            var rowObject = new GameObject("BacklogRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(DialogueBacklogRow));
            rowObject.transform.SetParent(parent, false);
            rowObject.SetActive(false);

            var rect = (RectTransform)rowObject.transform;
            rect.sizeDelta = new Vector2(900f, 86f);

            var image = rowObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.095f, 0.11f, 0.88f);

            var layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 86f;
            layout.preferredHeight = 86f;

            var speaker = CreateText("Speaker", rowObject.transform, new Vector2(18f, -30f), new Vector2(-18f, 4f), 18f, FontStyles.Bold);
            var body = CreateText("Body", rowObject.transform, new Vector2(18f, 8f), new Vector2(-18f, -34f), 18f, FontStyles.Normal);
            body.overflowMode = TextOverflowModes.Ellipsis;

            var row = rowObject.GetComponent<DialogueBacklogRow>();
            SetPrivateField(row, "speakerText", speaker);
            SetPrivateField(row, "bodyText", body);

            return row;
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

        private static void SetPrivateField<TTarget, TValue>(TTarget target, string fieldName, TValue value) where TTarget : class
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"NovelGameBootstrap: {target.GetType().Name} field '{fieldName}' was not found.");
                return;
            }

            field.SetValue(target, value);
        }

        private DialogueSaveSlot SaveWithThumbnail(int slot, bool isAutosave, string title)
        {
            _saveSystem.SaveWithThumbnail(slot, isAutosave, title);
            var result = _saveSystem.LastOperationResult;
            return result != null && result.Failed ? null : _saveSystem.Peek(slot);
        }

        private string BuildSaveTitle(int slot)
        {
            if (_manager != null && _manager.CurrentData != null)
            {
                var speaker = _manager.CurrentData.Speaker ?? string.Empty;
                var text = _manager.CurrentData.Text ?? string.Empty;
                var prefix = string.IsNullOrEmpty(speaker) ? string.Empty : speaker + ": ";
                var title = prefix + text;
                return title.Length > 40 ? title.Substring(0, 40) + "..." : title;
            }

            return slot == DialogueSaveSystem.QuickSaveSlot ? "Quick Save" : $"Save {slot}";
        }

        private void HandleSaveOperationFailed(DialogueSaveOperationResult result)
        {
            if (result == null)
                return;

            Debug.LogWarning($"NovelGameBootstrap: dialogue save {result.Operation} failed for slot {result.SlotIndex}: {result.Message}");
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
