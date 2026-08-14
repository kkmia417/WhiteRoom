using System.Collections.Generic;
using System.Linq;
using kkmia.TalkSystem;
using kkmia.TalkSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class WhiteRoomCharacterAssetsTests
    {
        private const string DatabasePath =
            "Assets/Presentation/Databases/WhiteRoomCharacterExpressionDatabase.asset";
        private const string ScenarioPath =
            "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        private static readonly Dictionary<string, string[]> RequiredStageExpressions =
            new Dictionary<string, string[]>
            {
                { "Rei", new[] { "blank", "determined", "frozen", "lost", "running", "serious", "shocked", "soft", "surprise", "tired" } },
                { "Nagi", new[] { "angry", "focus", "running", "serious", "shadow", "shocked", "smile", "soft", "tired", "wary" } },
                { "Researcher", new[] { "guilty", "nervous", "neutral" } },
                { "Placeholder", new[] { "neutral" } }
            };

        private static readonly Dictionary<string, string> RequiredAliases =
            new Dictionary<string, string>
            {
                { "レイ", "Rei" },
                { "ナギ", "Nagi" },
                { "研究員", "Researcher" },
                { "若い研究員", "Researcher" }
            };

        private sealed class RecordingStageView : IDialogueStageView
        {
            public readonly List<string> CharacterCalls = new List<string>();
            public int ClearCount;

            public void SetBackground(string backgroundKey, bool clear, string transition, float duration) { }

            public void SetCharacter(string slot, string characterKey, string expression, string animation)
            {
                CharacterCalls.Add(slot + "|" + characterKey + "|" + expression + "|" + animation);
            }

            public void RemoveCharacter(string slot, string characterKey, string animation) { }

            public void ClearCharacters()
            {
                ClearCount++;
            }
        }

        [Test]
        public void RequiredCharactersExpressionsAndAliasesResolveUniquely()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterExpressionDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.Characters.GroupBy(character => character.speakerKey)
                .All(group => group.Count() == 1), Is.True);

            foreach (var pair in RequiredStageExpressions)
            {
                Assert.That(database.TryGetCharacter(pair.Key, out var canonical), Is.True, pair.Key);
                Assert.That(canonical.defaultSprite, Is.Not.Null, pair.Key);
                foreach (var expression in pair.Value)
                    Assert.That(database.TryGetExactSprite(pair.Key, expression, out var sprite) && sprite != null,
                        Is.True, pair.Key + ":" + expression);
            }

            foreach (var pair in RequiredAliases)
            {
                Assert.That(database.TryGetCharacter(pair.Key, out var alias), Is.True, pair.Key);
                Assert.That(database.TryGetCharacter(pair.Value, out var canonical), Is.True, pair.Value);
                Assert.That(alias.defaultSprite, Is.SameAs(canonical.defaultSprite), pair.Key);
                foreach (var expression in RequiredStageExpressions[pair.Value])
                {
                    Assert.That(database.TryGetExactSprite(pair.Key, expression, out var aliasSprite), Is.True);
                    Assert.That(database.TryGetExactSprite(pair.Value, expression, out var canonicalSprite), Is.True);
                    Assert.That(aliasSprite, Is.SameAs(canonicalSprite), pair.Key + ":" + expression);
                }
            }
        }

        [Test]
        public void CharacterImportProfileAndCanvasAreConsistent()
        {
            var spritePaths = RequiredStageExpressions.SelectMany(pair => pair.Value.Select(expression =>
                WhiteRoomCharacterImportSettings.CharacterFolder + pair.Key + "/" + pair.Key + "_" + expression + ".png"));

            foreach (var path in spritePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                Assert.That(importer, Is.Not.Null, path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite.texture.width, Is.EqualTo(1024), path);
                Assert.That(sprite.texture.height, Is.EqualTo(1536), path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(WhiteRoomCharacterImportSettings.PixelsPerUnit), path);
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                Assert.That(textureSettings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Custom), path);
                Assert.That(textureSettings.spritePivot, Is.EqualTo(new Vector2(0.5f, 0f)), path);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(WhiteRoomCharacterImportSettings.MaximumTextureSize), path);
            }
        }

        [Test]
        public void ScenarioStageDirectivesResolveAndReachAllSlotsFadeAndClear()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterExpressionDatabase>(DatabasePath);
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values;

            foreach (var directive in rows.SelectMany(row => row.GetStageDirectives())
                         .Where(directive => !directive.IsClearAll && !directive.IsExit))
            {
                Assert.That(database.TryGetExactSprite(directive.CharacterKey, directive.Expression, out _),
                    Is.True, directive.ToString());
            }

            var centerFade = rows.First(row => row.GetStageDirectives().Any(directive =>
                directive.Slot == DialogueStageSlot.Center && directive.Animation == "fadein"));
            var leftAndRight = rows.First(row =>
                row.GetStageDirectives().Any(directive => directive.Slot == DialogueStageSlot.Left) &&
                row.GetStageDirectives().Any(directive => directive.Slot == DialogueStageSlot.Right));
            var clear = rows.First(row => row.GetStageDirectives().Any(directive => directive.IsClearAll));
            var view = new RecordingStageView();
            var director = new DialogueStageDirector(view);

            director.Apply(centerFade);
            director.Apply(leftAndRight);
            director.Apply(clear);

            Assert.That(view.CharacterCalls.Any(call => call.StartsWith("center|") && call.EndsWith("|fadein")), Is.True);
            Assert.That(view.CharacterCalls.Any(call => call.StartsWith("left|")), Is.True);
            Assert.That(view.CharacterCalls.Any(call => call.StartsWith("right|")), Is.True);
            Assert.That(view.ClearCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void OpeningConversationKeepsReiAndSubstituteSpeakerSideBySide()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values;

            foreach (var id in new[] { 1000019, 1000020 })
            {
                var row = rows.Single(item => item.Id == id);
                var directives = row.GetStageDirectives().ToArray();
                var visible = directives.Where(item => !item.IsClearAll && !item.IsExit).ToArray();

                Assert.That(directives.Count(item => item.IsClearAll), Is.EqualTo(1), id.ToString());
                Assert.That(visible, Has.Length.EqualTo(2), id.ToString());
                Assert.That(visible.Select(item => item.Slot), Does.Contain(DialogueStageSlot.Left), id.ToString());
                Assert.That(visible.Select(item => item.Slot), Does.Contain(DialogueStageSlot.Right), id.ToString());
                Assert.That(visible.Select(item => item.CharacterKey), Does.Contain("Rei"), id.ToString());
                Assert.That(visible.Select(item => item.CharacterKey), Does.Contain("PlaceholderRight"), id.ToString());
            }
        }

        [Test]
        public void MissingAssetConversationKeepsTwoDistinctSubstitutesVisible()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = CsvLoader.Parse<DialogueData>(scenario).Values;
            var row = rows.Single(item => item.Id == 1000077);
            var directives = row.GetStageDirectives().ToArray();
            var visible = directives.Where(item => !item.IsClearAll && !item.IsExit).ToArray();

            Assert.That(visible, Has.Length.EqualTo(2));
            Assert.That(visible.Select(item => item.CharacterKey), Is.EquivalentTo(new[]
            {
                "PlaceholderLeft",
                "PlaceholderRight"
            }));

            var state = new DialogueStageState();
            state.Apply(directives);
            Assert.That(state.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                DialogueStageSlot.Left,
                DialogueStageSlot.Right
            }));
        }

        [Test]
        public void OpeningChapterDoesNotRevealNagi()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var row = CsvLoader.Parse<DialogueData>(scenario).Values.Single(item => item.Id == 1000001);
            var visible = row.GetStageDirectives()
                .Where(item => !item.IsClearAll && !item.IsExit)
                .ToArray();

            Assert.That(visible.Select(item => item.CharacterKey), Does.Not.Contain("Nagi"));
            Assert.That(visible.Select(item => item.CharacterKey), Is.EquivalentTo(new[]
            {
                "Rei",
                "PlaceholderRight"
            }));
        }

        [Test]
        public void MissingExpressionFallsBackToDefaultAndRaisesWarning()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterExpressionDatabase>(DatabasePath);
            Assert.That(database.TryGetCharacter("Nagi", out var character), Is.True);
            Assert.That(database.TryGetSprite("Nagi", "missing-expression", out var fallback, out var usedFallback), Is.True);
            Assert.That(usedFallback, Is.True);
            Assert.That(fallback, Is.SameAs(character.defaultSprite));

            var objectUnderTest = new GameObject("CharacterFallbackTest");
            var imageObject = new GameObject("Center", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var view = objectUnderTest.AddComponent<DialogueStageView>();
                var image = imageObject.GetComponent<Image>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("characterDatabase").objectReferenceValue = database;
                var slots = serialized.FindProperty("slots");
                slots.arraySize = 1;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("slot").stringValue = DialogueStageSlot.Center;
                slots.GetArrayElementAtIndex(0).FindPropertyRelative("image").objectReferenceValue = image;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DialoguePresentationIssueContext issue = null;
                view.PresentationIssueRaised += context => issue = context;
                LogAssert.Expect(LogType.Warning,
                    "[DialogueStageView] Expression \"missing-expression\" for character \"Nagi\" is missing; using the default sprite.");

                view.SetCharacter(DialogueStageSlot.Center, "Nagi", "missing-expression", string.Empty);

                Assert.That(image.sprite, Is.SameAs(character.defaultSprite));
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Kind, Is.EqualTo(DialoguePresentationIssueKind.Character));
            }
            finally
            {
                Object.DestroyImmediate(imageObject);
                Object.DestroyImmediate(objectUnderTest);
            }
        }

        [Test]
        public void FullScenarioHasNoMissingCharacterOrExpressionReferences()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(
                "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset");

            var report = DialogueValidationRunner.ValidateProfile(profile);

            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Speaker ||
                message.FieldName == DialogueSchema.EmotionKey ||
                message.FieldName == DialogueSchema.Characters), Is.False);
        }
    }
}
