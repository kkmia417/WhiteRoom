using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public static class DialogueSchema
    {
        public const string Id = "Id";
        public const string Speaker = "Speaker";
        public const string Text = "Text";
        public const string NextId = "NextId";
        public const string EmotionKey = "EmotionKey";
        public const string TriggerKey = "TriggerKey";
        public const string ConditionKey = "ConditionKey";
        public const string EventKey = "EventKey";
        public const string Choices = "Choices";
        public const string AutoNextSeconds = "AutoNextSeconds";

        // 演出（ステージ）列。すべて任意で、既存CSVとの後方互換を保つためヘッダー名で読み込む。
        public const string Background = "Background";
        public const string Bgm = "Bgm";
        public const string Se = "Se";
        public const string Voice = "Voice";
        public const string Characters = "Characters";
        public const string ChapterKey = "ChapterKey";
        public const string RouteKey = "RouteKey";
        public const string EndingKey = "EndingKey";
        public const string Localization = "Localization";

        public static readonly string[] DefaultHeaders =
        {
            Id,
            Speaker,
            Text,
            NextId,
            EmotionKey,
            TriggerKey,
            ConditionKey
        };

        public static readonly string[] ExtendedHeaders =
        {
            Id,
            Speaker,
            Text,
            NextId,
            EmotionKey,
            TriggerKey,
            ConditionKey,
            EventKey,
            Choices,
            AutoNextSeconds
        };

        // 演出列のみ。ExtendedHeaders に追記して FullHeaders を構成する。
        public static readonly string[] PresentationHeaders =
        {
            Background,
            Bgm,
            Se,
            Voice,
            Characters
        };

        public static readonly string[] ProgressHeaders =
        {
            ChapterKey,
            RouteKey,
            EndingKey
        };

        // 対話＋演出をすべて含む書き出し用ヘッダー。エディタ/インポートの round-trip はこれを使う。
        public static readonly string[] FullHeaders =
        {
            Id,
            Speaker,
            Text,
            NextId,
            EmotionKey,
            TriggerKey,
            ConditionKey,
            EventKey,
            Choices,
            AutoNextSeconds,
            Background,
            Bgm,
            Se,
            Voice,
            Characters,
            ChapterKey,
            RouteKey,
            EndingKey
        };

        // TalkSystem が意味を解釈する全ヘッダー。ここに無いヘッダーはユーザー独自の
        // 拡張カラムとして DialogueData.ExtraColumns に取り込まれる。
        private static readonly HashSet<string> KnownHeaderSet = BuildKnownHeaderSet();

        private static HashSet<string> BuildKnownHeaderSet()
        {
            var set = new HashSet<string>(FullHeaders, StringComparer.OrdinalIgnoreCase);
            set.Add(Localization);
            return set;
        }

        /// <summary>TalkSystem が解釈する既知ヘッダーかどうか（大文字小文字を区別しない）。</summary>
        public static bool IsKnownHeader(string header)
        {
            return !string.IsNullOrWhiteSpace(header) && KnownHeaderSet.Contains(header.Trim());
        }

        public static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return map;

            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header.Trim()))
                    map.Add(header.Trim(), i);
            }

            return map;
        }
    }
}
