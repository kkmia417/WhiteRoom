using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class WhiteRoomSettingsAndQuitTests
    {
        private sealed class MemoryStorage : IWhiteRoomSettingsStorage
        {
            public string Json;
            public bool TryLoad(out string json) { json = Json; return !string.IsNullOrEmpty(Json); }
            public void Save(string json) { Json = json; }
        }

        private sealed class RecordingQuitter : IApplicationQuitter
        {
            public bool IsAvailable { get; set; }
            public string UnavailableReason { get; set; } = "Unavailable";
            public int QuitCount { get; private set; }
            public void Quit() { QuitCount++; }
        }

        [Test]
        public void VersionedSettingsRoundTripEveryProductSetting()
        {
            var storage = new MemoryStorage();
            var store = new VersionedDialogueSettingsStore(storage);
            var source = new DialogueSettings
            {
                TextSpeed = 0.8f,
                AutoAdvanceDelay = 2.7f,
                BgmVolume = 0.3f,
                SeVolume = 0.4f,
                VoiceVolume = 0.5f,
                SkipReadOnly = false
            };
            store.Save(source);
            StringAssert.Contains("\"SchemaVersion\":1", storage.Json);

            var restored = new DialogueSettings();
            store.Load(restored);

            Assert.That(restored.TextSpeed, Is.EqualTo(0.8f));
            Assert.That(restored.AutoAdvanceDelay, Is.EqualTo(2.7f));
            Assert.That(restored.BgmVolume, Is.EqualTo(0.3f));
            Assert.That(restored.SeVolume, Is.EqualTo(0.4f));
            Assert.That(restored.VoiceVolume, Is.EqualTo(0.5f));
            Assert.That(restored.SkipReadOnly, Is.False);
        }

        [Test]
        public void FutureSettingsSchemaIsRejectedWithoutOverwritingDefaults()
        {
            var storage = new MemoryStorage { Json = "{\"SchemaVersion\":99,\"TextSpeed\":1}" };
            var store = new VersionedDialogueSettingsStore(storage);
            var settings = new DialogueSettings { TextSpeed = 0.25f };

            store.Load(settings);

            Assert.That(settings.TextSpeed, Is.EqualTo(0.25f));
            Assert.That(store.LastWarning, Does.Contain("99"));
        }

        [Test]
        public void ConfigChangesApplyAndPersistImmediately()
        {
            var storage = new MemoryStorage();
            var store = new VersionedDialogueSettingsStore(storage);
            var settings = new DialogueSettings();
            var changes = 0;
            settings.Changed += () => changes++;
            var controller = new ConfigScreenController(settings, store);

            controller.SetTextSpeed(0.9f);
            controller.SetAutoDelay(3.2f);
            controller.SetBgmVolume(0.2f);
            controller.SetSeVolume(0.3f);
            controller.SetVoiceVolume(0.4f);
            controller.SetSkipReadOnly(false);

            Assert.That(changes, Is.EqualTo(6));
            var saved = JsonUtility.FromJson<WhiteRoomSettingsDocument>(storage.Json);
            Assert.That(saved.TextSpeed, Is.EqualTo(0.9f));
            Assert.That(saved.SkipReadOnly, Is.False);
        }

        [Test]
        public void QuitServiceCallsAvailableAdapterAndExplainsUnavailablePlatform()
        {
            var available = new RecordingQuitter { IsAvailable = true };
            Assert.That(new ApplicationQuitService(available).ConfirmQuit(), Is.True);
            Assert.That(available.QuitCount, Is.EqualTo(1));

            var unavailable = new RecordingQuitter { IsAvailable = false, UnavailableReason = "Use browser close" };
            var service = new ApplicationQuitService(unavailable);
            Assert.That(service.ConfirmQuit(), Is.False);
            Assert.That(unavailable.QuitCount, Is.Zero);
            Assert.That(service.UnavailableReason, Is.EqualTo("Use browser close"));
        }

        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(3440, 1440)]
        public void SafeAreaAnchorsRemainOperationalAtSupportedAspectRatios(int width, int height)
        {
            var root = new GameObject("safe-area", typeof(RectTransform));
            try
            {
                var target = root.GetComponent<RectTransform>();
                var safe = new Rect(width * 0.04f, height * 0.03f, width * 0.92f, height * 0.94f);
                NovelSafeAreaUtility.Apply(target, safe, width, height);
                Assert.That(target.anchorMin.x, Is.EqualTo(0.04f).Within(0.001f));
                Assert.That(target.anchorMin.y, Is.EqualTo(0.03f).Within(0.001f));
                Assert.That(target.anchorMax.x, Is.EqualTo(0.96f).Within(0.001f));
                Assert.That(target.anchorMax.y, Is.EqualTo(0.97f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
