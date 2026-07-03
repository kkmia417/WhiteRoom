using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueSaveTests
    {
        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            public readonly Dictionary<int, DialogueSaveSlot> Slots = new Dictionary<int, DialogueSaveSlot>();
            public readonly Dictionary<int, byte[]> Thumbs = new Dictionary<int, byte[]>();

            public bool TryLoad(int slot, out DialogueSaveSlot data)
            {
                return Slots.TryGetValue(slot, out data);
            }

            public void Save(DialogueSaveSlot slot)
            {
                Slots[slot.SlotIndex] = slot;
            }

            public void Delete(int slot)
            {
                Slots.Remove(slot);
                Thumbs.Remove(slot);
            }

            public bool Exists(int slot)
            {
                return Slots.ContainsKey(slot);
            }

            public IEnumerable<int> ListSlots()
            {
                return new List<int>(Slots.Keys);
            }

            public byte[] LoadThumbnail(int slot)
            {
                byte[] bytes;
                return Thumbs.TryGetValue(slot, out bytes) ? bytes : null;
            }

            public void SaveThumbnail(int slot, byte[] pngBytes)
            {
                Thumbs[slot] = pngBytes;
            }
        }

        private sealed class FakeContributor : IDialogueSaveContributor
        {
            public int CaptureCount;
            public int RestoreCount;
            public string Restored;

            public void Capture(DialogueSaveData data)
            {
                CaptureCount++;
                data.SetExtra("stage", "Alice@left");
            }

            public void Restore(DialogueSaveData data)
            {
                RestoreCount++;
                data.TryGetExtra("stage", out Restored);
            }
        }

        private sealed class ThrowingSaveStorage : IDialogueSaveStorage
        {
            public bool TryLoad(int slot, out DialogueSaveSlot data)
            {
                data = null;
                return false;
            }

            public void Save(DialogueSaveSlot slot)
            {
                throw new IOException("disk full");
            }

            public void Delete(int slot) { }
            public bool Exists(int slot) { return false; }
            public IEnumerable<int> ListSlots() { return new List<int>(); }
            public byte[] LoadThumbnail(int slot) { return null; }
            public void SaveThumbnail(int slot, byte[] pngBytes) { }
        }

        private sealed class LegacyDataMigration : IDialogueSaveDataMigration
        {
            public int Count;

            public int FromSchemaVersion
            {
                get { return 0; }
            }

            public int ToSchemaVersion
            {
                get { return DialogueSaveSchema.CurrentVersion; }
            }

            public void Migrate(DialogueSaveData data, DialogueSaveMigrationContext context)
            {
                Count++;
                data.SetExtra("migrated-channel", context.ProductChannel);
            }
        }

        [Test]
        public void ExtraState_SetOverwritesAndGetReturnsValue()
        {
            var data = new DialogueSaveData();
            data.SetExtra("bgm", "theme");
            data.SetExtra("bgm", "battle");

            string value;
            Assert.IsTrue(data.TryGetExtra("bgm", out value));
            Assert.AreEqual("battle", value);
            Assert.AreEqual(1, data.ExtraState.Count);
            Assert.IsFalse(data.TryGetExtra("missing", out _));
        }

        [Test]
        public void Service_Save_RunsContributorsAndPersistsMetadata()
        {
            var storage = new MemoryStorage();
            var contributor = new FakeContributor();
            var service = new DialogueSaveService(storage, new[] { contributor });

            var slot = service.Save(2, new DialogueSaveData { CurrentDialogueId = 7 }, "Chapter 1", true, 1700000000);

            Assert.AreEqual(1, contributor.CaptureCount);
            Assert.AreEqual(2, slot.SlotIndex);
            Assert.AreEqual("Chapter 1", slot.Title);
            Assert.IsTrue(slot.IsAutosave);
            Assert.IsTrue(storage.Exists(2));
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, slot.SchemaVersion);
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, slot.Data.SchemaVersion);

            string stage;
            Assert.IsTrue(storage.Slots[2].Data.TryGetExtra("stage", out stage));
            Assert.AreEqual("Alice@left", stage);
        }

        [Test]
        public void Service_LoadThenApplyRestore_RoundTripsAndRestoresContributors()
        {
            var storage = new MemoryStorage();
            var contributor = new FakeContributor();
            var service = new DialogueSaveService(storage, new[] { contributor });
            service.Save(1, new DialogueSaveData { CurrentDialogueId = 9 }, "t", false, 0);

            var loaded = service.Load(1);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(9, loaded.Data.CurrentDialogueId);

            service.ApplyRestore(loaded.Data);
            Assert.AreEqual(1, contributor.RestoreCount);
            Assert.AreEqual("Alice@left", contributor.Restored);
        }

        [Test]
        public void Service_ListSlots_ReturnsSorted()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage);
            service.Save(3, new DialogueSaveData(), "c", false, 0);
            service.Save(1, new DialogueSaveData(), "a", false, 0);
            service.Save(2, new DialogueSaveData(), "b", false, 0);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, service.ListSlots());
        }

        [Test]
        public void Service_Delete_RemovesSlot()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage);
            service.Save(1, new DialogueSaveData(), "a", false, 0);

            service.Delete(1);

            Assert.IsFalse(service.Exists(1));
            Assert.IsNull(service.Load(1));
        }

        [Test]
        public void Service_GetSlotViewModel_ReportsEmptySlot()
        {
            var service = new DialogueSaveService(new MemoryStorage());

            var model = service.GetSlotViewModel(4);

            Assert.AreEqual(4, model.SlotIndex);
            Assert.AreEqual(DialogueSaveSlotCategory.Manual, model.Category);
            Assert.IsTrue(model.IsEmpty);
            Assert.IsFalse(model.CanLoad);
            Assert.IsFalse(model.HasError);
        }

        [Test]
        public void Service_ViewModels_CategorizeSlotsAndExposeThumbnailMetadata()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage);
            var thumbnail = new byte[] { 1, 2, 3 };

            service.Save(DialogueSaveSlotConventions.AutosaveSlot, new DialogueSaveData { CurrentDialogueId = 1 }, "auto", true, 10);
            service.QuickSave(new DialogueSaveData { CurrentDialogueId = 2 }, "quick", 30);
            service.Save(2, new DialogueSaveData { CurrentDialogueId = 3 }, "manual", false, 20);
            storage.Thumbs[2] = thumbnail;

            var autosave = service.GetSlotViewModel(DialogueSaveSlotConventions.AutosaveSlot, includeThumbnail: false);
            var quicksave = service.GetSlotViewModel(DialogueSaveSlotConventions.QuickSaveSlot, includeThumbnail: false);
            var manual = service.GetSlotViewModel(2);

            Assert.AreEqual(DialogueSaveSlotCategory.Autosave, autosave.Category);
            Assert.AreEqual(DialogueSaveSlotCategory.QuickSave, quicksave.Category);
            Assert.AreEqual(DialogueSaveSlotCategory.Manual, manual.Category);
            Assert.AreEqual("manual", manual.Title);
            Assert.AreEqual(20, manual.SavedAtUnix);
            Assert.IsTrue(manual.CanLoad);
            Assert.IsTrue(manual.HasThumbnail);
            CollectionAssert.AreEqual(thumbnail, manual.ThumbnailPngBytes);
        }

        [Test]
        public void Service_GetLatestContinueCandidate_UsesNewestLoadableSlot()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage);

            service.Save(DialogueSaveSlotConventions.AutosaveSlot, new DialogueSaveData { CurrentDialogueId = 1 }, "auto", true, 10);
            service.QuickSave(new DialogueSaveData { CurrentDialogueId = 2 }, "quick", 30);
            service.Save(2, new DialogueSaveData { CurrentDialogueId = 3 }, "manual", false, 20);

            var latest = service.GetLatestContinueCandidate(includeThumbnail: false);
            var latestWithoutQuick = service.GetLatestContinueCandidate(includeQuickSaves: false, includeThumbnail: false);
            var latestManualOnly = service.GetLatestContinueCandidate(includeAutosaves: false, includeQuickSaves: false, includeThumbnail: false);

            Assert.AreEqual(DialogueSaveSlotConventions.QuickSaveSlot, latest.SlotIndex);
            Assert.AreEqual(2, latestWithoutQuick.SlotIndex);
            Assert.AreEqual(2, latestManualOnly.SlotIndex);
        }

        [Test]
        public void Service_QuickLoadAndDelete_UseReservedQuickSaveSlot()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage);

            service.QuickSave(new DialogueSaveData { CurrentDialogueId = 5 }, "quick", 99);
            var loaded = service.QuickLoad();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(DialogueSaveSlotConventions.QuickSaveSlot, loaded.SlotIndex);
            Assert.AreEqual(5, loaded.Data.CurrentDialogueId);

            service.Delete(DialogueSaveSlotConventions.QuickSaveSlot);
            var model = service.GetSlotViewModel(DialogueSaveSlotConventions.QuickSaveSlot);

            Assert.IsTrue(model.IsEmpty);
            Assert.IsNull(service.GetLatestContinueCandidate(includeThumbnail: false));
        }

        [Test]
        public void Service_SaveFailure_ReturnsNullAndReportsFailure()
        {
            var service = new DialogueSaveService(new ThrowingSaveStorage());
            DialogueSaveOperationResult reported = null;
            service.OperationFailed += result => reported = result;

            var slot = service.Save(1, new DialogueSaveData(), "a", false, 0);

            Assert.IsNull(slot);
            Assert.IsNotNull(reported);
            Assert.AreEqual(DialogueSaveOperation.Save, reported.Operation);
            Assert.AreEqual(1, reported.SlotIndex);
            Assert.IsTrue(service.LastResult.Failed);
            StringAssert.Contains("disk full", service.LastResult.Message);
        }

        [Test]
        public void Service_LoadThumbnail_MissingThumbnailReturnsNullWithoutFailure()
        {
            var service = new DialogueSaveService(new MemoryStorage());

            var thumbnail = service.LoadThumbnail(99);

            Assert.IsNull(thumbnail);
            Assert.IsNotNull(service.LastResult);
            Assert.IsFalse(service.LastResult.Failed);
            Assert.AreEqual(DialogueSaveOperation.LoadThumbnail, service.LastResult.Operation);
        }

        [Test]
        public void FileStorage_CorruptedJson_ReportsLoadFailure()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "slot_1.json"), "{not valid json");
                var service = new DialogueSaveService(new FileDialogueSaveStorage(directory));
                DialogueSaveOperationResult reported = null;
                service.OperationFailed += result => reported = result;

                var loaded = service.Load(1);

                Assert.IsNull(loaded);
                Assert.IsNotNull(reported);
                Assert.AreEqual(DialogueSaveOperation.Load, reported.Operation);
                Assert.IsTrue(service.LastResult.Failed);
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void FileStorage_CorruptedJson_LoadsPreviousBackupWhenAvailable()
        {
            var directory = CreateTempDirectory();
            try
            {
                var storage = new FileDialogueSaveStorage(directory);
                var service = new DialogueSaveService(storage);
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 10 }, "first", false, 1);
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 20 }, "second", false, 2);

                var slotPath = Path.Combine(directory, "slot_1.json");
                Assert.IsTrue(File.Exists(slotPath + ".bak"));
                File.WriteAllText(slotPath, "{not valid json");

                var loaded = service.Load(1);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(10, loaded.Data.CurrentDialogueId);
                Assert.AreEqual("first", loaded.Title);
                Assert.IsFalse(service.LastResult.Failed);
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void FileStorage_Delete_RemovesSlotAndBackup()
        {
            var directory = CreateTempDirectory();
            try
            {
                var storage = new FileDialogueSaveStorage(directory);
                var service = new DialogueSaveService(storage);
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 10 }, "first", false, 1);
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 20 }, "second", false, 2);

                var slotPath = Path.Combine(directory, "slot_1.json");
                Assert.IsTrue(File.Exists(slotPath + ".bak"));

                service.Delete(1);

                Assert.IsFalse(File.Exists(slotPath));
                Assert.IsFalse(File.Exists(slotPath + ".bak"));
                Assert.IsNull(service.Load(1));
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void FileStorage_Save_OverwritesExistingSlotAndLoadsLatestData()
        {
            var directory = CreateTempDirectory();
            try
            {
                var service = new DialogueSaveService(new FileDialogueSaveStorage(directory));
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 10 }, "first", false, 1);
                service.Save(1, new DialogueSaveData { CurrentDialogueId = 20 }, "second", false, 2);

                var loaded = service.Load(1);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(20, loaded.Data.CurrentDialogueId);
                Assert.AreEqual("second", loaded.Title);
                Assert.IsFalse(File.Exists(Path.Combine(directory, "slot_1.json.tmp")));
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void SaveSystem_SetStorage_UsesCustomStorage()
        {
            var go = new GameObject("DialogueSaveSystemTest");
            try
            {
                var system = go.AddComponent<DialogueSaveSystem>();
                var storage = new MemoryStorage();

                system.SetStorage(storage);
                system.Service.Save(5, new DialogueSaveData { CurrentDialogueId = 12 }, "custom", false, 0);

                Assert.IsTrue(storage.Exists(5));
                Assert.AreEqual(12, storage.Slots[5].Data.CurrentDialogueId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Service_Save_WritesContentAndProductMetadata()
        {
            var storage = new MemoryStorage();
            var service = new DialogueSaveService(storage, null, new DialogueSaveServiceOptions
            {
                ContentVersion = "chapter-2",
                ProductChannel = "demo"
            });

            var slot = service.Save(1, new DialogueSaveData(), "meta", false, 0);

            Assert.AreEqual("chapter-2", slot.ContentVersion);
            Assert.AreEqual("demo", slot.ProductChannel);
            Assert.AreEqual("chapter-2", slot.Data.ContentVersion);
            Assert.AreEqual("demo", slot.Data.ProductChannel);
        }

        [Test]
        public void Service_Load_RunsOlderDataMigrationAndPreservesExtraState()
        {
            var storage = new MemoryStorage();
            var data = new DialogueSaveData
            {
                SchemaVersion = 0,
                CurrentDialogueId = 1
            };
            data.SetExtra("bgm", "theme");
            storage.Slots[1] = new DialogueSaveSlot
            {
                SchemaVersion = 0,
                SlotIndex = 1,
                Data = data
            };

            var migration = new LegacyDataMigration();
            var service = new DialogueSaveService(storage, null, new DialogueSaveServiceOptions
            {
                ProductChannel = "demo"
            });
            service.AddDataMigration(migration);

            var loaded = service.Load(1);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, migration.Count);
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.SchemaVersion);
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.Data.SchemaVersion);

            string bgm;
            Assert.IsTrue(loaded.Data.TryGetExtra("bgm", out bgm));
            Assert.AreEqual("theme", bgm);

            string channel;
            Assert.IsTrue(loaded.Data.TryGetExtra("migrated-channel", out channel));
            Assert.AreEqual("demo", channel);
        }

        [Test]
        public void Service_Load_SchemaOneChoiceHistoryMigratesToLossyChoiceRecords()
        {
            var storage = new MemoryStorage();
            storage.Slots[1] = new DialogueSaveSlot
            {
                SchemaVersion = 1,
                SlotIndex = 1,
                Data = new DialogueSaveData
                {
                    SchemaVersion = 1,
                    CurrentDialogueId = 7,
                    ChoiceRecords = null,
                    ChoiceHistory = new List<int> { 2 }
                }
            };
            var service = new DialogueSaveService(storage);

            var loaded = service.Load(1);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.SchemaVersion);
            Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.Data.SchemaVersion);
            Assert.AreEqual(1, loaded.Data.ChoiceRecords.Count);
            Assert.AreEqual(2, loaded.Data.ChoiceRecords[0].RawChoiceIndex);
            Assert.AreEqual(-1, loaded.Data.ChoiceRecords[0].LineId);
            Assert.AreEqual(-1, loaded.Data.ChoiceRecords[0].NextId);
        }

        [Test]
        public void FileStorage_LegacyJsonWithoutSchemaVersion_RunsOlderDataMigration()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "slot_1.json"),
                    "{\"SlotIndex\":1,\"Data\":{\"CurrentDialogueId\":7,\"ExtraState\":[{\"Key\":\"bgm\",\"Value\":\"theme\"}]}}");

                var migration = new LegacyDataMigration();
                var service = new DialogueSaveService(
                    new FileDialogueSaveStorage(directory),
                    null,
                    new DialogueSaveServiceOptions { ProductChannel = "legacy" });
                service.AddDataMigration(migration);

                var loaded = service.Load(1);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, migration.Count);
                Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.SchemaVersion);
                Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.Data.SchemaVersion);

                string bgm;
                Assert.IsTrue(loaded.Data.TryGetExtra("bgm", out bgm));
                Assert.AreEqual("theme", bgm);
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void FileStorage_SchemaOneChoiceHistoryJson_MigratesToChoiceRecords()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "slot_1.json"),
                    "{\"SchemaVersion\":1,\"SlotIndex\":1,\"Data\":{\"SchemaVersion\":1,\"CurrentDialogueId\":7,\"ChoiceHistory\":[1]}}");

                var service = new DialogueSaveService(new FileDialogueSaveStorage(directory));

                var loaded = service.Load(1);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.SchemaVersion);
                Assert.AreEqual(DialogueSaveSchema.CurrentVersion, loaded.Data.SchemaVersion);
                Assert.AreEqual(1, loaded.Data.ChoiceRecords.Count);
                Assert.AreEqual(1, loaded.Data.ChoiceRecords[0].RawChoiceIndex);
                Assert.AreEqual(-1, loaded.Data.ChoiceRecords[0].NextId);
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void FileStorage_SchemaTwoChoiceRecordsJson_LoadsStableChoiceRecord()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "slot_1.json"),
                    "{\"SchemaVersion\":2,\"SlotIndex\":1,\"Data\":{\"SchemaVersion\":2,\"CurrentDialogueId\":7,\"ChoiceRecords\":[{\"LineId\":10,\"RawChoiceIndex\":1,\"NextId\":20,\"Text\":\"Go\",\"ConditionKey\":\"route_a\"}]}}");

                var service = new DialogueSaveService(new FileDialogueSaveStorage(directory));

                var loaded = service.Load(1);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.Data.ChoiceRecords.Count);
                Assert.AreEqual(10, loaded.Data.ChoiceRecords[0].LineId);
                Assert.AreEqual(1, loaded.Data.ChoiceRecords[0].RawChoiceIndex);
                Assert.AreEqual(20, loaded.Data.ChoiceRecords[0].NextId);
                Assert.AreEqual("Go", loaded.Data.ChoiceRecords[0].Text);
                Assert.AreEqual("route_a", loaded.Data.ChoiceRecords[0].ConditionKey);
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        [Test]
        public void Service_Load_RejectsFutureSchemaVersion()
        {
            var storage = new MemoryStorage();
            storage.Slots[1] = new DialogueSaveSlot
            {
                SchemaVersion = DialogueSaveSchema.CurrentVersion + 100,
                SlotIndex = 1,
                Data = new DialogueSaveData()
            };

            var service = new DialogueSaveService(storage);

            var loaded = service.Load(1);

            Assert.IsNull(loaded);
            Assert.IsTrue(service.LastResult.Failed);
            StringAssert.Contains("newer than supported", service.LastResult.Message);
        }

        [Test]
        public void Service_Load_MissingDialogueCanRestoreAsEnded()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,-1\n").Values);
            var storage = new MemoryStorage();
            storage.Slots[1] = new DialogueSaveSlot
            {
                SlotIndex = 1,
                Data = new DialogueSaveData
                {
                    CurrentDialogueId = 99,
                    State = DialogueSessionState.WaitingForInput
                }
            };
            var service = new DialogueSaveService(storage, null, new DialogueSaveServiceOptions
            {
                Repository = repo,
                MissingDialoguePolicy = DialogueMissingDialoguePolicy.RestoreEnded
            });

            var loaded = service.Load(1);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(-1, loaded.Data.CurrentDialogueId);
            Assert.AreEqual(DialogueSessionState.Ended, loaded.Data.State);
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "TalkSystemTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
