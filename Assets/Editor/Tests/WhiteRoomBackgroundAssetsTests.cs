using System.Collections.Generic;
using System.Linq;
using kkmia.TalkSystem;
using kkmia.TalkSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class WhiteRoomBackgroundAssetsTests
    {
        private const string DatabasePath =
            "Assets/Presentation/Databases/WhiteRoomBackgroundDatabase.asset";
        private const string ScenarioPath =
            "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        private static readonly string[] RequiredKeys =
        {
            "back_corridor",
            "drain_dark",
            "duct_dark",
            "duct_entry",
            "duct_inner",
            "furnace_gate",
            "lab_room_alarm",
            "lab_room_night",
            "lab_room_white",
            "maintenance_corridor",
            "outside_wall_night",
            "soft_cell",
            "stairwell_down",
            "waste_furnace"
        };

        private sealed class RecordingStageView : IDialogueStageView
        {
            public readonly List<DialogueMediaCue> BackgroundCues = new List<DialogueMediaCue>();

            public void SetBackground(string backgroundKey, bool clear, string transition, float duration)
            {
                BackgroundCues.Add(new DialogueMediaCue(backgroundKey, clear, transition, duration));
            }

            public void SetCharacter(string slot, string characterKey, string expression, string animation) { }
            public void RemoveCharacter(string slot, string characterKey, string animation) { }
            public void ClearCharacters() { }
        }

        [Test]
        public void DatabaseContainsEachRequiredKeyOnceWithProductionSprite()
        {
            var database = AssetDatabase.LoadAssetAtPath<BackgroundDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.Backgrounds.Count, Is.EqualTo(RequiredKeys.Length));
            CollectionAssert.AreEquivalent(
                RequiredKeys,
                database.Backgrounds.Select(entry => entry.backgroundKey));
            Assert.That(
                database.Backgrounds.GroupBy(entry => entry.backgroundKey).All(group => group.Count() == 1),
                Is.True);

            foreach (var key in RequiredKeys)
            {
                Assert.That(database.TryGetSprite(key, out var sprite), Is.True, key);
                Assert.That(sprite, Is.Not.Null, key);
                Assert.That(sprite.texture.width, Is.EqualTo(1920), key);
                Assert.That(sprite.texture.height, Is.EqualTo(1080), key);
                Assert.That(sprite.name, Is.EqualTo(key), key);
            }
        }

        [Test]
        public void BackgroundImportProfileIsConsistent()
        {
            foreach (var key in RequiredKeys)
            {
                var path = WhiteRoomBackgroundImportSettings.BackgroundFolder + key + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.maxTextureSize,
                    Is.EqualTo(WhiteRoomBackgroundImportSettings.MaximumTextureSize), path);
                Assert.That(importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.CompressedHQ), path);
            }
        }

        [Test]
        public void ScenarioFadeAndCutCuesResolveAndReachStageDirector()
        {
            var database = AssetDatabase.LoadAssetAtPath<BackgroundDatabase>(DatabasePath);
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values;
            var fadeRow = rows.First(row => row.GetBackgroundCue().Transition == "fade");
            var cutRow = rows.First(row => row.GetBackgroundCue().Transition == "cut");
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);

            director.Apply(fadeRow);
            director.Apply(cutRow);

            Assert.That(database.TryGetSprite(fadeRow.GetBackgroundCue().Key, out _), Is.True);
            Assert.That(database.TryGetSprite(cutRow.GetBackgroundCue().Key, out _), Is.True);
            Assert.That(view.BackgroundCues[0].Transition, Is.EqualTo("fade"));
            Assert.That(view.BackgroundCues[0].Duration, Is.GreaterThan(0f));
            Assert.That(view.BackgroundCues[1].Transition, Is.EqualTo("cut"));
            Assert.That(view.BackgroundCues[1].Duration, Is.EqualTo(0f));
        }

        [Test]
        public void FullScenarioHasNoMissingBackgroundReferences()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(
                "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset");

            var report = DialogueValidationRunner.ValidateProfile(profile);

            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Background), Is.False);
        }
    }
}
