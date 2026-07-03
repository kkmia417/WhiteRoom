using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueConfigTests
    {
        private sealed class MemoryReadStore : IDialogueReadStore
        {
            public List<int> Saved = new List<int>();
            private readonly List<int> _initial;

            public MemoryReadStore(IEnumerable<int> initial = null)
            {
                _initial = initial != null ? new List<int>(initial) : new List<int>();
            }

            public IEnumerable<int> Load()
            {
                return _initial;
            }

            public void Save(IEnumerable<int> readIds)
            {
                Saved = new List<int>(readIds);
            }
        }

        [Test]
        public void Settings_ClampsAndRaisesChangedOnce()
        {
            // 既定値は 1f。クランプが「実際の変化」になるよう、購読前に 0.5f から始める。
            var settings = new DialogueSettings { MasterVolume = 0.5f };
            var changes = 0;
            settings.Changed += () => changes++;

            settings.MasterVolume = 2f; // clamps to 1 (0.5 -> 1) -> 変化したので 1 回発火
            settings.MasterVolume = 1f; // 既に 1 -> 変化なし -> 発火しない

            Assert.AreEqual(1f, settings.MasterVolume);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Settings_EffectiveVolume_IsMasterTimesChannel()
        {
            var settings = new DialogueSettings { MasterVolume = 0.5f, BgmVolume = 0.4f };

            Assert.AreEqual(0.2f, settings.EffectiveBgmVolume, 1e-5f);
        }

        [Test]
        public void Volume_LinearToDecibels_MutesAtZero()
        {
            Assert.AreEqual(-80f, DialogueVolume.LinearToDecibels(0f));
            Assert.AreEqual(0f, DialogueVolume.LinearToDecibels(1f), 1e-4f);
            Assert.Less(DialogueVolume.LinearToDecibels(0.5f), 0f);
        }

        [Test]
        public void TextSpeed_MapsNormalizedToInterval()
        {
            Assert.AreEqual(DialogueTextSpeed.DefaultSlowestInterval, DialogueTextSpeed.ToInterval(0f), 1e-6f);
            Assert.AreEqual(DialogueTextSpeed.DefaultFastestInterval, DialogueTextSpeed.ToInterval(1f), 1e-6f);
        }

        [Test]
        public void ReadRegistry_LoadsMarksAndSaves()
        {
            var store = new MemoryReadStore(new[] { 1, 2 });
            var registry = new DialogueReadRegistry(store);

            Assert.IsTrue(registry.IsRead(1));
            Assert.IsFalse(registry.IsRead(3));
            Assert.IsTrue(registry.MarkRead(3));
            Assert.IsFalse(registry.MarkRead(3)); // already read

            registry.Save();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, store.Saved);
        }

        [Test]
        public void Planner_Skip_StopsOnUnreadWhenReadOnly()
        {
            var planner = new DialoguePlaybackPlanner();
            var settings = new DialogueSettings { SkipReadOnly = true };

            var plan = planner.Plan(DialoguePlaybackMode.Skip, hasChoices: false, isRead: false, settings: settings);

            Assert.IsFalse(plan.ShouldAdvance);
            Assert.IsTrue(plan.CancelSkip);
        }

        [Test]
        public void Planner_Skip_AdvancesOnReadOrWhenNotReadOnly()
        {
            var planner = new DialoguePlaybackPlanner();

            var readPlan = planner.Plan(DialoguePlaybackMode.Skip, false, true, new DialogueSettings { SkipReadOnly = true });
            Assert.IsTrue(readPlan.ShouldAdvance);
            Assert.AreEqual(0f, readPlan.Delay);

            var anyPlan = planner.Plan(DialoguePlaybackMode.Skip, false, false, new DialogueSettings { SkipReadOnly = false });
            Assert.IsTrue(anyPlan.ShouldAdvance);
        }

        [Test]
        public void Planner_Auto_UsesConfiguredDelay_AndWaitsOnChoices()
        {
            var planner = new DialoguePlaybackPlanner();
            var settings = new DialogueSettings { AutoAdvanceDelay = 2.5f };

            var auto = planner.Plan(DialoguePlaybackMode.Auto, false, true, settings);
            Assert.IsTrue(auto.ShouldAdvance);
            Assert.AreEqual(2.5f, auto.Delay);

            var withChoices = planner.Plan(DialoguePlaybackMode.Auto, true, true, settings);
            Assert.IsFalse(withChoices.ShouldAdvance);
        }

        [Test]
        public void Planner_Skip_WaitsOnChoices()
        {
            var planner = new DialoguePlaybackPlanner();

            var plan = planner.Plan(DialoguePlaybackMode.Skip, hasChoices: true, isRead: true, settings: new DialogueSettings());

            Assert.IsFalse(plan.ShouldAdvance);
            Assert.IsFalse(plan.CancelSkip);
        }

        [Test]
        public void Planner_Normal_AlwaysWaits()
        {
            var planner = new DialoguePlaybackPlanner();

            var plan = planner.Plan(DialoguePlaybackMode.Normal, false, true, new DialogueSettings());

            Assert.IsFalse(plan.ShouldAdvance);
            Assert.IsFalse(plan.CancelSkip);
        }
    }
}
