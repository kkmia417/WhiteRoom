using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed class QuitConfirmationController
    {
        private readonly ApplicationQuitService _service;
        private GameObject _root;
        private TMP_Text _message;
        private Button _confirm;

        public QuitConfirmationController(ApplicationQuitService service) => _service = service;
        public event Action<bool> VisibilityChanged;
        public bool IsOpen => _root != null && _root.activeSelf;

        public void Open()
        {
            EnsureCreated();
            _message.text = _service.IsAvailable ? "ゲームを終了しますか？" : _service.UnavailableReason;
            _confirm.gameObject.SetActive(_service.IsAvailable);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            VisibilityChanged?.Invoke(true);
        }

        public void Cancel()
        {
            if (_root != null) _root.SetActive(false);
            VisibilityChanged?.Invoke(false);
        }

        public bool Confirm()
        {
            if (!_service.ConfirmQuit()) return false;
            return true;
        }

        private void EnsureCreated()
        {
            if (_root != null) return;
            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject("QuitConfirmation", typeof(RectTransform), typeof(Image), typeof(QuitConfirmationInputDriver));
            _root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);
            _root.GetComponent<QuitConfirmationInputDriver>().Configure(this);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 230f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.055f, 0.99f);
            _message = NovelUiFactory.CreateText("Message", panel.transform, new Vector2(28f, 92f), new Vector2(-28f, -24f), 24f, FontStyles.Bold);
            _message.alignment = TextAlignmentOptions.Center;
            _confirm = CreateButton(panel.transform, "Quit", Confirm, -110f);
            CreateButton(panel.transform, "Cancel", () => { Cancel(); return true; }, 110f);
            _root.SetActive(false);
        }

        private static Button CreateButton(Transform parent, string label, Func<bool> action, float x)
        {
            var button = NovelUiFactory.CreateButton(label + "Button", parent, label, 19f, UiButtonStyle.Default);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 28f);
            rect.sizeDelta = new Vector2(180f, 50f);
            button.onClick.AddListener(() => action());
            return button;
        }
    }

    public sealed class QuitConfirmationInputDriver : MonoBehaviour
    {
        private QuitConfirmationController _controller;
        public void Configure(QuitConfirmationController controller) => _controller = controller;
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _controller?.Cancel();
        }
    }
}
