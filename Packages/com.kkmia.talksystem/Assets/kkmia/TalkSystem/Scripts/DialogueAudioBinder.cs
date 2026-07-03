using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="DialogueAudioPlayer"/> を <see cref="DialogueManager"/> のイベントに接続し、
    /// 各行の BGM・SE・ボイスを再生する補助コンポーネント。
    /// <see cref="DialogueViewBinder"/> / <see cref="DialogueStageBinder"/> と同様、置くだけで自動配線される。
    /// </summary>
    [RequireComponent(typeof(DialogueAudioPlayer))]
    public class DialogueAudioBinder : MonoBehaviour, IDialogueSaveContributor
    {
        private const string AudioKey = "audio";
        private const string BgmKey = "audio.bgm";

        [Tooltip("会話終了時に BGM も停止するか（false ならボイスのみ停止）。")]
        [SerializeField] private bool stopBgmOnDialogueEnd = true;

        private DialogueAudioDirector _director;
        private DialogueAudioPlayer _player;
        private DialogueManager _boundManager;

        private void Awake()
        {
            _player = GetComponent<DialogueAudioPlayer>();
            _director = new DialogueAudioDirector(_player);
        }

        private void OnEnable()
        {
            // 起動順に依存しないよう、現在の Instance へ即時接続しつつ
            // InstanceChanged を購読して Manager 生成・差し替え後も再接続する。
            DialogueManager.InstanceChanged += OnInstanceChanged;
            Bind(DialogueManager.Instance);
        }

        private void OnDisable()
        {
            DialogueManager.InstanceChanged -= OnInstanceChanged;
            Unbind();
        }

        private void OnInstanceChanged(DialogueManager manager)
        {
            if (manager != null)
                Bind(manager);
            else
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
            if (stopBgmOnDialogueEnd)
                _director.StopAll();
            else
                _player.StopVoice();
        }

        // --- IDialogueSaveContributor（セーブの完全復元）---

        void IDialogueSaveContributor.Capture(DialogueSaveData data)
        {
            if (data == null || _director == null) return;
            data.SetExtra(AudioKey, JsonUtility.ToJson(_director.CaptureSnapshot()));
            data.SetExtra(BgmKey, _director.CurrentBgmKey);
        }

        void IDialogueSaveContributor.Restore(DialogueSaveData data)
        {
            if (data == null || _player == null || _director == null) return;

            string json;
            if (data.TryGetExtra(AudioKey, out json) && !string.IsNullOrEmpty(json))
            {
                var snapshot = JsonUtility.FromJson<DialogueAudioSnapshot>(json);
                _director.RestoreSnapshot(snapshot);
                return;
            }

            string key;
            if (!data.TryGetExtra(BgmKey, out key))
                return;

            _director.RestoreSnapshot(new DialogueAudioSnapshot { bgmKey = key ?? string.Empty });
        }
    }
}
