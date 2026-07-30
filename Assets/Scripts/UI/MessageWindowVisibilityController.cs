using System;
using kkmia.TalkSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Hides only narrative UI (dialogue/name/text/choices and command bar), leaving
    /// the presentation stage visible. A dedicated recovery driver consumes the
    /// first keyboard or pointer input so it cannot also advance dialogue.
    /// </summary>
    public sealed class MessageWindowVisibilityController : IDisposable
    {
        private readonly DialogueView _view;
        private readonly NovelCommandBarController _commandBar;
        private readonly Func<bool> _shouldShowCommandBar;
        private GameObject _recoveryInput;

        public MessageWindowVisibilityController(
            DialogueView view,
            NovelCommandBarController commandBar,
            Func<bool> shouldShowCommandBar)
        {
            _view = view;
            _commandBar = commandBar;
            _shouldShowCommandBar = shouldShowCommandBar ?? throw new ArgumentNullException(nameof(shouldShowCommandBar));
        }

        public event Action<bool> HiddenChanged;
        public bool IsHidden { get; private set; }

        public bool Hide()
        {
            if (IsHidden || _view == null || !_view.gameObject.activeInHierarchy)
                return false;

            EnsureRecoveryInput();
            IsHidden = true;
            _view.gameObject.SetActive(false);
            _commandBar?.SetSceneVisible(false);
            _recoveryInput.SetActive(true);
            HiddenChanged?.Invoke(true);
            return true;
        }

        public bool Restore()
        {
            if (!IsHidden)
                return false;

            IsHidden = false;
            if (_view != null)
                _view.gameObject.SetActive(true);
            if (_recoveryInput != null)
                _recoveryInput.SetActive(false);
            _commandBar?.SetSceneVisible(_shouldShowCommandBar());
            HiddenChanged?.Invoke(false);
            return true;
        }

        public void Reset()
        {
            Restore();
        }

        public void Dispose()
        {
            if (_recoveryInput != null)
                UnityEngine.Object.Destroy(_recoveryInput);
            _recoveryInput = null;
        }

        private void EnsureRecoveryInput()
        {
            if (_recoveryInput != null)
                return;

            var canvas = NovelUiFactory.EnsureCanvas();
            _recoveryInput = new GameObject(
                "MessageWindowRecoveryInput",
                typeof(RectTransform),
                typeof(MessageWindowRecoveryInputDriver));
            _recoveryInput.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)_recoveryInput.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _recoveryInput.GetComponent<MessageWindowRecoveryInputDriver>().Configure(this);
            _recoveryInput.SetActive(false);
        }
    }

    public sealed class MessageWindowRecoveryInputDriver : MonoBehaviour
    {
        private MessageWindowVisibilityController _controller;

        public void Configure(MessageWindowVisibilityController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var keyboardRestore = keyboard != null &&
                                  (keyboard.spaceKey.wasPressedThisFrame ||
                                   keyboard.enterKey.wasPressedThisFrame ||
                                   keyboard.escapeKey.wasPressedThisFrame);
            var pointerRestore = mouse != null &&
                                 (mouse.leftButton.wasPressedThisFrame ||
                                  mouse.rightButton.wasPressedThisFrame);
            if (keyboardRestore || pointerRestore)
                _controller?.Restore();
        }
    }
}
