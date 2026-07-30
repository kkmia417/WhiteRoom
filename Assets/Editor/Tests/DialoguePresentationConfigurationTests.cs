using System;
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
    public sealed class DialoguePresentationConfigurationTests
    {
        private const string ValidationProfilePath =
            "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset";

        private sealed class PresentationIssueSource : IDialoguePresentationIssueSource
        {
            public event Action<DialoguePresentationIssueContext> PresentationIssueRaised;

            public void Raise(DialoguePresentationIssueContext context)
            {
                PresentationIssueRaised?.Invoke(context);
            }
        }

        [TearDown]
        public void TearDown()
        {
            DestroySceneObjects<DialogueStageView>();
            DestroySceneObjects<DialogueAudioPlayer>();
            DestroySceneObjects<Canvas>();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DefaultConfigurationReferencesThreeDatabasesAndValidationProfile()
        {
            var configuration = NovelPresentationConfiguration.LoadDefault();
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(ValidationProfilePath);

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.BackgroundDatabase, Is.Not.Null);
            Assert.That(configuration.CharacterDatabase, Is.Not.Null);
            Assert.That(configuration.AudioDatabase, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CsvFiles.Count, Is.EqualTo(1));
            Assert.That(profile.BackgroundDatabase, Is.SameAs(configuration.BackgroundDatabase));
            Assert.That(profile.CharacterDatabase, Is.SameAs(configuration.CharacterDatabase));
            Assert.That(profile.AudioDatabase, Is.SameAs(configuration.AudioDatabase));
            Assert.That(profile.MissingReferenceSeverity, Is.EqualTo(DialogueValidationSeverity.Warning));
            Assert.That(profile.RunAsBuildGate, Is.True);
            Assert.That(profile.FailBuildOnErrors, Is.True);
        }

        [Test]
        public void FactoryWiresConfiguredDatabasesAndReusesPresentationObjects()
        {
            var configuration = NovelPresentationConfiguration.LoadDefault();

            var presentation = DialoguePresentationFactory.Ensure(
                configuration.BackgroundDatabase,
                configuration.CharacterDatabase,
                configuration.AudioDatabase);

            var serializedStage = new SerializedObject(presentation.StageView);
            var serializedAudio = new SerializedObject(presentation.AudioPlayer);
            Assert.That(
                serializedStage.FindProperty("backgroundDatabase").objectReferenceValue,
                Is.SameAs(configuration.BackgroundDatabase));
            Assert.That(
                serializedStage.FindProperty("characterDatabase").objectReferenceValue,
                Is.SameAs(configuration.CharacterDatabase));
            Assert.That(
                serializedAudio.FindProperty("audioDatabase").objectReferenceValue,
                Is.SameAs(configuration.AudioDatabase));
            var backgroundImage = (Image)serializedStage.FindProperty("backgroundImage").objectReferenceValue;
            Assert.That(backgroundImage, Is.Not.Null);
            Assert.That(backgroundImage.preserveAspect, Is.True);

            var second = DialoguePresentationFactory.Ensure(
                configuration.BackgroundDatabase,
                configuration.CharacterDatabase,
                configuration.AudioDatabase);
            Assert.That(second.StageView, Is.SameAs(presentation.StageView));
            Assert.That(second.AudioPlayer, Is.SameAs(presentation.AudioPlayer));
            Assert.That(FindSceneObjects<DialogueStageView>().Length, Is.EqualTo(1));
            Assert.That(FindSceneObjects<DialogueAudioPlayer>().Length, Is.EqualTo(1));
        }

        [Test]
        public void IssueLoggerIncludesDialogueColumnAndKeyAndSuppressesDuplicates()
        {
            var dialogueId = 42;
            var source = new PresentationIssueSource();
            using (var logger = new DialoguePresentationIssueLogger(() => dialogueId))
            {
                logger.Watch(source);
                const string message =
                    "DialoguePresentationIssueLogger: DialogueId=42 Column=Bgm Key='missing_bgm': missing";
                LogAssert.Expect(LogType.Warning, message);

                var issue = new DialoguePresentationIssueContext(
                    DialoguePresentationIssueKind.Bgm,
                    "missing_bgm",
                    "missing");
                source.Raise(issue);
                source.Raise(issue);

                dialogueId = 43;
                LogAssert.Expect(
                    LogType.Warning,
                    "DialoguePresentationIssueLogger: DialogueId=43 Column=Bgm Key='missing_bgm': missing");
                source.Raise(issue);
            }
        }

        [Test]
        public void ValidationProfileChecksEntireCsvAndIgnoresControlInstructions()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(ValidationProfilePath);

            var report = DialogueValidationRunner.ValidateProfile(profile);

            Assert.That(report.HasErrors, Is.False);
            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Background), Is.False);
            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Bgm &&
                message.Message.Contains("\"stop\"")), Is.False);
            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Characters &&
                message.Message.Contains("\"*\"")), Is.False);
            Assert.That(report.Messages.Any(message =>
                message.FieldName == DialogueSchema.Voice), Is.False);
        }

        private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void DestroySceneObjects<T>() where T : Component
        {
            foreach (var component in FindSceneObjects<T>())
            {
                if (component != null)
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }
    }
}
