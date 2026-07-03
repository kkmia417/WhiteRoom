using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// バックログ 1 行分の UGUI 表示。話者名・本文（インラインタグ解釈つき）・ボイス再生ボタンを持つ。
    /// <see cref="DialogueBacklogView"/> から生成・<see cref="Bind"/> される。
    /// </summary>
    public class DialogueBacklogRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button voiceButton;

        private string _voiceKey;
        private Action<string> _onVoice;

        public void Bind(DialogueBacklogEntry entry, Action<string> onVoice)
        {
            _onVoice = onVoice;
            _voiceKey = entry != null ? entry.VoiceKey : null;

            if (speakerText != null)
                speakerText.text = entry != null ? entry.Speaker : string.Empty;

            if (bodyText != null)
                bodyText.text = entry != null ? DialogueInlineText.Build(entry.Text).DisplayText : string.Empty;

            if (voiceButton != null)
            {
                voiceButton.onClick.RemoveListener(HandleVoiceClicked);
                var hasVoice = entry != null && entry.HasVoice;
                voiceButton.gameObject.SetActive(hasVoice);
                if (hasVoice)
                    voiceButton.onClick.AddListener(HandleVoiceClicked);
            }
        }

        private void HandleVoiceClicked()
        {
            if (_onVoice != null && !string.IsNullOrEmpty(_voiceKey))
                _onVoice(_voiceKey);
        }
    }
}
