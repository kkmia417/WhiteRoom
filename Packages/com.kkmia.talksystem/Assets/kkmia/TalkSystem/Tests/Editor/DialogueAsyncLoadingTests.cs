using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueAsyncLoadingTests
    {
        [Test]
        public void CompositeRepository_FindsDataAcrossLoadedRepositories()
        {
            var first = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId,TriggerKey\n1,A,First,-1,Intro\n").Values);
            var second = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId,TriggerKey\n10,B,Second,-1,Boss\n").Values);
            var composite = new CompositeDialogueRepository(new[] { first, second });

            Assert.AreEqual("First", composite.Get(1).Text);
            Assert.AreEqual("Second", composite.Get(10).Text);
            Assert.AreEqual(10, composite.GetByTriggerKey("Boss").Id);
        }

        [Test]
        public void CompositeRepositoryLoader_CompletesWithMergedRepository()
        {
            var loader = new CompositeDialogueRepositoryLoader(new IDialogueRepositoryLoader[]
            {
                new FakeLoader(new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                    "Id,Speaker,Text,NextId,TriggerKey\n1,A,First,-1,Intro\n").Values)),
                new FakeLoader(new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                    "Id,Speaker,Text,NextId,TriggerKey\n2,B,Second,-1,Outro\n").Values))
            });

            IDialogueRepository repository = null;
            string error = null;
            Exhaust(loader.Load(result => repository = result, message => error = message));

            Assert.IsNull(error);
            Assert.IsNotNull(repository);
            Assert.AreEqual("Second", repository.GetByTriggerKey("Outro").Text);
        }

        [Test]
        public void CompositeRepositoryLoader_StopsOnLoaderError()
        {
            var loader = new CompositeDialogueRepositoryLoader(new IDialogueRepositoryLoader[]
            {
                new FakeLoader(null, "Missing addressable dialogue asset.")
            });

            IDialogueRepository repository = null;
            string error = null;
            Exhaust(loader.Load(result => repository = result, message => error = message));

            Assert.IsNull(repository);
            Assert.AreEqual("Missing addressable dialogue asset.", error);
        }

        private static void Exhaust(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                var nested = enumerator.Current as IEnumerator;
                if (nested != null)
                    Exhaust(nested);
            }
        }

        private sealed class FakeLoader : IDialogueRepositoryLoader
        {
            private readonly IDialogueRepository _repository;
            private readonly string _error;

            public FakeLoader(IDialogueRepository repository, string error = null)
            {
                _repository = repository;
                _error = error;
            }

            public IEnumerator Load(Action<IDialogueRepository> onCompleted, Action<string> onError)
            {
                yield return null;

                if (!string.IsNullOrEmpty(_error))
                {
                    onError(_error);
                    yield break;
                }

                onCompleted(_repository);
            }
        }
    }
}
