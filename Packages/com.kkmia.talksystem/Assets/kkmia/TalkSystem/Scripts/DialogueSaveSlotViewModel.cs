using System;

namespace kkmia.TalkSystem
{
    public enum DialogueSaveSlotCategory
    {
        Manual,
        Autosave,
        QuickSave
    }

    public static class DialogueSaveSlotConventions
    {
        public const int QuickSaveSlot = -1;
        public const int AutosaveSlot = 0;
        public const int FirstManualSlot = 1;

        public static DialogueSaveSlotCategory GetCategory(int slotIndex, bool isAutosave = false)
        {
            if (slotIndex == QuickSaveSlot)
                return DialogueSaveSlotCategory.QuickSave;
            if (slotIndex == AutosaveSlot || isAutosave)
                return DialogueSaveSlotCategory.Autosave;
            return DialogueSaveSlotCategory.Manual;
        }
    }

    [Serializable]
    public sealed class DialogueSaveSlotViewModel
    {
        public int SlotIndex;
        public DialogueSaveSlotCategory Category;
        public bool IsEmpty;
        public bool CanLoad;
        public string Title = string.Empty;
        public long SavedAtUnix;
        public string ContentVersion = string.Empty;
        public string ProductChannel = string.Empty;
        public bool HasThumbnail;
        public byte[] ThumbnailPngBytes;
        public string ErrorMessage = string.Empty;

        public bool HasError
        {
            get { return !string.IsNullOrEmpty(ErrorMessage); }
        }

        public DateTime SavedAtUtc
        {
            get { return SavedAtUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(SavedAtUnix).UtcDateTime : DateTime.MinValue; }
        }

        public static DialogueSaveSlotViewModel Empty(int slotIndex, string errorMessage = null)
        {
            return new DialogueSaveSlotViewModel
            {
                SlotIndex = slotIndex,
                Category = DialogueSaveSlotConventions.GetCategory(slotIndex),
                IsEmpty = true,
                CanLoad = false,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }

        public static DialogueSaveSlotViewModel FromSlot(DialogueSaveSlot slot, byte[] thumbnailPngBytes = null, string errorMessage = null)
        {
            if (slot == null)
                return Empty(-1, errorMessage);

            return new DialogueSaveSlotViewModel
            {
                SlotIndex = slot.SlotIndex,
                Category = DialogueSaveSlotConventions.GetCategory(slot.SlotIndex, slot.IsAutosave),
                IsEmpty = false,
                CanLoad = slot.Data != null,
                Title = slot.Title ?? string.Empty,
                SavedAtUnix = slot.SavedAtUnix,
                ContentVersion = slot.ContentVersion ?? string.Empty,
                ProductChannel = slot.ProductChannel ?? string.Empty,
                HasThumbnail = thumbnailPngBytes != null && thumbnailPngBytes.Length > 0,
                ThumbnailPngBytes = thumbnailPngBytes,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }
}
