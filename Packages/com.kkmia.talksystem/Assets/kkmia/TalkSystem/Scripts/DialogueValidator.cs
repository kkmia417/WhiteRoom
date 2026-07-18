using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public static class DialogueValidator
    {
        public static DialogueValidationReport ValidateCsv(string csvText, IEnumerable<int> entryIds = null)
        {
            var document = DialogueCsvCodec.Parse(csvText);
            var report = new DialogueValidationReport();
            report.AddRange(document.Diagnostics.Messages);

            ValidateHeaders(document.Headers, report);

            var rows = CsvLoader.ParseRows<DialogueData>(document, report);
            ValidateData(rows, report, entryIds);
            return report;
        }

        public static DialogueValidationReport ValidateCsv(string csvText, IEnumerable<int> entryIds, DialogueValidationProfile profile)
        {
            var document = DialogueCsvCodec.Parse(csvText);
            var report = new DialogueValidationReport();
            report.AddRange(document.Diagnostics.Messages);

            ValidateHeaders(document.Headers, report);

            var rows = CsvLoader.ParseRows<DialogueData>(document, report);
            ValidateData(rows, report, entryIds);
            ValidateAssets(rows, profile, report);
            return report;
        }

        public static DialogueValidationReport ValidateData(IEnumerable<DialogueData> data, IEnumerable<int> entryIds = null)
        {
            var report = new DialogueValidationReport();
            ValidateData(data, report, entryIds);
            return report;
        }

        public static DialogueValidationReport ValidateData(IEnumerable<DialogueData> data, IEnumerable<int> entryIds, DialogueValidationProfile profile)
        {
            var report = new DialogueValidationReport();
            var rows = data != null ? data.ToList() : new List<DialogueData>();
            ValidateData(rows, report, entryIds);
            ValidateAssets(rows, profile, report);
            return report;
        }

        public static DialogueValidationReport ValidateAssets(IEnumerable<DialogueData> data, DialogueValidationProfile profile)
        {
            var report = new DialogueValidationReport();
            ValidateAssets(data, profile, report);
            return report;
        }

        private static void ValidateHeaders(IReadOnlyList<string> headers, DialogueValidationReport report)
        {
            if (headers == null || headers.Count == 0)
            {
                report.Add(DialogueValidationSeverity.Error, 1, string.Empty, "CSV header row is missing.");
                return;
            }

            foreach (var required in DialogueSchema.DefaultHeaders)
            {
                if (!headers.Any(h => h == required))
                    report.Add(DialogueValidationSeverity.Error, 1, required, "Required header is missing.");
            }
        }

        private static void ValidateData(IEnumerable<DialogueData> data, DialogueValidationReport report, IEnumerable<int> entryIds)
        {
            var rows = data != null ? data.ToList() : new List<DialogueData>();
            var byId = new Dictionary<int, DialogueData>();
            var seen = new Dictionary<int, int>();
            var triggerKeys = new Dictionary<string, int>();

            foreach (var row in rows)
            {
                if (row.Id <= 0)
                    report.Add(DialogueValidationSeverity.Error, row.RowNumber, DialogueSchema.Id, "Id must be a positive integer.");

                if (seen.ContainsKey(row.Id))
                    report.Add(DialogueValidationSeverity.Error, row.RowNumber, DialogueSchema.Id, "Duplicate Id also appears at row " + seen[row.Id] + ".");
                else
                    seen[row.Id] = row.RowNumber;

                if (!byId.ContainsKey(row.Id))
                    byId.Add(row.Id, row);

                if (string.IsNullOrWhiteSpace(row.Speaker))
                    report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Speaker, "Speaker is empty.");

                if (string.IsNullOrWhiteSpace(row.Text))
                    report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Text, "Text is empty.");

                if (!string.IsNullOrEmpty(row.TriggerKey))
                {
                    if (triggerKeys.ContainsKey(row.TriggerKey))
                        report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.TriggerKey, "TriggerKey is duplicated with row " + triggerKeys[row.TriggerKey] + ".");
                    else
                        triggerKeys.Add(row.TriggerKey, row.RowNumber);
                }

                ValidateChoiceSyntax(row, report);
                ValidateStageSyntax(row, report);
                ValidateMediaSyntax(row, report);
            }

            foreach (var row in rows)
            {
                if (row.NextId >= 0 && !byId.ContainsKey(row.NextId))
                    report.Add(DialogueValidationSeverity.Error, row.RowNumber, DialogueSchema.NextId, "NextId " + row.NextId + " does not exist.");

                var choices = row.GetChoices();
                foreach (var choice in choices)
                {
                    if (choice.NextId >= 0 && !byId.ContainsKey(choice.NextId))
                        report.Add(DialogueValidationSeverity.Error, row.RowNumber, DialogueSchema.Choices, "Choice target " + choice.NextId + " does not exist.");
                }
            }

            ValidateReachability(rows, byId, report, entryIds);
            DetectCycles(rows, byId, report);
        }

        private static void ValidateChoiceSyntax(DialogueData row, DialogueValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(row.ChoicesRaw))
                return;

            foreach (var entry in row.ChoicesRaw.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                DialogueChoice parsed;
                if (!DialogueChoice.TryParseEntry(entry, out parsed))
                    report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Choices,
                        "Choice entry could not be parsed and was ignored: \"" + entry.Trim() + "\".");
            }
        }

        private static void ValidateStageSyntax(DialogueData row, DialogueValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(row.CharactersRaw))
                return;

            foreach (var entry in row.CharactersRaw.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                DialogueStageDirective parsed;
                if (!DialogueStageDirective.TryParseEntry(entry, out parsed))
                    report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Characters,
                        "Character directive could not be parsed and was ignored: \"" + entry.Trim() + "\".");
            }
        }

        private static void ValidateMediaSyntax(DialogueData row, DialogueValidationReport report)
        {
            ValidateMediaCell(row.Background, DialogueSchema.Background, row.RowNumber, report);
            ValidateMediaCell(row.Bgm, DialogueSchema.Bgm, row.RowNumber, report);
        }

        private static void ValidateMediaCell(string raw, string field, int rowNumber, DialogueValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            bool durationMalformed;
            var cue = DialogueMediaCue.Parse(raw, out durationMalformed);

            if (durationMalformed)
                report.Add(DialogueValidationSeverity.Warning, rowNumber, field,
                    "Transition duration is not a number and was ignored: \"" + raw.Trim() + "\".");

            if (cue.HasDuration && cue.Duration < 0f)
                report.Add(DialogueValidationSeverity.Warning, rowNumber, field,
                    "Transition duration must not be negative: \"" + raw.Trim() + "\".");
        }

        private static void ValidateReachability(List<DialogueData> rows, Dictionary<int, DialogueData> byId, DialogueValidationReport report, IEnumerable<int> entryIds)
        {
            var starts = entryIds != null ? entryIds.Where(byId.ContainsKey).ToList() : rows.Where(r => r.HasTriggerKey).Select(r => r.Id).ToList();
            if (starts.Count == 0 && rows.Count > 0)
                starts.Add(rows[0].Id);

            var reachable = new HashSet<int>();
            var stack = new Stack<int>(starts);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!reachable.Add(id)) continue;

                DialogueData row;
                if (!byId.TryGetValue(id, out row)) continue;

                if (row.NextId >= 0)
                    stack.Push(row.NextId);

                foreach (var choice in row.GetChoices())
                    if (choice.NextId >= 0)
                        stack.Push(choice.NextId);
            }

            foreach (var row in rows)
            {
                if (!reachable.Contains(row.Id))
                    report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Id, "Row is unreachable from configured entry points.");
            }
        }

        private const int ColorVisiting = 1;
        private const int ColorDone = 2;

        // 反復DFS（三色マーキング）による循環検出。
        // 再帰だと長大なチェーンで StackOverflow になり得るため明示スタックで実装する。
        private static void DetectCycles(List<DialogueData> rows, Dictionary<int, DialogueData> byId, DialogueValidationReport report)
        {
            var color = new Dictionary<int, int>();
            var reported = new HashSet<int>();
            var stack = new Stack<int>();

            foreach (var startRow in rows)
            {
                var startId = startRow.Id;
                int startColor;
                if (color.TryGetValue(startId, out startColor) && startColor == ColorDone)
                    continue;

                stack.Push(startId);
                while (stack.Count > 0)
                {
                    var id = stack.Peek();
                    int state;
                    color.TryGetValue(id, out state);

                    if (state == ColorVisiting)
                    {
                        // 子の探索が完了したノード。完了（黒）にしてスタックから外す。
                        color[id] = ColorDone;
                        stack.Pop();
                        continue;
                    }

                    if (state == ColorDone)
                    {
                        stack.Pop();
                        continue;
                    }

                    // 未訪問（白）。訪問中（灰）にして後続を積む。
                    color[id] = ColorVisiting;

                    DialogueData row;
                    if (!byId.TryGetValue(id, out row))
                        continue;

                    PushSuccessor(stack, color, reported, byId, report, row.NextId);
                    foreach (var choice in row.GetChoices())
                        PushSuccessor(stack, color, reported, byId, report, choice.NextId);
                }
            }
        }

        private static void PushSuccessor(Stack<int> stack, Dictionary<int, int> color, HashSet<int> reported,
            Dictionary<int, DialogueData> byId, DialogueValidationReport report, int nextId)
        {
            if (nextId < 0) return;

            int nextColor;
            color.TryGetValue(nextId, out nextColor);

            if (nextColor == ColorVisiting)
            {
                // 訪問中ノードへの後退辺 = 循環。ノードごとに一度だけ報告する。
                if (reported.Add(nextId))
                {
                    DialogueData cycleRow;
                    if (byId.TryGetValue(nextId, out cycleRow))
                        report.Add(DialogueValidationSeverity.Info, cycleRow.RowNumber, DialogueSchema.NextId, "Cycle detected. Cycles are allowed but should be intentional.");
                }

                return;
            }

            if (nextColor == 0)
                stack.Push(nextId);
        }

        public static DialogueValidationReport ValidateCharacters(IEnumerable<DialogueData> data, CharacterExpressionDatabase database)
        {
            return ValidateCharacters(data, database, DialogueValidationSeverity.Warning);
        }

        public static DialogueValidationReport ValidateCharacters(IEnumerable<DialogueData> data, CharacterExpressionDatabase database, DialogueValidationSeverity severity)
        {
            var report = new DialogueValidationReport();
            if (database == null || data == null) return report;

            foreach (var row in data)
            {
                ValidateStageDirectiveCharacters(row, database, severity, report);

                CharacterDefinition character;
                if (!database.TryGetCharacter(row.Speaker, out character))
                {
                    report.Add(severity, row.RowNumber, DialogueSchema.Speaker, "Speaker is not found in the character expression database.");
                    continue;
                }

                Sprite sprite;
                if (!string.IsNullOrEmpty(row.EmotionKey) && !character.TryGetSprite(row.EmotionKey, out sprite))
                    report.Add(severity, row.RowNumber, DialogueSchema.EmotionKey, "EmotionKey is not found in the character expression database.");
            }

            return report;
        }

        private static readonly Regex VariablePattern = new Regex(@"\{([A-Za-z0-9_.-]+)\}", RegexOptions.Compiled);

        private static void ValidateAssets(IEnumerable<DialogueData> data, DialogueValidationProfile profile, DialogueValidationReport report)
        {
            if (profile == null || data == null) return;

            var rows = data as IList<DialogueData> ?? data.ToList();
            var severity = profile.MissingReferenceSeverity;

            if (profile.CharacterDatabase != null)
                report.AddRange(ValidateCharacters(rows, profile.CharacterDatabase, severity).Messages);

            ValidateCatalog(profile.EventKeyCatalog, DialogueSchema.EventKey, severity, report);
            ValidateCatalog(profile.ConditionKeyCatalog, DialogueSchema.ConditionKey, severity, report);
            ValidateCatalog(profile.VariableCatalog, "Variable", severity, report);
            ValidateCatalog(profile.ChapterKeyCatalog, DialogueSchema.ChapterKey, severity, report);
            ValidateCatalog(profile.RouteKeyCatalog, DialogueSchema.RouteKey, severity, report);
            ValidateCatalog(profile.EndingKeyCatalog, DialogueSchema.EndingKey, severity, report);

            foreach (var row in rows)
            {
                ValidateBackgroundReference(row, profile.BackgroundDatabase, severity, report);
                ValidateAudioReferences(row, profile.AudioDatabase, severity, report);
                ValidateCatalogReferences(row, profile.EventKeyCatalog, profile.ConditionKeyCatalog, profile.VariableCatalog,
                    profile.ChapterKeyCatalog, profile.RouteKeyCatalog, profile.EndingKeyCatalog, severity, report);
            }

            DialogueLocalizationValidator.Validate(rows, profile, report);
        }

        internal static HashSet<string> ExtractVariableNames(string text)
        {
            var variables = new HashSet<string>();
            if (string.IsNullOrEmpty(text))
                return variables;

            foreach (Match match in VariablePattern.Matches(text))
                variables.Add(match.Groups[1].Value);

            return variables;
        }

        private static void ValidateBackgroundReference(DialogueData row, BackgroundDatabase database, DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            if (database == null || string.IsNullOrWhiteSpace(row.Background))
                return;

            var cue = row.GetBackgroundCue();
            if (!cue.HasValue || cue.IsClear)
                return;

            Sprite sprite;
            if (!database.TryGetSprite(cue.Key, out sprite))
                report.Add(severity, row.RowNumber, DialogueSchema.Background, "Background key \"" + cue.Key + "\" is not found in the background database.");
        }

        private static void ValidateAudioReferences(DialogueData row, AudioDatabase database, DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            if (database == null)
                return;

            var bgm = row.GetBgmCue();
            if (bgm.HasValue && !bgm.IsClear)
            {
                AudioClip clip;
                if (!database.TryGetBgm(bgm.Key, out clip))
                    report.Add(severity, row.RowNumber, DialogueSchema.Bgm, "Bgm key \"" + bgm.Key + "\" is not found in the audio database.");
            }

            foreach (var seKey in row.GetSeKeys())
            {
                AudioClip clip;
                if (!database.TryGetSe(seKey, out clip))
                    report.Add(severity, row.RowNumber, DialogueSchema.Se, "Se key \"" + seKey + "\" is not found in the audio database.");
            }

            if (!string.IsNullOrWhiteSpace(row.Voice))
            {
                var voiceKey = row.Voice.Trim();
                AudioClip clip;
                if (!database.TryGetVoice(voiceKey, out clip))
                    report.Add(severity, row.RowNumber, DialogueSchema.Voice, "Voice key \"" + voiceKey + "\" is not found in the audio database.");
            }
        }

        private static void ValidateCatalogReferences(DialogueData row, DialogueKeyCatalog eventCatalog,
            DialogueKeyCatalog conditionCatalog, DialogueKeyCatalog variableCatalog,
            DialogueKeyCatalog chapterCatalog, DialogueKeyCatalog routeCatalog, DialogueKeyCatalog endingCatalog,
            DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            if (eventCatalog != null && !string.IsNullOrEmpty(row.EventKey) && !eventCatalog.Contains(row.EventKey))
                report.Add(severity, row.RowNumber, DialogueSchema.EventKey, "EventKey \"" + row.EventKey + "\" is not found in the event key catalog.");

            if (conditionCatalog != null && !string.IsNullOrEmpty(row.ConditionKey) && !conditionCatalog.Contains(row.ConditionKey))
                report.Add(severity, row.RowNumber, DialogueSchema.ConditionKey, "ConditionKey \"" + row.ConditionKey + "\" is not found in the condition key catalog.");

            if (conditionCatalog != null)
            {
                foreach (var choice in row.GetChoices())
                {
                    if (!string.IsNullOrEmpty(choice.ConditionKey) && !conditionCatalog.Contains(choice.ConditionKey))
                        report.Add(severity, row.RowNumber, DialogueSchema.Choices, "Choice ConditionKey \"" + choice.ConditionKey + "\" is not found in the condition key catalog.");
                }
            }

            if (chapterCatalog != null && !string.IsNullOrEmpty(row.ChapterKey) && !chapterCatalog.Contains(row.ChapterKey))
                report.Add(severity, row.RowNumber, DialogueSchema.ChapterKey, "ChapterKey \"" + row.ChapterKey + "\" is not found in the chapter key catalog.");

            if (routeCatalog != null && !string.IsNullOrEmpty(row.RouteKey) && !routeCatalog.Contains(row.RouteKey))
                report.Add(severity, row.RowNumber, DialogueSchema.RouteKey, "RouteKey \"" + row.RouteKey + "\" is not found in the route key catalog.");

            if (endingCatalog != null && !string.IsNullOrEmpty(row.EndingKey) && !endingCatalog.Contains(row.EndingKey))
                report.Add(severity, row.RowNumber, DialogueSchema.EndingKey, "EndingKey \"" + row.EndingKey + "\" is not found in the ending key catalog.");

            if (variableCatalog == null || string.IsNullOrEmpty(row.Text))
                return;

            foreach (Match match in VariablePattern.Matches(row.Text))
            {
                var variableName = match.Groups[1].Value;
                if (!variableCatalog.Contains(variableName))
                    report.Add(severity, row.RowNumber, "Variable", "Variable \"" + variableName + "\" is not found in the variable catalog.");
            }
        }

        private static void ValidateCatalog(DialogueKeyCatalog catalog, string fieldName, DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            if (catalog == null) return;

            var seen = new Dictionary<string, int>();
            var entries = catalog.Keys;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                var key = entry.key.Trim();
                if (seen.ContainsKey(key))
                    report.Add(severity, 0, fieldName, "Catalog key \"" + key + "\" is duplicated.");
                else
                    seen.Add(key, i);
            }
        }

        private static void ValidateStageDirectiveCharacters(DialogueData row, CharacterExpressionDatabase database,
            DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            foreach (var directive in row.GetStageDirectives())
            {
                // クリア/退場はキャラクターの実在を要求しない（既にステージから外す指示のため）。
                if (directive.IsClearAll || directive.IsExit)
                    continue;

                CharacterDefinition character;
                if (!database.TryGetCharacter(directive.CharacterKey, out character))
                {
                    report.Add(severity, row.RowNumber, DialogueSchema.Characters,
                        "Character \"" + directive.CharacterKey + "\" is not found in the character expression database.");
                    continue;
                }

                Sprite sprite;
                if (directive.HasExpression && !character.TryGetSprite(directive.Expression, out sprite))
                    report.Add(severity, row.RowNumber, DialogueSchema.Characters,
                        "Expression \"" + directive.Expression + "\" for \"" + directive.CharacterKey + "\" is not found in the character expression database.");
            }
        }
    }
}
