using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public interface IReadOnlyDialogueSession
    {
        DialogueSessionState State { get; }
        DialogueData CurrentData { get; }
        IReadOnlyList<DialogueChoice> CurrentChoices { get; }
        IReadOnlyList<int> SeenLineIds { get; }
        IReadOnlyList<DialogueChoiceRecord> ChoiceRecords { get; }
        IReadOnlyList<int> ChoiceHistory { get; }
        IReadOnlyList<DialogueHistoryEntry> History { get; }
        DialogueProgressState Progress { get; }
        string TriggerKey { get; }
    }

    public sealed class DialogueSession : IReadOnlyDialogueSession
    {
        // 「選択肢なし」「マーカーなし」を表す共有インスタンス。行送り・会話終了のたびに
        // 空リストを確保しないための定数。
        private static readonly IReadOnlyList<DialogueChoice> EmptyChoices = new DialogueChoice[0];
        private static readonly IReadOnlyList<DialogueProgressMarker> EmptyMarkers = new DialogueProgressMarker[0];

        private readonly IDialogueRepository _repository;
        private readonly List<int> _seenLineIds = new List<int>();
        private readonly HashSet<int> _seenLineIdSet = new HashSet<int>();
        private readonly List<DialogueChoiceRecord> _choiceRecords = new List<DialogueChoiceRecord>();
        private readonly List<int> _legacyChoiceHistory = new List<int>();
        private readonly List<DialogueHistoryEntry> _history = new List<DialogueHistoryEntry>();
        private readonly HashSet<int> _skipGuard = new HashSet<int>();
        private DialogueProgressState _progress = new DialogueProgressState();

        public DialogueSession(IDialogueRepository repository)
        {
            _repository = repository;
            ConditionEvaluator = new AllowAllDialogueConditionEvaluator();
            State = DialogueSessionState.Idle;
        }

        public DialogueSessionState State { get; private set; }
        public DialogueData CurrentData { get; private set; }
        public IReadOnlyList<DialogueChoice> CurrentChoices { get; private set; } = EmptyChoices;
        public IReadOnlyList<int> SeenLineIds { get { return _seenLineIds; } }
        public IReadOnlyList<DialogueChoiceRecord> ChoiceRecords { get { return _choiceRecords; } }
        public IReadOnlyList<int> ChoiceHistory { get { return _legacyChoiceHistory; } }
        public IReadOnlyList<DialogueHistoryEntry> History { get { return _history; } }
        public DialogueProgressState Progress { get { return _progress; } }
        public string TriggerKey { get; private set; }
        public IDialogueConditionEvaluator ConditionEvaluator { get; set; }

        public bool Start(int id, string triggerKey = null)
        {
            // DialogueSession は 1 回の会話開始から終了までの状態を表す。
            // 新しい会話を開始したら、表示履歴・既読行・選択履歴を会話単位で初期化する。
            // ゲーム全体の既読/履歴が必要な場合は別コンポーネントとして設計する想定。
            TriggerKey = triggerKey ?? string.Empty;
            _skipGuard.Clear();
            _seenLineIds.Clear();
            _seenLineIdSet.Clear();
            _choiceRecords.Clear();
            _legacyChoiceHistory.Clear();
            _history.Clear();
            _progress = new DialogueProgressState();
            return LoadLine(id);
        }

        public void MarkTyping()
        {
            State = DialogueSessionState.Typing;
        }

        public void MarkLineReady()
        {
            if (CurrentData == null)
            {
                State = DialogueSessionState.Idle;
                return;
            }

            State = CurrentChoices.Count > 0 ? DialogueSessionState.ChoicePending : DialogueSessionState.WaitingForInput;
        }

        public void RecordDisplayedLine(DialogueData displayData)
        {
            if (displayData == null) return;
            _history.Add(new DialogueHistoryEntry(displayData, _history.Count));
        }

        public IReadOnlyList<DialogueProgressMarker> MarkProgress(DialogueData data)
        {
            if (data == null)
                return EmptyMarkers;

            // マーカー付きの行は少数派なので、必要になるまでリストを確保しない。
            List<DialogueProgressMarker> markers = null;
            AddProgressMarker(ref markers, DialogueProgressMarkerType.Chapter, data.ChapterKey);
            AddProgressMarker(ref markers, DialogueProgressMarkerType.Route, data.RouteKey);
            AddProgressMarker(ref markers, DialogueProgressMarkerType.Ending, data.EndingKey);
            return markers != null ? markers : EmptyMarkers;
        }

        public bool Advance()
        {
            // 行の表示準備（MarkLineReady）が済むまで送りは受け付けない。ShowingLine は
            // LoadLine 直後の過渡状態であり、Presenter/PreviewWindow は必ず MarkTyping /
            // MarkLineReady を経由してから Advance を呼ぶ契約になっている。
            if (State != DialogueSessionState.WaitingForInput)
                return false;

            if (CurrentData == null)
            {
                End();
                return false;
            }

            // _skipGuard は「1回の遷移で条件不成立ノードを辿る連鎖」の無限ループ防止用。
            // 非再帰の入口でクリアし、正当な再訪（ループ会話など）が誤って終了扱いにならないようにする。
            _skipGuard.Clear();

            if (CurrentData.NextId >= 0)
                return LoadLine(CurrentData.NextId);

            End();
            return false;
        }

        public bool SelectChoice(int index)
        {
            if (State != DialogueSessionState.ChoicePending)
                return false;

            if (index < 0 || index >= CurrentChoices.Count)
                return false;

            _skipGuard.Clear();
            var choice = CurrentChoices[index];
            var rawChoiceIndex = ResolveRawChoiceIndex(index);
            var record = new DialogueChoiceRecord(
                CurrentData != null ? CurrentData.Id : -1,
                rawChoiceIndex,
                choice.NextId,
                choice.Text,
                choice.ConditionKey);
            _choiceRecords.Add(record);
            _legacyChoiceHistory.Add(rawChoiceIndex);
            return LoadLine(choice.NextId);
        }

        public void End()
        {
            CurrentData = null;
            CurrentChoices = EmptyChoices;
            State = DialogueSessionState.Ended;
        }

        public DialogueSaveData Capture()
        {
            return new DialogueSaveData
            {
                CurrentDialogueId = CurrentData != null ? CurrentData.Id : -1,
                TriggerKey = TriggerKey,
                State = State,
                SeenLineIds = new List<int>(_seenLineIds),
                ChoiceRecords = CloneChoiceRecords(_choiceRecords),
                ChoiceHistory = new List<int>(),
                History = new List<DialogueHistoryEntry>(_history),
                Progress = _progress != null ? _progress.Clone() : new DialogueProgressState()
            };
        }

        public bool Restore(DialogueSaveData saveData)
        {
            if (saveData == null)
                return false;

            _skipGuard.Clear();
            _seenLineIds.Clear();
            _seenLineIdSet.Clear();
            if (saveData.SeenLineIds != null)
            {
                for (var i = 0; i < saveData.SeenLineIds.Count; i++)
                    AddSeenLineId(saveData.SeenLineIds[i]);
            }

            _choiceRecords.Clear();
            if (saveData.ChoiceRecords != null && saveData.ChoiceRecords.Count > 0)
            {
                for (var i = 0; i < saveData.ChoiceRecords.Count; i++)
                {
                    if (saveData.ChoiceRecords[i] != null)
                        _choiceRecords.Add(saveData.ChoiceRecords[i].Clone());
                }
            }
            else if (saveData.ChoiceHistory != null)
            {
                for (var i = 0; i < saveData.ChoiceHistory.Count; i++)
                    _choiceRecords.Add(new DialogueChoiceRecord(-1, saveData.ChoiceHistory[i], -1, string.Empty, string.Empty));
            }

            _legacyChoiceHistory.Clear();
            for (var i = 0; i < _choiceRecords.Count; i++)
                _legacyChoiceHistory.Add(_choiceRecords[i].RawChoiceIndex);

            _history.Clear();
            if (saveData.History != null)
                _history.AddRange(saveData.History);

            _progress = saveData.Progress != null ? saveData.Progress.Clone() : new DialogueProgressState();
            TriggerKey = saveData.TriggerKey ?? string.Empty;

            if (saveData.CurrentDialogueId >= 0)
            {
                // 復元は「保存時の行をそのまま再構築する」操作であり、新規進行ではない。
                // LoadLine は条件スキップや State=ShowingLine への上書きを伴うため使わず、
                // 保存された CurrentData / CurrentChoices を組み立てる。
                var data = _repository.Get(saveData.CurrentDialogueId);
                if (data == null)
                {
                    End();
                    return false;
                }

                CurrentData = data;
                AddSeenLineId(data.Id);

                CurrentChoices = FilterChoices(data.GetChoices());
                State = CoerceRestoredActiveState(saveData.State, CurrentChoices.Count > 0);
                return true;
            }

            CurrentData = null;
            CurrentChoices = EmptyChoices;
            State = CoerceRestoredIdleState(saveData.State);
            return true;
        }

        /// <summary>
        /// セーブデータは外部入力（破損・改竄・条件変化があり得る）なので、State は保存値を
        /// そのまま信頼せず、復元後の実データと突き合わせて休止状態へ補正する。
        /// 選択肢ゼロの ChoicePending（進行不能）や選択肢ありの WaitingForInput
        /// （選択肢を無視して NextId へ進んでしまう）をここで防ぐ。
        /// Typing / ShowingLine は表示過渡状態であり、復元後の再描画で改めて遷移するため保持しない。
        /// </summary>
        private static DialogueSessionState CoerceRestoredActiveState(DialogueSessionState saved, bool hasChoices)
        {
            return hasChoices ? DialogueSessionState.ChoicePending : DialogueSessionState.WaitingForInput;
        }

        /// <summary>現在行を持たない復元で有効な State は Idle / Ended のみ。それ以外（不正値含む）は Ended に補正する。</summary>
        private static DialogueSessionState CoerceRestoredIdleState(DialogueSessionState saved)
        {
            return saved == DialogueSessionState.Idle ? DialogueSessionState.Idle : DialogueSessionState.Ended;
        }

        private bool LoadLine(int id)
        {
            while (true)
            {
                var data = _repository.Get(id);
                if (data == null)
                {
                    End();
                    return false;
                }

                if (!PassesCondition(data))
                {
                    if (!_skipGuard.Add(id))
                    {
                        End();
                        return false;
                    }

                    if (data.NextId >= 0)
                    {
                        id = data.NextId;
                        continue;
                    }

                    End();
                    return false;
                }

                CurrentData = data;
                AddSeenLineId(data.Id);

                CurrentChoices = FilterChoices(data.GetChoices());
                State = DialogueSessionState.ShowingLine;
                return true;
            }
        }

        private void AddSeenLineId(int id)
        {
            if (_seenLineIdSet.Add(id))
                _seenLineIds.Add(id);
        }

        private bool PassesCondition(DialogueData data)
        {
            if (data == null || !data.HasConditionKey)
                return true;

            return ConditionEvaluator == null || ConditionEvaluator.Evaluate(data.ConditionKey, data);
        }

        private bool PassesCondition(DialogueChoice choice)
        {
            if (choice == null || !choice.HasConditionKey)
                return true;

            return ConditionEvaluator == null || ConditionEvaluator.Evaluate(choice.ConditionKey, CurrentData);
        }

        private int ResolveRawChoiceIndex(int filteredIndex)
        {
            if (CurrentData == null)
                return -1;

            var rawChoices = CurrentData.GetChoices();
            var visibleIndex = -1;
            for (var i = 0; i < rawChoices.Count; i++)
            {
                if (!PassesCondition(rawChoices[i]))
                    continue;

                visibleIndex++;
                if (visibleIndex == filteredIndex)
                    return i;
            }

            return -1;
        }

        private static List<DialogueChoiceRecord> CloneChoiceRecords(IEnumerable<DialogueChoiceRecord> records)
        {
            var result = new List<DialogueChoiceRecord>();
            if (records == null)
                return result;

            foreach (var record in records)
            {
                if (record != null)
                    result.Add(record.Clone());
            }

            return result;
        }

        private void AddProgressMarker(ref List<DialogueProgressMarker> markers, DialogueProgressMarkerType type, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var normalized = key.Trim();
            var isFirstReach = _progress.Mark(type, normalized);
            if (markers == null)
                markers = new List<DialogueProgressMarker>();
            markers.Add(new DialogueProgressMarker(type, normalized, isFirstReach));
        }

        /// <summary>
        /// 条件を満たす選択肢のみを返す。全選択肢が可視ならパース済みリストをそのまま共有し、
        /// 選択肢が無い/全滅の場合は共有の空リストを返して行送りごとの確保を避ける。
        /// </summary>
        private IReadOnlyList<DialogueChoice> FilterChoices(IReadOnlyList<DialogueChoice> rawChoices)
        {
            if (rawChoices == null || rawChoices.Count == 0)
                return EmptyChoices;

            List<DialogueChoice> filtered = null;
            for (var i = 0; i < rawChoices.Count; i++)
            {
                var choice = rawChoices[i];
                if (PassesCondition(choice))
                {
                    if (filtered != null)
                        filtered.Add(choice);
                    continue;
                }

                // 初めて非表示の選択肢が見つかった時点で、それまでの可視分をコピーする。
                if (filtered == null)
                {
                    filtered = new List<DialogueChoice>(rawChoices.Count);
                    for (var j = 0; j < i; j++)
                        filtered.Add(rawChoices[j]);
                }
            }

            if (filtered == null)
                return rawChoices;

            return filtered.Count > 0 ? filtered : EmptyChoices;
        }
    }
}
