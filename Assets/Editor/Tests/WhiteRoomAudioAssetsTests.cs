using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using kkmia.TalkSystem;
using kkmia.TalkSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class WhiteRoomAudioAssetsTests
    {
        private const string DatabasePath = "Assets/Presentation/Databases/WhiteRoomAudioDatabase.asset";
        private const string ScenarioPath = "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        private static readonly string[] RequiredBgmKeys =
        {
            "alarm", "alarm_low", "corridor_rush", "duct_alarm", "duct_tension", "escape_begin",
            "escape_final", "furnace_rumble", "quiet_dark", "stair_descent", "sterile_low", "tense_low"
        };

        private static readonly string[] RequiredSeKeys =
        {
            "body_fall", "camera_down", "camera_focus", "distant_door", "distant_drone", "door_close",
            "door_grind", "door_open", "drone_alert", "footsteps", "furnace_start", "gate_open", "grab",
            "inject", "lock", "metal_crash", "screw", "spark", "splash", "vent_close", "vent_open"
        };

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public readonly List<string> Calls = new List<string>();

            public void PlayBgm(string bgmKey, bool stop, string transition, float duration)
            {
                Calls.Add(stop
                    ? "bgm:stop:" + transition + ":" + duration
                    : "bgm:" + bgmKey + ":" + transition + ":" + duration);
            }

            public void PlaySe(string seKey) { Calls.Add("se:" + seKey); }
            public void PlayVoice(string voiceKey) { }
            public void StopVoice() { }
            public void StopAll() { }
        }

        private struct WaveStats
        {
            public int Channels;
            public int SampleRate;
            public float Duration;
            public float Peak;
            public float Rms;
        }

        [Test]
        public void DatabaseContainsRequiredBgmAndSeOnceWithoutStopClip()
        {
            var database = AssetDatabase.LoadAssetAtPath<AudioDatabase>(DatabasePath);
            Assert.That(database, Is.Not.Null);
            var serialized = new SerializedObject(database);

            AssertEntries(serialized.FindProperty("bgm"), RequiredBgmKeys);
            AssertEntries(serialized.FindProperty("se"), RequiredSeKeys);
            Assert.That(FindKeys(serialized.FindProperty("bgm")), Does.Not.Contain("stop"));
            Assert.That(serialized.FindProperty("voice").arraySize, Is.EqualTo(0));
        }

        [Test]
        public void AudioImportProfilesMatchBgmAndSePlaybackNeeds()
        {
            foreach (var key in RequiredBgmKeys)
            {
                var path = WhiteRoomAudioImportSettings.BgmFolder + key + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.channels, Is.EqualTo(1), path);
                Assert.That(clip.frequency, Is.EqualTo(44100), path);
                Assert.That(clip.length, Is.EqualTo(8f).Within(0.01f), path);
                Assert.That(importer.forceToMono, Is.True, path);
                Assert.That(importer.loadInBackground, Is.True, path);
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.False, path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), path);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), path);
            }

            foreach (var key in RequiredSeKeys)
            {
                var path = WhiteRoomAudioImportSettings.SeFolder + key + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.channels, Is.EqualTo(1), path);
                Assert.That(clip.frequency, Is.EqualTo(44100), path);
                Assert.That(clip.length, Is.InRange(0.29f, 1.61f), path);
                Assert.That(importer.forceToMono, Is.True, path);
                Assert.That(importer.loadInBackground, Is.False, path);
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True, path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), path);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.ADPCM), path);
            }
        }

        [Test]
        public void SourceWaveformsAreMonoNormalizedAndDoNotClip()
        {
            foreach (var key in RequiredBgmKeys)
            {
                var stats = ReadWave(WhiteRoomAudioImportSettings.BgmFolder + key + ".wav");
                Assert.That(stats.Channels, Is.EqualTo(1), key);
                Assert.That(stats.SampleRate, Is.EqualTo(44100), key);
                Assert.That(stats.Duration, Is.EqualTo(8f).Within(0.001f), key);
                Assert.That(stats.Peak, Is.InRange(0.24f, 0.56f), key);
                Assert.That(stats.Rms, Is.InRange(0.085f, 0.095f), key);
            }

            foreach (var key in RequiredSeKeys)
            {
                var stats = ReadWave(WhiteRoomAudioImportSettings.SeFolder + key + ".wav");
                Assert.That(stats.Channels, Is.EqualTo(1), key);
                Assert.That(stats.SampleRate, Is.EqualTo(44100), key);
                Assert.That(stats.Peak, Is.InRange(0.49f, 0.56f), key);
                Assert.That(stats.Rms, Is.InRange(0.035f, 0.11f), key);
            }
        }

        [Test]
        public void ScenarioFadeStopAndRepeatedSeReachIndependentCommands()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values;
            var fade = rows.First(row => row.GetBgmCue().HasValue &&
                                         !row.GetBgmCue().IsClear && row.GetBgmCue().Transition == "fade");
            var stop = rows.First(row => row.GetBgmCue().IsClear);
            var seRows = rows.Where(row => row.GetSeKeys().Count > 0).Take(2).ToArray();
            var player = new RecordingAudioPlayer();
            var director = new DialogueAudioDirector(player);

            director.Apply(fade);
            foreach (var row in seRows)
                director.Apply(row);
            director.Apply(stop);

            Assert.That(player.Calls.Any(call => call.StartsWith("bgm:") && call.Contains(":fade:")), Is.True);
            Assert.That(player.Calls.Count(call => call.StartsWith("se:")), Is.GreaterThanOrEqualTo(2));
            Assert.That(player.Calls.Any(call => call.StartsWith("bgm:stop:")), Is.True);
        }

        [Test]
        public void RuntimeUsesSeparateBgmAndSeSourcesAndSeDoesNotReplaceBgm()
        {
            var database = AssetDatabase.LoadAssetAtPath<AudioDatabase>(DatabasePath);
            var host = new GameObject("AudioAssetTest");
            var bgmObject = new GameObject("Bgm", typeof(AudioSource));
            var seObject = new GameObject("Se", typeof(AudioSource));
            var voiceObject = new GameObject("Voice", typeof(AudioSource));
            try
            {
                var player = host.AddComponent<DialogueAudioPlayer>();
                var bgm = bgmObject.GetComponent<AudioSource>();
                var se = seObject.GetComponent<AudioSource>();
                var voice = voiceObject.GetComponent<AudioSource>();
                var serialized = new SerializedObject(player);
                serialized.FindProperty("audioDatabase").objectReferenceValue = database;
                serialized.FindProperty("bgmSource").objectReferenceValue = bgm;
                serialized.FindProperty("seSource").objectReferenceValue = se;
                serialized.FindProperty("voiceSource").objectReferenceValue = voice;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                player.PlayBgm("sterile_low", false, string.Empty, 0f);
                var playingBgm = bgm.clip;
                player.PlaySe("door_open");
                player.PlaySe("door_close");

                Assert.That(bgm, Is.Not.SameAs(se));
                Assert.That(bgm.clip, Is.SameAs(playingBgm));
                Assert.That(bgm.loop, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(voiceObject);
                UnityEngine.Object.DestroyImmediate(seObject);
                UnityEngine.Object.DestroyImmediate(bgmObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FullScenarioHasNoMissingBgmOrSeReferences()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(
                "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset");
            var report = DialogueValidationRunner.ValidateProfile(profile);

            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Bgm || message.FieldName == DialogueSchema.Se), Is.False);
        }

        private static void AssertEntries(SerializedProperty property, IReadOnlyCollection<string> requiredKeys)
        {
            var keys = FindKeys(property);
            Assert.That(keys.Count, Is.EqualTo(requiredKeys.Count));
            CollectionAssert.AreEquivalent(requiredKeys, keys);
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
            for (var index = 0; index < property.arraySize; index++)
            {
                var entry = property.GetArrayElementAtIndex(index);
                var key = entry.FindPropertyRelative("audioKey").stringValue;
                var clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
                Assert.That(clip, Is.Not.Null, key);
                Assert.That(clip.name, Is.EqualTo(key), key);
            }
        }

        private static List<string> FindKeys(SerializedProperty property)
        {
            var keys = new List<string>();
            for (var index = 0; index < property.arraySize; index++)
                keys.Add(property.GetArrayElementAtIndex(index).FindPropertyRelative("audioKey").stringValue);
            return keys;
        }

        private static WaveStats ReadWave(string assetPath)
        {
            var path = Path.GetFullPath(assetPath);
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"), assetPath);
                reader.ReadInt32();
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"), assetPath);
                var channels = 0;
                var sampleRate = 0;
                byte[] data = null;
                while (stream.Position + 8 <= stream.Length)
                {
                    var chunk = new string(reader.ReadChars(4));
                    var size = reader.ReadInt32();
                    if (chunk == "fmt ")
                    {
                        Assert.That(reader.ReadInt16(), Is.EqualTo(1), assetPath);
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        stream.Position += size - 8;
                    }
                    else if (chunk == "data")
                    {
                        data = reader.ReadBytes(size);
                    }
                    else
                    {
                        stream.Position += size;
                    }

                    if ((size & 1) != 0 && stream.Position < stream.Length)
                        stream.Position++;
                }

                Assert.That(data, Is.Not.Null, assetPath);
                var samples = data.Length / 2;
                double squareSum = 0.0;
                var peak = 0f;
                for (var index = 0; index < samples; index++)
                {
                    var sample = BitConverter.ToInt16(data, index * 2) / 32768f;
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                    squareSum += sample * sample;
                }

                return new WaveStats
                {
                    Channels = channels,
                    SampleRate = sampleRate,
                    Duration = samples / (float)(channels * sampleRate),
                    Peak = peak,
                    Rms = (float)Math.Sqrt(squareSum / samples)
                };
            }
        }
    }
}
