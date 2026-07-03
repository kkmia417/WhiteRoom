using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 行 ID ごとに言語キー→テキストを保持する翻訳テーブル。Unity 型に依存しないためテスト可能。
    /// 翻訳 CSV（先頭列 Id、以降の各列が言語キー）から構築できる。
    /// </summary>
    public sealed class DialogueTranslationTable
    {
        private readonly Dictionary<int, Dictionary<string, string>> _byId =
            new Dictionary<int, Dictionary<string, string>>();

        /// <summary>翻訳テーブルに 1 件登録する。</summary>
        public void Add(int id, string languageKey, string text)
        {
            if (string.IsNullOrEmpty(languageKey)) return;

            Dictionary<string, string> byLanguage;
            if (!_byId.TryGetValue(id, out byLanguage))
            {
                byLanguage = new Dictionary<string, string>();
                _byId[id] = byLanguage;
            }

            byLanguage[languageKey] = text ?? string.Empty;
        }

        /// <summary>指定 ID・言語のテキストを取得する。無ければ false。</summary>
        public bool TryGet(int id, string languageKey, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(languageKey)) return false;

            Dictionary<string, string> byLanguage;
            return _byId.TryGetValue(id, out byLanguage) && byLanguage.TryGetValue(languageKey, out text);
        }

        public IReadOnlyList<int> Ids
        {
            get { return _byId.Keys.OrderBy(id => id).ToList(); }
        }

        public IReadOnlyList<string> LanguageKeys
        {
            get
            {
                var languages = new HashSet<string>();
                foreach (var byLanguage in _byId.Values)
                {
                    foreach (var language in byLanguage.Keys)
                    {
                        if (!string.IsNullOrEmpty(language))
                            languages.Add(language);
                    }
                }

                return languages.OrderBy(language => language).ToList();
            }
        }

        public bool HasNonEmptyText(int id, string languageKey)
        {
            string text;
            return TryGet(id, languageKey, out text) && !string.IsNullOrWhiteSpace(text);
        }

        /// <summary>
        /// 翻訳 CSV から構築する。1 列目ヘッダは Id、以降のヘッダ列が言語キー。
        /// </summary>
        public static DialogueTranslationTable FromCsv(string csvText)
        {
            var table = new DialogueTranslationTable();
            if (string.IsNullOrEmpty(csvText)) return table;

            var doc = DialogueCsvCodec.Parse(csvText);
            if (doc.Headers == null || doc.Headers.Count < 2)
                return table;

            for (var r = 0; r < doc.Rows.Count; r++)
            {
                var values = doc.Rows[r].Values;
                if (values == null || values.Count == 0) continue;

                int id;
                if (!int.TryParse(values[0], out id)) continue;

                for (var c = 1; c < doc.Headers.Count && c < values.Count; c++)
                {
                    if (IsMetadataHeader(doc.Headers[c]))
                        continue;

                    table.Add(id, doc.Headers[c], values[c]);
                }
            }

            return table;
        }

        public static bool IsMetadataHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return true;

            var normalized = header.Trim().ToLowerInvariant();
            return normalized == "id" ||
                   normalized == "speaker" ||
                   normalized == "source" ||
                   normalized == "text" ||
                   normalized == "notes" ||
                   normalized == "comment";
        }
    }

    /// <summary>
    /// 翻訳テーブルから言語別テキストを解決する <see cref="IDialogueTextResolver"/> 実装。
    /// 指定言語が無ければフォールバック言語、それも無ければ行の素のテキストを使う。
    /// 解決後のテキストに変数（<c>{name}</c>）展開を適用する。言語切替は
    /// <see cref="DialogueManager.SetLanguage"/> による languageKey の変更で行う。
    /// </summary>
    public sealed class LocalizedDialogueTextResolver : IDialogueTextResolver
    {
        private static readonly Regex VariablePattern = new Regex(@"\{([A-Za-z0-9_.-]+)\}", RegexOptions.Compiled);

        private readonly DialogueTranslationTable _table;
        private readonly string _fallbackLanguage;

        public LocalizedDialogueTextResolver(DialogueTranslationTable table, string fallbackLanguage = null)
        {
            _table = table;
            _fallbackLanguage = fallbackLanguage ?? string.Empty;
        }

        public string Resolve(DialogueData data, string languageKey, IDialogueVariableResolver variableResolver)
        {
            if (data == null) return string.Empty;

            var text = ResolveRaw(data, languageKey);
            if (string.IsNullOrEmpty(text) || variableResolver == null)
                return text ?? string.Empty;

            return VariablePattern.Replace(text, match =>
            {
                var name = match.Groups[1].Value;
                string value;
                return variableResolver.TryResolve(name, data, out value) ? value ?? string.Empty : match.Value;
            });
        }

        private string ResolveRaw(DialogueData data, string languageKey)
        {
            string text;
            if (_table != null && _table.TryGet(data.Id, languageKey, out text))
                return text;

            if (_table != null && !string.IsNullOrEmpty(_fallbackLanguage) &&
                _table.TryGet(data.Id, _fallbackLanguage, out text))
                return text;

            return data.Text;
        }
    }
}
