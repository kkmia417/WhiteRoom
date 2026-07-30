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
        private const string DefaultCollectionCatalogResourcePath = "WhiteRoom/collection_catalog";

        private static NovelGameBootstrap _instance;

        [SerializeField] private string dialogueResourcePath = DefaultDialogueResourcePath;
        [SerializeField] private string startTriggerKey = DefaultStartTriggerKey;
        [SerializeField] private string collectionCatalogResourcePath = DefaultCollectionCatalogResourcePath;
        [SerializeField] private bool startOnLaunch = true;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private float typewriterInterval = 0.025f;
        [SerializeField] private NovelUiConfiguration novelUiConfiguration;
        [SerializeField] private DialogueView dialogueViewPrefab;
        [SerializeField] private DialogueBacklogView dialogueBacklogViewPrefab;
        [SerializeField] private NovelPresentationConfiguration novelPresentationConfiguration;
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
        private DialoguePresentation _presentation;
        private NovelSaveService _saveService;
        private DialogueProgressService _progress;
        private EndingFlowService _endingFlow;
        private EndingResultScreenController _endingResultScreen;
        private CollectionScreenController _collectionScreen;
        private ConfigScreenController _configScreen;
        private QuitConfirmationController _quitConfirmation;
        private VersionedDialogueSettingsStore _settingsStore;
        private DialoguePresentationIssueLogger _presentationIssueLogger;
        private BacklogController _backlog;
        private TitleMenuController _titleMenu;
        private SaveLoadScreenController _saveLoadScreen;
        private NovelCommandBarController _commandBar;
        private NovelNotificationController _notifications;
        private DialogueKeyboardInput _dialogueKeyboardInput;
        private DialoguePlaybackController _playbackController;
        private DialogueBackSkipController _backSkip;
        private bool _quickLoadAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeBootstrap()
        {
            // The command-line PlayMode runner owns its temporary scene and creates
            // explicit fixtures. Auto-starting the product loop there can load Main
            // before the runner begins executing tests.
            if (IsCommandLineTestRun())
                return;

            if (FindFirstObjectByType<NovelGameBootstrap>() != null)
                return;

            var bootstrap = new GameObject(nameof(NovelGameBootstrap));
            bootstrap.AddComponent<NovelGameBootstrap>();
        }

        private static bool IsCommandLineTestRun()
        {
#if UNITY_EDITOR
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-runTests", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
#endif
            return false;
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
            _endingFlow?.Dispose();
            _progress?.Dispose();
            if (_collectionScreen != null)
                _collectionScreen.VisibilityChanged -= HandleCollectionVisibilityChanged;
            if (_configScreen != null)
                _configScreen.VisibilityChanged -= HandleTitleSubScreenVisibilityChanged;
            if (_quitConfirmation != null)
                _quitConfirmation.VisibilityChanged -= HandleTitleSubScreenVisibilityChanged;
            _presentationIssueLogger?.Dispose();
            _commandBar?.Dispose();
            if (_playbackController != null)
                _playbackController.StateChanged -= HandlePlaybackStateChanged;

            if (_instance == this)
                _instance = null;
        }

        private void Start()
        {
            BuildRuntime();
        }

        private void Update()
        {
            if (IsEndingInputBlocked())
                return;

            if (_collectionScreen != null && _collectionScreen.IsOpen)
                return;

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
            if (_manager == null || IsEndingInputBlocked())
                return;

            _manager.StartDialogue(id);
        }

        public void StartDialogueForTrigger(string triggerKey)
        {
            if (_manager == null || IsEndingInputBlocked())
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
            if (_manager != null && !IsEndingInputBlocked())
                _manager.RequestNext();
        }

        public void Rollback()
        {
            if (IsEndingInputBlocked())
                return;

            StopPlaybackAutomation();
            if (_manager != null)
                _manager.Rollback();
        }

        public bool SaveDialogue()
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.Save();
        }

        public bool SaveDialogue(int slot)
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.Save(slot);
        }

        public bool LoadDialogue()
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.Load();
        }

        public bool LoadDialogue(int slot)
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.Load(slot);
        }

        public bool QuickSave()
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.QuickSave();
        }

        public bool QuickLoad()
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.QuickLoad();
        }

        public bool ContinueLatest()
        {
            return !IsEndingInputBlocked() && _saveService != null && _saveService.ContinueLatest();
        }

        public void OpenSaveScreen()
        {
            if (!IsEndingInputBlocked())
                _saveLoadScreen?.OpenSave();
        }

        public void OpenLoadScreen()
        {
            if (!IsEndingInputBlocked())
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
            if (!IsEndingInputBlocked())
                _backlog?.Toggle();
        }

        public void OpenBacklog()
        {
            if (!IsEndingInputBlocked())
                _backlog?.Open();
        }

        public void CloseBacklog()
        {
            _backlog?.Close();
        }

        public void OpenEndingList()
        {
            if (!IsEndingInputBlocked())
                _collectionScreen?.OpenEndingList();
        }

        public void OpenGallery()
        {
            if (!IsEndingInputBlocked())
                _collectionScreen?.OpenGallery();
        }

        public void CloseCollectionScreen()
        {
            _collectionScreen?.Close();
        }

        public void OpenConfig()
        {
            if (!IsEndingInputBlocked())
                _configScreen?.Open();
        }

        public void OpenQuitConfirmation()
        {
            if (!IsEndingInputBlocked())
                _quitConfirmation?.Open();
        }

        private void BuildRuntime()
        {
            NovelUiFactory.EnsureFont(uiFontAsset, uiFontResourcePath);
            NovelUiFactory.EnsureEventSystem();

            var uiConfiguration = novelUiConfiguration != null
                ? novelUiConfiguration
                : NovelUiConfiguration.LoadDefault();
            var resolvedDialogueViewPrefab = dialogueViewPrefab != null
                ? dialogueViewPrefab
                : uiConfiguration != null ? uiConfiguration.DialogueViewPrefab : null;
            var resolvedBacklogViewPrefab = dialogueBacklogViewPrefab != null
                ? dialogueBacklogViewPrefab
                : uiConfiguration != null ? uiConfiguration.DialogueBacklogViewPrefab : null;
            var dialogueWindowSprite = uiConfiguration != null
                ? uiConfiguration.DialogueWindowSprite
                : null;

            _view = DialogueViewFactory.EnsureDialogueView(resolvedDialogueViewPrefab, dialogueWindowSprite);
            var backlogView = DialogueViewFactory.EnsureBacklogView(resolvedBacklogViewPrefab);

            _manager = DialogueRuntimeFactory.EnsureManager();
            var saveSystem = DialogueRuntimeFactory.EnsureSaveSystem(_manager, saveContentVersion, saveProductChannel);
            var playbackController = DialogueRuntimeFactory.EnsurePlaybackController(_manager);
            _playbackController = playbackController;
            _settingsStore = new VersionedDialogueSettingsStore();
            if (_playbackController != null && _playbackController.Settings != null)
            {
                _playbackController.Settings.Load(_settingsStore);
                if (!string.IsNullOrEmpty(_settingsStore.LastWarning))
                    Debug.LogWarning(_settingsStore.LastWarning);
            }
            if (_playbackController != null)
                _playbackController.StateChanged += HandlePlaybackStateChanged;
            _backSkip = new DialogueBackSkipController(_manager, playbackController);
            var backSkipDriver = _manager.GetComponent<DialogueBackSkipDriver>();
            if (backSkipDriver == null)
                backSkipDriver = _manager.gameObject.AddComponent<DialogueBackSkipDriver>();
            backSkipDriver.Configure(_backSkip);
            var pointerStopper = _view.GetComponent<DialogueBackSkipPointerStopper>();
            if (pointerStopper == null)
                pointerStopper = _view.gameObject.AddComponent<DialogueBackSkipPointerStopper>();
            pointerStopper.Configure(_backSkip);

            var presentationConfiguration = novelPresentationConfiguration != null
                ? novelPresentationConfiguration
                : NovelPresentationConfiguration.LoadDefault();
            var resolvedBackgroundDatabase = backgroundDatabase != null
                ? backgroundDatabase
                : presentationConfiguration != null ? presentationConfiguration.BackgroundDatabase : null;
            var resolvedCharacterDatabase = characterDatabase != null
                ? characterDatabase
                : presentationConfiguration != null ? presentationConfiguration.CharacterDatabase : null;
            var resolvedAudioDatabase = audioDatabase != null
                ? audioDatabase
                : presentationConfiguration != null ? presentationConfiguration.AudioDatabase : null;

            if (resolvedBackgroundDatabase == null || resolvedCharacterDatabase == null || resolvedAudioDatabase == null)
            {
                Debug.LogError(
                    "NovelGameBootstrap: presentation configuration is missing or incomplete; " +
                    "unresolved cues will use no-op fallbacks. " +
                    $"Check Resources/{NovelPresentationConfiguration.DefaultResourcePath}.");
            }

            _presentation = DialoguePresentationFactory.Ensure(
                resolvedBackgroundDatabase,
                resolvedCharacterDatabase,
                resolvedAudioDatabase);
            _presentation.RegisterSaveContributors(saveSystem);
            _presentationIssueLogger = new DialoguePresentationIssueLogger(
                () => _manager != null && _manager.CurrentData != null
                    ? (int?)_manager.CurrentData.Id
                    : null);
            _presentationIssueLogger.Watch(_presentation.StageView);
            _presentationIssueLogger.Watch(_presentation.AudioPlayer);
            _presentation.AudioPlayer.BindSettings(_playbackController != null ? _playbackController.Settings : null);

            _progress = new DialogueProgressService(unlockProgressMarkers);
            _endingFlow = new EndingFlowService(
                () => _progress != null && _progress.FlushUnlocks(),
                endingKey => _progress != null && _progress.IsUnlocked("ending:" + endingKey),
                ResetForTitleTransition,
                ReturnToTitle);
            _endingResultScreen = new EndingResultScreenController(
                () => _endingFlow.ConfirmAndReturnToTitle());
            _endingFlow.ResultReady += HandleEndingResultReady;
            _endingFlow.TransitionFailed += HandleEndingTransitionFailed;
            _endingFlow.AttachTo(_manager);
            // EndingFlow must observe the pre-unlock registry state so its NEW badge
            // reflects durable first reach, not only this dialogue session.
            _progress.AttachTo(_manager);

            if (enableDialogueKeyboardInput)
            {
                DialogueRuntimeFactory.EnsureKeyboardInputRouting(_view, backlogView, playbackController);
                _dialogueKeyboardInput = _view.GetComponent<DialogueKeyboardInput>();
            }

            var autoAdvanceGate = new DialogueAutoAdvanceGate(_view);
            _saveService = new NovelSaveService(_manager, saveSystem, defaultManualSaveSlot, saveThumbnails);
            _quickLoadAvailable = _saveService.HasSave(DialogueSaveSystem.QuickSaveSlot);
            _backlog = new BacklogController(backlogView, autoAdvanceGate, StopPlaybackAutomation);
            var collectionCatalogAsset = Resources.Load<TextAsset>(collectionCatalogResourcePath);
            var collectionCatalogResult = CollectionCatalogLoader.Load(collectionCatalogAsset);
            for (var index = 0; index < collectionCatalogResult.Warnings.Count; index++)
                Debug.LogWarning(collectionCatalogResult.Warnings[index]);
            if (collectionCatalogAsset == null)
                Debug.LogWarning("NovelGameBootstrap: collection catalog was not found at Resources/" + collectionCatalogResourcePath + ".");
            var collectionService = new CollectionService(
                collectionCatalogResult.Catalog,
                category => _progress != null ? _progress.ListUnlockedIds(category) : new List<string>(),
                Debug.LogWarning);
            _collectionScreen = new CollectionScreenController(collectionService);
            _configScreen = new ConfigScreenController(_playbackController.Settings, _settingsStore);
            _quitConfirmation = new QuitConfirmationController(
                new ApplicationQuitService(new UnityApplicationQuitter()));
            _titleMenu = new TitleMenuController(
                _saveService,
                StartNewGame,
                OpenLoadScreen,
                OpenEndingList,
                OpenGallery,
                OpenConfig,
                OpenQuitConfirmation);
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
            _collectionScreen.VisibilityChanged += HandleCollectionVisibilityChanged;
            _configScreen.VisibilityChanged += HandleTitleSubScreenVisibilityChanged;
            _quitConfirmation.VisibilityChanged += HandleTitleSubScreenVisibilityChanged;

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

        private void HandlePlaybackStateChanged(DialoguePlaybackState state)
        {
            if (state.HasChoices && state.Mode != DialoguePlaybackMode.Normal)
            {
                _backSkip?.Stop();
                _playbackController?.SetMode(DialoguePlaybackMode.Normal);
                return;
            }

            _commandBar?.Refresh();
        }

        private void HandleSaveLoadVisibilityChanged(bool visible)
        {
            if (visible)
                StopPlaybackAutomation();
            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = !visible;
            _commandBar?.SetInputBlocked(visible);
        }

        private void HandleCollectionVisibilityChanged(bool visible)
        {
            if (visible)
            {
                _titleMenu?.Hide();
                _saveLoadScreen?.Close();
                _saveLoadScreen?.SetTitleMenuVisible(true);
            }
            else if (ShouldShowTitleMenu())
            {
                _titleMenu?.Show();
            }

            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = !visible;
        }

        private void HandleTitleSubScreenVisibilityChanged(bool visible)
        {
            if (visible)
            {
                _titleMenu?.Hide();
                _saveLoadScreen?.SetTitleMenuVisible(true);
            }
            else if (ShouldShowTitleMenu())
            {
                _titleMenu?.Show();
            }
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

            _endingFlow?.NotifySceneLoaded();
            _endingResultScreen?.Hide();
            StopPlaybackAutomation();
            if (ShouldShowTitleMenu(scene.name))
                _titleMenu.Show();
            else
                _titleMenu.Hide();

            _commandBar?.SetSceneVisible(ShouldShowCommandBar(scene.name));
            _commandBar?.SetInputBlocked(false);
            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = true;
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
                BackSkip = _backSkip != null ? _backSkip.Toggle : null,
                ToggleBacklog = ToggleBacklog,
                ToggleAuto = playbackController != null
                    ? () =>
                    {
                        _backSkip?.Stop();
                        playbackController.ToggleAuto();
                    }
                    : null,
                ToggleSkip = playbackController != null
                    ? () =>
                    {
                        _backSkip?.Stop();
                        playbackController.ToggleSkip();
                    }
                    : null,
                CanSave = () => !IsEndingInputBlocked() && _saveService != null && _saveService.CanSaveNow && !_saveService.IsBusy,
                CanQuickLoad = () => !IsEndingInputBlocked() && _quickLoadAvailable,
                CanBackSkip = () => _backSkip != null && _backSkip.CanStart,
                HasDialogue = () => _manager != null && _manager.CurrentData != null,
                IsBacklogOpen = () => _backlog != null && _backlog.IsOpen,
                IsBackSkipActive = () => _backSkip != null && _backSkip.IsActive,
                IsAutoActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Auto,
                IsSkipActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Skip
            };

            return new NovelCommandBarController(NovelCommandCatalog.Create(bindings));
        }

        private void StopPlaybackAutomation()
        {
            _backSkip?.Stop();
            _playbackController?.SetMode(DialoguePlaybackMode.Normal);
            _commandBar?.Refresh();
        }

        private void HandleEndingResultReady(EndingResultInfo result)
        {
            StopPlaybackAutomation();
            _backlog?.Close();
            _saveLoadScreen?.Close();
            _endingResultScreen?.Show(result);
            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = false;
            _commandBar?.SetInputBlocked(true);
        }

        private void HandleEndingTransitionFailed(string message)
        {
            _notifications?.Show(message, false);
        }

        private void ResetForTitleTransition()
        {
            StopPlaybackAutomation();
            _backlog?.Close();
            _saveLoadScreen?.Close();
            _view?.ForceStop();
            _view?.Clear();
            if (_view != null)
                _view.gameObject.SetActive(false);

            _presentation?.StageView?.ClearCharacters();
            _presentation?.StageView?.SetBackground(string.Empty, true, string.Empty, 0f);
            _presentation?.AudioPlayer?.ResetPlayback();
            _endingResultScreen?.Hide();
            _commandBar?.SetInputBlocked(true);
        }

        private void ReturnToTitle()
        {
            if (string.IsNullOrEmpty(titleSceneName))
            {
                HandleEndingTransitionFailed("Title sceneが設定されていません。");
                _endingFlow?.NotifySceneLoaded();
                return;
            }

            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    titleSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _endingFlow?.NotifySceneLoaded();
                _titleMenu?.Show();
                return;
            }

            SceneManager.LoadScene(titleSceneName);
        }

        private bool IsEndingInputBlocked()
        {
            return (_endingFlow != null && _endingFlow.IsInputBlocked)
                || (_collectionScreen != null && _collectionScreen.IsOpen)
                || (_configScreen != null && _configScreen.IsOpen)
                || (_quitConfirmation != null && _quitConfirmation.IsOpen);
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
