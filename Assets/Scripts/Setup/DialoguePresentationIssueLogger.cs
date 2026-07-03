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
        private readonly List<IDialoguePresentationIssueSource> _sources = new List<IDialoguePresentationIssueSource>();

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
        }

        private void HandleIssue(DialoguePresentationIssueContext context)
        {
            if (context == null)
                return;

            Debug.LogWarning($"DialoguePresentationIssueLogger: presentation issue {context.Kind} '{context.Key}': {context.Message}");
        }
    }
}
