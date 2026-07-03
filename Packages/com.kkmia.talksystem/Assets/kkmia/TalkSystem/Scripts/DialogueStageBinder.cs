using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="DialogueStageView"/> を <see cref="DialogueManager"/> のイベントに接続し、
    /// 各行の背景・立ち絵指示をステージへ反映する補助コンポーネント。
    /// <see cref="DialogueViewBinder"/> と同様、シーンに置くだけで自動配線される。
    /// </summary>
    [RequireComponent(typeof(DialogueStageView))]
    public class DialogueStageBinder : MonoBehaviour, IDialogueSaveContributor
    {
        private const string StageKey = "stage";

        [Tooltip("会話終了時にステージ（背景・立ち絵）も消去するか。")]
        [SerializeField] private bool clearStageOnDialogueEnd = true;

        private DialogueStageDirector _director;
        private DialogueManager _boundManager;

        private void Awake()
        {
            var view = GetComponent<DialogueStageView>();
            _director = new DialogueStageDirector(view);
        }

        private void OnEnable()
        {
            Bind(DialogueManager.Instance);
        }

        private void Start()
        {
            // Instance が Awake 順で遅れて生成される場合に備え、Start でも接続を試みる。
            if (_boundManager == null)
                Bind(DialogueManager.Instance);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind(DialogueManager manager)
        {
            if (manager == null || _boundManager == manager) return;

            Unbind();
            _boundManager = manager;
            _boundManager.LineStarted += HandleLineStarted;
            _boundManager.DialogueEnded += HandleDialogueEnded;
        }

        private void Unbind()
        {
            if (_boundManager == null) return;
            _boundManager.LineStarted -= HandleLineStarted;
            _boundManager.DialogueEnded -= HandleDialogueEnded;
            _boundManager = null;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (context != null)
                _director.Apply(context.Data);
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            if (clearStageOnDialogueEnd)
                _director.Clear();
        }

        // --- IDialogueSaveContributor（セーブの完全復元）---

        void IDialogueSaveContributor.Capture(DialogueSaveData data)
        {
            if (data == null || _director == null) return;
            data.SetExtra(StageKey, JsonUtility.ToJson(_director.CaptureSnapshot()));
        }

        void IDialogueSaveContributor.Restore(DialogueSaveData data)
        {
            if (data == null || _director == null) return;

            string json;
            if (!data.TryGetExtra(StageKey, out json) || string.IsNullOrEmpty(json))
                return;

            var snapshot = JsonUtility.FromJson<DialogueStageSnapshot>(json);
            _director.RestoreSnapshot(snapshot);
        }
    }
}
