using UnityEngine;

namespace WhiteRoom.Novel
{
    public sealed class UnityApplicationQuitter : IApplicationQuitter
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public bool IsAvailable => false;
        public string UnavailableReason => "ブラウザー版はページを閉じて終了してください。";
#else
        public bool IsAvailable => true;
        public string UnavailableReason => string.Empty;
#endif

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }
    }
}
