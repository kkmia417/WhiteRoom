using System;
using System.Reflection;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public enum DialogueInputAction
    {
        Next,
        Skip,
        Auto,
        Backlog,
        ChoiceUp,
        ChoiceDown,
        Confirm,
        Rollback
    }

    public enum DialogueKeyCode
    {
        None,
        Space,
        Enter,
        B,
        LeftControl,
        A,
        PageUp,
        L,
        F5,
        F9,
        Alpha1,
        Alpha2,
        Alpha3,
        R
    }

    [Serializable]
    public sealed class DialogueKeyCodeMapping
    {
        public DialogueInputAction action = DialogueInputAction.Next;
        public DialogueKeyCode key = DialogueKeyCode.Space;
    }

    public interface IDialogueInputSource
    {
        event Action<DialogueInputAction> InputReceived;
    }

    public sealed class DialogueKeyboardInput : MonoBehaviour, IDialogueInputSource
    {
        [SerializeField] private DialogueKeyCode nextKey = DialogueKeyCode.Space;
        [SerializeField] private DialogueKeyCode backlogKey = DialogueKeyCode.B;
        [SerializeField] private DialogueKeyCode skipKey = DialogueKeyCode.LeftControl;
        [SerializeField] private DialogueKeyCode autoKey = DialogueKeyCode.A;
        [SerializeField] private DialogueKeyCode rollbackKey = DialogueKeyCode.PageUp;

        public event Action<DialogueInputAction> InputReceived;

        private void Update()
        {
            if (DialogueKeyboard.GetKeyDown(nextKey))
                Raise(DialogueInputAction.Next);
            if (DialogueKeyboard.GetKeyDown(backlogKey))
                Raise(DialogueInputAction.Backlog);
            if (DialogueKeyboard.GetKeyDown(skipKey))
                Raise(DialogueInputAction.Skip);
            if (DialogueKeyboard.GetKeyDown(autoKey))
                Raise(DialogueInputAction.Auto);
            if (DialogueKeyboard.GetKeyDown(rollbackKey))
                Raise(DialogueInputAction.Rollback);
        }

        private void Raise(DialogueInputAction action)
        {
            if (InputReceived != null)
                InputReceived(action);
        }
    }

    public static class DialogueKeyboard
    {
        public static bool GetKeyDown(DialogueKeyCode key)
        {
            if (key == DialogueKeyCode.None)
                return false;

#if ENABLE_LEGACY_INPUT_MANAGER
            return LegacyInputBackend.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            return InputSystemReflectionBackend.GetKeyDown(key);
#else
            return false;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static class LegacyInputBackend
        {
            public static bool GetKeyDown(DialogueKeyCode key)
            {
                return Input.GetKeyDown(ToUnityKeyCode(key));
            }

            private static KeyCode ToUnityKeyCode(DialogueKeyCode key)
            {
                switch (key)
                {
                    case DialogueKeyCode.Space: return KeyCode.Space;
                    case DialogueKeyCode.Enter: return KeyCode.Return;
                    case DialogueKeyCode.B: return KeyCode.B;
                    case DialogueKeyCode.LeftControl: return KeyCode.LeftControl;
                    case DialogueKeyCode.A: return KeyCode.A;
                    case DialogueKeyCode.PageUp: return KeyCode.PageUp;
                    case DialogueKeyCode.L: return KeyCode.L;
                    case DialogueKeyCode.F5: return KeyCode.F5;
                    case DialogueKeyCode.F9: return KeyCode.F9;
                    case DialogueKeyCode.Alpha1: return KeyCode.Alpha1;
                    case DialogueKeyCode.Alpha2: return KeyCode.Alpha2;
                    case DialogueKeyCode.Alpha3: return KeyCode.Alpha3;
                    case DialogueKeyCode.R: return KeyCode.R;
                    default: return KeyCode.None;
                }
            }
        }
#endif

#if ENABLE_INPUT_SYSTEM
        private static class InputSystemReflectionBackend
        {
            private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            private static readonly PropertyInfo CurrentProperty = KeyboardType != null ? KeyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static) : null;

            // GetKeyDown は毎フレーム呼ばれるため、リフレクション検索の結果をキーごとにキャッシュする。
            // 見つからないキーも null としてキャッシュし、毎フレームの再検索を避ける。
            private static readonly System.Collections.Generic.Dictionary<DialogueKeyCode, PropertyInfo> KeyProperties =
                new System.Collections.Generic.Dictionary<DialogueKeyCode, PropertyInfo>();
            private static PropertyInfo _wasPressedThisFrameProperty;

            public static bool GetKeyDown(DialogueKeyCode key)
            {
                if (CurrentProperty == null)
                    return false;

                var keyboard = CurrentProperty.GetValue(null, null);
                if (keyboard == null)
                    return false;

                var keyProperty = GetKeyProperty(key);
                if (keyProperty == null)
                    return false;

                var keyControl = keyProperty.GetValue(keyboard, null);
                if (keyControl == null)
                    return false;

                // KeyControl 型は全キー共通のため、wasPressedThisFrame は一度だけ解決すればよい。
                if (_wasPressedThisFrameProperty == null)
                    _wasPressedThisFrameProperty = keyControl.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);

                return _wasPressedThisFrameProperty != null && (bool)_wasPressedThisFrameProperty.GetValue(keyControl, null);
            }

            private static PropertyInfo GetKeyProperty(DialogueKeyCode key)
            {
                PropertyInfo keyProperty;
                if (KeyProperties.TryGetValue(key, out keyProperty))
                    return keyProperty;

                var propertyName = ToInputSystemPropertyName(key);
                keyProperty = string.IsNullOrEmpty(propertyName)
                    ? null
                    : KeyboardType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                KeyProperties.Add(key, keyProperty);
                return keyProperty;
            }

            private static string ToInputSystemPropertyName(DialogueKeyCode key)
            {
                switch (key)
                {
                    case DialogueKeyCode.Space: return "spaceKey";
                    case DialogueKeyCode.Enter: return "enterKey";
                    case DialogueKeyCode.B: return "bKey";
                    case DialogueKeyCode.LeftControl: return "leftCtrlKey";
                    case DialogueKeyCode.A: return "aKey";
                    case DialogueKeyCode.PageUp: return "pageUpKey";
                    case DialogueKeyCode.L: return "lKey";
                    case DialogueKeyCode.F5: return "f5Key";
                    case DialogueKeyCode.F9: return "f9Key";
                    case DialogueKeyCode.Alpha1: return "digit1Key";
                    case DialogueKeyCode.Alpha2: return "digit2Key";
                    case DialogueKeyCode.Alpha3: return "digit3Key";
                    case DialogueKeyCode.R: return "rKey";
                    default: return string.Empty;
                }
            }
        }
#endif
    }
}
