using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed class TitleReturnConfirmationController
    {
        private readonly TitleReturnService _service;
        private GameObject _root;
        private Button _confirm;
        private Button _cancel;

        public TitleReturnConfirmationController(TitleReturnService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action<bool> VisibilityChanged;
        public bool IsOpen => _root != null && _root.activeSelf;

        public bool Request()
        {
            var result = _service.RequestReturnToTitle();
            if (result == TitleReturnRequestResult.Started)
                return true;
            if (result != TitleReturnRequestResult.ConfirmationRequired)
                return false;

            Open();
            return true;
        }

        public bool Confirm()
        {
            if (!IsOpen)
                return false;

            var result = _service.ConfirmReturnToTitle();
            if (result != TitleReturnRequestResult.Started)
                return false;

            Hide(false);
            return true;
        }

        public void Cancel()
        {
            Hide(true);
        }

        public void Reset()
        {
            Hide(false);
        }

        private void Open()
        {
            EnsureCreated();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            VisibilityChanged?.Invoke(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_cancel.gameObject);
        }

        private void Hide(bool notify)
        {
            if (!IsOpen)
                return;
            _root.SetActive(false);
            if (notify)
                VisibilityChanged?.Invoke(false);
        }

        private void EnsureCreated()
        {
            if (_root != null)
                return;

            NovelUiFactory.EnsureEventSystem();
            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject(
                "TitleReturnConfirmation",
                typeof(RectTransform),
                typeof(Image),
                typeof(TitleReturnConfirmationInputDriver));
            _root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);
            _root.GetComponent<TitleReturnConfirmationInputDriver>().Configure(this);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 250f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.055f, 0.99f);

            var message = NovelUiFactory.CreateText(
                "Message",
                panel.transform,
                new Vector2(32f, 105f),
                new Vector2(-32f, -28f),
                24f,
                FontStyles.Bold);
            message.text = "Unsaved progress will be lost. Return to Title?";
            message.alignment = TextAlignmentOptions.Center;

            _confirm = CreateButton(panel.transform, "Return to Title", Confirm, -130f);
            _cancel = CreateButton(panel.transform, "Cancel", () => { Cancel(); return true; }, 130f);
            _root.SetActive(false);
        }

        private static Button CreateButton(Transform parent, string label, Func<bool> action, float x)
        {
            var button = NovelUiFactory.CreateButton(
                label.Replace(" ", string.Empty) + "Button",
                parent,
                label,
                19f,
                UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 30f);
            rect.sizeDelta = new Vector2(220f, 52f);
            button.onClick.AddListener(() => action());
            return button;
        }
    }

    public sealed class TitleReturnConfirmationInputDriver : MonoBehaviour
    {
        private TitleReturnConfirmationController _controller;

        public void Configure(TitleReturnConfirmationController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                _controller?.Cancel();
            else if (keyboard.enterKey.wasPressedThisFrame)
                _controller?.Confirm();
        }
    }
}
