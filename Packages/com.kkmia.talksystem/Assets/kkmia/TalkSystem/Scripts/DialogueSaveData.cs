using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>セーブに含める任意のサブシステム状態（ステージ占有・現在BGMなど）の 1 項目。</summary>
    [Serializable]
    public sealed class DialogueSaveValue
    {
        public string Key = string.Empty;
        public string Value = string.Empty;

        public DialogueSaveValue() { }

        public DialogueSaveValue(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }

    /// <summary>
    /// 選択履歴を、表示時点のフィルタ済み UI index ではなく安定した事実として保存する。
    /// schema 1 の <see cref="DialogueSaveData.ChoiceHistory"/> から移行した record は
    /// line / destination が復元不能なため、RawChoiceIndex 以外を既定値にした lossy record になる。
    /// </summary>
    [Serializable]
    public sealed class DialogueChoiceRecord
    {
        public int LineId = -1;
        public int RawChoiceIndex = -1;
        public int NextId = -1;
        public string Text = string.Empty;
        public string ConditionKey = string.Empty;

        public DialogueChoiceRecord() { }

        public DialogueChoiceRecord(int lineId, int rawChoiceIndex, int nextId, string text, string conditionKey)
        {
            LineId = lineId;
            RawChoiceIndex = rawChoiceIndex;
            NextId = nextId;
            Text = text ?? string.Empty;
            ConditionKey = conditionKey ?? string.Empty;
        }

        public DialogueChoiceRecord Clone()
        {
            return new DialogueChoiceRecord(LineId, RawChoiceIndex, NextId, Text, ConditionKey);
        }
    }

    [Serializable]
    public sealed class DialogueSaveData
    {
        public int SchemaVersion = DialogueSaveSchema.CurrentVersion;
        public string ContentVersion = string.Empty;
        public string ProductChannel = string.Empty;
        public int CurrentDialogueId = -1;
        public string TriggerKey = string.Empty;
        public DialogueSessionState State = DialogueSessionState.Idle;
        public List<int> SeenLineIds = new List<int>();
        public List<DialogueChoiceRecord> ChoiceRecords = new List<DialogueChoiceRecord>();
        public List<int> ChoiceHistory = new List<int>();
        public List<DialogueHistoryEntry> History = new List<DialogueHistoryEntry>();
        public DialogueProgressState Progress = new DialogueProgressState();

        /// <summary>
        /// 演出系（ステージ・音声など）が完全復元のために書き込む任意の追加状態。
        /// <see cref="IDialogueSaveContributor"/> 経由で各サブシステムが読み書きする。
        /// </summary>
        public List<DialogueSaveValue> ExtraState = new List<DialogueSaveValue>();

        /// <summary>追加状態を設定（既存キーは上書き）。</summary>
        public void SetExtra(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;

            for (var i = 0; i < ExtraState.Count; i++)
            {
                if (ExtraState[i] != null && ExtraState[i].Key == key)
                {
                    ExtraState[i].Value = value ?? string.Empty;
                    return;
                }
            }

            ExtraState.Add(new DialogueSaveValue(key, value));
        }

        public bool TryGetExtra(string key, out string value)
        {
            for (var i = 0; i < ExtraState.Count; i++)
            {
                if (ExtraState[i] != null && ExtraState[i].Key == key)
                {
                    value = ExtraState[i].Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
