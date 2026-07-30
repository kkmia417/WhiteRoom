using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed class NovelNotificationController
    {
        private const float DisplaySeconds = 2.4f;

        private GameObject _root;
        private TMP_Text _label;
        private Image _background;
        private float _hideAt;
        private bool _captureHidden;
        private bool _restoreAfterCapture;

        public bool IsVisible => _root != null && _root.activeSelf;
        public string CurrentMessage => _label != null ? _label.text : string.Empty;

        public void Show(string message, bool succeeded)
        {
            EnsureCreated();
            _label.text = message ?? string.Empty;
            _background.color = succeeded
                ? new Color(0.10f, 0.30f, 0.20f, 0.96f)
                : new Color(0.42f, 0.12f, 0.10f, 0.96f);
            Activate();
        }

        public void ShowInfo(string message)
        {
            EnsureCreated();
            _label.text = message ?? string.Empty;
            _background.color = new Color(0.16f, 0.20f, 0.24f, 0.96f);
            Activate();
        }

        public void Tick(float now)
        {
            if (_root != null && _root.activeSelf && now >= _hideAt)
                _root.SetActive(false);
        }

        public void SetCaptureHidden(bool hidden)
        {
            _captureHidden = hidden;
            if (hidden)
            {
                _restoreAfterCapture = IsVisible;
                if (_root != null)
                    _root.SetActive(false);
                return;
            }

            if (_restoreAfterCapture && _root != null && Time.unscaledTime < _hideAt)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
            }
            _restoreAfterCapture = false;
        }

        private void Activate()
        {
            _hideAt = Time.unscaledTime + DisplaySeconds;
            _restoreAfterCapture = _captureHidden;
            _root.SetActive(!_captureHidden);
            _root.transform.SetAsLastSibling();
        }

        private void EnsureCreated()
        {
            if (_root != null)
                return;

            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject(
                "NovelNotification",
                typeof(RectTransform),
                typeof(Image),
                typeof(NovelNotificationDriver));
            _root.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -28f);
            rect.sizeDelta = new Vector2(520f, 46f);

            _background = _root.GetComponent<Image>();
            _background.raycastTarget = false;
            _label = NovelUiFactory.CreateText(
                "Label",
                _root.transform,
                new Vector2(16f, 5f),
                new Vector2(-16f, -5f),
                18f,
                FontStyles.Bold);
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            _root.GetComponent<NovelNotificationDriver>().Configure(this);
            _root.SetActive(false);
        }
    }

    public sealed class NovelNotificationDriver : MonoBehaviour
    {
        private NovelNotificationController _controller;

        public void Configure(NovelNotificationController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            _controller?.Tick(Time.unscaledTime);
        }
    }
}
