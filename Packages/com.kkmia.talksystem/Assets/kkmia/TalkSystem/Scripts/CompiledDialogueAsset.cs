using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>Importer output. CSV remains the authoring source; player code only builds indexes.</summary>
    public sealed class CompiledDialogueAsset : ScriptableObject
    {
        [Serializable]
        private sealed class ExtraColumn
        {
            public string key;
            public string value;
        }

        [Serializable]
        private sealed class Record
        {
            public DialogueData data;
            public int rowNumber;
            public ExtraColumn[] extras;

            public DialogueData Restore()
            {
                data.RowNumber = rowNumber;
                if (extras.Length > 0 && !data.HasExtraColumns)
                {
                    var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var extra in extras)
                        columns.Add(extra.key, extra.value);
                    data.SetExtraColumns(columns);
                }
                return data;
            }
        }

        [SerializeField] private Record[] records = Array.Empty<Record>();
        [SerializeField] private string importError = "Not compiled.";

        public int Count => records.Length;
        public string ImportError => importError;

        /// <summary>Authoring/import API. Rejects invalid data before replacing compiled records.</summary>
        public void Compile(string csv)
        {
            records = Array.Empty<Record>();
            importError = "Compilation did not complete.";
            var report = DialogueValidator.ValidateCsv(csv);
            if (report.HasErrors)
            {
                importError = string.Join("\n", report.Messages
                    .Where(message => message.Severity == DialogueValidationSeverity.Error));
                throw new InvalidOperationException(importError);
            }

            records = CsvLoader.ParseText<DialogueData>(csv).Values.OrderBy(row => row.RowNumber)
                .Select(row => new Record
                {
                    data = row,
                    rowNumber = row.RowNumber,
                    extras = row.ExtraColumns.Select(pair => new ExtraColumn
                        { key = pair.Key, value = pair.Value }).ToArray()
                }).ToArray();
            if (records.Length == 0)
                throw new InvalidOperationException("Dialogue source contains no rows.");
            importError = string.Empty;
        }

        public IDialogueRepository CreateRepository()
        {
            if (!string.IsNullOrEmpty(importError))
                throw new InvalidOperationException("Dialogue import failed: " + importError);
            return new CompiledRepository(records.Select(record => record.Restore()));
        }

        private sealed class CompiledRepository : IDialogueRepository
        {
            private readonly DialogueData[] ordered;
            private readonly Dictionary<int, DialogueData> byId;
            private readonly Dictionary<string, DialogueData> byTrigger =
                new Dictionary<string, DialogueData>(StringComparer.Ordinal);

            public CompiledRepository(IEnumerable<DialogueData> rows)
            {
                ordered = rows.ToArray();
                byId = new Dictionary<int, DialogueData>(ordered.Length);
                foreach (var row in ordered)
                {
                    byId.Add(row.Id, row);
                    if (row.HasTriggerKey && !byTrigger.ContainsKey(row.TriggerKey))
                        byTrigger.Add(row.TriggerKey, row);
                }
            }

            public DialogueData Get(int id) => byId.TryGetValue(id, out var row) ? row : null;
            public IEnumerable<DialogueData> GetAll() => ordered;
            public DialogueData GetByTriggerKey(string key) =>
                !string.IsNullOrEmpty(key) && byTrigger.TryGetValue(key, out var row) ? row : null;
        }
    }

    public sealed class CompiledDialogueRepositoryLoader : IDialogueRepositoryLoader
    {
        private readonly CompiledDialogueAsset asset;

        public CompiledDialogueRepositoryLoader(CompiledDialogueAsset asset) { this.asset = asset; }

        public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
        {
            yield return null;
            IDialogueRepository repository;
            try
            {
                if (asset == null) throw new InvalidOperationException("Compiled dialogue asset is missing.");
                repository = asset.CreateRepository();
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception.Message);
                yield break;
            }
            onCompleted?.Invoke(repository);
        }
    }
}
