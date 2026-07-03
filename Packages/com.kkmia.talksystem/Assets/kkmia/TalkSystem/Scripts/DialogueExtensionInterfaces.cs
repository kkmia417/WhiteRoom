using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public interface IDialogueConditionEvaluator
    {
        bool Evaluate(string conditionKey, DialogueData data);
    }

    public interface IDialogueVariableResolver
    {
        bool TryResolve(string variableName, DialogueData data, out string value);
    }

    public interface IDialogueTextResolver
    {
        string Resolve(DialogueData data, string languageKey, IDialogueVariableResolver variableResolver);
    }

    public interface IDialogueView
    {
        event Action NextRequested;
        event Action<int> ChoiceSelected;

        bool IsTyping { get; }

        void Show(DialogueData data, IReadOnlyList<DialogueChoice> choices, Action onComplete);
        void CompleteTyping();
        void Clear();
        void ForceStop();
        void SetTypewriterSpeed(float newInterval);
    }

    public sealed class AllowAllDialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool Evaluate(string conditionKey, DialogueData data)
        {
            return true;
        }
    }

    public sealed class EmptyDialogueVariableResolver : IDialogueVariableResolver
    {
        public bool TryResolve(string variableName, DialogueData data, out string value)
        {
            value = null;
            return false;
        }
    }
}
