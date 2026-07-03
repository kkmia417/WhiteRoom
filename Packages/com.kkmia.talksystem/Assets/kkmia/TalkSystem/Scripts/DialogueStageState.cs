using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public enum DialogueStageOperationKind
    {
        Show,
        Hide,
        ClearAll
    }

    /// <summary>
    /// ステージ状態の差分として算出された単一の演出操作。
    /// View はこの操作列を順番に適用するだけでよい。
    /// </summary>
    public readonly struct DialogueStageOperation
    {
        public DialogueStageOperation(DialogueStageOperationKind kind, string slot, string characterKey, string expression, string animation)
        {
            Kind = kind;
            Slot = slot ?? string.Empty;
            CharacterKey = characterKey ?? string.Empty;
            Expression = expression ?? string.Empty;
            Animation = animation ?? string.Empty;
        }

        public DialogueStageOperationKind Kind { get; }
        public string Slot { get; }
        public string CharacterKey { get; }
        public string Expression { get; }
        public string Animation { get; }

        public static DialogueStageOperation Show(string slot, string characterKey, string expression, string animation)
        {
            return new DialogueStageOperation(DialogueStageOperationKind.Show, slot, characterKey, expression, animation);
        }

        public static DialogueStageOperation Hide(string slot, string characterKey, string animation)
        {
            return new DialogueStageOperation(DialogueStageOperationKind.Hide, slot, characterKey, string.Empty, animation);
        }

        public static DialogueStageOperation ClearAll()
        {
            return new DialogueStageOperation(DialogueStageOperationKind.ClearAll, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// どのスロットに誰が立っているかを追跡する純ロジック。
    /// 立ち絵指示（<see cref="DialogueStageDirective"/>）を、View が適用すべき操作列へ変換する。
    /// Unity 型に依存しないためユニットテスト可能。
    /// </summary>
    public sealed class DialogueStageState
    {
        // slot -> characterKey。挿入順を保つため List ではなく Dictionary + 補助探索を使う。
        private readonly Dictionary<string, string> _occupancy = new Dictionary<string, string>();
        // slot -> 現在の表情。セーブの完全復元のために占有と並行して保持する。
        private readonly Dictionary<string, string> _expressions = new Dictionary<string, string>();
        private readonly string _defaultSlot;

        public DialogueStageState(string defaultSlot = DialogueStageSlot.Center)
        {
            _defaultSlot = string.IsNullOrEmpty(defaultSlot) ? DialogueStageSlot.Center : defaultSlot;
        }

        /// <summary>現在の slot -> characterKey の対応（読み取り専用）。セーブ等で参照する。</summary>
        public IReadOnlyDictionary<string, string> Occupancy
        {
            get { return _occupancy; }
        }

        public void Reset()
        {
            _occupancy.Clear();
            _expressions.Clear();
        }

        /// <summary>現在のステージ占有をスナップショット化する（セーブ用）。</summary>
        public List<DialogueStageCharacterSnapshot> Snapshot()
        {
            var result = new List<DialogueStageCharacterSnapshot>();
            foreach (var pair in _occupancy)
            {
                string expression;
                _expressions.TryGetValue(pair.Key, out expression);
                result.Add(new DialogueStageCharacterSnapshot(pair.Key, pair.Value, expression ?? string.Empty));
            }

            result.Sort((a, b) => string.CompareOrdinal(a.slot, b.slot));
            return result;
        }

        /// <summary>スナップショットから占有状態を再構築する（操作列は発行しない）。</summary>
        public void RestoreSnapshot(IEnumerable<DialogueStageCharacterSnapshot> snapshot)
        {
            Reset();
            if (snapshot == null) return;

            foreach (var entry in snapshot)
            {
                if (string.IsNullOrEmpty(entry.slot) || string.IsNullOrEmpty(entry.characterKey))
                    continue;

                _occupancy[entry.slot] = entry.characterKey;
                _expressions[entry.slot] = entry.expression ?? string.Empty;
            }
        }

        /// <summary>
        /// 立ち絵指示列を適用し、発生する操作列を返します。状態（占有スロット）も更新します。
        /// </summary>
        public IReadOnlyList<DialogueStageOperation> Apply(IReadOnlyList<DialogueStageDirective> directives)
        {
            var operations = new List<DialogueStageOperation>();
            if (directives == null) return operations;

            foreach (var directive in directives)
            {
                if (directive == null) continue;

                if (directive.IsClearAll)
                {
                    _occupancy.Clear();
                    _expressions.Clear();
                    operations.Add(DialogueStageOperation.ClearAll());
                    continue;
                }

                if (directive.IsExit)
                {
                    var slot = directive.HasSlot ? directive.Slot : FindSlotOf(directive.CharacterKey);
                    if (!string.IsNullOrEmpty(slot))
                    {
                        _occupancy.Remove(slot);
                        _expressions.Remove(slot);
                    }

                    operations.Add(DialogueStageOperation.Hide(slot, directive.CharacterKey, directive.Animation));
                    continue;
                }

                ApplyShow(directive, operations);
            }

            return operations;
        }

        private void ApplyShow(DialogueStageDirective directive, List<DialogueStageOperation> operations)
        {
            // スロット解決順: 明示指定 > 既に立っている位置 > 既定スロット。
            var slot = directive.Slot;
            if (string.IsNullOrEmpty(slot))
                slot = FindSlotOf(directive.CharacterKey);
            if (string.IsNullOrEmpty(slot))
                slot = _defaultSlot;

            // 同じキャラが別スロットから移動する場合は元スロットを空け、表情を引き継ぐ。
            var previousSlot = FindSlotOf(directive.CharacterKey);
            string carriedExpression = null;
            if (!string.IsNullOrEmpty(previousSlot))
                _expressions.TryGetValue(previousSlot, out carriedExpression);

            if (!string.IsNullOrEmpty(previousSlot) && previousSlot != slot)
            {
                _occupancy.Remove(previousSlot);
                _expressions.Remove(previousSlot);
                operations.Add(DialogueStageOperation.Hide(previousSlot, directive.CharacterKey, directive.Animation));
            }

            string displacedCharacter;
            if (_occupancy.TryGetValue(slot, out displacedCharacter) && displacedCharacter != directive.CharacterKey)
            {
                _occupancy.Remove(slot);
                _expressions.Remove(slot);
                operations.Add(DialogueStageOperation.Hide(slot, displacedCharacter, string.Empty));
            }

            _occupancy[slot] = directive.CharacterKey;
            // 表情未指定の再表示では直前の表情を維持する。
            var resolvedExpression = directive.HasExpression ? directive.Expression : (carriedExpression ?? string.Empty);
            _expressions[slot] = resolvedExpression;
            operations.Add(DialogueStageOperation.Show(slot, directive.CharacterKey, resolvedExpression, directive.Animation));
        }

        private string FindSlotOf(string characterKey)
        {
            if (string.IsNullOrEmpty(characterKey)) return string.Empty;

            foreach (var pair in _occupancy)
            {
                if (pair.Value == characterKey)
                    return pair.Key;
            }

            return string.Empty;
        }
    }
}
