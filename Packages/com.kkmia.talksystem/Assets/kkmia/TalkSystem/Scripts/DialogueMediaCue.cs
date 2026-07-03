using System;
using System.Globalization;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 背景・BGM 列の 1 セルを表す解析済みキュー。
    /// 記法は <c>key</c> / <c>key#transition</c> / <c>key#transition:duration</c>。
    /// <c>stop</c>・<c>none</c>・<c>hide</c>・<c>clear</c> は対象をクリアする指示として扱う。
    /// 値の表示・トランジションの実体は Phase 2 以降の演出層が解釈する。ここはデータの正規化のみを担う。
    /// </summary>
    public readonly struct DialogueMediaCue
    {
        public static readonly DialogueMediaCue None = new DialogueMediaCue(string.Empty, false, string.Empty, float.NaN);

        public DialogueMediaCue(string key, bool isClear, string transition, float duration)
        {
            Key = key ?? string.Empty;
            IsClear = isClear;
            Transition = transition ?? string.Empty;
            Duration = duration;
        }

        /// <summary>表示するアセットキー。変更なし・クリア時は空。</summary>
        public string Key { get; }

        /// <summary>stop/none/hide/clear 指定。対象を消すことを表す。</summary>
        public bool IsClear { get; }

        /// <summary>トランジション名（fade / crossfade など）。未指定は空＝カット。</summary>
        public string Transition { get; }

        /// <summary>トランジション秒数。未指定は NaN（負値は指定済みだが不正を表す）。</summary>
        public float Duration { get; }

        /// <summary>この行で背景/BGM に対する変更があるか。</summary>
        public bool HasValue
        {
            get { return IsClear || !string.IsNullOrEmpty(Key); }
        }

        public bool HasDuration
        {
            get { return !float.IsNaN(Duration); }
        }

        /// <summary>
        /// セル文字列を解析します。空・空白のみは <see cref="None"/> を返します。
        /// 期間が数値でない場合は <paramref name="durationMalformed"/> を true にして Duration=NaN とします。
        /// </summary>
        public static DialogueMediaCue Parse(string raw, out bool durationMalformed)
        {
            durationMalformed = false;
            var value = (raw ?? string.Empty).Trim();
            if (value.Length == 0)
                return None;

            var transition = string.Empty;
            var duration = float.NaN;

            var hashIndex = value.IndexOf('#');
            if (hashIndex >= 0)
            {
                var modifier = value.Substring(hashIndex + 1).Trim();
                value = value.Substring(0, hashIndex).Trim();

                var colonIndex = modifier.IndexOf(':');
                if (colonIndex >= 0)
                {
                    var durationText = modifier.Substring(colonIndex + 1).Trim();
                    transition = modifier.Substring(0, colonIndex).Trim();

                    float parsed;
                    if (float.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                        duration = parsed;
                    else if (durationText.Length > 0)
                        durationMalformed = true;
                }
                else
                {
                    transition = modifier;
                }
            }

            if (IsClearKeyword(value))
                return new DialogueMediaCue(string.Empty, true, transition, duration);

            return new DialogueMediaCue(value, false, transition, duration);
        }

        public static DialogueMediaCue Parse(string raw)
        {
            bool _;
            return Parse(raw, out _);
        }

        private static bool IsClearKeyword(string value)
        {
            return string.Equals(value, "stop", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "hide", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "clear", StringComparison.OrdinalIgnoreCase);
        }
    }
}
