using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueUnlockTests
    {
        private sealed class MemoryUnlockStorage : IDialogueUnlockStorage
        {
            private DialogueUnlockState _state;

            public bool TryLoad(out DialogueUnlockState state)
            {
                state = _state != null ? _state.Clone() : null;
                return _state != null;
            }

            public void Save(DialogueUnlockState state)
            {
                _state = state != null ? state.Clone() : new DialogueUnlockState();
            }

            public void Delete()
            {
                _state = null;
            }

            public bool Exists()
            {
                return _state != null;
            }
        }

        private sealed class CountingEventDispatcher : IDialogueEventDispatcher
        {
            public int DispatchCount { get; private set; }
            public string LastEventKey { get; private set; }

            public void Dispatch(DialogueEventContext context)
            {
                DispatchCount++;
                LastEventKey = context != null ? context.EventKey : string.Empty;
            }
        }

        [Test]
        public void Registry_MarkUnlocked_InfersCategoryAndRaisesEvent()
        {
            var registry = new DialogueUnlockRegistry();
            var events = new List<DialogueUnlockEventContext>();
            registry.Unlocked += events.Add;

            Assert.IsTrue(registry.MarkUnlocked("cg:hero_smile", 100));

            Assert.IsTrue(registry.IsUnlocked("cg:hero_smile"));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("cg:hero_smile", events[0].Entry.Id);
            Assert.AreEqual(DialogueUnlockCategories.Cg, events[0].Entry.Category);
            Assert.AreEqual(100, events[0].Entry.UnlockedAtUnix);
            CollectionAssert.AreEqual(new[] { "cg:hero_smile" }, registry.ListUnlockedIds(DialogueUnlockCategories.Cg));
        }

        [Test]
        public void Registry_DuplicateUnlock_DoesNotRaiseOrOverwriteEntry()
        {
            var registry = new DialogueUnlockRegistry();
            var eventCount = 0;
            registry.Unlocked += _ => eventCount++;

            Assert.IsTrue(registry.MarkUnlocked("scene:prologue", 10));
            Assert.IsFalse(registry.MarkUnlocked("scene:prologue", 20));

            var entry = registry.GetUnlock("scene:prologue");
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(10, entry.UnlockedAtUnix);
            Assert.AreEqual(1, registry.ListUnlocks().Count);
        }

        [Test]
        public void EventDispatcher_UnlocksPrefixedEventKeyAndForwardsEvent()
        {
            var registry = new DialogueUnlockRegistry();
            var inner = new CountingEventDispatcher();
            var dispatcher = new DialogueUnlockEventDispatcher(registry, inner);
            var context = new DialogueEventContext(null, "unlock:scene:prologue", DialogueSessionState.ShowingLine);

            dispatcher.Dispatch(context);

            Assert.IsTrue(registry.IsUnlocked("scene:prologue"));
            Assert.AreEqual(1, inner.DispatchCount);
            Assert.AreEqual("unlock:scene:prologue", inner.LastEventKey);
        }

        [Test]
        public void SaveService_PersistsAndLoadsGlobalUnlockState()
        {
            var storage = new MemoryUnlockStorage();
            var service = new DialogueUnlockSaveService(storage);
            var registry = new DialogueUnlockRegistry();
            registry.MarkUnlocked("cg:ending", 100);
            registry.MarkUnlocked("bonus:voice_test", "bonus", 110);

            Assert.IsTrue(service.Save(registry));

            var restored = new DialogueUnlockRegistry();
            Assert.IsTrue(service.LoadInto(restored));

            Assert.IsTrue(restored.IsUnlocked("cg:ending"));
            Assert.IsTrue(restored.IsUnlocked("bonus:voice_test"));
            CollectionAssert.AreEqual(new[] { "cg:ending" }, restored.ListUnlockedIds(DialogueUnlockCategories.Cg));
            CollectionAssert.AreEqual(new[] { "bonus:voice_test" }, restored.ListUnlockedIds("bonus"));
        }

        [Test]
        public void Registry_ResetCategoryAndReset_RemoveUnlocks()
        {
            var registry = new DialogueUnlockRegistry();
            var resetCount = 0;
            registry.ResetPerformed += _ => resetCount++;
            registry.MarkUnlocked("cg:a", 1);
            registry.MarkUnlocked("cg:b", 2);
            registry.MarkUnlocked("scene:a", 3);

            Assert.AreEqual(2, registry.ResetCategory(DialogueUnlockCategories.Cg));
            Assert.IsFalse(registry.IsUnlocked("cg:a"));
            Assert.IsTrue(registry.IsUnlocked("scene:a"));
            Assert.AreEqual(1, resetCount);

            Assert.AreEqual(1, registry.Reset());
            Assert.AreEqual(0, registry.ListUnlocks().Count);
            Assert.AreEqual(2, resetCount);
        }

        [Test]
        public void SaveService_Reset_DeletesPersistedUnlockState()
        {
            var storage = new MemoryUnlockStorage();
            var service = new DialogueUnlockSaveService(storage);
            var registry = new DialogueUnlockRegistry();
            registry.MarkUnlocked("cg:a", 1);

            Assert.IsTrue(service.Save(registry));
            Assert.IsTrue(service.Exists());

            Assert.IsTrue(service.Reset());
            Assert.IsFalse(service.Exists());

            DialogueUnlockState state;
            Assert.IsFalse(service.TryLoad(out state));
            Assert.AreEqual(0, state.ListUnlocks().Count);
        }
    }
}
