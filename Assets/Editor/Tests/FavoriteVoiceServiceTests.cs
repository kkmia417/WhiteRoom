using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class FavoriteVoiceServiceTests
    {
        private sealed class MemoryStorage : IFavoriteVoiceStorage
        {
            public string Json;
            public bool ThrowOnLoad;
            public bool ThrowOnSave;
            public bool TryLoad(out string json)
            {
                if (ThrowOnLoad) throw new InvalidOperationException("disk unreadable");
                json = Json;
                return !string.IsNullOrWhiteSpace(json);
            }
            public void Save(string json)
            {
                if (ThrowOnSave) throw new InvalidOperationException("disk unavailable");
                Json = json;
            }
        }

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public readonly List<string> Calls = new List<string>();
            public void PlayBgm(string bgmKey, bool stop, string transition, float duration) { }
            public void PlaySe(string seKey) { }
            public void PlayVoice(string voiceKey) => Calls.Add("play:" + voiceKey);
            public void StopVoice() => Calls.Add("stop");
            public void StopAll() => Calls.Add("stop-all");
        }

        [Test]
        public void AddReplayDuplicateAndReloadUseStableIdentityAndOrder()
        {
            var rows = Rows();
            var current = rows[1];
            var storage = new MemoryStorage();
            var audio = new RecordingAudioPlayer();
            var service = Create(() => current, rows, _ => true, audio, storage);

            Assert.That(service.CanUseCurrentVoice, Is.True);
            Assert.That(service.ReplayCurrent().Succeeded, Is.True);
            CollectionAssert.AreEqual(new[] { "stop", "play:voice_001" }, audio.Calls);
            Assert.That(service.AddCurrent().Status, Is.EqualTo(FavoriteVoiceStatus.Success));
            Assert.That(service.AddCurrent().Status, Is.EqualTo(FavoriteVoiceStatus.AlreadyRegistered));

            current = rows[2];
            Assert.That(service.AddCurrent().Succeeded, Is.True);
            var list = service.BuildList();
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].Record.StableId, Is.EqualTo("1:voice_001"));
            Assert.That(list[0].Speaker, Is.EqualTo("Alice"));
            Assert.That(list[0].Text, Is.EqualTo("Localized first"));
            Assert.That(list[1].Record.StableId, Is.EqualTo("2:voice_002"));

            var restored = Create(() => current, rows, _ => true, audio, storage);
            CollectionAssert.AreEqual(
                new[] { "1:voice_001", "2:voice_002" },
                restored.BuildList().ConvertAll(item => item.Record.StableId));
        }

        [Test]
        public void VersionZeroMigratesDeduplicatesAndUnknownRowsAreIgnored()
        {
            var rows = Rows();
            var warnings = new List<string>();
            var storage = new MemoryStorage
            {
                Json = "{\"SchemaVersion\":0,\"Entries\":[" +
                       "{\"DialogueId\":2,\"VoiceKey\":\"voice_002\"}," +
                       "{\"DialogueId\":1,\"VoiceKey\":\"voice_001\"}," +
                       "{\"DialogueId\":1,\"VoiceKey\":\"voice_001\"}," +
                       "{\"DialogueId\":99,\"VoiceKey\":\"unknown\"}]}"
            };

            var service = Create(() => rows[1], rows, key => key == "voice_001", new RecordingAudioPlayer(), storage, warnings.Add);
            var list = service.BuildList();

            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].Record.Order, Is.EqualTo(0));
            Assert.That(list[1].Record.Order, Is.EqualTo(1));
            Assert.That(list[0].IsVoiceAvailable, Is.False, "missing assets remain visible and unavailable");
            Assert.That(list[1].IsVoiceAvailable, Is.True);
            Assert.That(warnings.Exists(message => message.Contains("99:unknown")), Is.True);
            StringAssert.Contains("\"SchemaVersion\":1", storage.Json);
        }

        [Test]
        public void FutureSchemaAndPersistenceFailureRemainNonBlocking()
        {
            var rows = Rows();
            var warnings = new List<string>();
            var future = new MemoryStorage { Json = "{\"SchemaVersion\":99,\"Entries\":[]}" };
            var service = Create(() => rows[1], rows, _ => true, new RecordingAudioPlayer(), future, warnings.Add);

            Assert.That(service.Count, Is.Zero);
            Assert.That(service.LastWarning, Does.Contain("Unsupported"));

            var unreadable = new MemoryStorage { ThrowOnLoad = true };
            service = Create(() => rows[1], rows, _ => true, new RecordingAudioPlayer(), unreadable, warnings.Add);
            Assert.That(service.Count, Is.Zero);
            Assert.That(service.LastWarning, Does.Contain("could not be read"));

            var failing = new MemoryStorage { ThrowOnSave = true };
            service = Create(() => rows[1], rows, _ => true, new RecordingAudioPlayer(), failing, warnings.Add);
            Assert.That(service.AddCurrent().Status, Is.EqualTo(FavoriteVoiceStatus.PersistenceFailed));
            Assert.That(service.Count, Is.Zero, "failed add must roll back in-memory mutation");
        }

        [Test]
        public void PlayStopAndRemoveHandleUnavailableAndMissingEntries()
        {
            var rows = Rows();
            var storage = new MemoryStorage();
            var audio = new RecordingAudioPlayer();
            var available = true;
            var service = Create(() => rows[1], rows, _ => available, audio, storage);
            Assert.That(service.AddCurrent().Succeeded, Is.True);
            var record = service.BuildList()[0].Record;

            Assert.That(service.Play(record).Succeeded, Is.True);
            service.Stop();
            CollectionAssert.AreEqual(new[] { "stop", "play:voice_001", "stop" }, audio.Calls);

            available = false;
            Assert.That(service.Play(record).Status, Is.EqualTo(FavoriteVoiceStatus.VoiceUnavailable));
            available = true;
            Assert.That(service.Remove(record).Succeeded, Is.True);
            Assert.That(service.Remove(record).Status, Is.EqualTo(FavoriteVoiceStatus.NotFound));
            Assert.That(service.HasFavorites, Is.False);
        }

        private static FavoriteVoiceService Create(
            Func<DialogueData> current,
            IReadOnlyDictionary<int, DialogueData> rows,
            Func<string, bool> canResolve,
            IDialogueAudioPlayer audio,
            IFavoriteVoiceStorage storage,
            Action<string> warning = null)
        {
            return new FavoriteVoiceService(
                current,
                id => rows.ContainsKey(id) ? rows[id] : null,
                canResolve,
                audio,
                storage,
                warning);
        }

        private static Dictionary<int, DialogueData> Rows()
        {
            return CsvLoader.Parse<DialogueData>(new TextAsset(
                "Id,Speaker,Text,NextId,Voice\n" +
                "1,Alice,Localized first,-1,voice_001\n" +
                "2,Bob,Localized second,-1,voice_002\n"));
        }
    }
}
