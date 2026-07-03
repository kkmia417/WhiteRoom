using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// DialogueView を持つオブジェクトが有効化されたとき、自動的に DialogueManager に登録する補助クラス。
    /// 起動順に依存しないよう、現在の Instance への即時接続に加えて
    /// <see cref="DialogueManager.InstanceChanged"/> を購読し、Manager 生成・差し替え後も再接続する。
    /// </summary>
    [RequireComponent(typeof(DialogueView))]
    public class DialogueViewBinder : MonoBehaviour
    {
        private DialogueView _view;

        private void Awake()
        {
            _view = GetComponent<DialogueView>();
        }

        private void OnEnable()
        {
            DialogueManager.InstanceChanged += OnInstanceChanged;
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.SetView(_view);
        }

        private void OnDisable()
        {
            DialogueManager.InstanceChanged -= OnInstanceChanged;
        }

        private void OnInstanceChanged(DialogueManager manager)
        {
            if (manager != null)
                manager.SetView(_view);
        }
    }
}
