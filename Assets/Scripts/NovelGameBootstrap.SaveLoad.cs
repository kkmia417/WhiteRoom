using System;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed partial class NovelGameBootstrap
    {
        private void EnsureSaveLoadLauncher()
        {
            if (!showSaveLoadLauncher || _saveLoadLauncherRoot != null)
                return;

            var canvas = EnsureDialogueCanvas();
            _saveLoadLauncherRoot = new GameObject("SaveLoadLauncher", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _saveLoadLauncherRoot.transform.SetParent(canvas.transform, false);
            _saveLoadLauncherRoot.transform.SetAsLastSibling();

            var rect = (RectTransform)_saveLoadLauncherRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(260f, 46f);

            var layout = _saveLoadLauncherRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateLauncherButton(_saveLoadLauncherRoot.transform, "Save", OpenSaveScreen);
            CreateLauncherButton(_saveLoadLauncherRoot.transform, "Load", OpenLoadScreen);
            RefreshSaveLoadLauncherVisibility();
        }

        private void RefreshSaveLoadLauncherVisibility()
        {
            if (_saveLoadLauncherRoot == null)
                return;

            var titleVisible = _titleMenuRoot != null && _titleMenuRoot.activeSelf;
            _saveLoadLauncherRoot.SetActive(showSaveLoadLauncher && !titleVisible);
        }

        private void ShowSaveLoadScreen(bool saveMode)
        {
            EnsureEventSystem();

            if (_saveLoadRoot == null)
                _saveLoadRoot = CreateSaveLoadScreen();

            _saveLoadModeIsSave = saveMode;
            RefreshSaveLoadScreen();
            _saveLoadRoot.SetActive(true);
            _saveLoadRoot.transform.SetAsLastSibling();

            if (_view != null)
                _view.SetAutoAdvanceSuspended(true);
        }

        private void HideSaveLoadScreen()
        {
            if (_saveLoadRoot != null)
                _saveLoadRoot.SetActive(false);

            if (_view != null && (_backlogView == null || !_backlogView.IsOpen))
                _view.SetAutoAdvanceSuspended(false);
        }

        private void RefreshSaveLoadScreen()
        {
            if (_saveLoadRoot == null)
                return;

            if (_saveLoadHeading != null)
                _saveLoadHeading.text = _saveLoadModeIsSave ? "Save" : "Load";

            SetTabSelected(_saveLoadSaveTabButton, _saveLoadModeIsSave);
            SetTabSelected(_saveLoadLoadTabButton, !_saveLoadModeIsSave);

            var canSave = CanSaveDialogue();
            for (var i = 0; i < _saveLoadRows.Count; i++)
            {
                var slot = DialogueSaveSlotConventions.FirstManualSlot + i;
                var viewModel = EnsureSaveSystemReady()
                    ? _saveSystem.GetSlotViewModel(slot, false)
                    : DialogueSaveSlotViewModel.Empty(slot, "Save system is not ready.");

                RefreshSaveLoadRow(_saveLoadRows[i], viewModel, canSave);
            }
        }

        private bool CanSaveDialogue()
        {
            return _manager != null && _manager.CurrentData != null && EnsureSaveSystemReady();
        }

        private void RefreshSaveLoadRow(SaveLoadSlotRow row, DialogueSaveSlotViewModel viewModel, bool canSave)
        {
            if (row == null || viewModel == null)
                return;

            var slot = viewModel.SlotIndex;
            row.SlotLabel.text = $"Slot {slot}";
            row.TitleLabel.text = viewModel.IsEmpty ? "Empty Slot" : FormatSaveTitle(viewModel.Title);
            row.MetaLabel.text = FormatSaveMeta(viewModel);
            row.ActionLabel.text = _saveLoadModeIsSave ? "Save" : "Load";
            row.ActionButton.interactable = _saveLoadModeIsSave ? canSave : viewModel.CanLoad;
            row.ActionButton.onClick.RemoveAllListeners();
            row.ActionButton.onClick.AddListener(() => HandleSaveLoadSlot(slot));
        }

        private void HandleSaveLoadSlot(int slot)
        {
            if (_saveLoadModeIsSave)
            {
                SaveDialogue(slot);
                return;
            }

            LoadDialogue(slot);
        }

        private GameObject CreateSaveLoadScreen()
        {
            var canvas = EnsureDialogueCanvas();
            var root = new GameObject("SaveLoadScreen", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.74f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.16f, 0.12f);
            panelRect.anchorMax = new Vector2(0.84f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.025f, 0.03f, 0.034f, 0.96f);

            _saveLoadHeading = CreateText("Heading", panel.transform, new Vector2(28f, -58f), new Vector2(-240f, 12f), 34f, FontStyles.Bold);
            _saveLoadHeading.alignment = TextAlignmentOptions.Left;

            _saveLoadSaveTabButton = CreateSaveLoadTabButton(panel.transform, "Save", () => ShowSaveLoadScreen(true), new Vector2(1f, 1f), new Vector2(-300f, -24f));
            _saveLoadLoadTabButton = CreateSaveLoadTabButton(panel.transform, "Load", () => ShowSaveLoadScreen(false), new Vector2(1f, 1f), new Vector2(-190f, -24f));
            CreateSaveLoadTabButton(panel.transform, "Close", CloseSaveLoadScreen, new Vector2(1f, 1f), new Vector2(-80f, -24f));

            var content = CreateSaveLoadScrollContent(panel.transform);
            _saveLoadRows.Clear();
            for (var i = 0; i < Mathf.Max(1, manualSaveSlotCount); i++)
                _saveLoadRows.Add(CreateSaveLoadSlotRow(content));

            root.SetActive(false);
            return root;
        }

        private static Transform CreateSaveLoadScrollContent(Transform parent)
        {
            var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);

            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(28f, 28f);
            scrollRectTransform.offsetMax = new Vector2(-28f, -94f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);

            var viewportRect = (RectTransform)viewportObject.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            var mask = viewportObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);

            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            return contentObject.transform;
        }

        private static SaveLoadSlotRow CreateSaveLoadSlotRow(Transform parent)
        {
            var rowObject = new GameObject("SaveSlotRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            var rect = (RectTransform)rowObject.transform;
            rect.sizeDelta = new Vector2(900f, 78f);

            var image = rowObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.095f, 0.105f, 0.96f);

            var layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 78f;
            layout.preferredHeight = 78f;

            var slotLabel = CreateText("Slot", rowObject.transform, new Vector2(18f, 10f), new Vector2(-760f, -10f), 22f, FontStyles.Bold);
            slotLabel.alignment = TextAlignmentOptions.MidlineLeft;
            slotLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var titleLabel = CreateText("Title", rowObject.transform, new Vector2(150f, -32f), new Vector2(-170f, 8f), 20f, FontStyles.Bold);
            titleLabel.alignment = TextAlignmentOptions.TopLeft;
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var metaLabel = CreateText("Meta", rowObject.transform, new Vector2(150f, 8f), new Vector2(-170f, -38f), 17f, FontStyles.Normal);
            metaLabel.alignment = TextAlignmentOptions.TopLeft;
            metaLabel.color = new Color(0.72f, 0.77f, 0.79f, 1f);
            metaLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var actionButton = CreateSaveLoadActionButton(rowObject.transform);
            var actionLabel = actionButton.GetComponentInChildren<TMP_Text>();

            return new SaveLoadSlotRow
            {
                SlotLabel = slotLabel,
                TitleLabel = titleLabel,
                MetaLabel = metaLabel,
                ActionButton = actionButton,
                ActionLabel = actionLabel
            };
        }

        private static Button CreateLauncherButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action)
        {
            var button = CreateSaveLoadButton(labelText + "Button", parent, labelText, 18f);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateSaveLoadTabButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action, Vector2 anchor, Vector2 position)
        {
            var button = CreateSaveLoadButton(labelText + "Button", parent, labelText, 18f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(96f, 42f);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateSaveLoadActionButton(Transform parent)
        {
            var button = CreateSaveLoadButton("ActionButton", parent, "Save", 18f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(126f, 46f);
            return button;
        }

        private static Button CreateSaveLoadButton(string name, Transform parent, string labelText, float fontSize)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.17f, 0.22f, 0.24f, 0.96f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.17f, 0.22f, 0.24f, 0.96f);
            colors.highlightedColor = new Color(0.25f, 0.31f, 0.34f, 1f);
            colors.pressedColor = new Color(0.11f, 0.15f, 0.17f, 1f);
            colors.disabledColor = new Color(0.08f, 0.09f, 0.10f, 0.55f);
            button.colors = colors;

            var label = CreateText("Label", buttonObject.transform, new Vector2(10f, 6f), new Vector2(-10f, -6f), fontSize, FontStyles.Bold);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return button;
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? new Color(0.30f, 0.36f, 0.38f, 1f) : new Color(0.17f, 0.22f, 0.24f, 0.96f);
        }

        private static string FormatSaveTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Saved Game";

            return title.Length > 46 ? title.Substring(0, 46) + "..." : title;
        }

        private static string FormatSaveMeta(DialogueSaveSlotViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            if (viewModel.HasError)
                return viewModel.ErrorMessage;

            if (viewModel.IsEmpty)
                return "No save data";

            return viewModel.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }

        private sealed class SaveLoadSlotRow
        {
            public TMP_Text SlotLabel;
            public TMP_Text TitleLabel;
            public TMP_Text MetaLabel;
            public Button ActionButton;
            public TMP_Text ActionLabel;
        }
    }
}
    }
}

