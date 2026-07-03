using System.Collections.Generic;
using System.Linq;

namespace kkmia.TalkSystem
{
    public sealed class DialogueValidationReport
    {
        private readonly List<DialogueValidationMessage> _messages = new List<DialogueValidationMessage>();

        public IReadOnlyList<DialogueValidationMessage> Messages
        {
            get { return _messages; }
        }

        public bool HasErrors
        {
            get { return _messages.Any(m => m.Severity == DialogueValidationSeverity.Error); }
        }

        public void Add(DialogueValidationSeverity severity, int rowNumber, string fieldName, string message)
        {
            _messages.Add(new DialogueValidationMessage(severity, rowNumber, fieldName, message));
        }

        public void AddRange(IEnumerable<DialogueValidationMessage> messages)
        {
            if (messages == null) return;
            _messages.AddRange(messages);
        }

        public void Clear()
        {
            _messages.Clear();
        }
    }
}
