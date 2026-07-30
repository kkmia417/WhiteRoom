using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WhiteRoom.Novel
{
    /// <summary>Excludes transient command UI and notifications from one capture frame.</summary>
    public sealed class ScreenshotCaptureUiController
    {
        private readonly NovelCommandBarController _commandBar;
        private readonly NovelNotificationController _notifications;
        private readonly Func<bool> _shouldShowCommandBar;
        private readonly Func<bool> _isMessageHidden;
        private bool _restoreCommandBar;
        private GameObject _previousFocus;

        public ScreenshotCaptureUiController(
            NovelCommandBarController commandBar,
            NovelNotificationController notifications,
            Func<bool> shouldShowCommandBar,
            Func<bool> isMessageHidden)
        {
            _commandBar = commandBar;
            _notifications = notifications;
            _shouldShowCommandBar = shouldShowCommandBar ?? throw new ArgumentNullException(nameof(shouldShowCommandBar));
            _isMessageHidden = isMessageHidden ?? throw new ArgumentNullException(nameof(isMessageHidden));
        }

        public bool IsCaptureUiHidden { get; private set; }

        public void HideForCapture()
        {
            if (IsCaptureUiHidden)
                return;

            IsCaptureUiHidden = true;
            _restoreCommandBar = _commandBar != null &&
                                 _commandBar.Root != null &&
                                 _commandBar.Root.activeSelf;
            _previousFocus = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            _commandBar?.SetSceneVisible(false);
            _notifications?.SetCaptureHidden(true);
        }

        public void RestoreAfterCapture()
        {
            if (!IsCaptureUiHidden)
                return;

            IsCaptureUiHidden = false;
            _notifications?.SetCaptureHidden(false);
            _commandBar?.SetSceneVisible(
                _restoreCommandBar && _shouldShowCommandBar() && !_isMessageHidden());

            if (_previousFocus != null &&
                _previousFocus.activeInHierarchy &&
                EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousFocus);
            _previousFocus = null;
            _restoreCommandBar = false;
        }
    }
}
