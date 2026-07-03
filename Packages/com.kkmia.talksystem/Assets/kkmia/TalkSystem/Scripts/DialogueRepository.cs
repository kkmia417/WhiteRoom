using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public class DialogueRepository : IDialogueRepository
    {
        private readonly Dictionary<int, DialogueData> _cache;
        // 挿入順（= CSV 行順）を保持する。Dictionary.Values の列挙順は不定のため、
        // GetAll / GetByTriggerKey はこのリスト順で走査して結果を決定的にする。
        private readonly List<DialogueData> _ordered = new List<DialogueData>();

        public DialogueRepository(TextAsset csv)
        {
            if (csv == null)
            {
                Debug.LogError("[DialogueRepository] CSVファイルが設定されていません。空のリポジトリとして初期化されます。");
                _cache = new Dictionary<int, DialogueData>();
                ValidationReport = new DialogueValidationReport();
                ValidationReport.Add(DialogueValidationSeverity.Error, 0, string.Empty, "CSV file is not assigned.");
                return;
            }

            _cache = CsvLoader.Parse<DialogueData>(csv);
            _ordered.AddRange(_cache.Values.OrderBy(d => d.RowNumber));
            ValidationReport = DialogueValidator.ValidateData(_ordered);
        }

        public DialogueRepository(IEnumerable<DialogueData> data)
        {
            _cache = new Dictionary<int, DialogueData>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    if (item != null && !_cache.ContainsKey(item.Id))
                    {
                        _cache.Add(item.Id, item);
                        _ordered.Add(item);
                    }
                }
            }

            ValidationReport = DialogueValidator.ValidateData(_ordered);
        }

        public DialogueValidationReport ValidationReport { get; private set; }

        public DialogueData Get(int id)
        {
            DialogueData data;
            return _cache.TryGetValue(id, out data) ? data : null;
        }

        public IEnumerable<DialogueData> GetAll()
        {
            return _ordered;
        }

        public DialogueData GetByTriggerKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _ordered.FirstOrDefault(d => d.TriggerKey == key);
        }
    }
}
