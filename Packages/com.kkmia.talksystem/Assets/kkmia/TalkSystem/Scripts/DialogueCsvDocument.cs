using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public sealed class DialogueCsvRow
    {
        public DialogueCsvRow(int rowNumber, IReadOnlyList<string> values)
        {
            RowNumber = rowNumber;
            Values = values;
        }

        public int RowNumber { get; private set; }
        public IReadOnlyList<string> Values { get; private set; }
    }

    public sealed class DialogueCsvDocument
    {
        public DialogueCsvDocument(
            IReadOnlyList<string> headers,
            IReadOnlyList<DialogueCsvRow> rows,
            DialogueValidationReport diagnostics)
        {
            Headers = headers;
            Rows = rows;
            Diagnostics = diagnostics ?? new DialogueValidationReport();
        }

        public IReadOnlyList<string> Headers { get; private set; }
        public IReadOnlyList<DialogueCsvRow> Rows { get; private set; }
        public DialogueValidationReport Diagnostics { get; private set; }
    }
}
