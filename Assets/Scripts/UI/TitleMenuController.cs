using System;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Builds and drives the title menu: New Game / Continue / Load Game / Quick Load.
    /// The menu UI is created lazily on first <see cref="Show"/>.
    /// </summary>
    public sealed class TitleMenuController
    {
        private static readonly UiButtonStyle ButtonStyle = new UiButtonStyle(
            new Color(0.18f, 0.22f, 0.24f, 0.96f),
            new Color(0.24f, 0.30f, 0.32f, 1f),
            new Color(0.12f, 0.16f, 0.18f, 1f),
            new Color(0.09f, 0.10f, 0.11f, 0.55f));

        private readonly NovelSaveService _saveService;
        private readonly Action _startNewGame;
        private readonly Action _openLoadScreen;
        private readonly Action _openEndingList;
        private readonly Action _openGallery;

        private GameObject _root;
        private Button _continueButton;
        private Button _quickLoadButton;

        public TitleMenuController(
            NovelSaveService saveService,
            Action startNewGame,
            Action openLoadScreen,
            Action openEndingList = null,
            Action openGallery = null)
        {
            _saveService = saveService;
            _startNewGame = startNewGame;
            _openLoadScreen = openLoadScreen;
            _openEndingList = openEndingList;
            _openGallery = openGallery;
        }

        public event Action<bool> VisibilityChanged;

        public void Show()
        {
            NovelUiFactory.EnsureEventSystem();

            if (_root == null)
                _root = CreateMenu();

            RefreshButtons();
            _root.SetActive(true);
            VisibilityChanged?.Invoke(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);

            VisibilityChanged?.Invoke(false);
        }

        public void RefreshButtons()
        {
            if (_root == null)
                return;

            if (_continueButton != null)
                _continueButton.interactable = _saveService.HasContinueSave();

            if (_quickLoadButton != null)
                _quickLoadButton.interactable = _saveService.HasSave(DialogueSaveSystem.QuickSaveSlot);
        }

        private GameObject CreateMenu()
        {
            var canvas = NovelUiFactory.EnsureCanvas();
            var root = new GameObject("WhiteRoomTitleMenu", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.018f, 0.02f, 0.022f, 0.96f);

            var menuObject = new GameObject("Menu", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuObject.transform.SetParent(root.transform, false);

            var menuRect = (RectTransform)menuObject.transform;
            menuRect.anchorMin = new Vector2(0.08f, 0.18f);
            menuRect.anchorMax = new Vector2(0.42f, 0.78f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            var layout = menuObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTitleLabel(menuObject.transform, "WhiteRoom", 64f, 96f, FontStyles.Bold);
            CreateSpacer(menuObject.transform, 18f);
            CreateMenuButton(menuObject.transform, "New Game", () => _startNewGame());
            _continueButton = CreateMenuButton(menuObject.transform, "Continue", () => _saveService.ContinueLatest());
            CreateMenuButton(menuObject.transform, "Load Game", () => _openLoadScreen());
            _quickLoadButton = CreateMenuButton(menuObject.transform, "Quick Load", () => _saveService.QuickLoad());
            if (_openEndingList != null)
                CreateMenuButton(menuObject.transform, "Ending List", () => _openEndingList());
            if (_openGallery != null)
                CreateMenuButton(menuObject.transform, "Gallery", () => _openGallery());

            root.SetActive(false);
            return root;
        }

        private static TextMeshProUGUI CreateTitleLabel(Transform parent, string textValue, float fontSize, float height, FontStyles style)
        {
            var textObject = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var layout = textObject.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return text;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);

            var layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static Button CreateMenuButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action)
        {
            var button = NovelUiFactory.CreateButton(
                labelText.Replace(" ", string.Empty) + "Button",
                parent,
                labelText,
                24f,
                ButtonStyle,
                TextAlignmentOptions.Left,
                new Vector2(18f, 9f));

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            button.onClick.AddListener(action);
            return button;
        }
    }
}
