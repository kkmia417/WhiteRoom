using System;
using System.Collections.Generic;
using System.Globalization;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public enum CollectionItemKind
    {
        Ending,
        Cg
    }

    public enum LockedNameRule
    {
        Show,
        Mask
    }

    public sealed class CollectionCatalogEntry
    {
        public CollectionCatalogEntry(
            CollectionItemKind kind,
            string key,
            string displayName,
            string category,
            int order,
            LockedNameRule lockedNameRule)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Order = order;
            LockedNameRule = lockedNameRule;
        }

        public CollectionItemKind Kind { get; }
        public string Key { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
        public LockedNameRule LockedNameRule { get; }
        public string UnlockId => CollectionCatalog.GetUnlockId(Kind, Key);
    }

    public sealed class CollectionCatalog
    {
        private readonly List<CollectionCatalogEntry> _entries;

        public CollectionCatalog(IEnumerable<CollectionCatalogEntry> entries)
        {
            _entries = entries != null
                ? new List<CollectionCatalogEntry>(entries)
                : new List<CollectionCatalogEntry>();
            _entries.Sort((left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Key, right.Key);
            });
        }

        public IReadOnlyList<CollectionCatalogEntry> Entries => _entries;

        public List<CollectionCatalogEntry> List(CollectionItemKind kind)
        {
            return _entries.FindAll(entry => entry.Kind == kind);
        }

        public static string GetUnlockId(CollectionItemKind kind, string key)
        {
            var prefix = kind == CollectionItemKind.Cg ? "cg" : "ending";
            return prefix + ":" + (string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim());
        }

        public static string GetUnlockCategory(CollectionItemKind kind)
        {
            return kind == CollectionItemKind.Cg ? DialogueUnlockCategories.Cg : "ending";
        }
    }

    public sealed class CollectionCatalogLoadResult
    {
        public CollectionCatalogLoadResult(CollectionCatalog catalog, IReadOnlyList<string> warnings)
        {
            Catalog = catalog ?? new CollectionCatalog(null);
            Warnings = warnings ?? new string[0];
        }

        public CollectionCatalog Catalog { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    public static class CollectionCatalogLoader
    {
        private static readonly string[] RequiredHeaders =
        {
            "Kind", "Key", "DisplayName", "Category", "Order", "LockedName"
        };

        public static CollectionCatalogLoadResult Load(TextAsset asset)
        {
            return LoadText(asset != null ? asset.text : string.Empty);
        }

        public static CollectionCatalogLoadResult LoadText(string csv)
        {
            var warnings = new List<string>();
            var entries = new List<CollectionCatalogEntry>();
            var document = DialogueCsvCodec.Parse(csv);
            var columns = IndexHeaders(document.Headers);
            for (var index = 0; index < RequiredHeaders.Length; index++)
            {
                if (!columns.ContainsKey(RequiredHeaders[index]))
                    warnings.Add("Collection catalog is missing column '" + RequiredHeaders[index] + "'.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var rowIndex = 0; rowIndex < document.Rows.Count; rowIndex++)
            {
                var row = document.Rows[rowIndex];
                CollectionItemKind kind;
                if (!TryParseKind(Read(row, columns, "Kind"), out kind))
                {
                    warnings.Add("Collection catalog row " + row.RowNumber + " has an unknown Kind.");
                    continue;
                }

                var key = Read(row, columns, "Key").Trim();
                if (key.Length == 0)
                {
                    warnings.Add("Collection catalog row " + row.RowNumber + " has an empty Key.");
                    continue;
                }

                var identity = kind + ":" + key;
                if (!seen.Add(identity))
                {
                    warnings.Add("Collection catalog ignored duplicate key '" + key + "'.");
                    continue;
                }

                int order;
                if (!int.TryParse(Read(row, columns, "Order"), NumberStyles.Integer, CultureInfo.InvariantCulture, out order))
                {
                    warnings.Add("Collection catalog row " + row.RowNumber + " has an invalid Order.");
                    order = row.RowNumber;
                }

                LockedNameRule lockedRule;
                if (!Enum.TryParse(Read(row, columns, "LockedName"), true, out lockedRule))
                {
                    warnings.Add("Collection catalog row " + row.RowNumber + " has an invalid LockedName rule.");
                    lockedRule = LockedNameRule.Mask;
                }

                entries.Add(new CollectionCatalogEntry(
                    kind,
                    key,
                    Read(row, columns, "DisplayName").Trim(),
                    Read(row, columns, "Category").Trim(),
                    order,
                    lockedRule));
            }

            return new CollectionCatalogLoadResult(new CollectionCatalog(entries), warnings);
        }

        private static Dictionary<string, int> IndexHeaders(IReadOnlyList<string> headers)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headers == null)
                return columns;
            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index] != null ? headers[index].Trim() : string.Empty;
                if (header.Length > 0 && !columns.ContainsKey(header))
                    columns.Add(header, index);
            }
            return columns;
        }

        private static string Read(DialogueCsvRow row, IReadOnlyDictionary<string, int> columns, string header)
        {
            int index;
            return row != null && columns.TryGetValue(header, out index) && index < row.Values.Count
                ? row.Values[index] ?? string.Empty
                : string.Empty;
        }

        private static bool TryParseKind(string value, out CollectionItemKind kind)
        {
            if (string.Equals(value, "ending", StringComparison.OrdinalIgnoreCase))
            {
                kind = CollectionItemKind.Ending;
                return true;
            }
            if (string.Equals(value, "cg", StringComparison.OrdinalIgnoreCase))
            {
                kind = CollectionItemKind.Cg;
                return true;
            }
            kind = CollectionItemKind.Ending;
            return false;
        }
    }

    public sealed class CollectionItemViewModel
    {
        public CollectionItemViewModel(CollectionCatalogEntry entry, bool isUnlocked)
        {
            Entry = entry;
            IsUnlocked = isUnlocked;
            DisplayName = isUnlocked || entry.LockedNameRule == LockedNameRule.Show
                ? entry.DisplayName
                : "????????";
        }

        public CollectionCatalogEntry Entry { get; }
        public bool IsUnlocked { get; }
        public string DisplayName { get; }
    }

    /// <summary>Builds safe UI projections from catalog data and persisted unlock IDs.</summary>
    public sealed class CollectionService
    {
        private readonly CollectionCatalog _catalog;
        private readonly Func<string, List<string>> _listUnlockedIds;
        private readonly Action<string> _warning;
        private readonly HashSet<string> _reportedUnknownIds = new HashSet<string>(StringComparer.Ordinal);

        public CollectionService(
            CollectionCatalog catalog,
            Func<string, List<string>> listUnlockedIds,
            Action<string> warning = null)
        {
            _catalog = catalog ?? new CollectionCatalog(null);
            _listUnlockedIds = listUnlockedIds ?? (_ => new List<string>());
            _warning = warning;
        }

        public List<CollectionItemViewModel> Build(CollectionItemKind kind)
        {
            var entries = _catalog.List(kind);
            var expectedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
                expectedIds.Add(entries[index].UnlockId);

            var persisted = _listUnlockedIds(CollectionCatalog.GetUnlockCategory(kind)) ?? new List<string>();
            var unlockedIds = new HashSet<string>(persisted, StringComparer.Ordinal);
            for (var index = 0; index < persisted.Count; index++)
            {
                var id = persisted[index] ?? string.Empty;
                if (!expectedIds.Contains(id) && _reportedUnknownIds.Add(id))
                    _warning?.Invoke("CollectionService: ignored unknown unlock id '" + id + "'.");
            }

            var result = new List<CollectionItemViewModel>();
            for (var index = 0; index < entries.Count; index++)
                result.Add(new CollectionItemViewModel(entries[index], unlockedIds.Contains(entries[index].UnlockId)));
            return result;
        }
    }
}
