using System;
using System.Collections;
using System.Collections.Generic;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Composition root of the novel game. Builds the dialogue runtime, services and
    /// UI controllers once at startup, wires them together, and exposes the public
    /// game API (start/save/load/backlog) they implement.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class NovelGameBootstrap : MonoBehaviour
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
        [SerializeField] private bool showCommandBar = true;
        [SerializeField] private bool showSaveLoadLauncher = true;
        [SerializeField] private bool saveThumbnails;
        [SerializeField] private string saveContentVersion = "r00_escape_talksystem";
        [SerializeField] private string saveProductChannel = string.Empty;

        private DialogueManager _manager;
        private DialogueView _view;
        private NovelSaveService _saveService;
        private DialogueProgressService _progress;
        private DialoguePresentationIssueLogger _presentationIssueLogger;
        private BacklogController _backlog;
        private TitleMenuController _titleMenu;
        private SaveLoadScreenController _saveLoadScreen;
        private NovelCommandBarController _commandBar;
        private NovelNotificationController _notifications;
        private DialogueKeyboardInput _dialogueKeyboardInput;
        private bool _quickLoadAvailable;

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

            _saveService?.Dispose();
            _progress?.Dispose();
            _presentationIssueLogger?.Dispose();
            _commandBar?.Dispose();

            if (_instance == this)
                _instance = null;
        }

        private void Start()
        {
            BuildRuntime();
        }

        private void Update()
        {
            if (_saveLoadScreen != null && _saveLoadScreen.IsOpen)
                return;

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

            _titleMenu?.Hide();
            _saveLoadScreen?.Close();
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
            return _saveService != null && _saveService.Save();
        }

        public bool SaveDialogue(int slot)
        {
            return _saveService != null && _saveService.Save(slot);
        }

        public bool LoadDialogue()
        {
            return _saveService != null && _saveService.Load();
        }

        public bool LoadDialogue(int slot)
        {
            return _saveService != null && _saveService.Load(slot);
        }

        public bool QuickSave()
        {
            return _saveService != null && _saveService.QuickSave();
        }

        public bool QuickLoad()
        {
            return _saveService != null && _saveService.QuickLoad();
        }

        public bool ContinueLatest()
        {
            return _saveService != null && _saveService.ContinueLatest();
        }

        public void OpenSaveScreen()
        {
            _saveLoadScreen?.OpenSave();
        }

        public void OpenLoadScreen()
        {
            _saveLoadScreen?.OpenLoad();
        }

        public void CloseSaveLoadScreen()
        {
            _saveLoadScreen?.Close();
        }

        public bool HasSave(int slot)
        {
            return _saveService != null && _saveService.HasSave(slot);
        }

        public bool HasContinueSave()
        {
            return _saveService != null && _saveService.HasContinueSave();
        }

        public bool HasReachedEvent(string eventKey)
        {
            return _progress != null && _progress.HasReachedEvent(eventKey);
        }

        public bool IsUnlocked(string unlockId)
        {
            return _progress != null && _progress.IsUnlocked(unlockId);
        }

        public List<string> ListUnlockedIds(string category)
        {
            return _progress != null ? _progress.ListUnlockedIds(category) : new List<string>();
        }

        public void ToggleBacklog()
        {
            _backlog?.Toggle();
        }

        public void OpenBacklog()
        {
            _backlog?.Open();
        }

        public void CloseBacklog()
        {
            _backlog?.Close();
        }

        private void BuildRuntime()
        {
            NovelUiFactory.EnsureFont(uiFontAsset, uiFontResourcePath);
            NovelUiFactory.EnsureEventSystem();

            _view = DialogueViewFactory.EnsureDialogueView(dialogueViewPrefab);
            var backlogView = DialogueViewFactory.EnsureBacklogView(dialogueBacklogViewPrefab);

            _manager = DialogueRuntimeFactory.EnsureManager();
            var saveSystem = DialogueRuntimeFactory.EnsureSaveSystem(_manager, saveContentVersion, saveProductChannel);
            var playbackController = DialogueRuntimeFactory.EnsurePlaybackController(_manager);

            var presentation = DialoguePresentationFactory.Ensure(backgroundDatabase, characterDatabase, audioDatabase);
            presentation.RegisterSaveContributors(saveSystem);
            _presentationIssueLogger = new DialoguePresentationIssueLogger();
            _presentationIssueLogger.Watch(presentation.StageView);
            _presentationIssueLogger.Watch(presentation.AudioPlayer);

            _progress = new DialogueProgressService(unlockProgressMarkers);
            _progress.AttachTo(_manager);

            if (enableDialogueKeyboardInput)
            {
                DialogueRuntimeFactory.EnsureKeyboardInputRouting(_view, backlogView, playbackController);
                _dialogueKeyboardInput = _view.GetComponent<DialogueKeyboardInput>();
            }

            var autoAdvanceGate = new DialogueAutoAdvanceGate(_view);
            _saveService = new NovelSaveService(_manager, saveSystem, defaultManualSaveSlot, saveThumbnails);
            _quickLoadAvailable = _saveService.HasSave(DialogueSaveSystem.QuickSaveSlot);
            _backlog = new BacklogController(backlogView, autoAdvanceGate);
            _titleMenu = new TitleMenuController(_saveService, StartNewGame, OpenLoadScreen);
            _notifications = new NovelNotificationController();
            _saveLoadScreen = new SaveLoadScreenController(
                _saveService,
                autoAdvanceGate,
                _notifications,
                manualSaveSlotCount,
                showSaveLoadLauncher && !showCommandBar);
            _saveLoadScreen.EnsureLauncher();
            _commandBar = CreateCommandBar(playbackController);
            _commandBar.EnsureCreated();
            _commandBar.SetSceneVisible(ShouldShowCommandBar());

            _saveService.Saved += HandleSaveCompleted;
            _saveService.Loaded += HandleLoadCompleted;
            _saveService.Feedback += HandleSaveFeedback;
            _titleMenu.VisibilityChanged += _saveLoadScreen.SetTitleMenuVisible;
            _saveLoadScreen.VisibilityChanged += HandleSaveLoadVisibilityChanged;

            _manager.SetView(_view);
            _manager.SetVariableResolver(new PlayerNameVariableResolver(() => playerName));
            _manager.SetConditionEvaluator(_progress);
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

        private IEnumerator LoadDialogueAndStart(TextAsset csv)
        {
            _manager.LoadRepository(new TextAssetDialogueRepositoryLoader(csv));
            yield return null;
            yield return null;

            if (ShouldShowTitleMenu())
            {
                _titleMenu.Show();
                yield break;
            }

            if (startOnLaunch && !string.IsNullOrEmpty(startTriggerKey))
                _manager.StartDialogueForState(startTriggerKey);
        }

        private void HandleSaveCompleted()
        {
            _quickLoadAvailable = _saveService != null &&
                                  _saveService.HasSave(DialogueSaveSystem.QuickSaveSlot);
            _titleMenu.RefreshButtons();
            _saveLoadScreen.Refresh();
            _commandBar?.Refresh();
        }

        private void HandleLoadCompleted()
        {
            _titleMenu.Hide();
            _saveLoadScreen.Close();
        }

        private void HandleSaveFeedback(NovelSaveFeedback feedback)
        {
            if (feedback != null)
                _notifications?.Show(feedback.Message, feedback.Succeeded);
        }

        private void HandleSaveLoadVisibilityChanged(bool visible)
        {
            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = !visible;
            _commandBar?.SetInputBlocked(visible);
        }

        private void HandleDialogueEvent(DialogueEventContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.EventKey))
                return;

            var eventKey = context.EventKey.Trim();
            if (eventKey.Length == 0)
                return;

            _progress.RecordEvent(eventKey);

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
            _titleMenu?.Hide();

            if (string.IsNullOrEmpty(mainSceneName)
                || string.Equals(SceneManager.GetActiveScene().name, mainSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            SceneManager.LoadScene(mainSceneName);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_titleMenu == null)
                return;

            if (ShouldShowTitleMenu(scene.name))
                _titleMenu.Show();
            else
                _titleMenu.Hide();

            _commandBar?.SetSceneVisible(ShouldShowCommandBar(scene.name));
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

        private NovelCommandBarController CreateCommandBar(DialoguePlaybackController playbackController)
        {
            var bindings = new NovelCommandBarBindings
            {
                OpenSave = OpenSaveScreen,
                DirectSave = _saveLoadScreen.DirectSave,
                OpenLoad = OpenLoadScreen,
                QuickSave = () => QuickSave(),
                QuickLoad = () => QuickLoad(),
                PreviousText = Rollback,
                ToggleBacklog = ToggleBacklog,
                ToggleAuto = playbackController != null ? playbackController.ToggleAuto : null,
                ToggleSkip = playbackController != null ? playbackController.ToggleSkip : null,
                CanSave = () => _saveService != null && _saveService.CanSaveNow && !_saveService.IsBusy,
                CanQuickLoad = () => _quickLoadAvailable,
                HasDialogue = () => _manager != null && _manager.CurrentData != null,
                IsBacklogOpen = () => _backlog != null && _backlog.IsOpen,
                IsAutoActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Auto,
                IsSkipActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Skip
            };

            return new NovelCommandBarController(NovelCommandCatalog.Create(bindings));
        }

        private bool ShouldShowCommandBar()
        {
            return ShouldShowCommandBar(SceneManager.GetActiveScene().name);
        }

        private bool ShouldShowCommandBar(string sceneName)
        {
            return showCommandBar
                && !string.IsNullOrEmpty(mainSceneName)
                && string.Equals(sceneName, mainSceneName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
