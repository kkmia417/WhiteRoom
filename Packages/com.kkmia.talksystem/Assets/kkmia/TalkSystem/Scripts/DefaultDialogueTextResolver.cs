using System.Text.RegularExpressions;

namespace kkmia.TalkSystem
{
    public sealed class DefaultDialogueTextResolver : IDialogueTextResolver
    {
        private static readonly Regex VariablePattern = new Regex(@"\{([A-Za-z0-9_.-]+)\}", RegexOptions.Compiled);

        public string Resolve(DialogueData data, string languageKey, IDialogueVariableResolver variableResolver)
        {
            var text = data != null ? data.Text : string.Empty;
            if (string.IsNullOrEmpty(text) || variableResolver == null)
                return text ?? string.Empty;

            return VariablePattern.Replace(text, match =>
            {
                var name = match.Groups[1].Value;
                string value;
                return variableResolver.TryResolve(name, data, out value) ? value ?? string.Empty : match.Value;
            });
        }
    }
}
