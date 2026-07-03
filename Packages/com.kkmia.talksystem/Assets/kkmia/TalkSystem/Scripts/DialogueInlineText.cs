using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 本文中のインラインタグを解釈する純粋ロジック。Unity 型に依存しないためテスト可能。
    /// 対応タグ:
    ///   [ruby=ふりがな]親文字[/ruby]   ルビ（ふりがな）
    ///   [w=秒] / [wait=秒]             タイプライターの一時停止
    ///   [speed=倍率]                   以降の表示速度倍率（1=標準, 2=倍速）
    ///   [color=#rrggbb]...[/color]     文字色
    ///   [[                             リテラルの '['
    /// 未知のタグや閉じ忘れはそのまま素通しし、クラッシュさせない。
    /// </summary>
    public static class DialogueInlineTagParser
    {
        public static IReadOnlyList<DialogueInlineToken> Parse(string text)
        {
            var tokens = new List<DialogueInlineToken>();
            if (string.IsNullOrEmpty(text))
                return tokens;

            var buffer = new StringBuilder();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];

                // エスケープ: "[[" -> リテラル '['
                if (c == '[' && i + 1 < text.Length && text[i + 1] == '[')
                {
                    buffer.Append('[');
                    i += 2;
                    continue;
                }

                if (c == '[')
                {
                    var close = text.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        // 閉じない '[' は素通し。
                        buffer.Append(c);
                        i++;
                        continue;
                    }

                    var inner = text.Substring(i + 1, close - i - 1);
                    if (TryConsumeTag(inner, text, ref close, tokens, buffer))
                    {
                        i = close + 1;
                        continue;
                    }

                    // 未知タグはそのまま（角括弧ごと）素通し。
                    buffer.Append('[').Append(inner).Append(']');
                    i = close + 1;
                    continue;
                }

                buffer.Append(c);
                i++;
            }

            FlushText(buffer, tokens);
            return tokens;
        }

        private static bool TryConsumeTag(string inner, string text, ref int close,
            List<DialogueInlineToken> tokens, StringBuilder buffer)
        {
            // ルビ: [ruby=ふりがな]親文字[/ruby]
            if (inner.StartsWith("ruby=", StringComparison.OrdinalIgnoreCase))
            {
                var ruby = inner.Substring("ruby=".Length);
                var endTag = text.IndexOf("[/ruby]", close + 1, StringComparison.OrdinalIgnoreCase);
                if (endTag < 0) return false; // 閉じ忘れは素通し

                var baseText = text.Substring(close + 1, endTag - close - 1);
                FlushText(buffer, tokens);
                tokens.Add(new DialogueRubyToken { Base = baseText, Ruby = ruby });
                close = endTag + "[/ruby]".Length - 1;
                return true;
            }

            // 色: [color=...] / [/color]
            if (inner.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
            {
                FlushText(buffer, tokens);
                tokens.Add(new DialogueColorToken { Color = inner.Substring("color=".Length), Push = true });
                return true;
            }
            if (inner.Equals("/color", StringComparison.OrdinalIgnoreCase))
            {
                FlushText(buffer, tokens);
                tokens.Add(new DialogueColorToken { Push = false });
                return true;
            }

            // ウェイト: [w=秒] / [wait=秒]
            if (TryParsePrefixedFloat(inner, "w=", out var wait) ||
                TryParsePrefixedFloat(inner, "wait=", out wait))
            {
                FlushText(buffer, tokens);
                tokens.Add(new DialogueWaitToken { Seconds = Math.Max(0f, wait) });
                return true;
            }

            // 速度: [speed=倍率]
            if (TryParsePrefixedFloat(inner, "speed=", out var scale))
            {
                FlushText(buffer, tokens);
                tokens.Add(new DialogueSpeedToken { Scale = scale <= 0f ? 1f : scale });
                return true;
            }

            return false;
        }

        private static bool TryParsePrefixedFloat(string inner, string prefix, out float value)
        {
            value = 0f;
            if (!inner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            return float.TryParse(inner.Substring(prefix.Length),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void FlushText(StringBuilder buffer, List<DialogueInlineToken> tokens)
        {
            if (buffer.Length == 0) return;
            tokens.Add(new DialogueTextToken { Text = buffer.ToString() });
            buffer.Length = 0;
        }
    }

    public abstract class DialogueInlineToken { }

    public sealed class DialogueTextToken : DialogueInlineToken
    {
        public string Text;
    }

    public sealed class DialogueRubyToken : DialogueInlineToken
    {
        public string Base;
        public string Ruby;
    }

    public sealed class DialogueColorToken : DialogueInlineToken
    {
        public string Color;
        public bool Push;
    }

    public sealed class DialogueWaitToken : DialogueInlineToken
    {
        public float Seconds;
    }

    public sealed class DialogueSpeedToken : DialogueInlineToken
    {
        public float Scale;
    }

    public enum DialogueInlineCommandKind
    {
        Wait,
        Speed
    }

    /// <summary>タイプライター進行に作用する位置つきコマンド（指定の可視文字数に達した時点で適用）。</summary>
    public sealed class DialogueInlineCommand
    {
        public DialogueInlineCommandKind Kind;
        public float Value;          // Wait: 秒, Speed: 速度倍率
        public int VisibleCharIndex; // この可視文字数を表示した時点で適用
    }

    /// <summary>
    /// インラインタグを解釈した結果。TMP へ渡す表示文字列と、タイプライター用の位置つきコマンド列を持つ。
    /// </summary>
    public sealed class DialogueInlineText
    {
        public DialogueInlineText(string displayText, IReadOnlyList<DialogueInlineCommand> commands)
        {
            DisplayText = displayText ?? string.Empty;
            Commands = commands ?? new List<DialogueInlineCommand>();
        }

        /// <summary>TMP リッチテキスト（ルビ・色を含む）。</summary>
        public string DisplayText { get; private set; }

        /// <summary>可視文字インデックス順のウェイト/速度コマンド。</summary>
        public IReadOnlyList<DialogueInlineCommand> Commands { get; private set; }

        public static DialogueInlineText Build(string raw)
        {
            var tokens = DialogueInlineTagParser.Parse(raw);
            var sb = new StringBuilder();
            var commands = new List<DialogueInlineCommand>();
            var visible = 0;

            foreach (var token in tokens)
            {
                switch (token)
                {
                    case DialogueTextToken t:
                        sb.Append(t.Text);
                        visible += VisibleLength(t.Text);
                        break;
                    case DialogueRubyToken r:
                        sb.Append(DialogueInlineMarkup.Ruby(r.Base, r.Ruby));
                        // 親文字とルビの両方が TMP の可視グリフとして数えられる。
                        visible += VisibleLength(r.Base) + VisibleLength(r.Ruby);
                        break;
                    case DialogueColorToken c:
                        sb.Append(c.Push ? "<color=" + c.Color + ">" : "</color>");
                        break;
                    case DialogueWaitToken w:
                        commands.Add(new DialogueInlineCommand
                        {
                            Kind = DialogueInlineCommandKind.Wait,
                            Value = w.Seconds,
                            VisibleCharIndex = visible
                        });
                        break;
                    case DialogueSpeedToken s:
                        commands.Add(new DialogueInlineCommand
                        {
                            Kind = DialogueInlineCommandKind.Speed,
                            Value = s.Scale,
                            VisibleCharIndex = visible
                        });
                        break;
                }
            }

            return new DialogueInlineText(sb.ToString(), commands);
        }

        private static int VisibleLength(string s)
        {
            return string.IsNullOrEmpty(s) ? 0 : s.Length;
        }
    }

    /// <summary>
    /// ルビの TMP リッチテキスト変換。TMP にネイティブのルビ機能は無いため近似表現を用いる。
    /// 正確な位置合わせはプロジェクト要件に依存するため、<see cref="Formatter"/> で差し替え可能。
    /// </summary>
    public static class DialogueInlineMarkup
    {
        /// <summary>(親文字, ルビ) -> TMP リッチテキスト。差し替えてプロジェクト独自のルビ表現にできる。</summary>
        public static Func<string, string, string> Formatter = DefaultRuby;

        public static string Ruby(string baseText, string ruby)
        {
            baseText = baseText ?? string.Empty;
            ruby = ruby ?? string.Empty;
            var formatter = Formatter ?? DefaultRuby;
            return formatter(baseText, ruby);
        }

        // 既定: ルビを小サイズで親文字の上に乗せる近似。親文字は素のまま残すのでタイプライター表示に乗る。
        private static string DefaultRuby(string baseText, string ruby)
        {
            if (string.IsNullOrEmpty(ruby)) return baseText;
            return "<voffset=1em><size=50%>" + ruby + "</size></voffset><space=-" +
                   ruby.Length.ToString(CultureInfo.InvariantCulture) + "em>" + baseText;
        }
    }
}
