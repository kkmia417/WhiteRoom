using System;
using System.Collections;
using System.Collections.Generic;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        [SerializeField] private bool saveThumbnails = true;
        [SerializeField] private Key screenshotShortcut = Key.F12;
        [SerializeField] private string saveContentVersion = "r00_chapters_01_14_v4";
        [SerializeField] private string saveProductChannel = string.Empty;

        private DialogueManager _manager;
        private DialogueView _view;
        private DialoguePresentation _presentation;
        private NovelDialogueMotionController _dialogueMotion;
        private DialogueSaveSystem _saveSystem;
        private NovelSaveService _saveService;
        private AutosaveCheckpointService _autosaveCheckpoints;
        private DialogueProgressService _progress;
        private EndingFlowService _endingFlow;
        private EndingResultScreenController _endingResultScreen;
        private CollectionScreenController _collectionScreen;
        private FavoriteVoiceService _favoriteVoices;
        private FavoriteVoiceScreenController _favoriteVoiceScreen;
        private ConfigScreenController _configScreen;
        private QuitConfirmationController _quitConfirmation;
        private TitleReturnService _titleReturnService;
        private TitleReturnConfirmationController _titleReturnConfirmation;
        private MessageWindowVisibilityController _messageVisibility;
        private GameplayOverlayCoordinator _gameplayOverlay;
        private ScreenshotCaptureService _screenshotService;
        private ScreenshotCaptureUiController _screenshotUi;
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
        private DialogueBoundaryNavigationService _boundaryNavigation;
        private AudioDatabase _resolvedAudioDatabase;
        private bool _quickLoadAvailable;
        private DialoguePlaybackMode? _thumbnailPlaybackMode;
        private bool _thumbnailKeyboardWasEnabled;
        private bool _startNewGameWhenMainSceneLoads;
        private Coroutine _gameplayInputRestore;

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

            if (_saveSystem != null)
            {
                _saveSystem.ThumbnailCaptureStarted -= HandleThumbnailCaptureStarted;
                _saveSystem.ThumbnailCaptureCompleted -= HandleThumbnailCaptureCompleted;
            }
            if (_screenshotService != null)
            {
                _screenshotService.CaptureStarted -= HandleScreenshotCaptureStarted;
                _screenshotService.CaptureCompleted -= HandleScreenshotCaptureCompleted;
            }

            _saveService?.Dispose();
            _autosaveCheckpoints?.Dispose();
            _boundaryNavigation?.Dispose();
            _endingFlow?.Dispose();
            _progress?.Dispose();
            if (_collectionScreen != null)
                _collectionScreen.VisibilityChanged -= HandleCollectionVisibilityChanged;
            if (_favoriteVoiceScreen != null)
                _favoriteVoiceScreen.VisibilityChanged -= HandleFavoriteVoiceVisibilityChanged;
            if (_configScreen != null)
                _configScreen.VisibilityChanged -= HandleConfigVisibilityChanged;
            if (_quitConfirmation != null)
                _quitConfirmation.VisibilityChanged -= HandleTitleSubScreenVisibilityChanged;
            if (_titleReturnConfirmation != null)
                _titleReturnConfirmation.VisibilityChanged -= HandleTitleReturnVisibilityChanged;
            if (_messageVisibility != null)
                _messageVisibility.HiddenChanged -= HandleMessageHiddenChanged;
            if (_manager != null)
                _manager.LineStarted -= HandleLineStartedForTitleReturn;
            _presentationIssueLogger?.Dispose();
            _commandBar?.Dispose();
            _saveLoadScreen?.Dispose();
            _messageVisibility?.Dispose();
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
            _autosaveCheckpoints?.TryFlush();

            if (IsScreenshotShortcutPressed())
                RequestScreenshot();

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
            _boundaryNavigation?.Reset();

            if (string.IsNullOrEmpty(mainSceneName)
                || string.Equals(SceneManager.GetActiveScene().name, mainSceneName, StringComparison.OrdinalIgnoreCase))
            {
                StartDialogueForTrigger(startTriggerKey);
                return;
            }

            _startNewGameWhenMainSceneLoads = true;
            LoadMainScene();
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
            if (IsEndingInputBlocked() || (_configScreen != null && _configScreen.IsOpen))
                return;

            _backlog?.Close();
            _saveLoadScreen?.Close();
            _collectionScreen?.Close();
            _quitConfirmation?.Cancel();
            _configScreen?.Open();
        }

        public void HideMessageWindow()
        {
            if (!IsEndingInputBlocked())
                _messageVisibility?.Hide();
        }

        public bool RequestScreenshot()
        {
            if (_screenshotService == null || IsScreenshotInteractionBlocked())
                return false;

            IEnumerator captureRoutine;
            var started = _screenshotService.TryBegin(out captureRoutine);
            _commandBar?.Refresh();
            if (!started || captureRoutine == null)
                return false;

            StartCoroutine(captureRoutine);
            return true;
        }

        public bool RequestReturnToTitle()
        {
            return !IsEndingInputBlocked() &&
                   _titleReturnConfirmation != null &&
                   _titleReturnConfirmation.Request();
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
            _saveSystem = saveSystem;
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
            _resolvedAudioDatabase = resolvedAudioDatabase;

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
            _dialogueMotion = DialogueMotionFactory.Ensure(
                _manager,
                _view,
                _presentation.StageView);
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
            _autosaveCheckpoints = new AutosaveCheckpointService(
                title => _saveService != null && _saveService.Autosave(title),
                () => _playbackController != null ? _playbackController.Mode : DialoguePlaybackMode.Normal,
                mode => _playbackController?.SetMode(mode),
                () => _saveSystem != null && _saveSystem.IsThumbnailCaptureInProgress);
            // Attach after progress so ending unlock persistence completes before
            // the final-line autosave is committed.
            _autosaveCheckpoints.AttachTo(_manager);
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
            _screenshotService = new ScreenshotCaptureService(new FileScreenshotStorage());
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

            _titleReturnService = new TitleReturnService(ResetForTitleTransition, ReturnToTitle);
            _titleReturnConfirmation = new TitleReturnConfirmationController(_titleReturnService);
            _messageVisibility = new MessageWindowVisibilityController(_view, _commandBar, ShouldShowCommandBar);
            _screenshotUi = new ScreenshotCaptureUiController(
                _commandBar,
                _notifications,
                ShouldShowCommandBar,
                () => _messageVisibility != null && _messageVisibility.IsHidden);
            _gameplayOverlay = new GameplayOverlayCoordinator(
                () => _playbackController != null ? _playbackController.Mode : DialoguePlaybackMode.Normal,
                mode => _playbackController?.SetMode(mode),
                () => _backSkip?.Stop(),
                SetGameplayInputEnabled);

            saveSystem.ThumbnailCaptureStarted += HandleThumbnailCaptureStarted;
            saveSystem.ThumbnailCaptureCompleted += HandleThumbnailCaptureCompleted;
            _screenshotService.CaptureStarted += HandleScreenshotCaptureStarted;
            _screenshotService.CaptureCompleted += HandleScreenshotCaptureCompleted;

            _saveService.Saved += HandleSaveCompleted;
            _saveService.Loaded += HandleLoadCompleted;
            _saveService.Feedback += HandleSaveFeedback;
            _titleMenu.VisibilityChanged += _saveLoadScreen.SetTitleMenuVisible;
            _saveLoadScreen.VisibilityChanged += HandleSaveLoadVisibilityChanged;
            _collectionScreen.VisibilityChanged += HandleCollectionVisibilityChanged;
            _configScreen.VisibilityChanged += HandleConfigVisibilityChanged;
            _quitConfirmation.VisibilityChanged += HandleTitleSubScreenVisibilityChanged;
            _titleReturnConfirmation.VisibilityChanged += HandleTitleReturnVisibilityChanged;
            _messageVisibility.HiddenChanged += HandleMessageHiddenChanged;
            _manager.LineStarted += HandleLineStartedForTitleReturn;

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

            _boundaryNavigation?.Dispose();
            _boundaryNavigation = new DialogueBoundaryNavigationService(
                _manager,
                _saveSystem,
                Debug.LogWarning);
            _saveSystem.RegisterContributor(_boundaryNavigation);
            _boundaryNavigation.Attach();
            _favoriteVoices = new FavoriteVoiceService(
                () => _manager != null ? _manager.CurrentData : null,
                id => _manager != null && _manager.Repository != null ? _manager.Repository.Get(id) : null,
                key =>
                {
                    AudioClip clip;
                    return _resolvedAudioDatabase != null && _resolvedAudioDatabase.TryGetVoice(key, out clip);
                },
                _presentation != null ? _presentation.AudioPlayer : null,
                null,
                Debug.LogWarning);
            _favoriteVoiceScreen = new FavoriteVoiceScreenController(
                _favoriteVoices,
                HandleFavoriteVoiceFeedback);
            _favoriteVoiceScreen.VisibilityChanged += HandleFavoriteVoiceVisibilityChanged;
            _commandBar?.Refresh();

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
            _titleReturnService?.MarkProgressSaved();
            _quickLoadAvailable = _saveService != null &&
                                  _saveService.HasSave(DialogueSaveSystem.QuickSaveSlot);
            _titleMenu.RefreshButtons();
            _saveLoadScreen.Refresh();
            _commandBar?.Refresh();
        }

        private void HandleLoadCompleted()
        {
            _titleReturnService?.MarkProgressSaved();
            StopPlaybackAutomation();
            _dialogueMotion?.ResetTransientState();
            _titleMenu.Hide();
            _saveLoadScreen.Close();
            _favoriteVoiceScreen?.Close();
        }

        private void HandleSaveFeedback(NovelSaveFeedback feedback)
        {
            if (feedback != null)
                _notifications?.Show(feedback.Message, feedback.Succeeded);
        }

        private void HandleThumbnailCaptureStarted(int slot)
        {
            _saveLoadScreen?.SetCaptureHidden(true);
            _notifications?.SetCaptureHidden(true);

            _thumbnailPlaybackMode = null;
            if (_playbackController != null && _playbackController.Mode != DialoguePlaybackMode.Normal)
            {
                _thumbnailPlaybackMode = _playbackController.Mode;
                _playbackController.SetMode(DialoguePlaybackMode.Normal);
            }

            if (_dialogueKeyboardInput != null)
            {
                _thumbnailKeyboardWasEnabled = _dialogueKeyboardInput.enabled;
                _dialogueKeyboardInput.enabled = false;
            }
            _commandBar?.SetInputBlocked(true);
        }

        private void HandleThumbnailCaptureCompleted(int slot, bool succeeded, string message)
        {
            _saveLoadScreen?.SetCaptureHidden(false);
            _notifications?.SetCaptureHidden(false);
            _autosaveCheckpoints?.NotifyThumbnailCaptureCompleted();

            if (_thumbnailPlaybackMode.HasValue && _playbackController != null)
                _playbackController.SetMode(_thumbnailPlaybackMode.Value);
            _thumbnailPlaybackMode = null;

            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = _thumbnailKeyboardWasEnabled
                                                 && !IsEndingInputBlocked()
                                                 && (_saveLoadScreen == null || !_saveLoadScreen.IsOpen);
            _commandBar?.SetInputBlocked(IsEndingInputBlocked() || (_saveLoadScreen != null && _saveLoadScreen.IsOpen));
        }

        private void HandleScreenshotCaptureStarted()
        {
            _screenshotUi?.HideForCapture();
            _commandBar?.Refresh();
        }

        private void HandleScreenshotCaptureCompleted(ScreenshotCaptureResult result)
        {
            _screenshotUi?.RestoreAfterCapture();
            _commandBar?.Refresh();
            if (result != null)
                _notifications?.Show(result.Message, result.Succeeded);
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

        private void HandleFavoriteVoiceVisibilityChanged(bool visible)
        {
            if (visible)
            {
                _backlog?.Close();
                _saveLoadScreen?.Close();
                _collectionScreen?.Close();
                _configScreen?.Close();
                _gameplayOverlay?.Suspend();
            }
            else
            {
                ResumeGameplayAfterSystemOverlay();
            }
        }

        private void HandleFavoriteVoiceFeedback(FavoriteVoiceResult result)
        {
            if (result == null)
                return;
            _notifications?.Show(result.Message, result.Succeeded);
            _commandBar?.Refresh();
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

        private void HandleConfigVisibilityChanged(bool visible)
        {
            HandleTitleSubScreenVisibilityChanged(visible);
            if (visible)
                _gameplayOverlay?.Suspend();
            else
                ResumeGameplayAfterSystemOverlay();
        }

        private void HandleTitleReturnVisibilityChanged(bool visible)
        {
            if (visible)
                _gameplayOverlay?.Suspend();
            else if (_titleReturnService == null || !_titleReturnService.IsTransitionInProgress)
                ResumeGameplayAfterSystemOverlay();
        }

        private void HandleMessageHiddenChanged(bool hidden)
        {
            if (hidden)
                _gameplayOverlay?.Suspend();
            else
                ResumeGameplayAfterSystemOverlay();
        }

        private void HandleLineStartedForTitleReturn(DialogueEventContext context)
        {
            _titleReturnService?.MarkProgressChanged();
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
            _gameplayOverlay?.ResetForTransition();
            _messageVisibility?.Reset();
            if (_configScreen != null && _configScreen.IsOpen)
                _configScreen.Close();
            _titleReturnConfirmation?.Reset();
            _titleReturnService?.NotifySceneLoaded();
            _endingResultScreen?.Hide();
            _favoriteVoiceScreen?.Close();
            StopPlaybackAutomation();
            if (ShouldShowTitleMenu(scene.name))
                _titleMenu.Show();
            else
                _titleMenu.Hide();

            _commandBar?.SetSceneVisible(ShouldShowCommandBar(scene.name));
            SetGameplayInputEnabled(ShouldShowCommandBar(scene.name));

            if (_startNewGameWhenMainSceneLoads
                && string.Equals(scene.name, mainSceneName, StringComparison.OrdinalIgnoreCase))
            {
                _startNewGameWhenMainSceneLoads = false;
                StartDialogueForTrigger(startTriggerKey);
            }
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
                OpenSystemConfig = OpenConfig,
                PreviousChoice = () => JumpBoundary(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Previous),
                PreviousScene = () => JumpBoundary(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Previous),
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
                NextScene = () => JumpBoundary(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Next),
                NextChoice = () => JumpBoundary(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Next),
                OpenFavoriteVoices = OpenFavoriteVoices,
                ReplayVoice = ReplayCurrentVoice,
                AddFavoriteVoice = AddCurrentFavoriteVoice,
                CaptureScreenshot = () => RequestScreenshot(),
                HideMessage = HideMessageWindow,
                ReturnTitle = () => RequestReturnToTitle(),
                CanSave = () => !IsEndingInputBlocked()
                                    && (_boundaryNavigation == null || !_boundaryNavigation.IsBusy)
                                    && _saveService != null
                                    && _saveService.CanSaveNow
                                    && !_saveService.IsBusy,
                CanQuickLoad = () => !IsEndingInputBlocked() && _quickLoadAvailable,
                CanBackSkip = () => _backSkip != null && _backSkip.CanStart,
                CanPreviousChoice = () => CanJumpBoundary(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Previous),
                CanPreviousScene = () => CanJumpBoundary(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Previous),
                CanNextScene = () => CanJumpBoundary(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Next),
                CanNextChoice = () => CanJumpBoundary(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Next),
                CanOpenFavoriteVoices = () => !IsEndingInputBlocked()
                                                && _favoriteVoices != null
                                                && _favoriteVoices.HasFavorites,
                CanReplayVoice = () => !IsEndingInputBlocked()
                                        && _favoriteVoices != null
                                        && _favoriteVoices.CanUseCurrentVoice,
                CanAddFavoriteVoice = () => !IsEndingInputBlocked()
                                             && _favoriteVoices != null
                                             && _favoriteVoices.CanUseCurrentVoice,
                CanOpenSystemConfig = () => !IsEndingInputBlocked(),
                CanCaptureScreenshot = () => _screenshotService != null
                                             && _screenshotService.IsAvailable
                                             && !_screenshotService.IsBusy
                                             && !IsScreenshotInteractionBlocked(),
                CanHideMessage = () => !IsEndingInputBlocked() && _manager != null && _manager.CurrentData != null,
                CanReturnTitle = () => !IsEndingInputBlocked(),
                ScreenshotUnavailableReason = _screenshotService != null
                    ? _screenshotService.UnavailableReason
                    : "Screenshot capture is unavailable",
                PreviousChoiceUnavailableReason = () => BoundaryUnavailableReason(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Previous),
                PreviousSceneUnavailableReason = () => BoundaryUnavailableReason(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Previous),
                NextSceneUnavailableReason = () => BoundaryUnavailableReason(
                    DialogueBoundaryKind.Scene,
                    DialogueBoundaryDirection.Next),
                NextChoiceUnavailableReason = () => BoundaryUnavailableReason(
                    DialogueBoundaryKind.Choice,
                    DialogueBoundaryDirection.Next),
                FavoriteVoiceListUnavailableReason = () => _favoriteVoices == null
                    ? "Favorite voices are not ready"
                    : _favoriteVoices.HasFavorites ? string.Empty : "No favorite voices",
                VoiceReplayUnavailableReason = CurrentVoiceUnavailableReason,
                FavoriteVoiceAddUnavailableReason = CurrentVoiceUnavailableReason,
                HasDialogue = () => _manager != null && _manager.CurrentData != null,
                IsBacklogOpen = () => _backlog != null && _backlog.IsOpen,
                IsBackSkipActive = () => _backSkip != null && _backSkip.IsActive,
                IsAutoActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Auto,
                IsSkipActive = () => playbackController != null && playbackController.Mode == DialoguePlaybackMode.Skip
            };

            return new NovelCommandBarController(NovelCommandCatalog.Create(bindings));
        }

        private void OpenFavoriteVoices()
        {
            if (!IsEndingInputBlocked() && _favoriteVoices != null && _favoriteVoices.HasFavorites)
                _favoriteVoiceScreen?.Open();
        }

        private void ReplayCurrentVoice()
        {
            if (_favoriteVoices == null)
                return;
            HandleFavoriteVoiceFeedback(_favoriteVoices.ReplayCurrent());
        }

        private void AddCurrentFavoriteVoice()
        {
            if (_favoriteVoices == null)
                return;
            HandleFavoriteVoiceFeedback(_favoriteVoices.AddCurrent());
        }

        private string CurrentVoiceUnavailableReason()
        {
            if (IsEndingInputBlocked())
                return "Dialogue input is blocked";
            return _favoriteVoices != null && _favoriteVoices.CanUseCurrentVoice
                ? string.Empty
                : "Current voice is unavailable";
        }

        private bool CanJumpBoundary(DialogueBoundaryKind kind, DialogueBoundaryDirection direction)
        {
            return !IsEndingInputBlocked()
                   && _boundaryNavigation != null
                   && _boundaryNavigation.CanJump(kind, direction);
        }

        private string BoundaryUnavailableReason(
            DialogueBoundaryKind kind,
            DialogueBoundaryDirection direction)
        {
            if (IsEndingInputBlocked())
                return "Dialogue input is blocked";
            return _boundaryNavigation != null
                ? _boundaryNavigation.GetUnavailableReason(kind, direction)
                : "Dialogue navigation is not ready";
        }

        private void JumpBoundary(DialogueBoundaryKind kind, DialogueBoundaryDirection direction)
        {
            if (!CanJumpBoundary(kind, direction))
                return;

            StopPlaybackAutomation();
            _backlog?.Close();
            _saveLoadScreen?.Close();
            _collectionScreen?.Close();
            _configScreen?.Close();
            _favoriteVoiceScreen?.Close();
            SetGameplayInputEnabled(false);

            var result = _boundaryNavigation.Jump(kind, direction);
            _notifications?.Show(result.Message, result.Succeeded);
            SetGameplayInputEnabled(true);
            _commandBar?.Refresh();
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
            _gameplayOverlay?.ResetForTransition();
            _backlog?.Close();
            _saveLoadScreen?.Close();
            _collectionScreen?.Close();
            if (_configScreen != null && _configScreen.IsOpen)
                _configScreen.Close();
            _quitConfirmation?.Cancel();
            _titleReturnConfirmation?.Reset();
            _messageVisibility?.Reset();
            _view?.ForceStop();
            _view?.Clear();
            if (_view != null)
                _view.gameObject.SetActive(false);

            _presentation?.StageView?.ClearCharacters();
            _presentation?.StageView?.SetBackground(string.Empty, true, string.Empty, 0f);
            _presentation?.AudioPlayer?.ResetPlayback();
            _endingResultScreen?.Hide();
            _commandBar?.SetInputBlocked(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void ReturnToTitle()
        {
            if (string.IsNullOrEmpty(titleSceneName))
            {
                HandleEndingTransitionFailed("Title sceneが設定されていません。");
                _endingFlow?.NotifySceneLoaded();
                _titleReturnService?.NotifySceneLoaded();
                return;
            }

            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    titleSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _endingFlow?.NotifySceneLoaded();
                _titleReturnService?.NotifySceneLoaded();
                _titleMenu?.Show();
                return;
            }

            SceneManager.LoadScene(titleSceneName);
        }

        private bool IsEndingInputBlocked()
        {
            return (_endingFlow != null && _endingFlow.IsInputBlocked)
                || (_titleReturnService != null && _titleReturnService.IsTransitionInProgress)
                || (_titleReturnConfirmation != null && _titleReturnConfirmation.IsOpen)
                || (_messageVisibility != null && _messageVisibility.IsHidden)
                || (_collectionScreen != null && _collectionScreen.IsOpen)
                || (_favoriteVoiceScreen != null && _favoriteVoiceScreen.IsOpen)
                || (_configScreen != null && _configScreen.IsOpen)
                || (_quitConfirmation != null && _quitConfirmation.IsOpen);
        }

        private bool IsScreenshotShortcutPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || screenshotShortcut == Key.None)
                return false;
            var key = keyboard[screenshotShortcut];
            return key != null && key.wasPressedThisFrame;
        }

        private bool IsScreenshotInteractionBlocked()
        {
            return !ShouldShowCommandBar()
                || (_endingFlow != null && _endingFlow.IsInputBlocked)
                || (_titleReturnService != null && _titleReturnService.IsTransitionInProgress)
                || (_titleReturnConfirmation != null && _titleReturnConfirmation.IsOpen)
                || (_collectionScreen != null && _collectionScreen.IsOpen)
                || (_favoriteVoiceScreen != null && _favoriteVoiceScreen.IsOpen)
                || (_configScreen != null && _configScreen.IsOpen)
                || (_quitConfirmation != null && _quitConfirmation.IsOpen)
                || (_saveLoadScreen != null && _saveLoadScreen.IsOpen)
                || (_backlog != null && _backlog.IsOpen)
                || (_saveSystem != null && _saveSystem.IsThumbnailCaptureInProgress);
        }

        private void ResumeGameplayAfterSystemOverlay()
        {
            if ((_configScreen != null && _configScreen.IsOpen)
                || (_favoriteVoiceScreen != null && _favoriteVoiceScreen.IsOpen)
                || (_titleReturnConfirmation != null && _titleReturnConfirmation.IsOpen)
                || (_messageVisibility != null && _messageVisibility.IsHidden))
                return;

            _gameplayOverlay?.Resume();
        }

        private void SetGameplayInputEnabled(bool enabled)
        {
            if (_gameplayInputRestore != null)
            {
                StopCoroutine(_gameplayInputRestore);
                _gameplayInputRestore = null;
            }

            if (!enabled)
            {
                if (_dialogueKeyboardInput != null)
                    _dialogueKeyboardInput.enabled = false;
                _commandBar?.SetInputBlocked(true);
                return;
            }

            _gameplayInputRestore = StartCoroutine(EnableGameplayInputNextFrame());
        }

        private IEnumerator EnableGameplayInputNextFrame()
        {
            // Consume the key/click that closed an overlay before dialogue input is
            // restored, preventing the same action from also advancing the line.
            yield return null;
            _gameplayInputRestore = null;
            var canEnable = ShouldShowCommandBar()
                            && !IsEndingInputBlocked()
                            && (_saveLoadScreen == null || !_saveLoadScreen.IsOpen)
                            && (_backlog == null || !_backlog.IsOpen);
            if (_dialogueKeyboardInput != null)
                _dialogueKeyboardInput.enabled = canEnable;
            _commandBar?.SetInputBlocked(!canEnable);
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
