using System.Collections.Generic;
using System.Text;

namespace kkmia.TalkSystem
{
    public static class DialogueCsvCodec
    {
        public static DialogueCsvDocument Parse(string text)
        {
            var diagnostics = new DialogueValidationReport();
            var records = new List<List<string>>();
            var recordStartLines = new List<int>();
            var current = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var lineNumber = 1;
            var recordStartLine = 1;
            var value = text ?? string.Empty;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < value.Length && value[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        if (c == '\n') lineNumber++;
                        field.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    if (field.Length == 0)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        diagnostics.Add(DialogueValidationSeverity.Error, lineNumber, string.Empty, "Unexpected quote in unquoted CSV field.");
                        field.Append(c);
                    }
                }
                else if (c == ',')
                {
                    current.Add(field.ToString());
                    field.Length = 0;
                }
                else if (c == '\r' || c == '\n')
                {
                    current.Add(field.ToString());
                    field.Length = 0;
                    AddRecord(records, recordStartLines, current, recordStartLine);
                    current = new List<string>();

                    if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                        i++;

                    lineNumber++;
                    recordStartLine = lineNumber;
                }
                else
                {
                    field.Append(c);
                }
            }

            if (inQuotes)
                diagnostics.Add(DialogueValidationSeverity.Error, recordStartLine, string.Empty, "CSV quoted field is not closed.");

            current.Add(field.ToString());
            AddRecord(records, recordStartLines, current, recordStartLine);

            if (records.Count == 0)
                return new DialogueCsvDocument(new List<string>(), new List<DialogueCsvRow>(), diagnostics);

            var headers = records[0];
            var rows = new List<DialogueCsvRow>();
            for (var i = 1; i < records.Count; i++)
                rows.Add(new DialogueCsvRow(recordStartLines[i], records[i]));

            return new DialogueCsvDocument(headers, rows, diagnostics);
        }

        public static string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();
            AppendRecord(builder, headers);

            if (rows != null)
            {
                foreach (var row in rows)
                    AppendRecord(builder, row);
            }

            return builder.ToString();
        }

        private static void AddRecord(List<List<string>> records, List<int> lines, List<string> record, int lineNumber)
        {
            if (record.Count == 1 && string.IsNullOrEmpty(record[0]))
                return;

            records.Add(record);
            lines.Add(lineNumber);
        }

        private static void AppendRecord(StringBuilder builder, IReadOnlyList<string> values)
        {
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(Escape(values[i]));
                }
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;

            var mustQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ||
                            value.StartsWith(" ") ||
                            value.EndsWith(" ");

            if (!mustQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
