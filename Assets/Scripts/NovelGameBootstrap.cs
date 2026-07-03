using System;
using System.Collections.Generic;
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
        private static TMP_FontAsset _runtimeUiFontAsset;

        [SerializeField] private string dialogueResourcePath = DefaultDialogueResourcePath;
        [SerializeField] private string startTriggerKey = DefaultStartTriggerKey;
        [SerializeField] private bool startOnLaunch = true;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private float typewriterInterval = 0.025f;
        [SerializeField] private DialogueView dialogueViewPrefab;
        [SerializeField] private DialogueBacklogView dialogueBacklogViewPrefab;
        [SerializeField] private BackgroundDatabase backgroundDatabase;
        [SerializeField] private CharacterExpressionDatabase characterDatabase;
        [SerializeField] private AudioDatabase audioDatabase;
        [SerializeField] private TMP_FontAsset uiFontAsset;
        [SerializeField] private string uiFontResourcePath = "Fonts/LogoTypeGothicCondense/LogoTypeGothicCondense";
        [SerializeField] private bool enableDebugSaveHotkeys = true;
        [SerializeField] private bool enableDialogueKeyboardInput = true;
        [SerializeField] private bool showTitleMenu = true;
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private string mainSceneName = "Main";
        [SerializeField] private bool unlockProgressMarkers = true;
        [SerializeField] private int defaultManualSaveSlot = DialogueSaveSlotConventions.FirstManualSlot;
        [SerializeField] private int manualSaveSlotCount = 6;
        [SerializeField] private bool showSaveLoadLauncher = true;
        [SerializeField] private bool saveThumbnails;
        [SerializeField] private string saveContentVersion = "r00_escape_talksystem";
        [SerializeField] private string saveProductChannel = string.Empty;

        private DialogueManager _manager;
        private DialogueView _view;
        private DialogueBacklogView _backlogView;
        private DialogueSaveSystem _saveSystem;
        private DialoguePlaybackController _playbackController;
        private DialogueInputRouter _inputRouter;
        private DialogueKeyboardInput _keyboardInput;
        private DialogueStageView _stageView;
        private DialogueStageBinder _stageBinder;
        private DialogueAudioPlayer _audioPlayer;
        private DialogueAudioBinder _audioBinder;
        private GameObject _titleMenuRoot;
        private Button _continueButton;
        private Button _quickLoadButton;
        private GameObject _saveLoadRoot;
        private GameObject _saveLoadLauncherRoot;
        private TMP_Text _saveLoadHeading;
        private Button _saveLoadSaveTabButton;
        private Button _saveLoadLoadTabButton;
        private bool _saveLoadModeIsSave = true;
        private DialogueUnlockRegistry _unlockRegistry;
        private DialogueUnlockSaveService _unlockSaveService;
        private readonly HashSet<string> _reachedEventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SaveLoadSlotRow> _saveLoadRows = new List<SaveLoadSlotRow>();

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
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (_manager != null)
                _manager.ProgressMarkerReached -= HandleProgressMarkerReached;

            if (_unlockRegistry != null)
                _unlockRegistry.Unlocked -= HandleDialogueUnlocked;

            DisconnectPresentationIssues();

            if (_instance == this)
                _instance = null;
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
                if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.L))
                    OpenLoadScreen();
            }
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

            HideTitleMenu();
            HideSaveLoadScreen();
            _manager.StartDialogueForState(triggerKey);
        }

        public void StartNewGame()
        {
            StartDialogueForTrigger(startTriggerKey);
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

            var succeeded = saved != null;
            if (succeeded)
            {
                RefreshTitleMenuButtons();
                RefreshSaveLoadScreen();
            }

            return succeeded;
        }

        public bool LoadDialogue()
        {
            return LoadDialogue(defaultManualSaveSlot);
        }

        public bool LoadDialogue(int slot)
        {
            if (!EnsureSaveSystemReady())
                return false;

            var loaded = _saveSystem.Load(slot);
            if (loaded)
            {
                HideTitleMenu();
                HideSaveLoadScreen();
            }

            return loaded;
        }

        public bool QuickSave()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var title = BuildSaveTitle(DialogueSaveSystem.QuickSaveSlot);
            if (saveThumbnails)
            {
                _saveSystem.QuickSaveWithThumbnail(title);
                var succeeded = _saveSystem.LastOperationResult == null || _saveSystem.LastOperationResult.Succeeded;
                if (succeeded)
                {
                    RefreshTitleMenuButtons();
                    RefreshSaveLoadScreen();
                }

                return succeeded;
            }

            var saved = _saveSystem.QuickSave(title) != null;
            if (saved)
            {
                RefreshTitleMenuButtons();
                RefreshSaveLoadScreen();
            }

            return saved;
        }

        public bool QuickLoad()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var loaded = _saveSystem.QuickLoad();
            if (loaded)
            {
                HideTitleMenu();
                HideSaveLoadScreen();
            }

            return loaded;
        }

        public bool ContinueLatest()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var candidate = _saveSystem.GetLatestContinueCandidate(true, true, false);
            var loaded = candidate != null && candidate.CanLoad && _saveSystem.Load(candidate.SlotIndex);
            if (loaded)
            {
                HideTitleMenu();
                HideSaveLoadScreen();
            }

            return loaded;
        }

        public void OpenSaveScreen()
        {
            ShowSaveLoadScreen(true);
        }

        public void OpenLoadScreen()
        {
            ShowSaveLoadScreen(false);
        }

        public void CloseSaveLoadScreen()
        {
            HideSaveLoadScreen();
        }

        public bool HasSave(int slot)
        {
            return EnsureSaveSystemReady() && _saveSystem.Exists(slot);
        }

        public bool HasContinueSave()
        {
            if (!EnsureSaveSystemReady())
                return false;

            var candidate = _saveSystem.GetLatestContinueCandidate(true, true, false);
            return candidate != null && candidate.CanLoad;
        }

        public bool HasReachedEvent(string eventKey)
        {
            return !string.IsNullOrWhiteSpace(eventKey) && _reachedEventKeys.Contains(eventKey.Trim());
        }

        public bool IsUnlocked(string unlockId)
        {
            return _unlockRegistry != null && _unlockRegistry.IsUnlocked(unlockId);
        }

        public List<string> ListUnlockedIds(string category)
        {
            return _unlockRegistry != null
                ? _unlockRegistry.ListUnlockedIds(category)
                : new List<string>();
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

            return EvaluateCondition(conditionKey);
        }

        private void BuildRuntime()
        {
            EnsureUiFontAsset();
            EnsureEventSystem();
            _view = EnsureDialogueView();
            _backlogView = EnsureDialogueBacklogView();
            _manager = EnsureDialogueManager();
            _saveSystem = EnsureDialogueSaveSystem(_manager);
            _playbackController = EnsureDialoguePlaybackController(_manager);
            EnsureDialoguePresentation(_saveSystem);
            EnsureDialogueUnlocks();
            ConnectProgressMarkers(_manager);
            EnsureDialogueInputRouting(_view, _backlogView, _playbackController);
            EnsureSaveLoadLauncher();

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

            if (ShouldShowTitleMenu())
            {
                ShowTitleMenu();
                yield break;
            }

            if (startOnLaunch && !string.IsNullOrEmpty(startTriggerKey))
                _manager.StartDialogueForState(startTriggerKey);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShouldShowTitleMenu(scene.name))
                ShowTitleMenu();
            else
                HideTitleMenu();
        }

        private bool ShouldShowTitleMenu()
        {
            return ShouldShowTitleMenu(SceneManager.GetActiveScene().name);
        }

        private bool ShouldShowTitleMenu(string sceneName)
        {
            return showTitleMenu
                && !string.IsNullOrEmpty(titleSceneName)
                && string.Equals(sceneName, titleSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluateCondition(string conditionKey)
        {
            var normalized = conditionKey.Trim();
            var invert = normalized.StartsWith("!", StringComparison.Ordinal);
            if (invert)
                normalized = normalized.Substring(1).Trim();

            var result = EvaluatePositiveCondition(normalized);
            return invert ? !result : result;
        }

        private bool EvaluatePositiveCondition(string conditionKey)
        {
            if (conditionKey.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                return HasReachedEvent(conditionKey.Substring("event:".Length));

            if (conditionKey.StartsWith("unlock:", StringComparison.OrdinalIgnoreCase))
                return IsUnlocked(conditionKey.Substring("unlock:".Length));

            if (conditionKey.StartsWith("chapter:", StringComparison.OrdinalIgnoreCase)
                || conditionKey.StartsWith("route:", StringComparison.OrdinalIgnoreCase)
                || conditionKey.StartsWith("ending:", StringComparison.OrdinalIgnoreCase))
            {
                return IsUnlocked(conditionKey);
            }

            return HasReachedEvent(conditionKey) || IsUnlocked(conditionKey);
        }

        private void EnsureDialogueUnlocks()
        {
            if (_unlockRegistry == null)
                _unlockRegistry = new DialogueUnlockRegistry();

            if (_unlockSaveService == null)
                _unlockSaveService = new DialogueUnlockSaveService(new FileDialogueUnlockStorage());

            _unlockRegistry.Unlocked -= HandleDialogueUnlocked;
            _unlockRegistry.Unlocked += HandleDialogueUnlocked;

            if (!_unlockSaveService.LoadInto(_unlockRegistry)
                && !string.IsNullOrEmpty(_unlockSaveService.LastError))
            {
                Debug.LogWarning($"NovelGameBootstrap: {_unlockSaveService.LastError}");
            }
        }

        private void ConnectProgressMarkers(DialogueManager manager)
        {
            if (manager == null)
                return;

            manager.ProgressMarkerReached -= HandleProgressMarkerReached;
            manager.ProgressMarkerReached += HandleProgressMarkerReached;
        }

        private void HandleProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (!unlockProgressMarkers || context == null || context.Marker == null)
                return;

            var marker = context.Marker;
            if (!marker.IsFirstReach || string.IsNullOrEmpty(marker.Key))
                return;

            var category = GetProgressUnlockCategory(marker.Type);
            if (string.IsNullOrEmpty(category))
                return;

            var unlockId = category + ":" + marker.Key;
            if (_unlockRegistry == null || !_unlockRegistry.MarkUnlocked(unlockId, category))
                return;

            SaveDialogueUnlocks();
        }

        private static string GetProgressUnlockCategory(DialogueProgressMarkerType markerType)
        {
            switch (markerType)
            {
                case DialogueProgressMarkerType.Chapter:
                    return "chapter";
                case DialogueProgressMarkerType.Route:
                    return "route";
                case DialogueProgressMarkerType.Ending:
                    return "ending";
                default:
                    return string.Empty;
            }
        }

        private void HandleDialogueUnlocked(DialogueUnlockEventContext context)
        {
            if (context == null || context.Entry == null)
                return;

            Debug.Log($"NovelGameBootstrap: unlocked '{context.Entry.Id}'.");
        }

        private void SaveDialogueUnlocks()
        {
            if (_unlockSaveService == null || _unlockRegistry == null)
                return;

            if (!_unlockSaveService.Save(_unlockRegistry))
                Debug.LogWarning($"NovelGameBootstrap: {_unlockSaveService.LastError}");
        }

        private void EnsureUiFontAsset()
        {
            if (uiFontAsset != null)
            {
                _runtimeUiFontAsset = uiFontAsset;
                return;
            }

            if (_runtimeUiFontAsset != null || string.IsNullOrEmpty(uiFontResourcePath))
                return;

            var font = Resources.Load<Font>(uiFontResourcePath);
            if (font == null)
            {
                Debug.LogWarning($"NovelGameBootstrap: UI font was not found at Resources/{uiFontResourcePath}.");
                return;
            }

            _runtimeUiFontAsset = TMP_FontAsset.CreateFontAsset(font);
            _runtimeUiFontAsset.name = font.name + " TMP Runtime";
        }

        private void ShowTitleMenu()
        {
            EnsureEventSystem();

            if (_titleMenuRoot == null)
                _titleMenuRoot = CreateTitleMenu();

            RefreshTitleMenuButtons();
            _titleMenuRoot.SetActive(true);
            RefreshSaveLoadLauncherVisibility();
        }

        private void HideTitleMenu()
        {
            if (_titleMenuRoot != null)
                _titleMenuRoot.SetActive(false);

            RefreshSaveLoadLauncherVisibility();
        }

        private void RefreshTitleMenuButtons()
        {
            if (_titleMenuRoot == null)
                return;

            if (_continueButton != null)
                _continueButton.interactable = HasContinueSave();

            if (_quickLoadButton != null)
                _quickLoadButton.interactable = HasSave(DialogueSaveSystem.QuickSaveSlot);
        }

        private GameObject CreateTitleMenu()
        {
            var canvas = EnsureDialogueCanvas();
            var root = new GameObject("WhiteRoomTitleMenu", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.018f, 0.02f, 0.022f, 0.96f);

            var menuObject = new GameObject("Menu", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuObject.transform.SetParent(root.transform, false);

            var menuRect = (RectTransform)menuObject.transform;
            menuRect.anchorMin = new Vector2(0.08f, 0.18f);
            menuRect.anchorMax = new Vector2(0.42f, 0.78f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            var layout = menuObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTitleLabel(menuObject.transform, "WhiteRoom", 64f, 96f, FontStyles.Bold);
            CreateTitleSpacer(menuObject.transform, 18f);
            CreateTitleButton(menuObject.transform, "New Game", StartNewGame);
            _continueButton = CreateTitleButton(menuObject.transform, "Continue", () => ContinueLatest());
            CreateTitleButton(menuObject.transform, "Load Game", OpenLoadScreen);
            _quickLoadButton = CreateTitleButton(menuObject.transform, "Quick Load", () => QuickLoad());

            root.SetActive(false);
            return root;
        }

        private void EnsureSaveLoadLauncher()
        {
            if (!showSaveLoadLauncher || _saveLoadLauncherRoot != null)
                return;

            var canvas = EnsureDialogueCanvas();
            _saveLoadLauncherRoot = new GameObject("SaveLoadLauncher", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _saveLoadLauncherRoot.transform.SetParent(canvas.transform, false);
            _saveLoadLauncherRoot.transform.SetAsLastSibling();

            var rect = (RectTransform)_saveLoadLauncherRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(260f, 46f);

            var layout = _saveLoadLauncherRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateLauncherButton(_saveLoadLauncherRoot.transform, "Save", OpenSaveScreen);
            CreateLauncherButton(_saveLoadLauncherRoot.transform, "Load", OpenLoadScreen);
            RefreshSaveLoadLauncherVisibility();
        }

        private void RefreshSaveLoadLauncherVisibility()
        {
            if (_saveLoadLauncherRoot == null)
                return;

            var titleVisible = _titleMenuRoot != null && _titleMenuRoot.activeSelf;
            _saveLoadLauncherRoot.SetActive(showSaveLoadLauncher && !titleVisible);
        }

        private void ShowSaveLoadScreen(bool saveMode)
        {
            EnsureEventSystem();

            if (_saveLoadRoot == null)
                _saveLoadRoot = CreateSaveLoadScreen();

            _saveLoadModeIsSave = saveMode;
            RefreshSaveLoadScreen();
            _saveLoadRoot.SetActive(true);
            _saveLoadRoot.transform.SetAsLastSibling();

            if (_view != null)
                _view.SetAutoAdvanceSuspended(true);
        }

        private void HideSaveLoadScreen()
        {
            if (_saveLoadRoot != null)
                _saveLoadRoot.SetActive(false);

            if (_view != null && (_backlogView == null || !_backlogView.IsOpen))
                _view.SetAutoAdvanceSuspended(false);
        }

        private void RefreshSaveLoadScreen()
        {
            if (_saveLoadRoot == null)
                return;

            if (_saveLoadHeading != null)
                _saveLoadHeading.text = _saveLoadModeIsSave ? "Save" : "Load";

            SetTabSelected(_saveLoadSaveTabButton, _saveLoadModeIsSave);
            SetTabSelected(_saveLoadLoadTabButton, !_saveLoadModeIsSave);

            var canSave = CanSaveDialogue();
            for (var i = 0; i < _saveLoadRows.Count; i++)
            {
                var slot = DialogueSaveSlotConventions.FirstManualSlot + i;
                var viewModel = EnsureSaveSystemReady()
                    ? _saveSystem.GetSlotViewModel(slot, false)
                    : DialogueSaveSlotViewModel.Empty(slot, "Save system is not ready.");

                RefreshSaveLoadRow(_saveLoadRows[i], viewModel, canSave);
            }
        }

        private bool CanSaveDialogue()
        {
            return _manager != null && _manager.CurrentData != null && EnsureSaveSystemReady();
        }

        private void RefreshSaveLoadRow(SaveLoadSlotRow row, DialogueSaveSlotViewModel viewModel, bool canSave)
        {
            if (row == null || viewModel == null)
                return;

            var slot = viewModel.SlotIndex;
            row.SlotLabel.text = $"Slot {slot}";
            row.TitleLabel.text = viewModel.IsEmpty ? "Empty Slot" : FormatSaveTitle(viewModel.Title);
            row.MetaLabel.text = FormatSaveMeta(viewModel);
            row.ActionLabel.text = _saveLoadModeIsSave ? "Save" : "Load";
            row.ActionButton.interactable = _saveLoadModeIsSave ? canSave : viewModel.CanLoad;
            row.ActionButton.onClick.RemoveAllListeners();
            row.ActionButton.onClick.AddListener(() => HandleSaveLoadSlot(slot));
        }

        private void HandleSaveLoadSlot(int slot)
        {
            if (_saveLoadModeIsSave)
            {
                SaveDialogue(slot);
                return;
            }

            LoadDialogue(slot);
        }

        private GameObject CreateSaveLoadScreen()
        {
            var canvas = EnsureDialogueCanvas();
            var root = new GameObject("SaveLoadScreen", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.74f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.16f, 0.12f);
            panelRect.anchorMax = new Vector2(0.84f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.025f, 0.03f, 0.034f, 0.96f);

            _saveLoadHeading = CreateText("Heading", panel.transform, new Vector2(28f, -58f), new Vector2(-240f, 12f), 34f, FontStyles.Bold);
            _saveLoadHeading.alignment = TextAlignmentOptions.Left;

            _saveLoadSaveTabButton = CreateSaveLoadTabButton(panel.transform, "Save", () => ShowSaveLoadScreen(true), new Vector2(1f, 1f), new Vector2(-300f, -24f));
            _saveLoadLoadTabButton = CreateSaveLoadTabButton(panel.transform, "Load", () => ShowSaveLoadScreen(false), new Vector2(1f, 1f), new Vector2(-190f, -24f));
            CreateSaveLoadTabButton(panel.transform, "Close", CloseSaveLoadScreen, new Vector2(1f, 1f), new Vector2(-80f, -24f));

            var content = CreateSaveLoadScrollContent(panel.transform);
            _saveLoadRows.Clear();
            for (var i = 0; i < Mathf.Max(1, manualSaveSlotCount); i++)
                _saveLoadRows.Add(CreateSaveLoadSlotRow(content));

            root.SetActive(false);
            return root;
        }

        private static Transform CreateSaveLoadScrollContent(Transform parent)
        {
            var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);

            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(28f, 28f);
            scrollRectTransform.offsetMax = new Vector2(-28f, -94f);

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
            layout.spacing = 10f;
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

        private static SaveLoadSlotRow CreateSaveLoadSlotRow(Transform parent)
        {
            var rowObject = new GameObject("SaveSlotRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            var rect = (RectTransform)rowObject.transform;
            rect.sizeDelta = new Vector2(900f, 78f);

            var image = rowObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.095f, 0.105f, 0.96f);

            var layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 78f;
            layout.preferredHeight = 78f;

            var slotLabel = CreateText("Slot", rowObject.transform, new Vector2(18f, 10f), new Vector2(-760f, -10f), 22f, FontStyles.Bold);
            slotLabel.alignment = TextAlignmentOptions.MidlineLeft;
            slotLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var titleLabel = CreateText("Title", rowObject.transform, new Vector2(150f, -32f), new Vector2(-170f, 8f), 20f, FontStyles.Bold);
            titleLabel.alignment = TextAlignmentOptions.TopLeft;
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var metaLabel = CreateText("Meta", rowObject.transform, new Vector2(150f, 8f), new Vector2(-170f, -38f), 17f, FontStyles.Normal);
            metaLabel.alignment = TextAlignmentOptions.TopLeft;
            metaLabel.color = new Color(0.72f, 0.77f, 0.79f, 1f);
            metaLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var actionButton = CreateSaveLoadActionButton(rowObject.transform);
            var actionLabel = actionButton.GetComponentInChildren<TMP_Text>();

            return new SaveLoadSlotRow
            {
                SlotLabel = slotLabel,
                TitleLabel = titleLabel,
                MetaLabel = metaLabel,
                ActionButton = actionButton,
                ActionLabel = actionLabel
            };
        }

        private static Button CreateLauncherButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action)
        {
            var button = CreateSaveLoadButton(labelText + "Button", parent, labelText, 18f);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateSaveLoadTabButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action, Vector2 anchor, Vector2 position)
        {
            var button = CreateSaveLoadButton(labelText + "Button", parent, labelText, 18f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(96f, 42f);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateSaveLoadActionButton(Transform parent)
        {
            var button = CreateSaveLoadButton("ActionButton", parent, "Save", 18f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(126f, 46f);
            return button;
        }

        private static Button CreateSaveLoadButton(string name, Transform parent, string labelText, float fontSize)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.17f, 0.22f, 0.24f, 0.96f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.17f, 0.22f, 0.24f, 0.96f);
            colors.highlightedColor = new Color(0.25f, 0.31f, 0.34f, 1f);
            colors.pressedColor = new Color(0.11f, 0.15f, 0.17f, 1f);
            colors.disabledColor = new Color(0.08f, 0.09f, 0.10f, 0.55f);
            button.colors = colors;

            var label = CreateText("Label", buttonObject.transform, new Vector2(10f, 6f), new Vector2(-10f, -6f), fontSize, FontStyles.Bold);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return button;
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? new Color(0.30f, 0.36f, 0.38f, 1f) : new Color(0.17f, 0.22f, 0.24f, 0.96f);
        }

        private static string FormatSaveTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Saved Game";

            return title.Length > 46 ? title.Substring(0, 46) + "..." : title;
        }

        private static string FormatSaveMeta(DialogueSaveSlotViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            if (viewModel.HasError)
                return viewModel.ErrorMessage;

            if (viewModel.IsEmpty)
                return "No save data";

            return viewModel.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }

        private static TextMeshProUGUI CreateTitleLabel(Transform parent, string textValue, float fontSize, float height, FontStyles style)
        {
            var textObject = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var layout = textObject.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return text;
        }

        private static void CreateTitleSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);

            var layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static Button CreateTitleButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(labelText.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.24f, 0.96f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(action);

            var colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.24f, 0.96f);
            colors.highlightedColor = new Color(0.24f, 0.30f, 0.32f, 1f);
            colors.pressedColor = new Color(0.12f, 0.16f, 0.18f, 1f);
            colors.disabledColor = new Color(0.09f, 0.10f, 0.11f, 0.55f);
            button.colors = colors;

            var label = CreateText("Label", buttonObject.transform, new Vector2(18f, 9f), new Vector2(-18f, -9f), 24f, FontStyles.Bold);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return button;
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

        private void EnsureDialoguePresentation(DialogueSaveSystem saveSystem)
        {
            _stageView = EnsureDialogueStageView();
            _stageBinder = EnsureDialogueStageBinder(_stageView);
            _audioPlayer = EnsureDialogueAudioPlayer();
            _audioBinder = EnsureDialogueAudioBinder(_audioPlayer);

            ConnectPresentationIssues();

            if (saveSystem == null)
                return;

            if (_stageBinder != null)
                saveSystem.RegisterContributor(_stageBinder);

            if (_audioBinder != null)
                saveSystem.RegisterContributor(_audioBinder);
        }

        private DialogueStageView EnsureDialogueStageView()
        {
            var existing = FindFirstObjectByType<DialogueStageView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigureStageDatabases(existing);
                return existing;
            }

            var canvas = EnsureDialogueCanvas();
            var stageObject = new GameObject("DialogueStage", typeof(RectTransform), typeof(DialogueStageView));
            stageObject.transform.SetParent(canvas.transform, false);
            stageObject.transform.SetAsFirstSibling();

            var rect = (RectTransform)stageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = CreateStageImage("Background", stageObject.transform, Vector2.zero, Vector2.one, false);
            var left = CreateStageImage("LeftCharacter", stageObject.transform, new Vector2(0.04f, 0.08f), new Vector2(0.42f, 0.94f), true);
            var center = CreateStageImage("CenterCharacter", stageObject.transform, new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.94f), true);
            var right = CreateStageImage("RightCharacter", stageObject.transform, new Vector2(0.58f, 0.08f), new Vector2(0.96f, 0.94f), true);

            var view = stageObject.GetComponent<DialogueStageView>();
            SetPrivateField(view, "backgroundDatabase", backgroundDatabase);
            SetPrivateField(view, "characterDatabase", characterDatabase);
            SetPrivateField(view, "backgroundImage", background);
            SetPrivateField(view, "slots", new List<DialogueStageSlotBinding>
            {
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Left, image = left },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Center, image = center },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Right, image = right }
            });

            return view;
        }

        private DialogueStageBinder EnsureDialogueStageBinder(DialogueStageView stageView)
        {
            if (stageView == null)
                return null;

            var binder = stageView.GetComponent<DialogueStageBinder>();
            if (binder == null)
                binder = stageView.gameObject.AddComponent<DialogueStageBinder>();

            return binder;
        }

        private DialogueAudioPlayer EnsureDialogueAudioPlayer()
        {
            var existing = FindFirstObjectByType<DialogueAudioPlayer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigureAudioDatabase(existing);
                return existing;
            }

            var audioObject = new GameObject("DialogueAudio", typeof(DialogueAudioPlayer));
            DontDestroyOnLoad(audioObject);

            var player = audioObject.GetComponent<DialogueAudioPlayer>();
            SetPrivateField(player, "audioDatabase", audioDatabase);
            SetPrivateField(player, "bgmSource", CreateDialogueAudioSource("BgmSource", audioObject.transform));
            SetPrivateField(player, "seSource", CreateDialogueAudioSource("SeSource", audioObject.transform));
            SetPrivateField(player, "voiceSource", CreateDialogueAudioSource("VoiceSource", audioObject.transform));

            return player;
        }

        private DialogueAudioBinder EnsureDialogueAudioBinder(DialogueAudioPlayer audioPlayer)
        {
            if (audioPlayer == null)
                return null;

            var binder = audioPlayer.GetComponent<DialogueAudioBinder>();
            if (binder == null)
                binder = audioPlayer.gameObject.AddComponent<DialogueAudioBinder>();

            return binder;
        }

        private void ConfigureStageDatabases(DialogueStageView stageView)
        {
            if (stageView == null)
                return;

            if (backgroundDatabase != null)
                SetPrivateField(stageView, "backgroundDatabase", backgroundDatabase);

            if (characterDatabase != null)
                SetPrivateField(stageView, "characterDatabase", characterDatabase);
        }

        private void ConfigureAudioDatabase(DialogueAudioPlayer audioPlayer)
        {
            if (audioPlayer != null && audioDatabase != null)
                SetPrivateField(audioPlayer, "audioDatabase", audioDatabase);
        }

        private void ConnectPresentationIssues()
        {
            DisconnectPresentationIssues();

            var stageIssues = _stageView as IDialoguePresentationIssueSource;
            if (stageIssues != null)
                stageIssues.PresentationIssueRaised += HandlePresentationIssue;

            var audioIssues = _audioPlayer as IDialoguePresentationIssueSource;
            if (audioIssues != null)
                audioIssues.PresentationIssueRaised += HandlePresentationIssue;
        }

        private void DisconnectPresentationIssues()
        {
            var stageIssues = _stageView as IDialoguePresentationIssueSource;
            if (stageIssues != null)
                stageIssues.PresentationIssueRaised -= HandlePresentationIssue;

            var audioIssues = _audioPlayer as IDialoguePresentationIssueSource;
            if (audioIssues != null)
                audioIssues.PresentationIssueRaised -= HandlePresentationIssue;
        }

        private void HandlePresentationIssue(DialoguePresentationIssueContext context)
        {
            if (context == null)
                return;

            Debug.LogWarning($"NovelGameBootstrap: presentation issue {context.Kind} '{context.Key}': {context.Message}");
        }

        private DialoguePlaybackController EnsureDialoguePlaybackController(DialogueManager manager)
        {
            if (manager == null)
                return null;

            var playbackController = manager.GetComponent<DialoguePlaybackController>();
            if (playbackController == null)
                playbackController = manager.gameObject.AddComponent<DialoguePlaybackController>();

            return playbackController;
        }

        private void EnsureDialogueInputRouting(DialogueView view, DialogueBacklogView backlog, DialoguePlaybackController playbackController)
        {
            if (!enableDialogueKeyboardInput || view == null)
                return;

            _keyboardInput = view.GetComponent<DialogueKeyboardInput>();
            if (_keyboardInput == null)
                _keyboardInput = view.gameObject.AddComponent<DialogueKeyboardInput>();

            _inputRouter = view.GetComponent<DialogueInputRouter>();
            if (_inputRouter == null)
                _inputRouter = view.gameObject.AddComponent<DialogueInputRouter>();

            SetPrivateField(_inputRouter, "inputSourceComponent", _keyboardInput);
            SetPrivateField(_inputRouter, "backlog", backlog);
            SetPrivateField(_inputRouter, "playbackController", playbackController);

            if (view.gameObject.activeInHierarchy)
                ConnectInputRouter(_inputRouter, view, _keyboardInput);
        }

        private static void ConnectInputRouter(DialogueInputRouter router, DialogueView view, DialogueKeyboardInput input)
        {
            if (router == null || input == null)
                return;

            SetPrivateField(router, "_view", view);
            SetPrivateField(router, "_inputSource", input);

            var method = typeof(DialogueInputRouter).GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning("NovelGameBootstrap: DialogueInputRouter.HandleInput was not found.");
                return;
            }

            var handler = (Action<DialogueInputAction>)Delegate.CreateDelegate(typeof(Action<DialogueInputAction>), router, method);
            input.InputReceived -= handler;
            input.InputReceived += handler;
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

        private static Image CreateStageImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;

            return image;
        }

        private static AudioSource CreateDialogueAudioSource(string name, Transform parent)
        {
            var sourceObject = new GameObject(name, typeof(AudioSource));
            sourceObject.transform.SetParent(parent, false);

            var source = sourceObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            return source;
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
            if (_runtimeUiFontAsset != null)
                text.font = _runtimeUiFontAsset;
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

            var eventKey = context.EventKey.Trim();
            if (eventKey.Length == 0)
                return;

            _reachedEventKeys.Add(eventKey);

            switch (eventKey)
            {
                case "scene_start":
                case "load_main":
                    LoadMainScene();
                    break;
                default:
                    Debug.Log($"NovelGameBootstrap: dialogue event '{context.EventKey}' was raised.");
                    break;
            }
        }

        private void LoadMainScene()
        {
            HideTitleMenu();

            if (string.IsNullOrEmpty(mainSceneName)
                || string.Equals(SceneManager.GetActiveScene().name, mainSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            SceneManager.LoadScene(mainSceneName);
        }

        private sealed class SaveLoadSlotRow
        {
            public TMP_Text SlotLabel;
            public TMP_Text TitleLabel;
            public TMP_Text MetaLabel;
            public Button ActionButton;
            public TMP_Text ActionLabel;
        }
    }
}
