using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 翻訳CSV（多言語テキスト）の検証。言語列の欠落・翻訳漏れ・重複・変数プレースホルダの
    /// 不一致を報告する。<see cref="DialogueValidator"/> のプロファイル検証から呼ばれる下位モジュール。
    /// </summary>
    internal static class DialogueLocalizationValidator
    {
        private sealed class LocalizationValue
        {
            public int RowNumber;
            public string Text;
        }

        private sealed class LocalizationEntry
        {
            public readonly Dictionary<string, LocalizationValue> Values = new Dictionary<string, LocalizationValue>();
        }

        internal static void Validate(IList<DialogueData> rows, DialogueValidationProfile profile, DialogueValidationReport report)
        {
            if (profile == null || profile.TranslationCsvFiles == null || profile.TranslationCsvFiles.Count == 0)
                return;

            var languages = NormalizeLanguages(profile.LocalizationLanguageKeys);
            if (languages.Count == 0)
            {
                report.Add(DialogueValidationSeverity.Warning, 0, DialogueSchema.Localization,
                    "Translation CSV files are configured, but no localization language keys are configured.");
                return;
            }

            var scenarioById = new Dictionary<int, DialogueData>();
            foreach (var row in rows)
            {
                if (row != null && row.Id > 0 && !scenarioById.ContainsKey(row.Id))
                    scenarioById.Add(row.Id, row);
            }

            var entries = new Dictionary<int, LocalizationEntry>();
            var availableLanguages = new HashSet<string>();
            var severity = profile.LocalizationSeverity;

            foreach (var translationFile in profile.TranslationCsvFiles)
            {
                if (translationFile == null)
                {
                    report.Add(DialogueValidationSeverity.Error, 0, DialogueSchema.Localization,
                        "Dialogue validation profile contains a missing translation CSV file reference.");
                    continue;
                }

                var document = DialogueCsvCodec.Parse(translationFile.text);
                report.AddRange(document.Diagnostics.Messages);
                MergeTranslationDocument(document, scenarioById, entries, availableLanguages, severity, report);
            }

            foreach (var language in languages)
            {
                if (!availableLanguages.Contains(language))
                    report.Add(severity, 1, DialogueSchema.Localization,
                        "Translation CSV is missing language column \"" + language + "\".");
            }

            foreach (var row in rows)
            {
                if (row == null || row.Id <= 0)
                    continue;

                LocalizationEntry entry;
                entries.TryGetValue(row.Id, out entry);

                foreach (var language in languages)
                    ValidateLocalizedText(row, entry, language, profile.FallbackLanguageKey, profile.VariableCatalog, severity, report);
            }
        }

        private static List<string> NormalizeLanguages(IReadOnlyList<string> languageKeys)
        {
            var result = new List<string>();
            if (languageKeys == null)
                return result;

            for (var i = 0; i < languageKeys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(languageKeys[i]))
                    continue;

                var language = languageKeys[i].Trim();
                if (!result.Contains(language))
                    result.Add(language);
            }

            return result;
        }

        private static void MergeTranslationDocument(DialogueCsvDocument document, Dictionary<int, DialogueData> scenarioById,
            Dictionary<int, LocalizationEntry> entries, HashSet<string> availableLanguages,
            DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            if (document.Headers == null || document.Headers.Count == 0)
            {
                report.Add(severity, 1, DialogueSchema.Localization, "Translation CSV header row is missing.");
                return;
            }

            var headerMap = DialogueSchema.BuildHeaderMap(document.Headers);
            int idColumn;
            if (!headerMap.TryGetValue(DialogueSchema.Id, out idColumn))
            {
                report.Add(severity, 1, DialogueSchema.Id, "Translation CSV requires an Id column.");
                return;
            }

            foreach (var row in document.Rows)
            {
                var values = row.Values;
                if (values == null || values.Count <= idColumn || string.IsNullOrWhiteSpace(values[idColumn]))
                    continue;

                int id;
                if (!int.TryParse(values[idColumn], out id))
                {
                    report.Add(severity, row.RowNumber, DialogueSchema.Id, "Translation Id must be an integer.");
                    continue;
                }

                if (!scenarioById.ContainsKey(id))
                    report.Add(severity, row.RowNumber, DialogueSchema.Localization,
                        "Translation Id " + id + " does not exist in scenario data.");

                LocalizationEntry entry;
                if (!entries.TryGetValue(id, out entry))
                {
                    entry = new LocalizationEntry();
                    entries.Add(id, entry);
                }

                for (var column = 0; column < document.Headers.Count; column++)
                {
                    var header = document.Headers[column];
                    if (DialogueTranslationTable.IsMetadataHeader(header))
                        continue;

                    var language = header.Trim();
                    availableLanguages.Add(language);
                    var text = column < values.Count ? values[column] : string.Empty;

                    if (entry.Values.ContainsKey(language) && !string.IsNullOrWhiteSpace(text))
                        report.Add(DialogueValidationSeverity.Warning, row.RowNumber, DialogueSchema.Localization,
                            "Duplicate translation for Id " + id + " language \"" + language + "\"; the later value will be used.");

                    entry.Values[language] = new LocalizationValue
                    {
                        RowNumber = row.RowNumber,
                        Text = text ?? string.Empty
                    };
                }
            }
        }

        private static void ValidateLocalizedText(DialogueData source, LocalizationEntry entry, string language,
            string fallbackLanguage, DialogueKeyCatalog variableCatalog,
            DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            LocalizationValue value = null;
            var hasText = false;
            if (entry != null && entry.Values.TryGetValue(language, out value))
                hasText = !string.IsNullOrWhiteSpace(value.Text);

            if (!hasText)
            {
                report.Add(severity, source.RowNumber, DialogueSchema.Localization,
                    "Missing translation for Id " + source.Id + " language \"" + language + "\".");

                if (!string.IsNullOrWhiteSpace(fallbackLanguage) && fallbackLanguage != language && entry != null)
                {
                    LocalizationValue fallbackValue;
                    if (entry.Values.TryGetValue(fallbackLanguage, out fallbackValue) &&
                        !string.IsNullOrWhiteSpace(fallbackValue.Text))
                    {
                        report.Add(DialogueValidationSeverity.Info, source.RowNumber, DialogueSchema.Localization,
                            "Id " + source.Id + " language \"" + language + "\" will use fallback language \"" + fallbackLanguage + "\".");
                    }
                }

                return;
            }

            ValidateLocalizedVariables(source, language, value, variableCatalog, severity, report);
        }

        private static void ValidateLocalizedVariables(DialogueData source, string language, LocalizationValue value,
            DialogueKeyCatalog variableCatalog, DialogueValidationSeverity severity, DialogueValidationReport report)
        {
            var sourceVariables = DialogueValidator.ExtractVariableNames(source.Text);
            var localizedVariables = DialogueValidator.ExtractVariableNames(value.Text);

            foreach (var variable in sourceVariables)
            {
                if (!localizedVariables.Contains(variable))
                    report.Add(severity, value.RowNumber, DialogueSchema.Localization,
                        "Variable placeholder \"" + variable + "\" from source Id " + source.Id + " is missing in language \"" + language + "\".");
            }

            foreach (var variable in localizedVariables)
            {
                if (!sourceVariables.Contains(variable) && (variableCatalog == null || !variableCatalog.Contains(variable)))
                    report.Add(severity, value.RowNumber, DialogueSchema.Localization,
                        "Variable placeholder \"" + variable + "\" in language \"" + language + "\" is not present in source text or the variable catalog.");
            }
        }
    }
}
