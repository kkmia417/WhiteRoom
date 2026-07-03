using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>立ち絵スロットの既定位置。Slot は任意文字列を許容し、これらは規約上の定数。</summary>
    public static class DialogueStageSlot
    {
        public const string Left = "left";
        public const string Center = "center";
        public const string Right = "right";
    }

    /// <summary>
    /// Characters 列の 1 エントリを表す、解析済みの立ち絵演出指示。
    /// 記法: <c>Character@slot:expression#animation</c>（@slot / :expression / #animation は任意）。
    /// 退場は先頭に <c>-</c>（例 <c>-Alice</c>）、全消去はエントリ <c>*</c>。
    /// 表示・配置・アニメの実体は Phase 2 以降の演出層が解釈する。ここはデータの正規化のみを担う。
    /// </summary>
    [Serializable]
    public sealed class DialogueStageDirective
    {
        public DialogueStageDirective(string characterKey, string slot, string expression, string animation, bool isExit, bool isClearAll)
        {
            CharacterKey = characterKey ?? string.Empty;
            Slot = slot ?? string.Empty;
            Expression = expression ?? string.Empty;
            Animation = animation ?? string.Empty;
            IsExit = isExit;
            IsClearAll = isClearAll;
        }

        /// <summary>対象キャラクターキー。ClearAll のときは空。</summary>
        public string CharacterKey { get; private set; }

        /// <summary>配置スロット。空＝既定（現在位置を維持／中央）。</summary>
        public string Slot { get; private set; }

        /// <summary>表情キー。空＝変更なし。</summary>
        public string Expression { get; private set; }

        /// <summary>入退場アニメ名（fadein / slidein など）。空＝なし。</summary>
        public string Animation { get; private set; }

        /// <summary>このキャラクターを退場させる指示か。</summary>
        public bool IsExit { get; private set; }

        /// <summary>ステージ上の全キャラクターを消去する指示か（エントリ <c>*</c>）。</summary>
        public bool IsClearAll { get; private set; }

        public bool HasSlot
        {
            get { return !string.IsNullOrEmpty(Slot); }
        }

        public bool HasExpression
        {
            get { return !string.IsNullOrEmpty(Expression); }
        }

        public bool HasAnimation
        {
            get { return !string.IsNullOrEmpty(Animation); }
        }

        public static IReadOnlyList<DialogueStageDirective> ParseList(string raw)
        {
            var result = new List<DialogueStageDirective>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var entry in raw.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                DialogueStageDirective directive;
                if (TryParseEntry(entry, out directive))
                    result.Add(directive);
            }

            return result;
        }

        /// <summary>
        /// 単一の演出エントリを解析します。記法ミスの検出をバリデーション側でも行えるよう公開しています。
        /// 空白のみ・キャラクターキー欠落などの記法不正は false を返します。
        /// </summary>
        internal static bool TryParseEntry(string entry, out DialogueStageDirective directive)
        {
            directive = null;

            var value = (entry ?? string.Empty).Trim();
            if (value.Length == 0)
                return false;

            if (value == "*")
            {
                directive = new DialogueStageDirective(string.Empty, string.Empty, string.Empty, string.Empty, false, true);
                return true;
            }

            var isExit = false;
            if (value[0] == '-')
            {
                isExit = true;
                value = value.Substring(1).Trim();
                if (value.Length == 0)
                    return false;
            }

            var animation = string.Empty;
            var hashIndex = value.IndexOf('#');
            if (hashIndex >= 0)
            {
                animation = value.Substring(hashIndex + 1).Trim();
                value = value.Substring(0, hashIndex).Trim();
            }

            var expression = string.Empty;
            var colonIndex = value.IndexOf(':');
            if (colonIndex >= 0)
            {
                expression = value.Substring(colonIndex + 1).Trim();
                value = value.Substring(0, colonIndex).Trim();
            }

            var slot = string.Empty;
            var atIndex = value.IndexOf('@');
            if (atIndex >= 0)
            {
                slot = value.Substring(atIndex + 1).Trim();
                value = value.Substring(0, atIndex).Trim();
            }

            var characterKey = value.Trim();
            if (characterKey.Length == 0)
                return false;

            directive = new DialogueStageDirective(characterKey, slot, expression, animation, isExit, false);
            return true;
        }

        public override string ToString()
        {
            if (IsClearAll) return "*";

            var prefix = IsExit ? "-" : string.Empty;
            var result = prefix + CharacterKey;
            if (HasSlot) result += "@" + Slot;
            if (HasExpression) result += ":" + Expression;
            if (HasAnimation) result += "#" + Animation;
            return result;
        }
    }
}
