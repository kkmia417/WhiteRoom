using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>Runtime-built Ending List and CG Gallery overlay.</summary>
    public sealed class CollectionScreenController
    {
        private readonly CollectionService _service;
        private readonly List<GameObject> _rows = new List<GameObject>();

        private GameObject _root;
        private Transform _content;
        private TMP_Text _heading;
        private TMP_Text _emptyState;
        private Button _endingTab;
        private Button _galleryTab;
        private Button _closeButton;

        public CollectionScreenController(CollectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action<bool> VisibilityChanged;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool IsEmptyStateVisible => _emptyState != null && _emptyState.gameObject.activeSelf;
        public int VisibleItemCount => _rows.Count;
        public CollectionItemKind CurrentKind { get; private set; }

        public void OpenEndingList()
        {
            Open(CollectionItemKind.Ending);
        }

        public void OpenGallery()
        {
            Open(CollectionItemKind.Cg);
        }

        public void Close()
        {
            if (_root != null)
                _root.SetActive(false);
            VisibilityChanged?.Invoke(false);
        }

        public void HandleCancel()
        {
            Close();
        }

        private void Open(CollectionItemKind kind)
        {
            EnsureCreated();
            CurrentKind = kind;
            Rebuild();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            VisibilityChanged?.Invoke(true);
            FocusHeader();
        }

        private void Rebuild()
        {
            ClearRows();
            var items = _service.Build(CurrentKind);
            _heading.text = CurrentKind == CollectionItemKind.Ending ? "Ending List" : "CG Gallery";
            _emptyState.text = CurrentKind == CollectionItemKind.Cg
                ? "CGはまだ登録されていません"
                : "Endingはまだ登録されていません";
            _emptyState.gameObject.SetActive(items.Count == 0);
            SetTabSelected(_endingTab, CurrentKind == CollectionItemKind.Ending);
            SetTabSelected(_galleryTab, CurrentKind == CollectionItemKind.Cg);

            for (var index = 0; index < items.Count; index++)
                _rows.Add(CreateRow(_content, items[index], index + 1));
        }

        private void EnsureCreated()
        {
            if (_root != null)
                return;

            NovelUiFactory.EnsureEventSystem();
            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject(
                "WhiteRoomCollectionScreen",
                typeof(RectTransform),
                typeof(Image),
                typeof(CollectionScreenInputDriver));
            _root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            _root.GetComponent<CollectionScreenInputDriver>().Configure(this);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.15f, 0.10f);
            panelRect.anchorMax = new Vector2(0.85f, 0.90f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.034f, 0.98f);

            _heading = NovelUiFactory.CreateText(
                "Heading", panel.transform, new Vector2(28f, -62f), new Vector2(-520f, 14f), 34f, FontStyles.Bold);
            _heading.alignment = TextAlignmentOptions.Left;
            _endingTab = CreateHeaderButton(panel.transform, "Ending List", OpenEndingList, -410f, 146f);
            _galleryTab = CreateHeaderButton(panel.transform, "Gallery", OpenGallery, -252f, 116f);
            _closeButton = CreateHeaderButton(panel.transform, "Back", Close, -92f, 112f);

            _content = NovelUiFactory.CreateVerticalScrollList(
                panel.transform,
                new Vector2(28f, 28f),
                new Vector2(-28f, -96f),
                9f);
            _emptyState = NovelUiFactory.CreateText(
                "EmptyState", panel.transform, new Vector2(36f, 36f), new Vector2(-36f, -104f), 26f, FontStyles.Normal);
            _emptyState.alignment = TextAlignmentOptions.Center;
            _emptyState.raycastTarget = false;
            _root.SetActive(false);
        }

        private void FocusHeader()
        {
            if (EventSystem.current == null)
                return;
            var target = CurrentKind == CollectionItemKind.Ending ? _endingTab : _galleryTab;
            EventSystem.current.SetSelectedGameObject(target != null ? target.gameObject : _closeButton.gameObject);
        }

        private void ClearRows()
        {
            for (var index = 0; index < _rows.Count; index++)
            {
                if (_rows[index] == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_rows[index]);
                else
                    UnityEngine.Object.DestroyImmediate(_rows[index]);
            }
            _rows.Clear();
        }

        private static GameObject CreateRow(Transform parent, CollectionItemViewModel item, int number)
        {
            var root = new GameObject("CollectionRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(900f, 68f);
            root.GetComponent<Image>().color = item.IsUnlocked
                ? new Color(0.09f, 0.15f, 0.14f, 0.98f)
                : new Color(0.065f, 0.075f, 0.08f, 0.98f);
            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = 68f;
            layout.preferredHeight = 68f;

            var indexLabel = NovelUiFactory.CreateText(
                "Number", root.transform, new Vector2(16f, 8f), new Vector2(-820f, -8f), 18f, FontStyles.Bold);
            indexLabel.text = number.ToString("00");
            indexLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var category = NovelUiFactory.CreateText(
                "Category", root.transform, new Vector2(86f, 8f), new Vector2(-650f, -8f), 18f, FontStyles.Bold);
            category.text = item.Entry.Category;
            category.alignment = TextAlignmentOptions.MidlineLeft;
            var name = NovelUiFactory.CreateText(
                "Name", root.transform, new Vector2(260f, 8f), new Vector2(-145f, -8f), 22f, FontStyles.Bold);
            name.text = item.DisplayName;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            var status = NovelUiFactory.CreateText(
                "Status", root.transform, new Vector2(770f, 8f), new Vector2(-16f, -8f), 16f, FontStyles.Bold);
            status.text = item.IsUnlocked ? "UNLOCKED" : "LOCKED";
            status.color = item.IsUnlocked ? new Color(0.72f, 0.92f, 0.78f) : new Color(0.62f, 0.65f, 0.67f);
            status.alignment = TextAlignmentOptions.MidlineRight;
            return root;
        }

        private static Button CreateHeaderButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            float x,
            float width)
        {
            var button = NovelUiFactory.CreateButton(label.Replace(" ", string.Empty) + "Button", parent, label, 18f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -24f);
            rect.sizeDelta = new Vector2(width, 46f);
            button.onClick.AddListener(action);
            return button;
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button != null)
                button.GetComponent<Image>().color = selected
                    ? new Color(0.30f, 0.36f, 0.38f, 1f)
                    : UiButtonStyle.Default.Normal;
        }
    }

    public sealed class CollectionScreenInputDriver : MonoBehaviour
    {
        private CollectionScreenController _controller;

        public void Configure(CollectionScreenController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _controller?.HandleCancel();
        }
    }
}
