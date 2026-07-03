using System;

namespace kkmia.TalkSystem
{
    public enum DialogueSaveOperation
    {
        Save,
        Load,
        Delete,
        Exists,
        ListSlots,
        SaveThumbnail,
        LoadThumbnail
    }

    [Serializable]
    public sealed class DialogueSaveOperationResult
    {
        public DialogueSaveOperation Operation;
        public int SlotIndex;
        public bool Succeeded;
        public string Message = string.Empty;

        [NonSerialized] public Exception Exception;

        public bool Failed
        {
            get { return !Succeeded; }
        }

        public static DialogueSaveOperationResult Success(DialogueSaveOperation operation, int slotIndex, string message = null)
        {
            return new DialogueSaveOperationResult
            {
                Operation = operation,
                SlotIndex = slotIndex,
                Succeeded = true,
                Message = message ?? string.Empty
            };
        }

        public static DialogueSaveOperationResult Failure(DialogueSaveOperation operation, int slotIndex, string message, Exception exception = null)
        {
            return new DialogueSaveOperationResult
            {
                Operation = operation,
                SlotIndex = slotIndex,
                Succeeded = false,
                Message = message ?? string.Empty,
                Exception = exception
            };
        }
    }
}
