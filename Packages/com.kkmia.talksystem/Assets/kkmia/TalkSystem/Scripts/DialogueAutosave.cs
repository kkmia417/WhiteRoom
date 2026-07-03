using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 会話進行に合わせて <see cref="DialogueSaveSystem.AutosaveSlot"/> へ自動保存する。
    /// 行開始ごとに保存し、最短間隔でスロットリングする。<see cref="DialogueViewBinder"/> 等と同様、置くだけで動く。
    /// </summary>
    [RequireComponent(typeof(DialogueSaveSystem))]
    public class DialogueAutosave : MonoBehaviour
    {
        [Tooltip("未設定なら DialogueManager.Instance を使う。")]
        [SerializeField] private DialogueManager manager;

        [Tooltip("オートセーブの最短間隔（秒）。")]
        [SerializeField] private float minInterval = 5f;

        [Tooltip("サムネイル付きで保存するか。")]
        [SerializeField] private bool captureThumbnail = true;

        private DialogueSaveSystem _saveSystem;
        private DialogueManager _bound;
        private float _lastSaveTime = -9999f;

        private void Awake()
        {
            _saveSystem = GetComponent<DialogueSaveSystem>();
        }

        private void OnEnable()
        {
            Bind(manager != null ? manager : DialogueManager.Instance);
        }

        private void Start()
        {
            if (_bound == null)
                Bind(manager != null ? manager : DialogueManager.Instance);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind(DialogueManager target)
        {
            if (target == null || _bound == target) return;

            Unbind();
            _bound = target;
            _bound.LineStarted += HandleLineStarted;
        }

        private void Unbind()
        {
            if (_bound == null) return;
            _bound.LineStarted -= HandleLineStarted;
            _bound = null;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            if (_saveSystem == null) return;
            if (Time.unscaledTime - _lastSaveTime < minInterval) return;

            _lastSaveTime = Time.unscaledTime;

            if (captureThumbnail)
                _saveSystem.SaveWithThumbnail(DialogueSaveSystem.AutosaveSlot, isAutosave: true);
            else
                _saveSystem.Save(DialogueSaveSystem.AutosaveSlot, isAutosave: true);
        }
    }
}
