using UnityEngine;

namespace kkmia.TalkSystem
{
    [RequireComponent(typeof(DialogueView))]
    public sealed class DialogueInputRouter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private DialogueBacklogView backlog;
        [SerializeField] private DialoguePlaybackController playbackController;

        private DialogueView _view;
        private IDialogueInputSource _inputSource;

        private void Awake()
        {
            _view = GetComponent<DialogueView>();
            _inputSource = inputSourceComponent as IDialogueInputSource;
            if (playbackController == null)
                playbackController = GetComponentInParent<DialoguePlaybackController>();

            if (_inputSource != null)
                _inputSource.InputReceived += HandleInput;
        }

        private void OnDestroy()
        {
            if (_inputSource != null)
                _inputSource.InputReceived -= HandleInput;
        }

        private void HandleInput(DialogueInputAction action)
        {
            Route(action);
        }

        public void Route(DialogueInputAction action)
        {
            if (_view == null) return;

            if (backlog != null && backlog.IsOpen)
            {
                if (action == DialogueInputAction.Backlog ||
                    action == DialogueInputAction.Next ||
                    action == DialogueInputAction.Confirm)
                    CloseBacklog();
                return;
            }

            if (action == DialogueInputAction.Next || action == DialogueInputAction.Confirm)
                _view.RequestNext();
            else if (action == DialogueInputAction.Skip && ResolvePlaybackController() != null)
                playbackController.ToggleSkip();
            else if (action == DialogueInputAction.Auto && ResolvePlaybackController() != null)
                playbackController.ToggleAuto();
            else if (action == DialogueInputAction.Rollback)
            {
                if (ResolvePlaybackController() != null)
                    playbackController.SetMode(DialoguePlaybackMode.Normal);
                if (DialogueManager.Instance != null)
                    DialogueManager.Instance.Rollback();
            }
            else if (action == DialogueInputAction.Backlog && backlog != null)
                OpenBacklog();
        }

        private void OpenBacklog()
        {
            if (backlog == null)
                return;

            if (ResolvePlaybackController() != null)
                playbackController.SetMode(DialoguePlaybackMode.Normal);

            _view.SetAutoAdvanceSuspended(true);
            backlog.Open();
        }

        private void CloseBacklog()
        {
            if (backlog == null)
                return;

            backlog.Close();
            _view.SetAutoAdvanceSuspended(false);
        }

        private DialoguePlaybackController ResolvePlaybackController()
        {
            if (playbackController != null)
                return playbackController;

            playbackController = GetComponentInParent<DialoguePlaybackController>();
            if (playbackController == null && DialogueManager.Instance != null)
                playbackController = DialogueManager.Instance.GetComponent<DialoguePlaybackController>();

            return playbackController;
        }
    }
}
