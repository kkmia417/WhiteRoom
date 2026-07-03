using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>セーブ復元用の、1 スロットに立つキャラクターのスナップショット。</summary>
    [Serializable]
    public struct DialogueStageCharacterSnapshot
    {
        public string slot;
        public string characterKey;
        public string expression;

        public DialogueStageCharacterSnapshot(string slot, string characterKey, string expression)
        {
            this.slot = slot ?? string.Empty;
            this.characterKey = characterKey ?? string.Empty;
            this.expression = expression ?? string.Empty;
        }
    }

    /// <summary>
    /// ステージの完全復元に必要な状態（背景・立ち絵スロット）。JsonUtility で文字列化して
    /// <see cref="DialogueSaveData.ExtraState"/> に格納する。
    /// </summary>
    [Serializable]
    public sealed class DialogueStageSnapshot
    {
        public string backgroundKey = string.Empty;
        public List<DialogueStageCharacterSnapshot> characters = new List<DialogueStageCharacterSnapshot>();
    }
}
