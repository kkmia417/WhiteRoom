using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 会話履歴をスクロール表示するバックログUI。<see cref="DialogueManager.History"/> から
    /// 表示モデルを構築し、行ごとに <see cref="DialogueBacklogRow"/> を生成する。
    /// 開閉は <see cref="Open"/> / <see cref="Close"/> / <see cref="Toggle"/>。
    /// ボイス付き行は <see cref="DialogueAudioPlayer"/> 経由で再生する。
    /// </summary>
    public class DialogueBacklogView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private DialogueBacklogRow rowPrefab;
        [SerializeField] private DialogueAudioPlayer audioPlayer;
        [Tooltip("新しい行を上に並べる場合は true。")]
        [SerializeField] private bool newestFirst;

        private readonly List<DialogueBacklogRow> _rows = new List<DialogueBacklogRow>();

        public bool IsOpen { get { return panel != null && panel.activeSelf; } }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            Rebuild();
            if (panel != null)
                panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null)
                panel.SetActive(false);
            ClearRows();
        }

        private void Rebuild()
        {
            ClearRows();
            if (contentContainer == null || rowPrefab == null)
                return;

            var history = DialogueManager.Instance != null ? DialogueManager.Instance.History : null;
            var entries = DialogueBacklog.Build(history, newestFirst);

            for (var i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(rowPrefab, contentContainer);
                row.Bind(entries[i], PlayVoice);
                row.gameObject.SetActive(true);
                _rows.Add(row);
            }
        }

        private void PlayVoice(string voiceKey)
        {
            DialogueBacklog.ReplayVoice(voiceKey, audioPlayer);
        }

        private void ClearRows()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Destroy(_rows[i].gameObject);
            }

            _rows.Clear();
        }
    }
}
