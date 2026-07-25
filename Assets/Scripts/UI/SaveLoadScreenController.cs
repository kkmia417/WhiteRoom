using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Drives manual slot selection, overwrite confirmation, Direct Save fallback,
    /// and keyboard navigation for the shared Save/Load overlay.
    /// </summary>
    public sealed class SaveLoadScreenController
    {
        private const int PageSize = 6;

        private readonly NovelSaveService _saveService;
        private readonly DialogueAutoAdvanceGate _autoAdvanceGate;
        private readonly NovelNotificationController _notifications;
        private readonly int _manualSlotCount;
        private readonly bool _showLauncher;
        private readonly List<SlotRow> _rows = new List<SlotRow>();

        private GameObject _root;
        private GameObject _launcherRoot;
        private GameObject _confirmationRoot;
        private TMP_Text _heading;
        private TMP_Text _pageLabel;
        private TMP_Text _confirmationLabel;
        private Button _saveTabButton;
        private Button _loadTabButton;
        private Button _previousPageButton;
        private Button _nextPageButton;
        private Button _confirmButton;
        private bool _modeIsSave = true;
        private bool _titleMenuVisible;
        private bool _confirmationIsDirectSave;
        private int _confirmationSlot = -1;
        private int _currentPage;

        public SaveLoadScreenController(
            NovelSaveService saveService,
            DialogueAutoAdvanceGate autoAdvanceGate,
            NovelNotificationController notifications,
            int manualSlotCount,
            bool showLauncher)
        {
            _saveService = saveService;
            _autoAdvanceGate = autoAdvanceGate;
            _notifications = notifications;
            _manualSlotCount = Mathf.Max(1, manualSlotCount);
            _showLauncher = showLauncher;
        }

        public event Action<bool> VisibilityChanged;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool IsConfirmingOverwrite => _confirmationRoot != null && _confirmationRoot.activeSelf;
        public int CurrentPage => _currentPage;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(_manualSlotCount / (float)PageSize));

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

        public void DirectSave()
        {
            if (!_saveService.CanSaveNow || _saveService.IsBusy)
            {
                _notifications.Show("Saving is not available at this point.", false);
                return;
            }

            if (!_saveService.HasDirectSaveTarget)
            {
                _notifications.ShowInfo("Select a slot for Direct Save.");
                OpenSave();
                return;
            }

            var slot = _saveService.DirectSaveSlot;
            var viewModel = _saveService.GetSlotViewModel(slot);
            if (viewModel != null && (!viewModel.IsEmpty || viewModel.HasError))
            {
                _currentPage = Mathf.Clamp(
                    (slot - DialogueSaveSlotConventions.FirstManualSlot) / PageSize,
                    0,
                    PageCount - 1);
                OpenSave();
                ShowOverwriteConfirmation(slot, true);
                return;
            }

            _saveService.DirectSave();
        }

        public void Close()
        {
            HideOverwriteConfirmation();
            if (_root != null)
                _root.SetActive(false);

            _autoAdvanceGate.Resume(this);
            VisibilityChanged?.Invoke(false);
        }

        public void HandleCancel()
        {
            if (IsConfirmingOverwrite)
            {
                CancelOverwrite();
                return;
            }

            Close();
        }

        public void ChangePage(int delta)
        {
            if (IsConfirmingOverwrite)
                return;

            var target = Mathf.Clamp(_currentPage + delta, 0, PageCount - 1);
            if (target == _currentPage)
                return;

            _currentPage = target;
            Refresh();
            FocusFirstSlot();
        }

        public void Refresh()
        {
            if (_root == null)
                return;

            if (_heading != null)
                _heading.text = _modeIsSave ? "Save" : "Load";
            if (_pageLabel != null)
                _pageLabel.text = $"{_currentPage + 1} / {PageCount}";

            SetTabSelected(_saveTabButton, _modeIsSave);
            SetTabSelected(_loadTabButton, !_modeIsSave);
            _previousPageButton.interactable = _currentPage > 0 && !IsConfirmingOverwrite;
            _nextPageButton.interactable = _currentPage + 1 < PageCount && !IsConfirmingOverwrite;

            var canSave = _saveService.CanSaveNow && !_saveService.IsBusy;
            for (var i = 0; i < _rows.Count; i++)
            {
                var slotOffset = _currentPage * PageSize + i;
                var active = slotOffset < _manualSlotCount;
                _rows[i].Root.SetActive(active);
                if (!active)
                    continue;

                var slot = DialogueSaveSlotConventions.FirstManualSlot + slotOffset;
                RefreshRow(_rows[i], _saveService.GetSlotViewModel(slot), canSave);
            }
        }

        private void Open(bool saveMode)
        {
            NovelUiFactory.EnsureEventSystem();
            if (_root == null)
                _root = CreateScreen();

            _modeIsSave = saveMode;
            HideOverwriteConfirmation();
            Refresh();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _autoAdvanceGate.Suspend(this);
            VisibilityChanged?.Invoke(true);
            FocusFirstSlot();
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
            row.TitleLabel.text = viewModel.HasError
                ? "Unavailable Slot"
                : viewModel.IsEmpty ? "Empty Slot" : FormatSaveTitle(viewModel.Title);
            row.MetaLabel.text = FormatSaveMeta(viewModel);
            row.ActionLabel.text = _modeIsSave ? "Save" : "Load";
            row.ActionButton.interactable = !IsConfirmingOverwrite &&
                                            (_modeIsSave ? canSave : viewModel.CanLoad && !viewModel.HasError);
            row.ActionButton.onClick.RemoveAllListeners();
            row.ActionButton.onClick.AddListener(() => HandleSlotAction(slot, viewModel));
            row.Background.color = viewModel.HasError
                ? new Color(0.24f, 0.08f, 0.08f, 0.96f)
                : viewModel.IsEmpty
                    ? new Color(0.07f, 0.085f, 0.095f, 0.96f)
                    : new Color(0.08f, 0.095f, 0.105f, 0.96f);
        }

        private void HandleSlotAction(int slot, DialogueSaveSlotViewModel viewModel)
        {
            if (_saveService.IsBusy)
                return;

            if (_modeIsSave)
            {
                if (viewModel != null && (!viewModel.IsEmpty || viewModel.HasError))
                    ShowOverwriteConfirmation(slot, false);
                else
                    _saveService.Save(slot);
            }
            else if (viewModel != null && viewModel.CanLoad && !viewModel.HasError)
            {
                _saveService.Load(slot);
            }
        }

        private void ShowOverwriteConfirmation(int slot, bool directSave)
        {
            if (_confirmationRoot == null)
                return;

            _confirmationSlot = slot;
            _confirmationIsDirectSave = directSave;
            _confirmationLabel.text = directSave
                ? $"Overwrite Slot {slot} with Direct Save?"
                : $"Overwrite the data in Slot {slot}?";
            _confirmationRoot.SetActive(true);
            _confirmationRoot.transform.SetAsLastSibling();
            _confirmButton.Select();
            Refresh();
        }

        private void ConfirmOverwrite()
        {
            if (_confirmationSlot < DialogueSaveSlotConventions.FirstManualSlot || _saveService.IsBusy)
                return;

            var slot = _confirmationSlot;
            var direct = _confirmationIsDirectSave;
            HideOverwriteConfirmation();
            if (direct)
                _saveService.DirectSave();
            else
                _saveService.Save(slot);
            Refresh();
        }

        private void CancelOverwrite()
        {
            HideOverwriteConfirmation();
            _notifications.ShowInfo("Save cancelled.");
            Refresh();
            FocusFirstSlot();
        }

        private void HideOverwriteConfirmation()
        {
            if (_confirmationRoot != null)
                _confirmationRoot.SetActive(false);
            _confirmationSlot = -1;
            _confirmationIsDirectSave = false;
        }

        private GameObject CreateScreen()
        {
            var canvas = NovelUiFactory.EnsureCanvas();
            var root = new GameObject(
                "SaveLoadScreen",
                typeof(RectTransform),
                typeof(Image),
                typeof(SaveLoadScreenInputDriver));
            root.transform.SetParent(canvas.transform, false);
            root.GetComponent<SaveLoadScreenInputDriver>().Configure(this);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.74f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.16f, 0.12f);
            panelRect.anchorMax = new Vector2(0.84f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.034f, 0.96f);

            _heading = NovelUiFactory.CreateText(
                "Heading",
                panel.transform,
                new Vector2(28f, -58f),
                new Vector2(-590f, 12f),
                34f,
                FontStyles.Bold);
            _heading.alignment = TextAlignmentOptions.Left;

            _saveTabButton = CreateHeaderButton(panel.transform, "Save", OpenSave, -520f, 96f);
            _loadTabButton = CreateHeaderButton(panel.transform, "Load", OpenLoad, -414f, 96f);
            _previousPageButton = CreateHeaderButton(panel.transform, "<", () => ChangePage(-1), -296f, 48f);
            _pageLabel = CreateHeaderLabel(panel.transform);
            _nextPageButton = CreateHeaderButton(panel.transform, ">", () => ChangePage(1), -166f, 48f);
            CreateHeaderButton(panel.transform, "Close", Close, -80f, 96f);

            var content = NovelUiFactory.CreateVerticalScrollList(
                panel.transform,
                new Vector2(28f, 28f),
                new Vector2(-28f, -94f),
                10f);
            _rows.Clear();
            for (var i = 0; i < PageSize; i++)
                _rows.Add(CreateSlotRow(content));

            CreateConfirmation(root.transform);
            root.SetActive(false);
            return root;
        }

        private void CreateConfirmation(Transform parent)
        {
            _confirmationRoot = new GameObject("OverwriteConfirmation", typeof(RectTransform), typeof(Image));
            _confirmationRoot.transform.SetParent(parent, false);
            var rect = (RectTransform)_confirmationRoot.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _confirmationRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_confirmationRoot.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 210f);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.065f, 0.07f, 0.99f);

            _confirmationLabel = NovelUiFactory.CreateText(
                "Message",
                panel.transform,
                new Vector2(28f, 82f),
                new Vector2(-28f, -28f),
                24f,
                FontStyles.Bold);
            _confirmationLabel.alignment = TextAlignmentOptions.Center;

            _confirmButton = CreateConfirmationButton(panel.transform, "Overwrite", ConfirmOverwrite, -110f);
            CreateConfirmationButton(panel.transform, "Cancel", CancelOverwrite, 110f);
            _confirmationRoot.SetActive(false);
        }

        private void FocusFirstSlot()
        {
            if (EventSystem.current == null)
                return;

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Root.activeSelf && _rows[i].ActionButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(_rows[i].ActionButton.gameObject);
                    return;
                }
            }

            if (_loadTabButton != null)
                EventSystem.current.SetSelectedGameObject(_loadTabButton.gameObject);
        }

        private static SlotRow CreateSlotRow(Transform parent)
        {
            var rowObject = new GameObject("SaveSlotRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);
            ((RectTransform)rowObject.transform).sizeDelta = new Vector2(900f, 78f);

            var background = rowObject.GetComponent<Image>();
            var layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 78f;
            layout.preferredHeight = 78f;

            var slotLabel = NovelUiFactory.CreateText(
                "Slot",
                rowObject.transform,
                new Vector2(18f, 10f),
                new Vector2(-760f, -10f),
                22f,
                FontStyles.Bold);
            slotLabel.alignment = TextAlignmentOptions.MidlineLeft;
            slotLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var titleLabel = NovelUiFactory.CreateText(
                "Title",
                rowObject.transform,
                new Vector2(150f, -32f),
                new Vector2(-170f, 8f),
                20f,
                FontStyles.Bold);
            titleLabel.alignment = TextAlignmentOptions.TopLeft;
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var metaLabel = NovelUiFactory.CreateText(
                "Meta",
                rowObject.transform,
                new Vector2(150f, 8f),
                new Vector2(-170f, -38f),
                17f,
                FontStyles.Normal);
            metaLabel.alignment = TextAlignmentOptions.TopLeft;
            metaLabel.color = new Color(0.72f, 0.77f, 0.79f, 1f);
            metaLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var actionButton = NovelUiFactory.CreateButton(
                "ActionButton",
                rowObject.transform,
                "Save",
                18f,
                UiButtonStyle.Default);
            var actionRect = (RectTransform)actionButton.transform;
            actionRect.anchorMin = new Vector2(1f, 0.5f);
            actionRect.anchorMax = new Vector2(1f, 0.5f);
            actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.anchoredPosition = new Vector2(-18f, 0f);
            actionRect.sizeDelta = new Vector2(126f, 46f);

            return new SlotRow
            {
                Root = rowObject,
                Background = background,
                SlotLabel = slotLabel,
                TitleLabel = titleLabel,
                MetaLabel = metaLabel,
                ActionButton = actionButton,
                ActionLabel = actionButton.GetComponentInChildren<TMP_Text>()
            };
        }

        private static Button CreateLauncherButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var button = NovelUiFactory.CreateButton(label + "Button", parent, label, 18f, UiButtonStyle.Default);
            button.onClick.AddListener(action);
            return button;
        }

        private static Button CreateHeaderButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            float x,
            float width)
        {
            var button = NovelUiFactory.CreateButton(label + "Button", parent, label, 18f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -24f);
            rect.sizeDelta = new Vector2(width, 42f);
            button.onClick.AddListener(action);
            return button;
        }

        private static TMP_Text CreateHeaderLabel(Transform parent)
        {
            var label = NovelUiFactory.CreateText(
                "PageLabel",
                parent,
                Vector2.zero,
                Vector2.zero,
                17f,
                FontStyles.Bold);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-231f, -24f);
            rect.sizeDelta = new Vector2(74f, 42f);
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private static Button CreateConfirmationButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            float x)
        {
            var button = NovelUiFactory.CreateButton(label + "Button", parent, label, 18f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 28f);
            rect.sizeDelta = new Vector2(180f, 48f);
            button.onClick.AddListener(action);
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

            var version = string.IsNullOrWhiteSpace(viewModel.ContentVersion)
                ? string.Empty
                : "  " + viewModel.ContentVersion;
            return viewModel.SavedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm") + version;
        }

        private sealed class SlotRow
        {
            public GameObject Root;
            public Image Background;
            public TMP_Text SlotLabel;
            public TMP_Text TitleLabel;
            public TMP_Text MetaLabel;
            public Button ActionButton;
            public TMP_Text ActionLabel;
        }
    }

    public sealed class SaveLoadScreenInputDriver : MonoBehaviour
    {
        private SaveLoadScreenController _controller;

        public void Configure(SaveLoadScreenController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                _controller?.HandleCancel();
            else if (keyboard.pageUpKey.wasPressedThisFrame)
                _controller?.ChangePage(-1);
            else if (keyboard.pageDownKey.wasPressedThisFrame)
                _controller?.ChangePage(1);
        }
    }
}
