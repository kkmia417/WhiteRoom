using System;
using System.Linq;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        /// <summary>
        /// Instance が確定（生成）または破棄されたときに発火する。Binder 系は起動順に依存せず、
        /// このイベントを購読することで Manager 生成後・差し替え後に自動接続できる。
        /// 引数は新しい Instance（破棄時は null）。
        /// </summary>
        public static event Action<DialogueManager> InstanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            InstanceChanged = null;
        }

        [Header("CSVファイル")]
        [Tooltip("会話データを含むCSVファイル (TextAsset)")]
        [SerializeField] private TextAsset csvFile;

        [Header("DialogueView (任意)")]
        [Tooltip("シーン内のViewが自動登録される場合は設定不要")]
        [SerializeField] private DialogueView view;

        [Header("Localization")]
        [SerializeField] private string languageKey = string.Empty;

        private DialoguePresenter _presenter;
        private IDialogueRepository _repository;
        private IDialogueConditionEvaluator _conditionEvaluator = new AllowAllDialogueConditionEvaluator();
        private IDialogueVariableResolver _variableResolver = new EmptyDialogueVariableResolver();
        private IDialogueTextResolver _textResolver = new DefaultDialogueTextResolver();
        private IDialogueEventDispatcher _eventDispatcher;
        private int _repositoryLoadGeneration;
        private bool _destroyed;

        public event Action<DialogueEventContext> LineStarted;
        public event Action<DialogueEventContext> LineCompleted;
        public event Action<DialogueEventContext> DialogueEnded;
        public event Action<DialogueEventContext> DialogueEventTriggered;
        public event Action<DialogueProgressEventContext> ProgressMarkerReached;
        public event Action<string> ErrorRaised;

        public IDialogueRepository Repository
        {
            get { return _repository; }
        }

        public DialogueSessionState State
        {
            get { return _presenter != null ? _presenter.State : DialogueSessionState.Idle; }
        }

        public DialogueData CurrentData
        {
            get { return _presenter != null ? _presenter.CurrentData : null; }
        }

        public DialogueProgressState Progress
        {
            get
            {
                return _presenter != null && _presenter.Session.Progress != null
                    ? _presenter.Session.Progress.Clone()
                    : new DialogueProgressState();
            }
        }

        public System.Collections.Generic.IReadOnlyList<DialogueHistoryEntry> History
        {
            get
            {
                return _presenter != null
                    ? _presenter.Session.History
                    : new System.Collections.Generic.List<DialogueHistoryEntry>();
            }
        }

        public int CurrentChoiceCount
        {
            get { return _presenter != null ? _presenter.CurrentChoiceCount : 0; }
        }

        public bool HasCurrentChoices
        {
            get { return CurrentChoiceCount > 0; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("DialogueManager: 重複インスタンスが存在したため削除します。");
                Destroy(gameObject);
                return;
            }

            _destroyed = false;
            Instance = this;
            // DontDestroyOnLoad は再生中のみ有効（エディタ/テストからの呼び出しは例外になる）。
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            _eventDispatcher = new DelegateDialogueEventDispatcher(context =>
            {
                if (DialogueEventTriggered != null)
                    DialogueEventTriggered(context);
            });

            if (csvFile != null)
                _repository = new DialogueRepository(csvFile);

            if (view != null)
                SetView(view);

            // Repository / View の初期化後に通知する。先に有効化済みの Binder が
            // ここで接続でき、後から有効化される Binder は OnEnable で直接接続する。
            RaiseInstanceChanged(this);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _repositoryLoadGeneration++;

            if (Instance == this)
            {
                Instance = null;
                RaiseInstanceChanged(null);
            }

            DisposePresenter();
        }

        private static void RaiseInstanceChanged(DialogueManager instance)
        {
            if (InstanceChanged != null)
                InstanceChanged(instance);
        }

        public void SetConditionEvaluator(IDialogueConditionEvaluator evaluator)
        {
            _conditionEvaluator = evaluator ?? new AllowAllDialogueConditionEvaluator();
            ApplyPresenterConfiguration();
        }

        public void SetVariableResolver(IDialogueVariableResolver resolver)
        {
            _variableResolver = resolver ?? new EmptyDialogueVariableResolver();
            ApplyPresenterConfiguration();
        }

        public void SetTextResolver(IDialogueTextResolver resolver)
        {
            _textResolver = resolver ?? new DefaultDialogueTextResolver();
            ApplyPresenterConfiguration();
        }

        public void SetLanguage(string newLanguageKey)
        {
            languageKey = newLanguageKey ?? string.Empty;
            ApplyPresenterConfiguration();
        }

        public void SetEventDispatcher(IDialogueEventDispatcher dispatcher)
        {
            _eventDispatcher = dispatcher;
            ApplyPresenterConfiguration();
        }

        public void SetView(DialogueView newView)
        {
            newView = newView ?? throw new ArgumentNullException(nameof(newView));

            if (view == newView && _presenter != null)
                return;

            view = newView;

            if (_repository == null)
            {
                view.Clear();
                view.gameObject.SetActive(false);
                return;
            }

            if (_presenter == null)
            {
                // Presenter 未生成時のみ新規作成する。
                CreatePresenter(view);
                view.Clear();
                view.gameObject.SetActive(false);
            }
            else
            {
                // View 差し替えはセッションを維持したまま再バインドする。
                // 進行中なら現在行を新 View に再描画し、そうでなければ初期表示へ戻す。
                _presenter.BindView(view);
                if (_presenter.CurrentData != null)
                {
                    view.gameObject.SetActive(true);
                    _presenter.RedrawCurrent();
                }
                else
                {
                    view.Clear();
                    view.gameObject.SetActive(false);
                }
            }

            Debug.Log("[DialogueManager] View がセットされました。");
        }

        private void CreatePresenter(DialogueView targetView)
        {
            DisposePresenter();
            _presenter = new DialoguePresenter(_repository, targetView);
            _presenter.LineStarted += RaiseLineStarted;
            _presenter.LineCompleted += RaiseLineCompleted;
            _presenter.DialogueEnded += RaiseDialogueEnded;
            _presenter.ProgressMarkerReached += RaiseProgressMarkerReached;
            _presenter.ErrorRaised += RaiseError;
            ApplyPresenterConfiguration();
        }

        /// <summary>
        /// 非同期に Repository を読み込み、成功した最新世代のロードだけを反映する。
        /// 進行中の会話がある間の Repository 差し替えは、セッション整合性を保つため拒否する。
        /// </summary>
        public void LoadRepository(IDialogueRepositoryLoader loader)
        {
            if (loader == null)
            {
                Debug.LogError("DialogueManager: loader is null.");
                return;
            }

            if (HasActiveDialogue())
            {
                const string message = "Cannot load repository while dialogue is active.";
                Debug.LogError("DialogueManager: " + message);
                RaiseError(message);
                return;
            }

            var generation = ++_repositoryLoadGeneration;
            StartCoroutine(loader.Load(repository =>
            {
                if (!IsCurrentRepositoryLoad(generation))
                    return;

                if (repository == null)
                {
                    RaiseRepositoryLoadError(generation, "Dialogue repository loader completed without a repository.");
                    return;
                }

                _repository = repository;
                if (view != null)
                {
                    // Repository が差し替わったら新 Repository 用に Presenter を作り直す。
                    CreatePresenter(view);
                    view.Clear();
                    view.gameObject.SetActive(false);
                }
            }, error =>
            {
                RaiseRepositoryLoadError(generation, error);
            }));
        }

        public void StartDialogue(int id)
        {
            if (!EnsureReady()) return;

            view.ForceStop();
            view.Clear();
            view.gameObject.SetActive(true);

            _presenter.Start(id);
        }

        public void StartDialogue(Func<DialogueData, bool> predicate)
        {
            if (!EnsureReady()) return;

            var data = _repository.GetAll().FirstOrDefault(predicate);
            if (data != null)
                StartDialogue(data.Id);
            else
                Debug.LogWarning("DialogueManager: 該当する会話データが見つかりません。");
        }

        public void StartDialogueForState(string triggerKey)
        {
            if (!EnsureReady()) return;

            if (string.IsNullOrEmpty(triggerKey))
            {
                Debug.LogWarning("DialogueManager: triggerKey が null または空です。");
                return;
            }

            var data = _repository.GetByTriggerKey(triggerKey);
            if (data != null)
                StartDialogue(data.Id, triggerKey);
            else
                Debug.LogWarning($"DialogueManager: TriggerKey \"{triggerKey}\" に該当するデータがありません。");
        }

        public void EndDialogue()
        {
            if (!EnsureReady()) return;

            _presenter.End();
            view.gameObject.SetActive(false);
        }

        /// <summary>タイプライターの 1 文字あたり間隔（秒）を設定する。コンフィグの文字速度から呼ぶ。</summary>
        public void SetTypewriterSpeed(float interval)
        {
            if (view != null)
                view.SetTypewriterSpeed(interval);
        }

        /// <summary>オート/スキップ進行のための自動送りオーバーライドを設定する。</summary>
        public void SetAutoAdvanceOverride(bool active, float seconds)
        {
            if (view != null)
                view.SetAutoAdvanceOverride(active, seconds);
        }

        /// <summary>現在行の送り（または文字送り完了）を要求する。オート/スキップ進行から呼ぶ。</summary>
        public void RequestNext()
        {
            if (view != null)
                view.RequestNext();
        }

        /// <summary>直前の行へ巻き戻せるか。</summary>
        public bool CanRollback
        {
            get { return _presenter != null && _presenter.CanRollback; }
        }

        /// <summary>直前の行へ巻き戻して再開する。戻れない場合は false。</summary>
        public bool Rollback()
        {
            return _presenter != null && _presenter.Rollback();
        }

        public DialogueSaveData CaptureState()
        {
            return _presenter != null ? _presenter.CaptureState() : new DialogueSaveData();
        }

        public bool RestoreState(DialogueSaveData saveData)
        {
            if (!EnsureReady()) return false;
            view.gameObject.SetActive(true);
            return _presenter.RestoreState(saveData);
        }

        private void StartDialogue(int id, string triggerKey)
        {
            if (!EnsureReady()) return;

            view.ForceStop();
            view.Clear();
            view.gameObject.SetActive(true);

            _presenter.Start(id, triggerKey);
        }

        private void ApplyPresenterConfiguration()
        {
            if (_presenter == null) return;
            _presenter.SetConditionEvaluator(_conditionEvaluator);
            _presenter.SetVariableResolver(_variableResolver);
            _presenter.SetTextResolver(_textResolver);
            _presenter.SetLanguage(languageKey);
            _presenter.SetEventDispatcher(_eventDispatcher);
        }

        private void DisposePresenter()
        {
            if (_presenter == null) return;
            _presenter.LineStarted -= RaiseLineStarted;
            _presenter.LineCompleted -= RaiseLineCompleted;
            _presenter.DialogueEnded -= RaiseDialogueEnded;
            _presenter.ProgressMarkerReached -= RaiseProgressMarkerReached;
            _presenter.ErrorRaised -= RaiseError;
            _presenter.Dispose();
            _presenter = null;
        }

        private bool EnsureReady()
        {
            if (_repository == null)
            {
                Debug.LogError("DialogueManager: Repository が初期化されていません。");
                return false;
            }

            if (view == null)
            {
                Debug.LogError("DialogueManager: DialogueView がセットされていません。");
                return false;
            }

            if (_presenter == null)
            {
                Debug.LogError("DialogueManager: Presenter が初期化されていません。");
                return false;
            }

            return true;
        }

        private bool HasActiveDialogue()
        {
            return _presenter != null
                   && _presenter.CurrentData != null
                   && _presenter.State != DialogueSessionState.Ended;
        }

        private bool IsCurrentRepositoryLoad(int generation)
        {
            return !_destroyed && generation == _repositoryLoadGeneration;
        }

        private void RaiseRepositoryLoadError(int generation, string error)
        {
            if (!IsCurrentRepositoryLoad(generation))
                return;

            var message = string.IsNullOrEmpty(error)
                ? "Dialogue repository loading failed."
                : error;
            Debug.LogError("DialogueManager: " + message);
            RaiseError(message);
        }

        private void RaiseLineStarted(DialogueEventContext context)
        {
            if (LineStarted != null) LineStarted(context);
        }

        private void RaiseLineCompleted(DialogueEventContext context)
        {
            if (LineCompleted != null) LineCompleted(context);
        }

        private void RaiseDialogueEnded(DialogueEventContext context)
        {
            // 自然終了・明示終了のどちらでも、外部購読者へ通知した後で View を非表示にする。
            // 終了時の View 状態を参照する購読者のために、通知 → 非表示の順序を固定する。
            if (DialogueEnded != null) DialogueEnded(context);
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private void RaiseProgressMarkerReached(DialogueProgressEventContext context)
        {
            if (ProgressMarkerReached != null) ProgressMarkerReached(context);
        }

        private void RaiseError(string message)
        {
            if (ErrorRaised != null) ErrorRaised(message);
        }
    }
}
