using System.Collections.Generic;
using System.Linq;
using kkmia.TalkSystem;
using kkmia.TalkSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class WhiteRoomVoicePolicyTests
    {
        private const string ScenarioPath = "Assets/Resources/Dialogue/r00_escape_talksystem.csv";
        private const string DatabasePath = "Assets/Presentation/Databases/WhiteRoomAudioDatabase.asset";

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public readonly List<string> VoiceKeys = new List<string>();
            public int StopVoiceCount;

            public void PlayBgm(string bgmKey, bool stop, string transition, float duration) { }
            public void PlaySe(string seKey) { }
            public void PlayVoice(string voiceKey) { VoiceKeys.Add(voiceKey); }
            public void StopVoice() { StopVoiceCount++; }
            public void StopAll() { StopVoiceCount++; }
        }

        [Test]
        public void ScenarioAndDatabaseIntentionallyContainNoVoiceContent()
        {
            var rows = LoadRows();
            var database = AssetDatabase.LoadAssetAtPath<AudioDatabase>(DatabasePath);
            var serialized = new SerializedObject(database);

            Assert.That(rows.Count, Is.GreaterThan(0));
            Assert.That(rows.All(row => string.IsNullOrWhiteSpace(row.Voice)), Is.True);
            Assert.That(rows.All(row => !row.HasVoice), Is.True);
            Assert.That(serialized.FindProperty("voice").arraySize, Is.EqualTo(0));
        }

        [Test]
        public void AdvancingAndSkippingRowsStopVoiceWithoutStartingPlayback()
        {
            var rows = LoadRows();
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            foreach (var row in rows)
                director.Apply(row);

            Assert.That(player.VoiceKeys, Is.Empty);
            Assert.That(player.StopVoiceCount, Is.EqualTo(rows.Count));

            var planner = new DialoguePlaybackPlanner();
            var skip = planner.Plan(DialoguePlaybackMode.Skip, false, true, new DialogueSettings());
            Assert.That(skip.ShouldAdvance, Is.True);
            Assert.That(skip.Delay, Is.EqualTo(0f));
        }

        [Test]
        public void RollbackAndLoadRestoreStopVoiceWithoutReplayOrDuplication()
        {
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);
            director.Apply(LoadRows()[0]);
            var snapshot = director.CaptureSnapshot();

            director.RestoreSnapshot(snapshot);
            director.RestoreSnapshot(new DialogueAudioSnapshot());

            Assert.That(snapshot.voiceKey, Is.Empty);
            Assert.That(director.CurrentVoiceKey, Is.Empty);
            Assert.That(player.VoiceKeys, Is.Empty);
            Assert.That(player.StopVoiceCount, Is.EqualTo(3));
        }

        [Test]
        public void AutoProgressUsesConfiguredDelayWithoutVoiceDuration()
        {
            var planner = new DialoguePlaybackPlanner();
            var settings = new DialogueSettings { AutoAdvanceDelay = 2.75f };

            var plan = planner.Plan(DialoguePlaybackMode.Auto, false, true, settings);

            Assert.That(plan.ShouldAdvance, Is.True);
            Assert.That(plan.Delay, Is.EqualTo(2.75f));
        }

        [Test]
        public void ValidationTreatsEmptyVoiceFieldsAsIntentional()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(
                "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset");
            var report = DialogueValidationRunner.ValidateProfile(profile);

            Assert.That(report.Messages.Any(message => message.FieldName == DialogueSchema.Voice), Is.False);
        }

        private static List<DialogueData> LoadRows()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            return CsvLoader.Parse<DialogueData>(scenario).Values.OrderBy(row => row.RowNumber).ToList();
        }
    }
}
