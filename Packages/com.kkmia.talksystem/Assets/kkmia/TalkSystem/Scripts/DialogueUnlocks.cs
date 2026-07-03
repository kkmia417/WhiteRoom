using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public static class DialogueUnlockCategories
    {
        public const string Cg = "cg";
        public const string Scene = "scene";
    }

    [Serializable]
    public sealed class DialogueUnlockEntry
    {
        public string Id = string.Empty;
        public string Category = string.Empty;
        public long UnlockedAtUnix;

        public DialogueUnlockEntry() { }

        public DialogueUnlockEntry(string id, string category, long unlockedAtUnix)
        {
            Id = NormalizeId(id);
            Category = NormalizeCategory(category);
            UnlockedAtUnix = unlockedAtUnix;
        }

        public DialogueUnlockEntry Clone()
        {
            return new DialogueUnlockEntry(Id, Category, UnlockedAtUnix);
        }

        internal static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        internal static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        }

        internal static string InferCategory(string id)
        {
            id = NormalizeId(id);
            var separator = id.IndexOf(':');
            return separator > 0 ? id.Substring(0, separator) : string.Empty;
        }
    }

    [Serializable]
    public sealed class DialogueUnlockState
    {
        public List<DialogueUnlockEntry> Entries = new List<DialogueUnlockEntry>();

        public DialogueUnlockState Clone()
        {
            var clone = new DialogueUnlockState();
            var entries = Entries != null ? Entries : new List<DialogueUnlockEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && !string.IsNullOrEmpty(DialogueUnlockEntry.NormalizeId(entries[i].Id)))
                    clone.Entries.Add(entries[i].Clone());
            }

            return clone;
        }

        public bool IsUnlocked(string id)
        {
            return FindIndex(id) >= 0;
        }

        public DialogueUnlockEntry Get(string id)
        {
            var index = FindIndex(id);
            return index >= 0 ? Entries[index].Clone() : null;
        }

        public List<DialogueUnlockEntry> List()
        {
            return List(null);
        }

        public List<DialogueUnlockEntry> List(string category)
        {
            EnsureEntries();
            var normalizedCategory = DialogueUnlockEntry.NormalizeCategory(category);
            var result = new List<DialogueUnlockEntry>();

            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                    continue;

                if (normalizedCategory.Length > 0 && DialogueUnlockEntry.NormalizeCategory(entry.Category) != normalizedCategory)
                    continue;

                result.Add(entry.Clone());
            }

            return result;
        }

        public List<DialogueUnlockEntry> ListUnlocks()
        {
            return List();
        }

        public List<DialogueUnlockEntry> ListUnlocks(string category)
        {
            return List(category);
        }

        public List<string> ListIds()
        {
            return ListIds(null);
        }

        public List<string> ListIds(string category)
        {
            var entries = List(category);
            var result = new List<string>();
            for (var i = 0; i < entries.Count; i++)
                result.Add(entries[i].Id);
            return result;
        }

        public List<string> ListUnlockedIds()
        {
            return ListIds();
        }

        public List<string> ListUnlockedIds(string category)
        {
            return ListIds(category);
        }

        public bool MarkUnlocked(string id, string category, long unlockedAtUnix)
        {
            id = DialogueUnlockEntry.NormalizeId(id);
            if (id.Length == 0)
                return false;

            EnsureEntries();
            if (FindIndex(id) >= 0)
                return false;

            category = DialogueUnlockEntry.NormalizeCategory(category);
            if (category.Length == 0)
                category = DialogueUnlockEntry.InferCategory(id);

            Entries.Add(new DialogueUnlockEntry(id, category, unlockedAtUnix));
            return true;
        }

        public int Reset()
        {
            EnsureEntries();
            var count = Entries.Count;
            Entries.Clear();
            return count;
        }

        public int ResetCategory(string category)
        {
            EnsureEntries();
            category = DialogueUnlockEntry.NormalizeCategory(category);
            if (category.Length == 0)
                return 0;

            var removed = 0;
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if (entry != null && DialogueUnlockEntry.NormalizeCategory(entry.Category) == category)
                {
                    Entries.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        internal void EnsureEntries()
        {
            if (Entries == null)
                Entries = new List<DialogueUnlockEntry>();
        }

        private int FindIndex(string id)
        {
            id = DialogueUnlockEntry.NormalizeId(id);
            if (id.Length == 0)
                return -1;

            EnsureEntries();
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && DialogueUnlockEntry.NormalizeId(Entries[i].Id) == id)
                    return i;
            }

            return -1;
        }
    }

    public sealed class DialogueUnlockEventContext
    {
        public DialogueUnlockEventContext(DialogueUnlockEntry entry, DialogueUnlockState state)
        {
            Entry = entry != null ? entry.Clone() : null;
            State = state != null ? state.Clone() : new DialogueUnlockState();
        }

        public DialogueUnlockEntry Entry { get; private set; }
        public DialogueUnlockState State { get; private set; }
    }

    public sealed class DialogueUnlockRegistry
    {
        private DialogueUnlockState _state;

        public DialogueUnlockRegistry()
            : this(null)
        {
        }

        public DialogueUnlockRegistry(DialogueUnlockState initialState)
        {
            RestoreState(initialState);
        }

        public event Action<DialogueUnlockEventContext> Unlocked;
        public event Action<DialogueUnlockState> ResetPerformed;

        public DialogueUnlockState State
        {
            get { return CaptureState(); }
        }

        public IReadOnlyList<DialogueUnlockEntry> Unlocks
        {
            get { return ListUnlocks(); }
        }

        public bool IsUnlocked(string id)
        {
            return _state.IsUnlocked(id);
        }

        public DialogueUnlockEntry GetUnlock(string id)
        {
            return _state.Get(id);
        }

        public List<DialogueUnlockEntry> ListUnlocks()
        {
            return _state.List();
        }

        public List<DialogueUnlockEntry> ListUnlocks(string category)
        {
            return _state.List(category);
        }

        public List<string> ListUnlockedIds()
        {
            return _state.ListIds();
        }

        public List<string> ListUnlockedIds(string category)
        {
            return _state.ListIds(category);
        }

        public bool MarkUnlocked(string id)
        {
            return MarkUnlocked(id, null, CurrentUnixTime());
        }

        public bool MarkUnlocked(string id, string category)
        {
            return MarkUnlocked(id, category, CurrentUnixTime());
        }

        public bool MarkUnlocked(string id, long unlockedAtUnix)
        {
            return MarkUnlocked(id, null, unlockedAtUnix);
        }

        public bool MarkUnlocked(string id, string category, long unlockedAtUnix)
        {
            if (!_state.MarkUnlocked(id, category, unlockedAtUnix))
                return false;

            RaiseUnlocked(_state.Get(id));
            return true;
        }

        public int Reset()
        {
            var removed = _state.Reset();
            if (removed > 0)
                RaiseReset();
            return removed;
        }

        public int ResetCategory(string category)
        {
            var removed = _state.ResetCategory(category);
            if (removed > 0)
                RaiseReset();
            return removed;
        }

        public DialogueUnlockState CaptureState()
        {
            return _state != null ? _state.Clone() : new DialogueUnlockState();
        }

        public void RestoreState(DialogueUnlockState state)
        {
            _state = state != null ? state.Clone() : new DialogueUnlockState();
            _state.EnsureEntries();
        }

        private void RaiseUnlocked(DialogueUnlockEntry entry)
        {
            if (Unlocked != null)
                Unlocked(new DialogueUnlockEventContext(entry, _state));
        }

        private void RaiseReset()
        {
            if (ResetPerformed != null)
                ResetPerformed(CaptureState());
        }

        private static long CurrentUnixTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
