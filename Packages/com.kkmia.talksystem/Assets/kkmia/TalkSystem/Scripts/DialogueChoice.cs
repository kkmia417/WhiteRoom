using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class DialogueChoice
    {
        public DialogueChoice(string text, int nextId, string conditionKey)
        {
            Text = text ?? string.Empty;
            NextId = nextId;
            ConditionKey = conditionKey ?? string.Empty;
        }

        public string Text { get; private set; }
        public int NextId { get; private set; }
        public string ConditionKey { get; private set; }

        public bool HasConditionKey
        {
            get { return !string.IsNullOrEmpty(ConditionKey); }
        }

        private static readonly IReadOnlyList<DialogueChoice> EmptyChoices = new DialogueChoice[0];

        public static IReadOnlyList<DialogueChoice> ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return EmptyChoices;

            var result = new List<DialogueChoice>();

            foreach (var entry in raw.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                DialogueChoice choice;
                if (TryParseEntry(entry, out choice))
                    result.Add(choice);
            }

            return result;
        }

        /// <summary>
        /// 単一の選択肢エントリ（"テキスト-&gt;ID ?条件"）をパースします。
        /// 記法ミスの検出をバリデーション側でも行えるよう、エントリ単位で公開しています。
        /// 空白のみの入力や記法不正の場合は false を返します。
        /// </summary>
        internal static bool TryParseEntry(string entry, out DialogueChoice choice)
        {
            choice = null;

            var value = (entry ?? string.Empty).Trim();
            if (value.Length == 0)
                return false;

            var condition = string.Empty;
            var conditionIndex = value.IndexOf('?');
            if (conditionIndex >= 0)
            {
                condition = value.Substring(conditionIndex + 1).Trim();
                value = value.Substring(0, conditionIndex).Trim();
            }

            var separator = value.IndexOf("->", StringComparison.Ordinal);
            var usingArrow = separator >= 0;
            if (separator < 0)
                separator = value.LastIndexOf(':');

            if (separator < 0)
                return false;

            var text = value.Substring(0, separator).Trim();
            var targetText = value.Substring(separator + (usingArrow ? 2 : 1)).Trim();

            int target;
            if (!int.TryParse(targetText, out target))
                return false;

            choice = new DialogueChoice(text, target, condition);
            return true;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(ConditionKey)
                ? Text + " -> " + NextId
                : Text + " -> " + NextId + " ?" + ConditionKey;
        }
    }
}
