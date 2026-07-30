using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Button state colors shared by <see cref="NovelUiFactory.CreateButton"/> callers.
    /// </summary>
    public readonly struct UiButtonStyle
    {
        public UiButtonStyle(Color normal, Color highlighted, Color pressed, Color disabled)
        {
            Normal = normal;
            Highlighted = highlighted;
            Pressed = pressed;
            Disabled = disabled;
        }

        public Color Normal { get; }
        public Color Highlighted { get; }
        public Color Pressed { get; }
        public Color Disabled { get; }

        public static UiButtonStyle Default => new UiButtonStyle(
            new Color(0.17f, 0.22f, 0.24f, 0.96f),
            new Color(0.25f, 0.31f, 0.34f, 1f),
            new Color(0.11f, 0.15f, 0.17f, 1f),
            new Color(0.08f, 0.09f, 0.10f, 0.55f));
    }

    /// <summary>
    /// Creates the shared runtime UI building blocks: the overlay canvas, event system,
    /// texts, buttons and vertical scroll lists used by every runtime-built screen.
    /// </summary>
    public static class NovelUiFactory
    {
        public const string CanvasName = "NovelDialogueCanvas";

        private static TMP_FontAsset _fontAsset;

        /// <summary>
        /// Resolves the font used by <see cref="CreateText"/>: an explicitly assigned TMP asset wins,
        /// otherwise a TMP asset is built once from the font at <paramref name="fontResourcePath"/>.
        /// </summary>
        public static void EnsureFont(TMP_FontAsset explicitAsset, string fontResourcePath)
        {
            if (explicitAsset != null)
            {
                _fontAsset = explicitAsset;
                return;
            }

            if (_fontAsset != null || string.IsNullOrEmpty(fontResourcePath))
                return;

            var font = Resources.Load<Font>(fontResourcePath);
            if (font == null)
            {
                Debug.LogWarning($"NovelUiFactory: UI font was not found at Resources/{fontResourcePath}.");
                return;
            }

            _fontAsset = TMP_FontAsset.CreateFontAsset(font);
            _fontAsset.name = font.name + " TMP Runtime";
        }

        public static Canvas EnsureCanvas()
        {
            var existingCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (existingCanvas != null && string.Equals(existingCanvas.name, CanvasName, StringComparison.Ordinal))
                return existingCanvas;

            return CreateCanvas();
        }

        /// <summary>
        /// Applies the resolved shared UI font to text components loaded from a prefab.
        /// Runtime-created text already receives the same font in <see cref="CreateText"/>.
        /// </summary>
        public static void ApplyFontToHierarchy(Component root)
        {
            if (_fontAsset == null || root == null)
                return;

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                text.font = _fontAsset;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(eventSystem);
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.color = Color.white;
            if (_fontAsset != null)
                text.font = _fontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.TopLeft;

            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string labelText,
            float fontSize,
            UiButtonStyle style,
            TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center,
            Vector2? labelPadding = null)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = style.Normal;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = style.Normal;
            colors.highlightedColor = style.Highlighted;
            colors.pressedColor = style.Pressed;
            colors.disabledColor = style.Disabled;
            button.colors = colors;

            var padding = labelPadding ?? new Vector2(10f, 6f);
            var label = CreateText("Label", buttonObject.transform, padding, -padding, fontSize, FontStyles.Bold);
            label.text = labelText;
            label.alignment = labelAlignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return button;
        }

        /// <summary>
        /// Builds a masked vertical scroll view inside <paramref name="parent"/> and returns
        /// the content container new rows should be parented to.
        /// </summary>
        public static Transform CreateVerticalScrollList(Transform parent, Vector2 offsetMin, Vector2 offsetMax, float spacing)
        {
            var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);

            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = offsetMin;
            scrollRectTransform.offsetMax = offsetMax;

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
            layout.spacing = spacing;
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

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(canvasObject);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }
    }

    public static class NovelSafeAreaUtility
    {
        public static void Apply(RectTransform target, Rect safeArea, int screenWidth, int screenHeight)
        {
            if (target == null || screenWidth <= 0 || screenHeight <= 0)
                return;
            target.anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            target.anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }

    public sealed class NovelSafeAreaDriver : MonoBehaviour
    {
        private RectTransform _target;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        public void Configure(RectTransform target)
        {
            _target = target;
            Apply();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            NovelSafeAreaUtility.Apply(_target, Screen.safeArea, Screen.width, Screen.height);
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
