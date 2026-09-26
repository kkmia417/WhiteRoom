using System;
using System.Collections;
using kkmia.TalkSystem;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>Prototype Resources adapter; compiled scenarios and custom legacy CSVs share a port.</summary>
    internal static class NovelDialogueLoaderFactory
    {
        public static IDialogueRepositoryLoader Create(string resourcePath)
        {
            var compiled = Resources.Load<CompiledDialogueAsset>(resourcePath);
            if (compiled != null) return new CompiledDialogueRepositoryLoader(compiled);
            var csv = Resources.Load<TextAsset>(resourcePath);
            return csv != null ? new TextAssetDialogueRepositoryLoader(csv) : null;
        }
    }

    internal sealed class CompletionDialogueLoader : IDialogueRepositoryLoader
    {
        private readonly IDialogueRepositoryLoader inner;
        private readonly Action completed;
        private readonly Action<string> failed;

        public CompletionDialogueLoader(IDialogueRepositoryLoader inner, Action completed, Action<string> failed)
        {
            this.inner = inner;
            this.completed = completed;
            this.failed = failed;
        }

        public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
        {
            return inner.Load(repository =>
            {
                onCompleted(repository);
                completed();
            }, error =>
            {
                onError(error);
                failed(error);
            });
        }
    }
}
