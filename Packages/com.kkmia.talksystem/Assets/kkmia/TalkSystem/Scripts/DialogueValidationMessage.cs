namespace kkmia.TalkSystem
{
    public sealed class DialogueValidationMessage
    {
        public DialogueValidationMessage(DialogueValidationSeverity severity, int rowNumber, string fieldName, string message)
        {
            Severity = severity;
            RowNumber = rowNumber;
            FieldName = fieldName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DialogueValidationSeverity Severity { get; private set; }
        public int RowNumber { get; private set; }
        public string FieldName { get; private set; }
        public string Message { get; private set; }

        public override string ToString()
        {
            var location = RowNumber > 0 ? "Row " + RowNumber : "Document";
            var field = string.IsNullOrEmpty(FieldName) ? string.Empty : " [" + FieldName + "]";
            return Severity + ": " + location + field + " - " + Message;
        }
    }
}
