using System;
using kkmia.TalkSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace WhiteRoom.Novel
{
    public sealed class DialogueBackSkipController
    {
        private const float DefaultIntervalSeconds = 0.12f;

        private readonly DialogueManager _manager;
        private readonly DialoguePlaybackController _playbackController;
        private readonly float _intervalSeconds;
        private float _nextRollbackAt;

        public DialogueBackSkipController(
            DialogueManager manager,
            DialoguePlaybackController playbackController,
            float intervalSeconds = DefaultIntervalSeconds)
        {
            _manager = manager;
            _playbackController = playbackController;
            _intervalSeconds = Mathf.Max(0.02f, intervalSeconds);
        }

        public event Action<bool> StateChanged;

        public bool IsActive { get; private set; }
        public bool CanStart => _manager != null &&
                                _manager.CanRollback &&
                                !_manager.HasCurrentChoices &&
                                _manager.State != DialogueSessionState.ChoicePending;

        public void Toggle()
        {
            if (IsActive)
            {
                Stop();
                return;
            }

            Start();
        }

        public bool Start()
        {
            if (!CanStart)
                return false;

            _playbackController?.SetMode(DialoguePlaybackMode.Normal);
            IsActive = true;
            _nextRollbackAt = 0f;
            StateChanged?.Invoke(true);
            return true;
        }

        public void Stop()
        {
            if (!IsActive)
                return;

            IsActive = false;
            StateChanged?.Invoke(false);
        }

        public void Tick(float now, bool cancelRequested)
        {
            if (!IsActive)
                return;
            if (cancelRequested || !CanStart)
            {
                Stop();
                return;
            }
            if (now < _nextRollbackAt)
                return;

            if (!_manager.Rollback())
            {
                Stop();
                return;
            }

            _nextRollbackAt = now + _intervalSeconds;
            if (!CanStart)
                Stop();
        }
    }

    [DefaultExecutionOrder(-400)]
    public sealed class DialogueBackSkipDriver : MonoBehaviour
    {
        private DialogueBackSkipController _controller;

        public void Configure(DialogueBackSkipController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var cancelRequested =
                (keyboard != null && keyboard.anyKey.wasPressedThisFrame) ||
                (mouse != null &&
                 (mouse.rightButton.wasPressedThisFrame ||
                  mouse.middleButton.wasPressedThisFrame));
            _controller?.Tick(Time.unscaledTime, cancelRequested);
        }
    }

    public sealed class DialogueBackSkipPointerStopper : MonoBehaviour, IPointerDownHandler
    {
        private DialogueBackSkipController _controller;

        public void Configure(DialogueBackSkipController controller)
        {
            _controller = controller;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.Stop();
        }
    }
}
