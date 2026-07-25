using System;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhiteRoom.Novel.Editor
{
    public static class NovelSaveWorkflowValidation
    {
        private const string MenuPath = "Tools/WhiteRoom/Validate Novel Save Workflow";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var store = new MemoryPreferenceStore(-1);
            var target = new NovelDirectSaveTarget(store);
            Require(!target.HasValue, "A missing preference must not select a Direct Save slot.");

            target.Remember(3);
            Require(target.HasValue && target.Slot == 3, "A selected manual slot must become the Direct Save target.");
            Require(store.SavedSlot == 3, "The Direct Save target must be persisted.");

            target.Remember(0);
            Require(target.Slot == 3, "Autosave and Quick Save slots must not replace the manual target.");

            var restored = new NovelDirectSaveTarget(new MemoryPreferenceStore(5));
            Require(restored.HasValue && restored.Slot == 5, "The persisted manual target must be restored.");

            var controller = new SaveLoadScreenController(null, null, null, 13, false);
            Require(controller.PageCount == 3, "Thirteen manual slots must produce three six-slot pages.");
            controller.ChangePage(1);
            Require(controller.CurrentPage == 1, "Page navigation must advance within bounds.");
            controller.ChangePage(20);
            Require(controller.CurrentPage == 2, "Page navigation must clamp to the last page.");
            controller.ChangePage(-20);
            Require(controller.CurrentPage == 0, "Page navigation must clamp to the first page.");

            var notification = new NovelNotificationController();
            notification.ShowInfo("Save cancelled.");
            Require(notification.IsVisible, "Save feedback must be visible after it is shown.");
            Require(notification.CurrentMessage == "Save cancelled.", "Save feedback must preserve its message.");
            notification.Tick(float.MaxValue);
            Require(!notification.IsVisible, "Save feedback must dismiss without blocking input.");

            ValidateSaveServiceIntegration();
            UnityEngine.Debug.Log("Novel save workflow validation passed.");
        }

        private static void ValidateSaveServiceIntegration()
        {
            ResetDialogueManager();
            var viewObject = new GameObject("SaveValidationView");
            var managerObject = new GameObject("SaveValidationManager");
            var view = viewObject.AddComponent<DialogueView>();
            var manager = managerObject.AddComponent<DialogueManager>();
            var saveSystem = managerObject.AddComponent<DialogueSaveSystem>();
            var storage = new MemorySaveStorage();
            var preferences = new MemoryPreferenceStore(-1);
            NovelSaveService service = null;

            try
            {
                SetPrivateField(manager, "csvFile", new TextAsset(
                    "Id,Speaker,Text,NextId\n1,A,Save validation,2\n2,A,Restored,-1\n"));
                SetPrivateField(manager, "view", view);
                Invoke(manager, "Awake");
                saveSystem.SetStorage(storage);
                service = new NovelSaveService(manager, saveSystem, 1, false, preferences);

                var feedbackCount = 0;
                service.Feedback += _ => feedbackCount++;
                manager.StartDialogue(1);

                Require(service.CanSaveNow, "A running dialogue must be a coherent save point.");
                Require(service.Save(2), "Manual Save must succeed.");
                Require(storage.Exists(2), "Manual Save must use the selected slot.");
                Require(service.HasDirectSaveTarget && service.DirectSaveSlot == 2, "Manual Save must set the Direct Save target.");
                Require(service.DirectSave(), "Direct Save must overwrite the selected manual slot.");
                Require(service.QuickSave(), "Quick Save must succeed.");
                Require(storage.Exists(DialogueSaveSystem.QuickSaveSlot), "Quick Save must use its dedicated slot.");
                Require(service.Load(2), "A saved manual slot must load.");
                Require(manager.CurrentData != null && manager.CurrentData.Id == 1, "Load must restore the captured dialogue position.");
                Require(feedbackCount == 4, "Every explicit save/load operation must report feedback.");
            }
            finally
            {
                service?.Dispose();
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(viewObject);
                ResetDialogueManager();
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Required integration field was not found: " + name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "Required integration method was not found: " + methodName);
            method.Invoke(target, null);
        }

        private static void ResetDialogueManager()
        {
            var method = typeof(DialogueManager).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            if (method != null)
                method.Invoke(null, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Novel save workflow validation failed: " + message);
        }

        private sealed class MemoryPreferenceStore : INovelSavePreferenceStore
        {
            private readonly int _loadedSlot;

            public MemoryPreferenceStore(int loadedSlot)
            {
                _loadedSlot = loadedSlot;
                SavedSlot = -1;
            }

            public int SavedSlot { get; private set; }

            public int LoadLastManualSlot()
            {
                return _loadedSlot;
            }

            public void SaveLastManualSlot(int slot)
            {
                SavedSlot = slot;
            }
        }

        private sealed class MemorySaveStorage : IDialogueSaveStorage
        {
            private readonly Dictionary<int, DialogueSaveSlot> _slots =
                new Dictionary<int, DialogueSaveSlot>();

            public bool TryLoad(int slot, out DialogueSaveSlot data)
            {
                return _slots.TryGetValue(slot, out data);
            }

            public void Save(DialogueSaveSlot slot)
            {
                _slots[slot.SlotIndex] = slot;
            }

            public void Delete(int slot)
            {
                _slots.Remove(slot);
            }

            public bool Exists(int slot)
            {
                return _slots.ContainsKey(slot);
            }

            public IEnumerable<int> ListSlots()
            {
                return _slots.Keys;
            }

            public byte[] LoadThumbnail(int slot)
            {
                return null;
            }

            public void SaveThumbnail(int slot, byte[] pngBytes)
            {
            }
        }
    }
}
