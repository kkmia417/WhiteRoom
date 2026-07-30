using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class EndingFlowServiceTests
    {
        private const string ScenarioPath = "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        [Test]
        public void ScenarioContainsFourteenUniqueEndingKeysWithDisplayableResults()
        {
            var rows = LoadEndingRows();
            var unique = rows.GroupBy(row => row.EndingKey).Select(group => group.First()).ToArray();

            Assert.That(unique.Length, Is.EqualTo(14));
            foreach (var row in unique)
            {
                var result = EndingResultInfo.Create(row.EndingKey, row.Text, true);
                Assert.That(result.EndingKey, Is.EqualTo(row.EndingKey));
                Assert.That(result.Type, Is.Not.Empty, row.EndingKey);
                Assert.That(result.DisplayName, Is.Not.Empty, row.EndingKey);
            }
        }

        [Test]
        public void ResultWaitsForDialogueEndAndPersistsBeforeResetAndTitle()
        {
            var calls = new List<string>();
            var service = new EndingFlowService(
                () => { calls.Add("persist"); return true; },
                () => calls.Add("reset"),
                () => calls.Add("title"));
            var row = LoadEndingRows().First(item => item.EndingKey == "bad_too_good");

            RaiseMarker(service, row, true);
            Assert.That(service.CurrentResult, Is.Null, "The final line must remain visible until DialogueEnded.");

            RaiseDialogueEnded(service);
            Assert.That(service.CurrentResult, Is.Not.Null);
            Assert.That(service.CurrentResult.Type, Is.EqualTo("BAD END"));
            Assert.That(service.CurrentResult.DisplayName, Is.EqualTo("できすぎる子"));

            Assert.That(service.ConfirmAndReturnToTitle(), Is.True);
            CollectionAssert.AreEqual(new[] { "persist", "reset", "title" }, calls);
            Assert.That(service.IsTransitionInProgress, Is.True);
            service.NotifySceneLoaded();
            Assert.That(service.IsInputBlocked, Is.False);
        }

        [Test]
        public void RepeatEndingStillShowsResultWithoutBeingFirstReach()
        {
            var service = new EndingFlowService(() => true, () => { }, () => { });
            var row = LoadEndingRows().First(item => item.EndingKey == "normal_drain_alone");

            RaiseMarker(service, row, false);
            RaiseDialogueEnded(service);

            Assert.That(service.CurrentResult, Is.Not.Null);
            Assert.That(service.CurrentResult.EndingKey, Is.EqualTo("normal_drain_alone"));
            Assert.That(service.CurrentResult.IsFirstReach, Is.False);
        }

        [Test]
        public void PersistedUnlockOverridesFreshSessionMarkerFirstReach()
        {
            var service = new EndingFlowService(
                () => true,
                key => key == "bad_too_good",
                () => { },
                () => { });
            var row = LoadEndingRows().First(item => item.EndingKey == "bad_too_good");

            RaiseMarker(service, row, true);
            RaiseDialogueEnded(service);

            Assert.That(service.CurrentResult.IsFirstReach, Is.False);
        }

        [Test]
        public void PersistenceFailureKeepsResultOpenAndDoesNotTransition()
        {
            var reset = 0;
            var title = 0;
            var failure = string.Empty;
            var service = new EndingFlowService(() => false, () => reset++, () => title++);
            service.TransitionFailed += message => failure = message;
            var row = LoadEndingRows().First();
            RaiseMarker(service, row, true);
            RaiseDialogueEnded(service);

            Assert.That(service.ConfirmAndReturnToTitle(), Is.False);
            Assert.That(service.IsAwaitingConfirmation, Is.True);
            Assert.That(reset, Is.Zero);
            Assert.That(title, Is.Zero);
            Assert.That(failure, Is.Not.Empty);
        }

        private static DialogueData[] LoadEndingRows()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            Assert.That(csv, Is.Not.Null);
            return CsvLoader.Parse<DialogueData>(csv).Values
                .Where(row => !string.IsNullOrWhiteSpace(row.EndingKey))
                .ToArray();
        }

        private static void RaiseMarker(EndingFlowService service, DialogueData row, bool firstReach)
        {
            Invoke(service, "HandleProgressMarkerReached", new DialogueProgressEventContext(
                row,
                new DialogueProgressMarker(DialogueProgressMarkerType.Ending, row.EndingKey, firstReach),
                new DialogueProgressState()));
        }

        private static void RaiseDialogueEnded(EndingFlowService service)
        {
            Invoke(service, "HandleDialogueEnded", new DialogueEventContext(
                null,
                string.Empty,
                DialogueSessionState.Ended));
        }

        private static void Invoke(object target, string methodName, object argument)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, new[] { argument });
        }
    }
}
