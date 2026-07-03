using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueBacklogTests
    {
        [Test]
        public void Build_MapsSpeakerTextVoiceAndOrder()
        {
            var history = new List<DialogueHistoryEntry>
            {
                new DialogueHistoryEntry { Speaker = "A", Text = "Hi", Voice = "v01", Order = 0 },
                new DialogueHistoryEntry { Speaker = "B", Text = "Yo", Voice = string.Empty, Order = 1 }
            };

            var entries = DialogueBacklog.Build(history);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("A", entries[0].Speaker);
            Assert.AreEqual("Hi", entries[0].Text);
            Assert.AreEqual("v01", entries[0].VoiceKey);
            Assert.IsTrue(entries[0].HasVoice);
            Assert.IsFalse(entries[1].HasVoice, "Voice 空は HasVoice=false");
        }

        [Test]
        public void Build_NewestFirst_ReversesOrder()
        {
            var history = new List<DialogueHistoryEntry>
            {
                new DialogueHistoryEntry { Text = "first", Order = 0 },
                new DialogueHistoryEntry { Text = "second", Order = 1 }
            };

            var oldestFirst = DialogueBacklog.Build(history, newestFirst: false);
            Assert.AreEqual("first", oldestFirst[0].Text);

            var newestFirst = DialogueBacklog.Build(history, newestFirst: true);
            Assert.AreEqual("second", newestFirst[0].Text);
        }

        [Test]
        public void Build_NullHistory_ReturnsEmpty()
        {
            Assert.IsEmpty(DialogueBacklog.Build(null));
        }

        [Test]
        public void HistoryEntry_CapturesVoiceFromData()
        {
            var data = CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId,Voice\n1,A,Hi,-1,voice_01\n")[1];

            var entry = new DialogueHistoryEntry(data, 0);

            Assert.AreEqual("voice_01", entry.Voice);
        }

        [Test]
        public void ReplayVoice_DelegatesVoiceKeyToAudioPlayer()
        {
            var player = new RecordingAudioPlayer();
            var entry = new DialogueBacklogEntry { VoiceKey = "voice_01" };

            Assert.IsTrue(DialogueBacklog.ReplayVoice(entry, player));
            Assert.AreEqual("voice_01", player.LastVoiceKey);
            Assert.IsFalse(DialogueBacklog.ReplayVoice(new DialogueBacklogEntry(), player));
        }

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public string LastVoiceKey;

            public void PlayBgm(string bgmKey, bool stop, string transition, float duration)
            {
            }

            public void PlaySe(string seKey)
            {
            }

            public void PlayVoice(string voiceKey)
            {
                LastVoiceKey = voiceKey;
            }

            public void StopVoice()
            {
            }

            public void StopAll()
            {
            }
        }
    }
}
