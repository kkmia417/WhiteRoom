using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="DialogueLipSync"/> の口の開き具合を、現在話している立ち絵モデルへ流し込む。
    /// 話者は <see cref="DialogueManager.LineStarted"/> の <see cref="DialogueData.Speaker"/> で判定し、
    /// <see cref="ModelDialogueCharacterBackend"/> からモデル（<see cref="IDialogueLipSyncTarget"/>）を引く。
    /// </summary>
    public class DialogueModelLipSyncBinder : MonoBehaviour
    {
        [SerializeField] private DialogueLipSync lipSync;
        [SerializeField] private ModelDialogueCharacterBackend backend;

        [Tooltip("未設定なら DialogueManager.Instance を使う。")]
        [SerializeField] private DialogueManager manager;

        private IDialogueLipSyncTarget _target;
        private DialogueManager _bound;

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

        private void Update()
        {
            if (_target != null && lipSync != null)
                _target.SetMouthOpen(lipSync.Openness);
        }

        private void Bind(DialogueManager target)
        {
            if (target == null || _bound == target) return;

            Unbind();
            _bound = target;
            _bound.LineStarted += HandleLineStarted;
            _bound.DialogueEnded += HandleDialogueEnded;
        }

        private void Unbind()
        {
            if (_bound == null) return;
            _bound.LineStarted -= HandleLineStarted;
            _bound.DialogueEnded -= HandleDialogueEnded;
            _bound = null;
        }

        private void HandleLineStarted(DialogueEventContext context)
        {
            _target = null;
            if (context == null || context.Data == null || backend == null)
                return;

            DialogueCharacterModel model;
            if (backend.TryGetModel(context.Data.Speaker, out model))
                _target = model;
        }

        private void HandleDialogueEnded(DialogueEventContext context)
        {
            if (_target != null)
                _target.SetMouthOpen(0f);
            _target = null;
        }
    }
}
