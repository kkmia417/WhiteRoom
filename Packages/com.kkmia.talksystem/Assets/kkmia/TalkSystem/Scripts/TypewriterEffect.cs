using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// テキストを一文字ずつ表示するタイプライター演出コンポーネント
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TypewriterEffect : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("1文字あたりの表示間隔（秒）")]
        private float interval = 0.05f;

        private TMP_Text _textComponent;
        private Coroutine _typingCoroutine;
        private string _fullText;
        private Action _onComplete;
        private IReadOnlyList<DialogueInlineCommand> _commands;

        // WaitForSeconds は再利用可能なので、待ち時間が変わらない限り同じインスタンスを
        // 使い回す。1文字ごとの new WaitForSeconds による GC 割り当てを避けるため。
        private WaitForSeconds _cachedWait;
        private float _cachedWaitSeconds = -1f;

        /// <summary>
        /// 現在タイプ中かどうかを返します。
        /// </summary>
        public bool IsTyping => _typingCoroutine != null;

        private void Awake()
        {
            _textComponent = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// テキストを指定してタイプライター演出を開始します。
        /// </summary>
        /// <param name="fullText">表示する全文</param>
        /// <param name="onComplete">表示完了時に呼ばれるコールバック</param>
        public void Play(string fullText, Action onComplete)
        {
            Play(fullText, null, onComplete);
        }

        /// <summary>
        /// インラインタグ解釈済みのテキストを表示する。<paramref name="commands"/> の
        /// ウェイト/速度コマンドを可視文字位置で適用する。
        /// </summary>
        public void Play(string displayText, IReadOnlyList<DialogueInlineCommand> commands, Action onComplete)
        {
            StopTyping();

            if (_textComponent == null)
            {
                Debug.LogError("[TypewriterEffect] TMP_Text が見つかりません。");
                return;
            }

            _fullText = displayText ?? string.Empty;
            _commands = commands;
            _onComplete = onComplete;
            _typingCoroutine = StartCoroutine(TypeRoutine());
        }

        /// <summary>インラインタグ付き本文を解析して表示する簡易ヘルパー。</summary>
        public void PlayInline(string rawText, Action onComplete)
        {
            var inline = DialogueInlineText.Build(rawText);
            Play(inline.DisplayText, inline.Commands, onComplete);
        }

        /// <summary>
        /// 表示中のテキストを即座に全表示に切り替え、コールバックを実行します。
        /// </summary>
        public void Complete()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;

                if (_textComponent != null)
                    _textComponent.maxVisibleCharacters = int.MaxValue;

                _onComplete?.Invoke();
            }
        }

        public void Cancel()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }

            _onComplete = null;
        }

        /// <summary>
        /// 表示間隔を変更します。
        /// </summary>
        /// <param name="newInterval">特定のタイミングで文字表示を遅らせたいときに使えます。</param>
        public void SetInterval(float newInterval)
        {
            interval = Mathf.Max(0f, newInterval);
        }

        private void StopTyping()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }
        }

        private IEnumerator TypeRoutine()
        {
            // 全文を一度だけ設定し、maxVisibleCharacters で表示文字数を制御する。
            // 文字列の再確保が発生せず、リッチテキストタグ（<color> など）も
            // 1文字ずつ露出しない（タグは可視文字数にカウントされないため）。
            _textComponent.text = _fullText;
            _textComponent.maxVisibleCharacters = 0;
            _textComponent.ForceMeshUpdate();

            var totalCharacters = _textComponent.textInfo.characterCount;
            var currentSpeed = 1f;
            var commandIndex = 0;

            for (var visible = 0; visible <= totalCharacters; visible++)
            {
                // この可視位置に紐づくウェイト/速度コマンドを適用する。
                while (_commands != null && commandIndex < _commands.Count &&
                       _commands[commandIndex].VisibleCharIndex == visible)
                {
                    var command = _commands[commandIndex++];
                    if (command.Kind == DialogueInlineCommandKind.Speed)
                        currentSpeed = command.Value > 0f ? command.Value : 1f;
                    else if (command.Kind == DialogueInlineCommandKind.Wait && command.Value > 0f)
                        yield return GetWait(command.Value);
                }

                _textComponent.maxVisibleCharacters = visible;

                if (visible < totalCharacters)
                    yield return GetWait(currentSpeed > 0f ? interval / currentSpeed : interval);
            }

            _textComponent.maxVisibleCharacters = int.MaxValue;
            _commands = null;
            _typingCoroutine = null;
            _onComplete?.Invoke();
        }

        private WaitForSeconds GetWait(float seconds)
        {
            if (_cachedWait == null || !Mathf.Approximately(_cachedWaitSeconds, seconds))
            {
                _cachedWaitSeconds = seconds;
                _cachedWait = new WaitForSeconds(seconds);
            }

            return _cachedWait;
        }
    }
}
