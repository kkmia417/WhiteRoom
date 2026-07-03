using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public static class CsvLoader
    {
        public static Dictionary<int, T> Parse<T>(TextAsset csv) where T : DialogueData, new()
        {
            if (csv == null)
            {
                Debug.LogError("CsvLoader: csvファイルが null です。");
                return new Dictionary<int, T>();
            }

            return ParseText<T>(csv.text);
        }

        public static Dictionary<int, T> ParseText<T>(string csvText) where T : DialogueData, new()
        {
            var report = new DialogueValidationReport();
            var document = DialogueCsvCodec.Parse(csvText);
            report.AddRange(document.Diagnostics.Messages);

            var rows = ParseRows<T>(document, report);
            var dict = new Dictionary<int, T>();

            foreach (var row in rows)
            {
                if (dict.ContainsKey(row.Id))
                {
                    Debug.LogWarning($"CsvLoader: 重複するIDが見つかりました ({row.Id})。最初の行を維持します。");
                    continue;
                }

                dict.Add(row.Id, row);
            }

            foreach (var message in report.Messages)
            {
                if (message.Severity == DialogueValidationSeverity.Error)
                    Debug.LogError("CsvLoader: " + message);
                else if (message.Severity == DialogueValidationSeverity.Warning)
                    Debug.LogWarning("CsvLoader: " + message);
            }

            return dict;
        }

        internal static List<T> ParseRows<T>(DialogueCsvDocument document, DialogueValidationReport report) where T : DialogueData, new()
        {
            var result = new List<T>();
            if (document == null || document.Headers == null || document.Headers.Count == 0)
            {
                report?.Add(DialogueValidationSeverity.Error, 1, string.Empty, "CSV header row is missing.");
                return result;
            }

            var map = DialogueSchema.BuildHeaderMap(document.Headers);

            foreach (var row in document.Rows)
            {
                var values = row.Values;
                if (IsCommentOrBlank(values))
                    continue;

                try
                {
                    var data = new T
                    {
                        RowNumber = row.RowNumber,
                        Id = ParseInt(values, map, DialogueSchema.Id, 0, -1),
                        Speaker = Get(values, map, DialogueSchema.Speaker, 1),
                        Text = Get(values, map, DialogueSchema.Text, 2),
                        NextId = ParseInt(values, map, DialogueSchema.NextId, 3, -1),
                        EmotionKey = Get(values, map, DialogueSchema.EmotionKey, 4),
                        TriggerKey = Get(values, map, DialogueSchema.TriggerKey, 5),
                        ConditionKey = Get(values, map, DialogueSchema.ConditionKey, 6),
                        EventKey = Get(values, map, DialogueSchema.EventKey, -1),
                        ChoicesRaw = Get(values, map, DialogueSchema.Choices, -1),
                        AutoNextSeconds = ParseFloat(values, map, DialogueSchema.AutoNextSeconds, -1, -1f),
                        Background = Get(values, map, DialogueSchema.Background, -1),
                        Bgm = Get(values, map, DialogueSchema.Bgm, -1),
                        Se = Get(values, map, DialogueSchema.Se, -1),
                        Voice = Get(values, map, DialogueSchema.Voice, -1),
                        CharactersRaw = Get(values, map, DialogueSchema.Characters, -1),
                        ChapterKey = Get(values, map, DialogueSchema.ChapterKey, -1),
                        RouteKey = Get(values, map, DialogueSchema.RouteKey, -1),
                        EndingKey = Get(values, map, DialogueSchema.EndingKey, -1)
                    };

                    result.Add(data);
                }
                catch (Exception e)
                {
                    report?.Add(DialogueValidationSeverity.Error, row.RowNumber, string.Empty, e.Message);
                }
            }

            return result;
        }

        private static bool IsCommentOrBlank(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return true;
            if (values.Count == 1 && string.IsNullOrWhiteSpace(values[0])) return true;
            return values[0] != null && values[0].TrimStart().StartsWith("#");
        }

        private static string Get(IReadOnlyList<string> values, Dictionary<string, int> map, string name, int fallbackIndex)
        {
            int index;
            if (!map.TryGetValue(name, out index))
                index = fallbackIndex;

            if (index < 0 || index >= values.Count)
                return string.Empty;

            return values[index] ?? string.Empty;
        }

        private static int ParseInt(IReadOnlyList<string> values, Dictionary<string, int> map, string name, int fallbackIndex, int defaultValue)
        {
            var raw = Get(values, map, name, fallbackIndex);
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            int result;
            if (int.TryParse(raw, out result))
                return result;

            throw new FormatException(name + " の数値変換に失敗しました: " + raw);
        }

        private static float ParseFloat(IReadOnlyList<string> values, Dictionary<string, int> map, string name, int fallbackIndex, float defaultValue)
        {
            var raw = Get(values, map, name, fallbackIndex);
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            float result;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;

            throw new FormatException(name + " の数値変換に失敗しました: " + raw);
        }
    }
}
