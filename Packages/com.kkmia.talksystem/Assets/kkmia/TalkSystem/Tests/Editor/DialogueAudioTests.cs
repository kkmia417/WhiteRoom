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
        public void AudioPlayer_ResetPlayback_ClearsEveryChannelImmediately()
        {
            var root = new GameObject("audio-reset-test");
            var bgm = root.AddComponent<AudioSource>();
            var seObject = new GameObject("se", typeof(AudioSource));
            var voiceObject = new GameObject("voice", typeof(AudioSource));
            var clip = AudioClip.Create("reset-clip", 16, 1, 8000, false);
            try
            {
                var se = seObject.GetComponent<AudioSource>();
                var voice = voiceObject.GetComponent<AudioSource>();
                var player = root.AddComponent<DialogueAudioPlayer>();
                var serialized = new SerializedObject(player);
                serialized.FindProperty("bgmSource").objectReferenceValue = bgm;
                serialized.FindProperty("seSource").objectReferenceValue = se;
                serialized.FindProperty("voiceSource").objectReferenceValue = voice;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                foreach (var source in new[] { bgm, se, voice })
                {
                    source.clip = clip;
                    source.loop = true;
                    source.volume = 0.25f;
                }

                player.ResetPlayback();

                foreach (var source in new[] { bgm, se, voice })
                {
                    Assert.That(source.clip, Is.Null);
                    Assert.That(source.loop, Is.False);
                    Assert.That(source.volume, Is.EqualTo(1f));
                }
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(voiceObject);
                Object.DestroyImmediate(seObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AudioPlayer_SettingsApplyChannelVolumesImmediately()
        {
            var root = new GameObject("audio-settings-test");
            var bgm = root.AddComponent<AudioSource>();
            var se = root.AddComponent<AudioSource>();
            var voice = root.AddComponent<AudioSource>();
            try
            {
                var player = root.AddComponent<DialogueAudioPlayer>();
                var serialized = new SerializedObject(player);
                serialized.FindProperty("bgmSource").objectReferenceValue = bgm;
                serialized.FindProperty("seSource").objectReferenceValue = se;
                serialized.FindProperty("voiceSource").objectReferenceValue = voice;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var settings = new DialogueSettings { MasterVolume = 0.5f };
                player.BindSettings(settings);
                settings.BgmVolume = 0.4f;
                settings.SeVolume = 0.6f;
                settings.VoiceVolume = 0.8f;

                Assert.That(bgm.volume, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(se.volume, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(voice.volume, Is.EqualTo(0.4f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
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
