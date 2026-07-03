using System.Collections.Generic;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Builds and drives the save/load slot screen and its Save/Load launcher buttons.
    /// The screen UI is created lazily the first time it is opened.
    /// </summary>
    public sealed class SaveLoadScreenController
    {
        private readonly NovelSaveService _saveService;
        private readonly DialogueAutoAdvanceGate _autoAdvanceGate;
        private readonly int _manualSlotCount;
        private readonly bool _showLauncher;
        private readonly List<SlotRow> _rows = new List<SlotRow>();

        private GameObject _root;
        private GameObject _launcherRoot;
        private TMP_Text _heading;
        private Button _saveTabButton;
        private Button _loadTabButton;
        private bool _modeIsSave = true;
        private bool _titleMenuVisible;

        public SaveLoadScreenController(NovelSaveService saveService, DialogueAutoAdvanceGate autoAdvanceGate, int manualSlotCount, bool showLauncher)
        {
            _saveService = saveService;
            _autoAdvanceGate = autoAdvanceGate;
            _manualSlotCount = manualSlotCount;
            _showLauncher = showLauncher;
        }

        public void EnsureLauncher()
        {
            if (!_showLauncher || _launcherRoot != null)
                return;

            var canvas = NovelUiFactory.EnsureCanvas();
            _launcherRoot = new GameObject("SaveLoadLauncher", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _launcherRoot.transform.SetParent(canvas.transform, false);
            _launcherRoot.transform.SetAsLastSibling();

            var rect = (RectTransform)_launcherRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(260f, 46f);

            var layout = _launcherRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateLauncherButton(_launcherRoot.transform, "Save", OpenSave);
            CreateLauncherButton(_launcherRoot.transform, "Load", OpenLoad);
            RefreshLauncherVisibility();
        }

        /// <summary>The launcher is hidden while the title menu covers the screen.</summary>
        public void SetTitleMenuVisible(bool visible)
        {
            _titleMenuVisible = visible;
            RefreshLauncherVisibility();
        }

        public void OpenSave()
        {
            Open(true);
        }

        public void OpenLoad()
        {
            Open(false);
        }

        public void Close()
        {
            if (_root != null)
                _root.SetActive(false);

            _autoAdvanceGate.Resume(this);
        }

        public void Refresh()
        {
            if (_root == null)
                return;

            if (_heading != null)
                _heading.text = _modeIsSave ? "Save" : "Load";

            SetTabSelected(_saveTabButton, _modeIsSave);
            SetTabSelected(_loadTabButton, !_modeIsSave);

            var canSave = _saveService.CanSaveNow;
            for (var i = 0; i < _rows.Count; i++)
            {
                var slot = DialogueSaveSlotConventions.FirstManualSlot + i;
                RefreshRow(_rows[i], _saveService.GetSlotViewModel(slot), canSave);
            }
        }

        private void Open(bool saveMode)
        {
            NovelUiFactory.EnsureEventSystem();

            if (_root == null)
                _root = CreateScreen();

            _modeIsSave = saveMode;
            Refresh();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _autoAdvanceGate.Suspend(this);
        }

        private void RefreshLauncherVisibility()
        {
            if (_launcherRoot != null)
                _launcherRoot.SetActive(_showLauncher && !_titleMenuVisible);
        }

        private void RefreshRow(SlotRow row, DialogueSaveSlotViewModel viewModel, bool canSave)
        {
            if (row == null || viewModel == null)
                return;

            var slot = viewModel.SlotIndex;
            row.SlotLabel.text = $"Slot {slot}";
            row.TitleLabel.text = viewModel.IsEmpty ? "Empty Slot" : FormatSaveTitle(viewModel.Title);
            row.MetaLabel.text = FormatSaveMeta(viewModel);
            row.ActionLabel.text = _modeIsSave ? "Save" : "Load";
            row.ActionButton.interactable = _modeIsSave ? canSave : viewModel.CanLoad;
            row.ActionButton.onClick.RemoveAllListeners();
            row.ActionButton.onClick.AddListener(() => HandleSlotAction(slot));
        }

        private void HandleSlotAction(int slot)
        {
            if (_modeIsSave)
                _saveService.Save(slot);
            else
                _saveService.Load(slot);
        }

        private GameObject CreateScreen()
        {
            var canvas = NovelUiFactory.EnsureCanvas();
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

            _heading = NovelUiFactory.CreateText("Heading", panel.transform, new Vector2(28f, -58f), new Vector2(-240f, 12f), 34f, FontStyles.Bold);
            _heading.alignment = TextAlignmentOptions.Left;

            _saveTabButton = CreateTabButton(panel.transform, "Save", OpenSave, new Vector2(-300f, -24f));
            _loadTabButton = CreateTabButton(panel.transform, "Load", OpenLoad, new Vector2(-190f, -24f));
            CreateTabButton(panel.transform, "Close", Close, new Vector2(-80f, -24f));

            var content = NovelUiFactory.CreateVerticalScrollList(panel.transform, new Vector2(28f, 28f), new Vector2(-28f, -94f), 10f);
            _rows.Clear();
            for (var i = 0; i < Mathf.Max(1, _manualSlotCount); i++)
                _rows.Add(CreateSlotRow(content));

            root.SetActive(false);
            return root;
        }

        private static SlotRow CreateSlotRow(Transform parent)
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

            var slotLabel = NovelUiFactory.CreateText("Slot", rowObject.transform, new Vector2(18f, 10f), new Vector2(-760f, -10f), 22f, FontStyles.Bold);
            slotLabel.alignment = TextAlignmentOptions.MidlineLeft;
            slotLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var titleLabel = NovelUiFactory.CreateText("Title", rowObject.transform, new Vector2(150f, -32f), new Vector2(-170f, 8f), 20f, FontStyles.Bold);
            titleLabel.alignment = TextAlignmentOptions.TopLeft;
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var metaLabel = NovelUiFactory.CreateText("Meta", rowObject.transform, new Vector2(150f, 8f), new Vector2(-170f, -38f), 17f, FontStyles.Normal);
            metaLabel.alignment = TextAlignmentOptions.TopLeft;
            metaLabel.color = new Color(0.72f, 0.77f, 0.79f, 1f);
            metaLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var actionButton = CreateActionButton(rowObject.transform);
            var actionLabel = actionButton.GetComponentInChildren<TMP_Text>();

            return new SlotRow
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
            var button = NovelUiFactory.CreateButton(labelText + "Button", parent, labelText, 18f, UiButtonStyle.Default);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateTabButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action, Vector2 position)
        {
            var button = NovelUiFactory.CreateButton(labelText + "Button", parent, labelText, 18f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(96f, 42f);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateActionButton(Transform parent)
        {
            var button = NovelUiFactory.CreateButton("ActionButton", parent, "Save", 18f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(126f, 46f);
            return button;
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? new Color(0.30f, 0.36f, 0.38f, 1f) : UiButtonStyle.Default.Normal;
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

        private sealed class SlotRow
        {
            public TMP_Text SlotLabel;
            public TMP_Text TitleLabel;
            public TMP_Text MetaLabel;
            public Button ActionButton;
            public TMP_Text ActionLabel;
        }
    }
}
