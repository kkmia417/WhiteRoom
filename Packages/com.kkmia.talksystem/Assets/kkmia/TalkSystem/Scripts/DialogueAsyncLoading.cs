using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public interface IDialogueRepositoryLoader
    {
        IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError);
    }

    public sealed class TextAssetDialogueRepositoryLoader : IDialogueRepositoryLoader
    {
        private readonly TextAsset _csv;

        public TextAssetDialogueRepositoryLoader(TextAsset csv)
        {
            _csv = csv;
        }

        public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
        {
            yield return null;

            if (_csv == null)
            {
                if (onError != null) onError("CSV TextAsset is null.");
                yield break;
            }

            if (onCompleted != null)
                onCompleted(new DialogueRepository(_csv));
        }
    }

    public sealed class CompositeDialogueRepository : IDialogueRepository
    {
        private readonly List<IDialogueRepository> _repositories = new List<IDialogueRepository>();

        public CompositeDialogueRepository(IEnumerable<IDialogueRepository> repositories)
        {
            if (repositories == null) return;

            foreach (var repository in repositories)
            {
                if (repository != null)
                    _repositories.Add(repository);
            }
        }

        public DialogueData Get(int id)
        {
            for (var i = 0; i < _repositories.Count; i++)
            {
                var data = _repositories[i].Get(id);
                if (data != null)
                    return data;
            }

            return null;
        }

        public IEnumerable<DialogueData> GetAll()
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < _repositories.Count; i++)
            {
                foreach (var data in _repositories[i].GetAll())
                {
                    if (data != null && seen.Add(data.Id))
                        yield return data;
                }
            }
        }

        public DialogueData GetByTriggerKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            for (var i = 0; i < _repositories.Count; i++)
            {
                var data = _repositories[i].GetByTriggerKey(key);
                if (data != null)
                    return data;
            }

            return null;
        }
    }

    public sealed class CompositeDialogueRepositoryLoader : IDialogueRepositoryLoader
    {
        private readonly List<IDialogueRepositoryLoader> _loaders = new List<IDialogueRepositoryLoader>();

        public CompositeDialogueRepositoryLoader(IEnumerable<IDialogueRepositoryLoader> loaders)
        {
            if (loaders == null) return;

            foreach (var loader in loaders)
            {
                if (loader != null)
                    _loaders.Add(loader);
            }
        }

        public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
        {
            if (_loaders.Count == 0)
            {
                if (onError != null) onError("No dialogue repository loaders were provided.");
                yield break;
            }

            var repositories = new List<IDialogueRepository>();
            for (var i = 0; i < _loaders.Count; i++)
            {
                IDialogueRepository loadedRepository = null;
                string loadError = null;

                yield return _loaders[i].Load(repository => loadedRepository = repository, error => loadError = error);

                if (!string.IsNullOrEmpty(loadError))
                {
                    if (onError != null) onError(loadError);
                    yield break;
                }

                if (loadedRepository == null)
                {
                    if (onError != null) onError("Dialogue repository loader completed without a repository.");
                    yield break;
                }

                repositories.Add(loadedRepository);
            }

            if (onCompleted != null)
                onCompleted(new CompositeDialogueRepository(repositories));
        }
    }
}
