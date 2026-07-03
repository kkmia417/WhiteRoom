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
    public sealed partial class NovelGameBootstrap : MonoBehaviour, IDialogueVariableResolver, IDialogueConditionEvaluator
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
    }
}
