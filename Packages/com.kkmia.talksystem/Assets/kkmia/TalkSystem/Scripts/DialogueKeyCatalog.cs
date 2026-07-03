using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class DialogueKeyCatalogEntry
    {
        public string key;
        public string description;
    }

    [CreateAssetMenu(menuName = "kkmia/Talk System/Dialogue Key Catalog")]
    public sealed class DialogueKeyCatalog : ScriptableObject
    {
        [SerializeField] private List<DialogueKeyCatalogEntry> keys = new List<DialogueKeyCatalogEntry>();

        public IReadOnlyList<DialogueKeyCatalogEntry> Keys
        {
            get { return keys; }
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Trim();
            for (var i = 0; i < keys.Count; i++)
            {
                var entry = keys[i];
                if (entry != null && entry.key == normalized)
                    return true;
            }

            return false;
        }

        public void SetKeys(IEnumerable<string> values)
        {
            keys.Clear();
            if (values == null) return;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                keys.Add(new DialogueKeyCatalogEntry { key = value.Trim() });
            }
        }
    }
}
