using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueAudioTests
    {
        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public readonly List<string> Calls = new List<string>();

            public void PlayBgm(string bgmKey, bool stop, string transition, float duration)
            {
                Calls.Add(stop ? "bgm:stop:" + transition + ":" + duration : "bgm:" + bgmKey + ":" + transition + ":" + duration);
            }

            public void PlaySe(string seKey)
            {
                Calls.Add("se:" + seKey);
            }

            public void PlayVoice(string voiceKey)
            {
                Calls.Add("voice:" + voiceKey);
            }

            public void StopVoice()
            {
                Calls.Add("stopVoice");
            }

            public void StopAll()
            {
                Calls.Add("stopAll");
            }
        }

        private static DialogueData BuildRow(string bgm, string se, string voice)
        {
            var csv = "Id,Speaker,Text,NextId,Bgm,Se,Voice\n" +
                      "1,A,Hi,-1," + Quote(bgm) + "," + Quote(se) + "," + Quote(voice) + "\n";
            return CsvLoader.ParseText<DialogueData>(csv)[1];
        }

        private static string Quote(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        [Test]
        public void Director_PlaysBgmSeAndVoiceInOrder()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(BuildRow("theme#fade:2", "door|step", "line_001"));

            CollectionAssert.AreEqual(
                new[] { "bgm:theme:fade:2", "se:door", "se:step", "voice:line_001" },
                player.Calls);
        }

        [Test]
        public void Director_BgmStopKeyword_StopsBgm()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(BuildRow("stop", string.Empty, string.Empty));

            // BGM 停止に加え、Voice 欄が空のため行単位ボイス仕様で StopVoice も呼ばれる。
            CollectionAssert.AreEqual(new[] { "bgm:stop::0", "stopVoice" }, player.Calls);
        }

        [Test]
        public void Director_EmptyVoiceLine_StopsVoice()
        {
            // 行単位ボイス仕様: Voice 欄が空の行に進んだら前行のボイスを停止する。
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(BuildRow(string.Empty, string.Empty, string.Empty));

            CollectionAssert.AreEqual(new[] { "stopVoice" }, player.Calls);
        }

        [Test]
        public void Director_VoiceThenEmptyVoiceLine_StopsPreviousVoice()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(BuildRow(string.Empty, string.Empty, "line_001"));
            director.Apply(BuildRow(string.Empty, string.Empty, string.Empty));

            CollectionAssert.AreEqual(new[] { "voice:line_001", "stopVoice" }, player.Calls);
        }

        [Test]
        public void Director_RepeatedBgmCue_DoesNotReplayBgmButStillAppliesSeAndVoice()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(BuildRow("theme", string.Empty, "line_001"));
            player.Calls.Clear();
            director.Apply(BuildRow("theme", "door|door", string.Empty));

            CollectionAssert.AreEqual(new[] { "se:door", "se:door", "stopVoice" }, player.Calls);
        }

        [Test]
        public void Director_RestoreSnapshot_ReplaysBgmAndVoiceDeterministically()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);
            director.Apply(BuildRow("theme", string.Empty, "line_001"));
            var snapshot = director.CaptureSnapshot();
            player.Calls.Clear();

            director.RestoreSnapshot(snapshot);

            CollectionAssert.AreEqual(new[] { "bgm:theme::0", "voice:line_001" }, player.Calls);

            player.Calls.Clear();
            director.RestoreSnapshot(new DialogueAudioSnapshot());

            CollectionAssert.AreEqual(new[] { "bgm:stop::0", "stopVoice" }, player.Calls);
        }

        [Test]
        public void AudioPlayer_MissingBgm_RaisesPresentationIssue()
        {
            var go = new GameObject("audio-test");
            try
            {
                var player = go.AddComponent<DialogueAudioPlayer>();
                var source = go.AddComponent<AudioSource>();
                var serialized = new SerializedObject(player);
                serialized.FindProperty("bgmSource").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DialoguePresentationIssueContext issue = null;
                player.PresentationIssueRaised += context => issue = context;

                player.PlayBgm("missing_bgm", false, string.Empty, 0f);

                Assert.IsNotNull(issue);
                Assert.AreEqual(DialoguePresentationIssueKind.Bgm, issue.Kind);
                Assert.AreEqual("missing_bgm", issue.Key);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Director_StopAll_DelegatesToPlayer()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.StopAll();

            CollectionAssert.AreEqual(new[] { "stopAll" }, player.Calls);
        }

        [Test]
        public void LipSync_Rms_ComputesRootMeanSquare()
        {
            var samples = new[] { 0.5f, -0.5f, 0.5f, -0.5f };

            var rms = DialogueLipSyncMath.Rms(samples, samples.Length);

            Assert.AreEqual(0.5f, rms, 1e-5f);
        }

        [Test]
        public void LipSync_Openness_RespectsThresholdAndClamp()
        {
            // しきい値未満は 0。
            Assert.AreEqual(0f, DialogueLipSyncMath.Openness(0.01f, 0.02f, 12f));
            // しきい値超過分 * 感度、上限 1 にクランプ。
            Assert.AreEqual(1f, DialogueLipSyncMath.Openness(0.5f, 0.02f, 12f));
            // 中間値。
            Assert.AreEqual((0.07f - 0.02f) * 5f, DialogueLipSyncMath.Openness(0.07f, 0.02f, 5f), 1e-5f);
        }
    }
}
