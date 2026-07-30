using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Logs presentation issues (missing backgrounds, audio clips, ...) raised by any
    /// watched component that implements <see cref="IDialoguePresentationIssueSource"/>.
    /// </summary>
    public sealed class DialoguePresentationIssueLogger : IDisposable
    {
        private readonly Func<int?> _dialogueIdProvider;
        private readonly List<IDialoguePresentationIssueSource> _sources = new List<IDialoguePresentationIssueSource>();
        private readonly HashSet<string> _reportedIssues = new HashSet<string>();

        public DialoguePresentationIssueLogger(Func<int?> dialogueIdProvider = null)
        {
            _dialogueIdProvider = dialogueIdProvider;
        }

        public void Watch(object candidate)
        {
            if (!(candidate is IDialoguePresentationIssueSource source) || _sources.Contains(source))
                return;

            source.PresentationIssueRaised += HandleIssue;
            _sources.Add(source);
        }

        public void Dispose()
        {
            foreach (var source in _sources)
                source.PresentationIssueRaised -= HandleIssue;

            _sources.Clear();
            _reportedIssues.Clear();
        }

        private void HandleIssue(DialoguePresentationIssueContext context)
        {
            if (context == null)
                return;

            var dialogueId = _dialogueIdProvider?.Invoke();
            var column = ToColumnName(context.Kind);
            var signature = $"{dialogueId?.ToString() ?? "unknown"}|{column}|{context.Key}";
            if (!_reportedIssues.Add(signature))
                return;

            Debug.LogWarning(
                $"DialoguePresentationIssueLogger: DialogueId={dialogueId?.ToString() ?? "unknown"} " +
                $"Column={column} Key='{context.Key}': {context.Message}");
        }

        private static string ToColumnName(DialoguePresentationIssueKind kind)
        {
            switch (kind)
            {
                case DialoguePresentationIssueKind.Background:
                    return DialogueSchema.Background;
                case DialoguePresentationIssueKind.Bgm:
                    return DialogueSchema.Bgm;
                case DialoguePresentationIssueKind.Se:
                    return DialogueSchema.Se;
                case DialoguePresentationIssueKind.Voice:
                    return DialogueSchema.Voice;
                case DialoguePresentationIssueKind.StageSlot:
                case DialoguePresentationIssueKind.Character:
                case DialoguePresentationIssueKind.CharacterModel:
                    return DialogueSchema.Characters;
                default:
                    return kind.ToString();
            }
        }
    }
}
