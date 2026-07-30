using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>Runtime-built overlay for replaying and removing favorite voices.</summary>
    public sealed class FavoriteVoiceScreenController
    {
        private readonly FavoriteVoiceService _service;
        private readonly Action<FavoriteVoiceResult> _feedback;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<Button> _playButtons = new List<Button>();

        private GameObject _root;
        private Transform _content;
        private TMP_Text _emptyState;
        private Button _stopButton;
        private Button _backButton;

        public FavoriteVoiceScreenController(
            FavoriteVoiceService service,
            Action<FavoriteVoiceResult> feedback = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _feedback = feedback;
        }

        public event Action<bool> VisibilityChanged;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool IsEmptyStateVisible => _emptyState != null && _emptyState.gameObject.activeSelf;
        public int VisibleItemCount => _rows.Count;

        public void Open()
        {
            EnsureCreated();
            _service.Stop();
            Rebuild();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            VisibilityChanged?.Invoke(true);
            FocusFirstControl();
        }

        public void Close()
        {
            _service.Stop();
            if (_root != null)
                _root.SetActive(false);
            VisibilityChanged?.Invoke(false);
        }

        public void Stop()
        {
            _service.Stop();
        }

        public void HandleCancel()
        {
            Close();
        }

        private void Rebuild()
        {
            ClearRows();
            var items = _service.BuildList();
            _emptyState.gameObject.SetActive(items.Count == 0);
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
                "WhiteRoomFavoriteVoiceScreen",
                typeof(RectTransform),
                typeof(Image),
                typeof(FavoriteVoiceScreenInputDriver));
            _root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.80f);
            _root.GetComponent<FavoriteVoiceScreenInputDriver>().Configure(this);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.08f, 0.08f);
            panelRect.anchorMax = new Vector2(0.92f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.034f, 0.98f);

            var heading = NovelUiFactory.CreateText(
                "Heading", panel.transform, new Vector2(28f, -62f), new Vector2(-430f, 14f), 34f, FontStyles.Bold);
            heading.text = "Favorite Voices";
            heading.alignment = TextAlignmentOptions.Left;
            _stopButton = CreateHeaderButton(panel.transform, "Stop", Stop, -220f, 112f);
            _backButton = CreateHeaderButton(panel.transform, "Back", Close, -92f, 112f);

            _content = NovelUiFactory.CreateVerticalScrollList(
                panel.transform,
                new Vector2(28f, 28f),
                new Vector2(-28f, -96f),
                9f);
            _emptyState = NovelUiFactory.CreateText(
                "EmptyState", panel.transform, new Vector2(36f, 36f), new Vector2(-36f, -104f), 26f, FontStyles.Normal);
            _emptyState.text = "お気に入りVoiceはまだ登録されていません";
            _emptyState.alignment = TextAlignmentOptions.Center;
            _emptyState.raycastTarget = false;
            _root.SetActive(false);
        }

        private GameObject CreateRow(Transform parent, FavoriteVoiceViewModel item, int number)
        {
            var root = new GameObject(
                "FavoriteVoiceRow_" + item.Record.DialogueId,
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1040f, 106f);
            root.GetComponent<Image>().color = item.IsVoiceAvailable
                ? new Color(0.09f, 0.15f, 0.14f, 0.98f)
                : new Color(0.065f, 0.075f, 0.08f, 0.98f);
            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = 106f;
            layout.preferredHeight = 106f;

            var identity = NovelUiFactory.CreateText(
                "Identity", root.transform, new Vector2(16f, 10f), new Vector2(-850f, -10f), 17f, FontStyles.Bold);
            identity.text = number.ToString("00") + "  " + item.Speaker + "  [#" + item.Record.DialogueId + "]";
            identity.alignment = TextAlignmentOptions.TopLeft;

            var text = NovelUiFactory.CreateText(
                "Text", root.transform, new Vector2(16f, 42f), new Vector2(-252f, -10f), 20f, FontStyles.Normal);
            text.text = item.Text;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;

            var play = NovelUiFactory.CreateButton(
                "PlayButton", root.transform, item.IsVoiceAvailable ? "Play" : "Unavailable", 17f, UiButtonStyle.Default);
            var playRect = (RectTransform)play.transform;
            playRect.anchorMin = new Vector2(1f, 0.5f);
            playRect.anchorMax = new Vector2(1f, 0.5f);
            playRect.pivot = new Vector2(1f, 0.5f);
            playRect.anchoredPosition = new Vector2(-126f, 0f);
            playRect.sizeDelta = new Vector2(112f, 44f);
            play.interactable = item.IsVoiceAvailable;
            play.onClick.AddListener(() => Publish(_service.Play(item.Record)));
            _playButtons.Add(play);

            var remove = NovelUiFactory.CreateButton(
                "RemoveButton", root.transform, "Remove", 17f, UiButtonStyle.Default);
            var removeRect = (RectTransform)remove.transform;
            removeRect.anchorMin = new Vector2(1f, 0.5f);
            removeRect.anchorMax = new Vector2(1f, 0.5f);
            removeRect.pivot = new Vector2(1f, 0.5f);
            removeRect.anchoredPosition = new Vector2(-14f, 0f);
            removeRect.sizeDelta = new Vector2(104f, 44f);
            remove.onClick.AddListener(() =>
            {
                var result = _service.Remove(item.Record);
                Publish(result);
                if (result.Succeeded)
                {
                    Rebuild();
                    FocusFirstControl();
                }
            });
            return root;
        }

        private void Publish(FavoriteVoiceResult result)
        {
            if (result != null)
                _feedback?.Invoke(result);
        }

        private void FocusFirstControl()
        {
            if (EventSystem.current == null)
                return;

            for (var index = 0; index < _playButtons.Count; index++)
            {
                if (_playButtons[index] != null && _playButtons[index].IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(_playButtons[index].gameObject);
                    return;
                }
            }
            EventSystem.current.SetSelectedGameObject(_backButton != null ? _backButton.gameObject : null);
        }

        private void ClearRows()
        {
            _playButtons.Clear();
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
            rect.sizeDelta = new Vector2(width, 46f);
            button.onClick.AddListener(action);
            return button;
        }
    }

    public sealed class FavoriteVoiceScreenInputDriver : MonoBehaviour
    {
        private FavoriteVoiceScreenController _controller;

        public void Configure(FavoriteVoiceScreenController controller)
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
            else if (keyboard.sKey.wasPressedThisFrame)
                _controller?.Stop();
        }
    }
}
