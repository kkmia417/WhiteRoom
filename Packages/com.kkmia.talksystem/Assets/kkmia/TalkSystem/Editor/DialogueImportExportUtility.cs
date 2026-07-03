using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    [Serializable]
    public sealed class DialogueJsonDatabase
    {
        public List<DialogueJsonRow> rows = new List<DialogueJsonRow>();
    }

    [Serializable]
    public sealed class DialogueJsonRow
    {
        public int id;
        public string speaker;
        public string text;
        public int nextId = -1;
        public string emotionKey;
        public string triggerKey;
        public string conditionKey;
        public string eventKey;
        public string choices;
        public float autoNextSeconds = -1f;
        public string background;
        public string bgm;
        public string se;
        public string voice;
        public string characters;
        public string chapterKey;
        public string routeKey;
        public string endingKey;
    }

    public static class DialogueImportExportUtility
    {
        public static string CsvToJson(string csvText, bool prettyPrint = true)
        {
            var database = new DialogueJsonDatabase();
            foreach (var row in CsvLoader.ParseText<DialogueData>(csvText).Values.OrderBy(d => d.Id))
            {
                database.rows.Add(new DialogueJsonRow
                {
                    id = row.Id,
                    speaker = row.Speaker,
                    text = row.Text,
                    nextId = row.NextId,
                    emotionKey = row.EmotionKey,
                    triggerKey = row.TriggerKey,
                    conditionKey = row.ConditionKey,
                    eventKey = row.EventKey,
                    choices = row.ChoicesRaw,
                    autoNextSeconds = row.AutoNextSeconds,
                    background = row.Background,
                    bgm = row.Bgm,
                    se = row.Se,
                    voice = row.Voice,
                    characters = row.CharactersRaw,
                    chapterKey = row.ChapterKey,
                    routeKey = row.RouteKey,
                    endingKey = row.EndingKey
                });
            }

            return JsonUtility.ToJson(database, prettyPrint);
        }

        public static string JsonToCsv(string jsonText)
        {
            var database = JsonUtility.FromJson<DialogueJsonDatabase>(jsonText);
            var rows = database != null && database.rows != null
                ? database.rows.OrderBy(r => r.id).Select(ToCsvRow)
                : Enumerable.Empty<IReadOnlyList<string>>();

            return DialogueCsvCodec.Write(DialogueSchema.FullHeaders, rows);
        }

        public static string ExportTranslationCsv(string scenarioCsv, IEnumerable<string> languageKeys, string existingTranslationCsv = null)
        {
            var languages = NormalizeTranslationLanguages(languageKeys);
            var existing = string.IsNullOrEmpty(existingTranslationCsv)
                ? null
                : DialogueTranslationTable.FromCsv(existingTranslationCsv);

            var headers = new List<string> { DialogueSchema.Id, DialogueSchema.Speaker, "Source" };
            headers.AddRange(languages);

            var rows = CsvLoader.ParseText<DialogueData>(scenarioCsv).Values
                .OrderBy(row => row.Id)
                .Select(row =>
                {
                    var values = new List<string>
                    {
                        row.Id.ToString(),
                        row.Speaker ?? string.Empty,
                        row.Text ?? string.Empty
                    };

                    foreach (var language in languages)
                    {
                        string text;
                        values.Add(existing != null && existing.TryGet(row.Id, language, out text)
                            ? text ?? string.Empty
                            : string.Empty);
                    }

                    return values;
                });

            return DialogueCsvCodec.Write(headers, rows);
        }

        public static string YarnLikeToCsv(string scriptText)
        {
            var rows = new List<IReadOnlyList<string>>();
            var id = 1;
            var currentId = 1;
            var nextId = -1;
            var pendingChoices = new List<string>();

            using (var reader = new StringReader(scriptText ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//")) continue;

                    if (trimmed.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(trimmed.Substring("title:".Length).Trim(), out currentId);
                        id = Math.Max(id, currentId);
                        continue;
                    }

                    if (trimmed.StartsWith("->"))
                    {
                        var target = trimmed.Substring(2).Trim();
                        pendingChoices.Add(target + "->" + ExtractTargetId(target));
                        continue;
                    }

                    var separator = trimmed.IndexOf(':');
                    if (separator <= 0) continue;

                    var speaker = trimmed.Substring(0, separator).Trim();
                    var text = trimmed.Substring(separator + 1).Trim();
                    nextId = pendingChoices.Count > 0 ? -1 : currentId + 1;
                    var choices = pendingChoices.Count > 0 ? string.Join("|", pendingChoices.ToArray()) : string.Empty;
                    rows.Add(new[]
                    {
                        currentId.ToString(),
                        speaker,
                        text,
                        nextId.ToString(),
                        string.Empty,
                        currentId == 1 ? "Start" : string.Empty,
                        string.Empty,
                        string.Empty,
                        choices,
                        string.Empty
                    });

                    pendingChoices.Clear();
                    currentId++;
                    id = Math.Max(id, currentId);
                }
            }

            return DialogueCsvCodec.Write(DialogueSchema.ExtendedHeaders, rows);
        }

        public static void WriteTextAsset(string path, string contents)
        {
            File.WriteAllText(path, contents ?? string.Empty, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static IReadOnlyList<string> ToCsvRow(DialogueJsonRow row)
        {
            return new[]
            {
                row.id.ToString(),
                row.speaker ?? string.Empty,
                row.text ?? string.Empty,
                row.nextId >= 0 ? row.nextId.ToString() : "-1",
                row.emotionKey ?? string.Empty,
                row.triggerKey ?? string.Empty,
                row.conditionKey ?? string.Empty,
                row.eventKey ?? string.Empty,
                row.choices ?? string.Empty,
                row.autoNextSeconds >= 0f ? row.autoNextSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                row.background ?? string.Empty,
                row.bgm ?? string.Empty,
                row.se ?? string.Empty,
                row.voice ?? string.Empty,
                row.characters ?? string.Empty,
                row.chapterKey ?? string.Empty,
                row.routeKey ?? string.Empty,
                row.endingKey ?? string.Empty
            };
        }

        private static List<string> NormalizeTranslationLanguages(IEnumerable<string> languageKeys)
        {
            var languages = new List<string>();
            if (languageKeys == null)
                return languages;

            foreach (var languageKey in languageKeys)
            {
                if (string.IsNullOrWhiteSpace(languageKey))
                    continue;

                var language = languageKey.Trim();
                if (!DialogueTranslationTable.IsMetadataHeader(language) && !languages.Contains(language))
                    languages.Add(language);
            }

            return languages;
        }

        private static int ExtractTargetId(string value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            int result;
            return int.TryParse(digits, out result) ? result : -1;
        }
    }
}
