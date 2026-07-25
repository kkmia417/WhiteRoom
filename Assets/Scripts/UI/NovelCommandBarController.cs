using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Builds and drives the fixed-order command bar used by the main novel scene.
    /// Command behavior is supplied by the composition root through typed definitions.
    /// </summary>
    public sealed class NovelCommandBarController : IDisposable
    {
        public const string RootName = "NovelCommandBar";
        public const int ExpectedCommandCount = 23;

        private const float AutoHideSeconds = 2.5f;
        private const float StateRefreshSeconds = 0.15f;
        private const float ButtonWidth = 44f;
        private const float ButtonHeight = 34f;
        private const float GroupSpacing = 7f;

        private static readonly UiButtonStyle CommandStyle = new UiButtonStyle(
            new Color(0.25f, 0.12f, 0.055f, 0.98f),
            new Color(0.48f, 0.25f, 0.09f, 1f),
            new Color(0.16f, 0.07f, 0.025f, 1f),
            new Color(0.12f, 0.10f, 0.09f, 0.52f));

        private static readonly Color ActiveColor = new Color(0.62f, 0.34f, 0.10f, 1f);
        private static readonly Color OutlineColor = new Color(0.88f, 0.66f, 0.36f, 0.92f);
        private static readonly Color DisabledOutlineColor = new Color(0.38f, 0.31f, 0.27f, 0.55f);

        private readonly IReadOnlyList<NovelCommandDefinition> _definitions;
        private readonly Dictionary<NovelCommandId, CommandButton> _buttons =
            new Dictionary<NovelCommandId, CommandButton>();

        private GameObject _root;
        private GameObject _safeAreaRoot;
        private GameObject _revealZone;
        private GameObject _tooltipRoot;
        private CanvasGroup _canvasGroup;
        private TMP_Text _tooltipLabel;
        private NovelCommandDefinition _tooltipCommand;
        private bool _locked = true;
        private bool _sceneVisible;
        private bool _inputBlocked;
        private bool _pointerInside;
        private float _lastActivityTime;
        private float _lastStateRefreshTime;

        public NovelCommandBarController(IReadOnlyList<NovelCommandDefinition> definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            ValidateDefinitions(_definitions);
        }

        public IReadOnlyList<NovelCommandDefinition> Definitions => _definitions;
        public GameObject Root => _root;
        public bool IsLocked => _locked;
        public bool IsBarShown => _canvasGroup != null && _canvasGroup.alpha > 0.99f;

        public void EnsureCreated()
        {
            if (_root != null)
                return;

            var canvas = NovelUiFactory.EnsureCanvas();
            CreateSafeAreaRoot(canvas.transform);
            CreateRevealZone(_safeAreaRoot.transform);
            CreateBar(_safeAreaRoot.transform);
            CreateTooltip(_safeAreaRoot.transform);
            Refresh();
            SetSceneVisible(false);
        }

        public void SetSceneVisible(bool visible)
        {
            EnsureCreated();
            _sceneVisible = visible;
            _revealZone.SetActive(visible);
            _root.SetActive(visible);

            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);

            if (visible)
            {
                _lastActivityTime = Time.unscaledTime;
                SetBarShown(true);
                Refresh();
            }
        }

        public Button GetButton(NovelCommandId id)
        {
            CommandButton entry;
            return _buttons.TryGetValue(id, out entry) ? entry.Button : null;
        }

        public void Refresh()
        {
            foreach (var pair in _buttons)
            {
                var entry = pair.Value;
                var isLock = entry.Definition.Id == NovelCommandId.ToolbarLock;
                var available = !_inputBlocked && (isLock || entry.Definition.CanExecute());
                var active = isLock ? _locked : entry.Definition.IsSelected();

                entry.Button.interactable = available;
                var colors = entry.Button.colors;
                colors.normalColor = active ? ActiveColor : CommandStyle.Normal;
                colors.selectedColor = active ? ActiveColor : CommandStyle.Highlighted;
                colors.highlightedColor = active ? ActiveColor : CommandStyle.Highlighted;
                colors.pressedColor = CommandStyle.Pressed;
                colors.disabledColor = CommandStyle.Disabled;
                entry.Button.colors = colors;
                entry.Outline.effectColor = available ? OutlineColor : DisabledOutlineColor;
            }
        }

        public void NotifyPointerEntered()
        {
            _pointerInside = true;
            RecordActivity();
        }

        public void SetInputBlocked(bool blocked)
        {
            _inputBlocked = blocked;
            Refresh();
            SetBarShown(IsBarShown);
        }

        public void NotifyPointerExited()
        {
            _pointerInside = false;
            _lastActivityTime = Time.unscaledTime;
        }

        public void Tick(float now, bool keyboardRevealRequested)
        {
            if (!_sceneVisible || _root == null)
                return;
            if (_inputBlocked)
                return;

            if (now - _lastStateRefreshTime >= StateRefreshSeconds)
            {
                Refresh();
                _lastStateRefreshTime = now;
            }

            if (keyboardRevealRequested)
            {
                RecordActivity(now);
                FocusFirstAvailableButton();
            }

            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            var hasFocusedCommand = selected != null && selected.transform.IsChildOf(_root.transform);
            if (_locked || _pointerInside || hasFocusedCommand)
            {
                SetBarShown(true);
                return;
            }

            if (now - _lastActivityTime >= AutoHideSeconds)
                SetBarShown(false);
        }

        public void DismissKeyboardFocus()
        {
            if (_root == null)
                return;

            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(_root.transform))
                eventSystem.SetSelectedGameObject(null);

            _tooltipCommand = null;
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);

            _pointerInside = false;
            if (!_locked)
                SetBarShown(false);
        }

        public void ShowTooltip(NovelCommandDefinition definition)
        {
            if (definition == null || _tooltipRoot == null)
                return;

            _tooltipCommand = definition;
            var available = definition.Id == NovelCommandId.ToolbarLock || definition.CanExecute();
            _tooltipLabel.text = available
                ? definition.Tooltip
                : definition.Tooltip + " - " + definition.UnavailableTooltip;
            _tooltipRoot.SetActive(_sceneVisible);
            RecordActivity();
        }

        public void HideTooltip(NovelCommandDefinition definition)
        {
            if (_tooltipCommand != definition || _tooltipRoot == null)
                return;

            _tooltipCommand = null;
            _tooltipRoot.SetActive(false);
        }

        public void Dispose()
        {
            DestroyObject(_tooltipRoot);
            DestroyObject(_root);
            DestroyObject(_revealZone);
            DestroyObject(_safeAreaRoot);
            _tooltipRoot = null;
            _root = null;
            _revealZone = null;
            _safeAreaRoot = null;
            _canvasGroup = null;
            _buttons.Clear();
        }

        private void CreateRevealZone(Transform parent)
        {
            _revealZone = new GameObject(
                "NovelCommandBarRevealZone",
                typeof(RectTransform),
                typeof(Image),
                typeof(NovelCommandBarPointerRelay));
            _revealZone.transform.SetParent(parent, false);

            var rect = (RectTransform)_revealZone.transform;
            rect.anchorMin = new Vector2(0.18f, 0f);
            rect.anchorMax = new Vector2(0.82f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 68f);

            var image = _revealZone.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            image.raycastTarget = true;

            _revealZone.GetComponent<NovelCommandBarPointerRelay>().Configure(this);
        }

        private void CreateSafeAreaRoot(Transform parent)
        {
            _safeAreaRoot = new GameObject(
                "NovelCommandBarSafeArea",
                typeof(RectTransform),
                typeof(NovelCommandBarSafeAreaDriver));
            _safeAreaRoot.transform.SetParent(parent, false);

            var rect = (RectTransform)_safeAreaRoot.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _safeAreaRoot.GetComponent<NovelCommandBarSafeAreaDriver>().Configure(rect);
        }

        private void CreateBar(Transform parent)
        {
            _root = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(NovelCommandBarPointerRelay),
                typeof(NovelCommandBarUpdateDriver));
            _root.transform.SetParent(parent, false);
            _root.transform.SetAsLastSibling();

            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(0f, 44f);

            var background = _root.GetComponent<Image>();
            background.color = new Color(0.055f, 0.035f, 0.028f, 0.38f);
            background.sprite = LoadRoundedSprite();
            background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            var layout = _root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = _root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var pointerRelay = _root.GetComponent<NovelCommandBarPointerRelay>();
            pointerRelay.Configure(this);
            _root.GetComponent<NovelCommandBarUpdateDriver>().Configure(this);

            NovelCommandGroup? previousGroup = null;
            for (var i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                if (previousGroup.HasValue && previousGroup.Value != definition.Group)
                    CreateGroupSpacer(_root.transform, definition.Group);

                CreateCommandButton(_root.transform, definition);
                previousGroup = definition.Group;
            }
        }

        private void CreateCommandButton(Transform parent, NovelCommandDefinition definition)
        {
            var button = NovelUiFactory.CreateButton(
                "Command_" + definition.Id,
                parent,
                definition.Label,
                11f,
                CommandStyle,
                TextAlignmentOptions.Center,
                new Vector2(2f, 2f));

            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = ButtonWidth;
            layout.preferredWidth = ButtonWidth;
            layout.minHeight = ButtonHeight;
            layout.preferredHeight = ButtonHeight;

            var image = button.GetComponent<Image>();
            image.sprite = LoadRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 6f;
                label.fontSizeMax = 11f;
                label.characterSpacing = 0f;
            }

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Horizontal;
            button.navigation = navigation;

            var relay = button.gameObject.AddComponent<NovelCommandTooltipRelay>();
            relay.Configure(this, definition);

            button.onClick.AddListener(() => Execute(definition));
            _buttons.Add(definition.Id, new CommandButton(definition, button, outline));
        }

        private static void CreateGroupSpacer(Transform parent, NovelCommandGroup group)
        {
            var spacer = new GameObject("Spacer_" + group, typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            var layout = spacer.GetComponent<LayoutElement>();
            layout.minWidth = GroupSpacing;
            layout.preferredWidth = GroupSpacing;
        }

        private void CreateTooltip(Transform parent)
        {
            _tooltipRoot = new GameObject("NovelCommandTooltip", typeof(RectTransform), typeof(Image));
            _tooltipRoot.transform.SetParent(parent, false);
            _tooltipRoot.transform.SetAsLastSibling();

            var rect = (RectTransform)_tooltipRoot.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 64f);
            rect.sizeDelta = new Vector2(430f, 30f);

            var image = _tooltipRoot.GetComponent<Image>();
            image.color = new Color(0.035f, 0.025f, 0.022f, 0.92f);
            image.sprite = LoadRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;

            _tooltipLabel = NovelUiFactory.CreateText(
                "Label",
                _tooltipRoot.transform,
                new Vector2(10f, 3f),
                new Vector2(-10f, -3f),
                14f,
                FontStyles.Normal);
            _tooltipLabel.alignment = TextAlignmentOptions.Center;
            _tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _tooltipLabel.raycastTarget = false;
            _tooltipRoot.SetActive(false);
        }

        private void Execute(NovelCommandDefinition definition)
        {
            RecordActivity();
            if (definition.Id == NovelCommandId.ToolbarLock)
            {
                _locked = !_locked;
                Refresh();
                return;
            }

            if (!definition.CanExecute())
                return;

            definition.Execute();
            Refresh();
        }

        private void RecordActivity()
        {
            RecordActivity(Time.unscaledTime);
        }

        private void RecordActivity(float now)
        {
            _lastActivityTime = now;
            SetBarShown(true);
        }

        private void FocusFirstAvailableButton()
        {
            if (EventSystem.current == null)
                return;

            for (var i = 0; i < _definitions.Count; i++)
            {
                var button = GetButton(_definitions[i].Id);
                if (button != null && button.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }
        }

        private void SetBarShown(bool shown)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = shown ? 1f : 0f;
            _canvasGroup.interactable = shown && !_inputBlocked;
            _canvasGroup.blocksRaycasts = shown && !_inputBlocked;
            if (!shown && _tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        private static Sprite LoadRoundedSprite()
        {
            return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void ValidateDefinitions(IReadOnlyList<NovelCommandDefinition> definitions)
        {
            if (definitions.Count != ExpectedCommandCount)
                throw new ArgumentException($"Command bar requires {ExpectedCommandCount} definitions.", nameof(definitions));

            var ids = new HashSet<NovelCommandId>();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    throw new ArgumentException("Command definition cannot be null.", nameof(definitions));
                if (!ids.Add(definition.Id))
                    throw new ArgumentException($"Duplicate command id: {definition.Id}.", nameof(definitions));
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private sealed class CommandButton
        {
            public CommandButton(NovelCommandDefinition definition, Button button, Outline outline)
            {
                Definition = definition;
                Button = button;
                Outline = outline;
            }

            public NovelCommandDefinition Definition { get; }
            public Button Button { get; }
            public Outline Outline { get; }
        }
    }

    public sealed class NovelCommandBarPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private NovelCommandBarController _controller;

        public void Configure(NovelCommandBarController controller)
        {
            _controller = controller;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _controller?.NotifyPointerEntered();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _controller?.NotifyPointerExited();
        }
    }

    public sealed class NovelCommandBarUpdateDriver : MonoBehaviour
    {
        private NovelCommandBarController _controller;

        public void Configure(NovelCommandBarController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var reveal = keyboard != null &&
                         (keyboard.tabKey.wasPressedThisFrame ||
                          keyboard.leftArrowKey.wasPressedThisFrame ||
                          keyboard.rightArrowKey.wasPressedThisFrame);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _controller?.DismissKeyboardFocus();
            _controller?.Tick(Time.unscaledTime, reveal);
        }
    }

    public sealed class NovelCommandBarSafeAreaDriver : MonoBehaviour
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
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_target == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            var safeArea = Screen.safeArea;
            _target.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height);
            _target.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height);
            _target.offsetMin = Vector2.zero;
            _target.offsetMax = Vector2.zero;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }

    public sealed class NovelCommandTooltipRelay :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private NovelCommandBarController _controller;
        private NovelCommandDefinition _definition;

        public void Configure(NovelCommandBarController controller, NovelCommandDefinition definition)
        {
            _controller = controller;
            _definition = definition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _controller?.ShowTooltip(_definition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _controller?.HideTooltip(_definition);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _controller?.ShowTooltip(_definition);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _controller?.HideTooltip(_definition);
        }
    }
}
