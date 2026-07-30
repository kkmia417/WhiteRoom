using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class ScreenshotCaptureServiceTests
    {
        private sealed class MemoryStorage : IScreenshotStorage
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public bool Available = true;
            public bool ThrowOnWrite;
            public bool IsAvailable => Available;
            public string UnavailableReason => Available ? string.Empty : "No filesystem capability";
            public string DirectoryPath => "memory://Screenshots";
            public bool Exists(string fileName) => Files.ContainsKey(fileName);
            public void WritePng(string fileName, byte[] pngBytes)
            {
                if (ThrowOnWrite)
                    throw new IOException("disk full");
                Files.Add(fileName, pngBytes);
            }
        }

        [Test]
        public void FileNamePolicyAddsStableSuffixWithoutOverwritingTimestampCollision()
        {
            var existing = new HashSet<string>
            {
                "WhiteRoom_20260730_120102_345Z.png",
                "WhiteRoom_20260730_120102_345Z_001.png"
            };

            var name = ScreenshotFileNamePolicy.CreateUnique(
                new DateTime(2026, 7, 30, 12, 1, 2, 345, DateTimeKind.Utc),
                existing.Contains);

            Assert.That(name, Is.EqualTo("WhiteRoom_20260730_120102_345Z_002.png"));
        }

        [Test]
        public void FileStorageRejectsTraversalAndNeverOverwritesExistingPng()
        {
            var directory = Path.Combine(Path.GetTempPath(), "WhiteRoomScreenshotTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileScreenshotStorage(directory);
                Assert.Throws<ArgumentException>(() => storage.WritePng("../escape.png", new byte[] { 1 }));
                storage.WritePng("capture.png", new byte[] { 1, 2, 3 });
                Assert.Throws<IOException>(() => storage.WritePng("capture.png", new byte[] { 4 }));
                Assert.That(File.ReadAllBytes(Path.Combine(directory, "capture.png")), Is.EqualTo(new byte[] { 1, 2, 3 }));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void UnsupportedAndCaptureFailuresReturnClassifiedNonFatalResults()
        {
            var unsupportedStorage = new MemoryStorage { Available = false };
            var unsupported = new ScreenshotCaptureService(unsupportedStorage);
            ScreenshotCaptureResult result = null;
            unsupported.CaptureCompleted += next => result = next;

            IEnumerator routine;
            Assert.That(unsupported.TryBegin(out routine), Is.False);
            Assert.That(routine, Is.Null);
            Assert.That(result.Kind, Is.EqualTo(ScreenshotCaptureResultKind.Unsupported));

            var failed = new ScreenshotCaptureService(
                new MemoryStorage(),
                captureProvider: () => throw new InvalidOperationException("GPU unavailable"));
            failed.CaptureCompleted += next => result = next;
            Assert.That(failed.TryBegin(out routine), Is.True);
            Run(routine);
            Assert.That(result.Kind, Is.EqualTo(ScreenshotCaptureResultKind.CaptureFailed));
            Assert.That(failed.IsBusy, Is.False);
        }

        [Test]
        public void BusyGuardAndStorageFailureDoNotLeakTheCaptureState()
        {
            var storage = new MemoryStorage { ThrowOnWrite = true };
            var service = new ScreenshotCaptureService(
                storage,
                () => new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                CreateTexture);
            var results = new List<ScreenshotCaptureResult>();
            service.CaptureCompleted += results.Add;

            IEnumerator routine;
            Assert.That(service.TryBegin(out routine), Is.True);
            IEnumerator ignored;
            Assert.That(service.TryBegin(out ignored), Is.False);
            Assert.That(results[0].Kind, Is.EqualTo(ScreenshotCaptureResultKind.Busy));

            Run(routine);

            Assert.That(results[1].Kind, Is.EqualTo(ScreenshotCaptureResultKind.StorageFailed));
            Assert.That(service.IsBusy, Is.False);
        }

        [Test]
        public void SuccessfulCaptureReportsFileNameAndDirectory()
        {
            var storage = new MemoryStorage();
            var service = new ScreenshotCaptureService(
                storage,
                () => new DateTime(2026, 7, 30, 1, 2, 3, 456, DateTimeKind.Utc),
                CreateTexture);
            ScreenshotCaptureResult result = null;
            service.CaptureCompleted += next => result = next;

            IEnumerator routine;
            Assert.That(service.TryBegin(out routine), Is.True);
            Run(routine);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Kind, Is.EqualTo(ScreenshotCaptureResultKind.Success));
            Assert.That(result.FileName, Is.EqualTo("WhiteRoom_20260730_010203_456Z.png"));
            Assert.That(result.DirectoryPath, Is.EqualTo(storage.DirectoryPath));
            Assert.That(result.Message, Does.Contain(result.FileName));
            Assert.That(result.Message, Does.Contain(result.DirectoryPath));
            Assert.That(storage.Files.Keys, Is.EquivalentTo(new[] { result.FileName }));
            Assert.That(service.IsBusy, Is.False);
        }

        [Test]
        public void CommandCatalogExposesCaptureActionAvailabilityAndPlatformReason()
        {
            var captures = 0;
            var available = false;
            var command = NovelCommandCatalog.Create(new NovelCommandBarBindings
                {
                    CaptureScreenshot = () => captures++,
                    CanCaptureScreenshot = () => available,
                    ScreenshotUnavailableReason = "No screenshot filesystem"
                })
                .Single(item => item.Id == NovelCommandId.Screenshot);

            Assert.That(command.CanExecute(), Is.False);
            Assert.That(command.UnavailableTooltip, Is.EqualTo("No screenshot filesystem"));
            available = true;
            Assert.That(command.CanExecute(), Is.True);
            command.Execute();
            Assert.That(captures, Is.EqualTo(1));
        }

        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(8, 4, TextureFormat.RGB24, false);
            texture.Apply(false, false);
            return texture;
        }

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
            }
        }
    }
}
