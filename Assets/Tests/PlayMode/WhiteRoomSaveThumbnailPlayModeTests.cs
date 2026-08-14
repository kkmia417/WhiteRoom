using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomSaveThumbnailPlayModeTests
    {
        private const string SaveServiceTypeName = "WhiteRoom.Novel.NovelSaveService, Assembly-CSharp";
        private const string ScreenTypeName = "WhiteRoom.Novel.SaveLoadScreenController, Assembly-CSharp";
        private const string GateTypeName = "WhiteRoom.Novel.DialogueAutoAdvanceGate, Assembly-CSharp";

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
        }

        private sealed class MemoryStorage : IDialogueSaveStorage
        {
            public readonly Dictionary<int, DialogueSaveSlot> Slots = new Dictionary<int, DialogueSaveSlot>();
            public readonly Dictionary<int, byte[]> Thumbnails = new Dictionary<int, byte[]>();
            public int ThumbnailLoadCount;
            public int ThumbnailClearCount;

            public bool TryLoad(int slot, out DialogueSaveSlot data) => Slots.TryGetValue(slot, out data);
            public void Save(DialogueSaveSlot slot) => Slots[slot.SlotIndex] = slot;
            public void Delete(int slot) { Slots.Remove(slot); Thumbnails.Remove(slot); }
            public bool Exists(int slot) => Slots.ContainsKey(slot);
            public IEnumerable<int> ListSlots() => new List<int>(Slots.Keys);
            public byte[] LoadThumbnail(int slot)
            {
                ThumbnailLoadCount++;
                byte[] bytes;
                return Thumbnails.TryGetValue(slot, out bytes) ? bytes : null;
            }
            public void SaveThumbnail(int slot, byte[] pngBytes)
            {
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    ThumbnailClearCount++;
                    Thumbnails.Remove(slot);
                }
                else
                {
                    Thumbnails[slot] = pngBytes;
                }
            }
        }

        [UnityTest]
        public IEnumerator ManualQuickAndAutoCaptureBoundedImagesAndUiReusesTextures()
        {
            yield return DestroyExistingManager();
            var managerObject = new GameObject("ThumbnailManager", typeof(DialogueManager), typeof(DialogueSaveSystem));
            var viewObject = new GameObject("ThumbnailView", typeof(RectTransform), typeof(DialogueView));
            var manager = managerObject.GetComponent<DialogueManager>();
            var view = viewObject.GetComponent<DialogueView>();
            var saveSystem = managerObject.GetComponent<DialogueSaveSystem>();
            var storage = new MemoryStorage();
            object saveService = null;
            object screen = null;
            EventInfo savedEvent = null;
            Action refreshAfterSave = null;

            try
            {
                manager.SetView(view);
                manager.LoadRepository(new TextAssetDialogueRepositoryLoader(new TextAsset(
                    "Id,Speaker,Text,NextId\n1,Narrator,Thumbnail state,-1\n")));
                saveSystem.SetStorage(storage);
                saveSystem.SetThumbnailCaptureProvider(() =>
                {
                    var source = new Texture2D(640, 360, TextureFormat.RGB24, false);
                    source.Apply(false, false);
                    return source;
                });
                yield return null;
                yield return null;
                manager.StartDialogue(1);

                var saveType = RequireType(SaveServiceTypeName);
                saveService = Activator.CreateInstance(saveType, manager, saveSystem, 1, true, null);
                storage.Thumbnails[1] = new byte[] { 9, 9, 9 };

                Assert.That((bool)Invoke(saveService, "Save", 1), Is.True);
                Assert.That(saveSystem.IsThumbnailCaptureInProgress, Is.True);
                Assert.That(storage.Thumbnails.ContainsKey(1), Is.False,
                    "The stale sidecar must be cleared before asynchronous capture.");
                Assert.That((bool)Invoke(saveService, "Save", 2), Is.False,
                    "A second save must be rejected while capture is in progress.");
                yield return null;
                yield return null;
                AssertThumbnail(storage, 1);

                Assert.That((bool)Invoke(saveService, "QuickSave"), Is.True);
                yield return null;
                yield return null;
                AssertThumbnail(storage, DialogueSaveSystem.QuickSaveSlot);

                Assert.That((bool)Invoke(saveService, "Autosave", "Auto checkpoint"), Is.True);
                yield return null;
                yield return null;
                AssertThumbnail(storage, DialogueSaveSystem.AutosaveSlot);

                saveSystem.Service.Save(2, manager.CaptureState(), "corrupt-image", false, 2);
                storage.Thumbnails[2] = new byte[] { 1, 2, 3, 4 };
                saveSystem.Service.Save(3, manager.CaptureState(), "missing-image", false, 3);

                var gate = Activator.CreateInstance(RequireType(GateTypeName), new object[] { view });
                screen = Activator.CreateInstance(
                    RequireType(ScreenTypeName),
                    saveService,
                    gate,
                    null,
                    6,
                    false);
                Invoke(screen, "OpenLoad");

                Assert.That((bool)GetProperty(screen, "IsOpen"), Is.True);
                Invoke(screen, "SetCaptureHidden", true);
                Assert.That((bool)GetProperty(screen, "IsOpen"), Is.False);
                Invoke(screen, "SetCaptureHidden", false);
                Assert.That((bool)GetProperty(screen, "IsOpen"), Is.True);

                Assert.That(Invoke(screen, "GetThumbnailState", 1), Is.EqualTo("Image"));
                Assert.That(Invoke(screen, "GetThumbnailState", 2), Is.EqualTo("Corrupt"));
                Assert.That(Invoke(screen, "GetThumbnailState", 3), Is.EqualTo("Missing"));
                Assert.That((int)GetProperty(screen, "LoadedThumbnailTextureCount"), Is.EqualTo(3),
                    "Auto, Quick, and manual slot 1 should each own one decoded texture on the first page.");

                var row = GameObject.Find("SaveSlotRow_1");
                Assert.That(row, Is.Not.Null);
                var thumbnail = row.transform.Find("Thumbnail").GetComponent<Image>();
                var textureId = thumbnail.sprite.texture.GetInstanceID();
                var loadsAfterFirstOpen = storage.ThumbnailLoadCount;
                for (var index = 0; index < 4; index++)
                {
                    Invoke(screen, "Close");
                    Invoke(screen, "OpenLoad");
                    Assert.That(thumbnail.sprite.texture.GetInstanceID(), Is.EqualTo(textureId));
                }
                Assert.That(storage.ThumbnailLoadCount, Is.EqualTo(loadsAfterFirstOpen),
                    "Repeated refreshes must use the service cache instead of disk reads.");

                Assert.That((bool)Invoke(saveService, "Load", 2), Is.True,
                    "A corrupt thumbnail must not make its save slot unloadable.");

                var completionRefreshCount = 0;
                savedEvent = saveType.GetEvent("Saved", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(savedEvent, Is.Not.Null);
                refreshAfterSave = () =>
                {
                    completionRefreshCount++;
                    Invoke(screen, "Refresh");
                };
                savedEvent.AddEventHandler(saveService, refreshAfterSave);
                saveSystem.SetThumbnailCaptureProvider(() => null);
                Assert.That((bool)Invoke(saveService, "Save", 1), Is.True,
                    "Thumbnail capture failure must not invalidate the save payload.");
                yield return null;
                yield return null;
                Assert.That(completionRefreshCount, Is.EqualTo(2),
                    "Slot consumers should refresh after payload save and failed thumbnail completion.");
                Assert.That(Invoke(screen, "GetThumbnailState", 1), Is.EqualTo("Missing"),
                    "A failed overwrite must not leave the previous thumbnail visible.");
                Assert.That((bool)Invoke(saveService, "Load", 1), Is.True);
            }
            finally
            {
                if (savedEvent != null && refreshAfterSave != null && saveService != null)
                    savedEvent.RemoveEventHandler(saveService, refreshAfterSave);
                if (screen != null)
                    Invoke(screen, "Dispose");
                if (saveService is IDisposable disposable)
                    disposable.Dispose();
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(viewObject);
            }
        }

        private static void AssertThumbnail(MemoryStorage storage, int slot)
        {
            Assert.That(storage.Thumbnails.ContainsKey(slot), Is.True, "slot " + slot);
            var bytes = storage.Thumbnails[slot];
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(DialogueSaveSystem.MaximumThumbnailBytes));
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(bytes), Is.True);
                Assert.That(texture.width, Is.EqualTo(DialogueSaveSystem.ThumbnailWidth));
                Assert.That(texture.height, Is.EqualTo(DialogueSaveSystem.ThumbnailHeight));
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static IEnumerator DestroyExistingManager()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(manager.gameObject);
            yield return null;
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo selected = null;
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (var index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == name && methods[index].GetParameters().Length == arguments.Length)
                {
                    selected = methods[index];
                    break;
                }
            }
            Assert.That(selected, Is.Not.Null, name);
            return selected.Invoke(target, arguments);
        }
    }
}
