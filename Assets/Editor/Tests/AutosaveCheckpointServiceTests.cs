using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class AutosaveCheckpointServiceTests
    {
        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            private readonly Dictionary<int, DialogueSaveSlot> _slots = new Dictionary<int, DialogueSaveSlot>();

            public bool TryLoad(int slot, out DialogueSaveSlot data) => _slots.TryGetValue(slot, out data);
            public void Save(DialogueSaveSlot slot) => _slots[slot.SlotIndex] = slot;
            public void Delete(int slot) => _slots.Remove(slot);
            public bool Exists(int slot) => _slots.ContainsKey(slot);
            public IEnumerable<int> ListSlots() => new List<int>(_slots.Keys);
            public byte[] LoadThumbnail(int slot) => null;
            public void SaveThumbnail(int slot, byte[] pngBytes) { }
        }

        [Test]
        public void ChapterCheckpointWaitsForReadyStateAndWritesOnlyOnce()
        {
            var writes = 0;
            var title = string.Empty;
            var modes = new List<DialoguePlaybackMode>();
            var service = new AutosaveCheckpointService(
                value => { writes++; title = value; return true; },
                () => DialoguePlaybackMode.Auto,
                modes.Add);
            var row = ParseRow("1,speaker,text,-1,chapter_a,,");

            Invoke(service, "HandleProgressMarkerReached", new DialogueProgressEventContext(
                row,
                new DialogueProgressMarker(DialogueProgressMarkerType.Chapter, "chapter_a", true),
                new DialogueProgressState()));

            Assert.That(service.TryFlush(DialogueSessionState.Typing, false), Is.False);
            Assert.That(service.TryFlush(DialogueSessionState.ChoicePending, true), Is.False);
            Assert.That(writes, Is.Zero);
            Assert.That(service.PendingCount, Is.EqualTo(1));

            Assert.That(service.TryFlush(DialogueSessionState.WaitingForInput, false), Is.True);
            Assert.That(service.TryFlush(DialogueSessionState.WaitingForInput, false), Is.False);
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(title, Is.EqualTo("Auto: Chapter chapter_a"));
            CollectionAssert.AreEqual(
                new[] { DialoguePlaybackMode.Normal, DialoguePlaybackMode.Auto },
                modes);
        }

        [Test]
        public void ChoiceCheckpointTargetsFirstReadyPostChoiceLine()
        {
            var writes = new List<string>();
            var service = new AutosaveCheckpointService(title => { writes.Add(title); return true; });
            var choice = ParseRow("2,speaker,choose,-1,,,go->3");
            var destination = ParseRow("3,speaker,chosen,-1,,,");

            Invoke(service, "HandleLineCompleted", new DialogueEventContext(
                choice,
                string.Empty,
                DialogueSessionState.ChoicePending));
            Assert.That(service.PendingCount, Is.Zero, "Pre-choice state must never be saved.");

            Invoke(service, "HandleLineStarted", new DialogueEventContext(
                destination,
                string.Empty,
                DialogueSessionState.Typing));
            Assert.That(service.TryFlush(DialogueSessionState.Typing, false), Is.False);
            Assert.That(service.TryFlush(DialogueSessionState.WaitingForInput, false), Is.True);

            CollectionAssert.AreEqual(new[] { "Auto: Choice confirmed" }, writes);
        }

        [Test]
        public void EndingCheckpointFailureIsConsumedAndDoesNotThrowOrRetry()
        {
            var writes = 0;
            var modes = new List<DialoguePlaybackMode>();
            var service = new AutosaveCheckpointService(
                title => { writes++; throw new InvalidOperationException("disk unavailable"); },
                () => DialoguePlaybackMode.Skip,
                modes.Add);
            var ending = ParseRow("4,speaker,ending,-1,,ending_a,");

            Assert.DoesNotThrow(() => Invoke(service, "HandleLineCompleted", new DialogueEventContext(
                ending,
                string.Empty,
                DialogueSessionState.WaitingForInput)));

            Assert.That(writes, Is.EqualTo(1));
            Assert.That(service.PendingCount, Is.Zero);
            Assert.That(service.TryFlush(DialogueSessionState.WaitingForInput, false), Is.False);
            CollectionAssert.AreEqual(new[] { DialoguePlaybackMode.Normal }, modes,
                "Ending flow remains normalized while it transitions to the result screen.");
        }

        [Test]
        public void ContinueUsesNewestCategoryAndManualAutosaveQuickTieOrder()
        {
            var root = new GameObject("ContinueCandidate", typeof(DialogueManager), typeof(DialogueSaveSystem));
            try
            {
                var manager = root.GetComponent<DialogueManager>();
                var system = root.GetComponent<DialogueSaveSystem>();
                system.SetStorage(new MemoryStorage());
                var saves = new NovelSaveService(manager, system, 1, false);

                system.Service.Save(0, new DialogueSaveData { CurrentDialogueId = 1 }, "auto", true, 50);
                system.Service.QuickSave(new DialogueSaveData { CurrentDialogueId = 2 }, "quick", 50);
                system.Service.Save(2, new DialogueSaveData { CurrentDialogueId = 3 }, "manual", false, 50);
                Assert.That(saves.GetContinueCandidate().SlotIndex, Is.EqualTo(2));

                system.Service.QuickSave(new DialogueSaveData { CurrentDialogueId = 4 }, "quick-new", 60);
                Assert.That(saves.GetContinueCandidate().SlotIndex, Is.EqualTo(DialogueSaveSystem.QuickSaveSlot));

                system.Service.Save(0, new DialogueSaveData { CurrentDialogueId = 5 }, "auto-new", true, 70);
                Assert.That(saves.GetContinueCandidate().SlotIndex, Is.EqualTo(DialogueSaveSystem.AutosaveSlot));
                saves.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static DialogueData ParseRow(string values)
        {
            var csv = new TextAsset("Id,Speaker,Text,NextId,ChapterKey,EndingKey,Choices\n" + values + "\n");
            return CsvLoader.Parse<DialogueData>(csv).Values.Single();
        }

        private static void Invoke(object target, string methodName, object argument)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, new[] { argument });
        }
    }
}
