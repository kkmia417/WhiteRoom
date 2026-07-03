using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public class DialoguePresenter : IDisposable
    {
        private readonly DialogueSession _session;
        private readonly DialogueRollbackHistory _rollback = new DialogueRollbackHistory();
        private IDialogueView _view;
        private IDialogueTextResolver _textResolver;
        private IDialogueVariableResolver _variableResolver;
        private IDialogueEventDispatcher _eventDispatcher;
        private string _languageKey = string.Empty;
        private bool _disposed;

        public DialoguePresenter(IDialogueRepository repository, IDialogueView view)
        {
            _session = new DialogueSession(repository);
            _textResolver = new DefaultDialogueTextResolver();
            _variableResolver = new EmptyDialogueVariableResolver();
            BindView(view);
        }

        public event Action<DialogueEventContext> LineStarted;
        public event Action<DialogueEventContext> LineCompleted;
        public event Action<DialogueEventContext> DialogueEnded;
        public event Action<DialogueProgressEventContext> ProgressMarkerReached;
        public event Action<string> ErrorRaised;

        public DialogueSessionState State
        {
            get { return _session.State; }
        }

        public DialogueData CurrentData
        {
            get { return _session.CurrentData; }
        }

        public int CurrentChoiceCount
        {
            get { return _session.CurrentChoices != null ? _session.CurrentChoices.Count : 0; }
        }

        public IReadOnlyDialogueSession Session
        {
            get { return _session; }
        }

        public void SetConditionEvaluator(IDialogueConditionEvaluator evaluator)
        {
            _session.ConditionEvaluator = evaluator ?? new AllowAllDialogueConditionEvaluator();
        }

        public void SetVariableResolver(IDialogueVariableResolver resolver)
        {
            _variableResolver = resolver ?? new EmptyDialogueVariableResolver();
        }

        public void SetTextResolver(IDialogueTextResolver resolver)
        {
            _textResolver = resolver ?? new DefaultDialogueTextResolver();
        }

        public void SetEventDispatcher(IDialogueEventDispatcher dispatcher)
        {
            _eventDispatcher = dispatcher;
        }

        public void SetLanguage(string languageKey)
        {
            _languageKey = languageKey ?? string.Empty;
        }

        public void BindView(IDialogueView view)
        {
            if (_view == view) return;

            UnbindView();
            _view = view;

            if (_view != null)
            {
                _view.NextRequested += HandleNextRequested;
                _view.ChoiceSelected += HandleChoiceSelected;
            }
        }

        public void Start(int id)
        {
            Start(id, null);
        }

        public void Start(int id, string triggerKey)
        {
            if (_disposed) return;

            // 新しい会話を開始したらロールバック履歴も会話単位で初期化する。
            _rollback.Clear();

            if (!_session.Start(id, triggerKey))
            {
                RaiseError("Dialogue data was not found for ID " + id + ".");
                if (_view != null) _view.Clear();
                RaiseEnded();
                return;
            }

            RenderCurrent();
        }

        /// <summary>直前の行へ巻き戻せるか。</summary>
        public bool CanRollback
        {
            get { return _rollback.CanRollback; }
        }

        /// <summary>
        /// 直前の行へ巻き戻して再開する。表示は <see cref="RenderReason.Restore"/> で再描画するため、
        /// 履歴の重複追加・LineStarted・イベント再発火は起こらない。戻れない場合は false。
        /// </summary>
        public bool Rollback()
        {
            if (_disposed) return false;

            var previous = _rollback.Rollback();
            if (previous == null) return false;

            if (!_session.Restore(previous))
                return false;

            if (_session.CurrentData != null)
                RenderCurrent(RenderReason.Restore);

            return true;
        }

        public void End()
        {
            if (_view != null)
            {
                _view.ForceStop();
                _view.Clear();
            }

            _session.End();
            RaiseEnded();
        }

        public void Reset()
        {
            _session.End();
        }

        public DialogueSaveData CaptureState()
        {
            return _session.Capture();
        }

        public bool RestoreState(DialogueSaveData saveData)
        {
            if (!_session.Restore(saveData))
                return false;

            _rollback.Clear();
            _rollback.Push(_session.Capture());

            if (_session.CurrentData != null)
                RenderCurrent(RenderReason.Restore);

            return true;
        }

        /// <summary>
        /// View を差し替えた後などに、進行中の現在行を新しい View へ再描画する。
        /// 履歴追加・イベント発火・LineStarted を伴わない表示専用の再描画。
        /// </summary>
        public void RedrawCurrent()
        {
            if (_session.CurrentData != null)
                RenderCurrent(RenderReason.Restore);
        }

        public void Dispose()
        {
            if (_disposed) return;
            UnbindView();
            _disposed = true;
        }

        private enum RenderReason
        {
            // 新規に行を開始する描画。履歴追加・LineStarted・イベント発火を伴う。
            NewLine,
            // セーブ復元や View 再バインド時の表示専用の再描画。副作用を伴わない。
            Restore
        }

        private void RenderCurrent()
        {
            RenderCurrent(RenderReason.NewLine);
        }

        private void RenderCurrent(RenderReason reason)
        {
            if (_view == null)
            {
                RaiseError("Dialogue view is not bound.");
                return;
            }

            var data = _session.CurrentData;
            if (data == null)
            {
                End();
                return;
            }

            var resolvedText = _textResolver.Resolve(data, _languageKey, _variableResolver);
            var displayData = data.WithResolvedText(resolvedText);

            if (reason == RenderReason.NewLine)
            {
                _session.MarkTyping();
                _session.RecordDisplayedLine(displayData);
                var progressMarkers = _session.MarkProgress(data);
                RaiseLineStarted(data);
                RaiseProgressMarkers(data, progressMarkers);

                if (data.HasEventKey && _eventDispatcher != null)
                    _eventDispatcher.Dispatch(new DialogueEventContext(data, data.EventKey, _session.State));

                // 巻き戻し用に、この行を表示した時点の状態を積む（復元再描画では積まない）。
                _rollback.Push(_session.Capture());
            }

            _view.Show(displayData, _session.CurrentChoices, () =>
            {
                _session.MarkLineReady();
            });
        }

        private void HandleNextRequested()
        {
            if (_view == null) return;

            if (_view.IsTyping || _session.State == DialogueSessionState.Typing)
            {
                _view.CompleteTyping();
                return;
            }

            if (_session.State == DialogueSessionState.ChoicePending)
                return;

            var completed = _session.CurrentData;
            if (completed != null)
                RaiseLineCompleted(completed);

            if (_session.Advance())
                RenderCurrent();
            else
                End();
        }

        private void HandleChoiceSelected(int index)
        {
            var completed = _session.CurrentData;
            if (completed != null)
                RaiseLineCompleted(completed);

            if (_session.SelectChoice(index))
                RenderCurrent();
            else
                End();
        }

        private void UnbindView()
        {
            if (_view == null) return;
            _view.NextRequested -= HandleNextRequested;
            _view.ChoiceSelected -= HandleChoiceSelected;
            _view = null;
        }

        private void RaiseLineStarted(DialogueData data)
        {
            var context = new DialogueEventContext(data, data != null ? data.EventKey : string.Empty, _session.State);
            if (LineStarted != null) LineStarted(context);
        }

        private void RaiseLineCompleted(DialogueData data)
        {
            var context = new DialogueEventContext(data, data != null ? data.EventKey : string.Empty, _session.State);
            if (LineCompleted != null) LineCompleted(context);
        }

        private void RaiseEnded()
        {
            var context = new DialogueEventContext(null, string.Empty, _session.State);
            if (DialogueEnded != null) DialogueEnded(context);
        }

        private void RaiseProgressMarkers(DialogueData data, IReadOnlyList<DialogueProgressMarker> markers)
        {
            if (markers == null || ProgressMarkerReached == null)
                return;

            for (var i = 0; i < markers.Count; i++)
                ProgressMarkerReached(new DialogueProgressEventContext(data, markers[i], _session.Progress));
        }

        private void RaiseError(string message)
        {
            Debug.LogWarning("[DialoguePresenter] " + message);
            if (ErrorRaised != null) ErrorRaised(message);
        }
    }
}
