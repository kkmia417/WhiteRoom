using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public interface INovelSavePreferenceStore
    {
        int LoadLastManualSlot();
        void SaveLastManualSlot(int slot);
    }

    public sealed class PlayerPrefsNovelSavePreferenceStore : INovelSavePreferenceStore
    {
        private const string LastManualSlotKey = "WhiteRoom.Novel.Save.LastManualSlot";

        public int LoadLastManualSlot()
        {
            return PlayerPrefs.GetInt(LastManualSlotKey, -1);
        }

        public void SaveLastManualSlot(int slot)
        {
            PlayerPrefs.SetInt(LastManualSlotKey, slot);
            PlayerPrefs.Save();
        }
    }

    public sealed class NovelDirectSaveTarget
    {
        private readonly INovelSavePreferenceStore _store;
        private int _slot;

        public NovelDirectSaveTarget(INovelSavePreferenceStore store)
        {
            _store = store;
            _slot = Normalize(store != null ? store.LoadLastManualSlot() : -1);
        }

        public bool HasValue => _slot >= DialogueSaveSlotConventions.FirstManualSlot;
        public int Slot => _slot;

        public void Remember(int slot)
        {
            var normalized = Normalize(slot);
            if (normalized < DialogueSaveSlotConventions.FirstManualSlot)
                return;

            _slot = normalized;
            _store?.SaveLastManualSlot(normalized);
        }

        private static int Normalize(int slot)
        {
            return slot >= DialogueSaveSlotConventions.FirstManualSlot ? slot : -1;
        }
    }
}
