using System;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 1 セーブスロット。メタ情報（タイトル・保存時刻・オートセーブ可否）と本体データを持つ。
    /// サムネイルは JSON に含めず別ファイルで扱う（<see cref="IDialogueSaveStorage"/>）。
    /// </summary>
    [Serializable]
    public sealed class DialogueSaveSlot
    {
        public int SchemaVersion = DialogueSaveSchema.CurrentVersion;
        public string ContentVersion = string.Empty;
        public string ProductChannel = string.Empty;
        public int SlotIndex = -1;
        public string Title = string.Empty;
        public long SavedAtUnix;
        public bool IsAutosave;
        public DialogueSaveData Data = new DialogueSaveData();

        public DateTime SavedAtUtc
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(SavedAtUnix).UtcDateTime; }
        }
    }

    /// <summary>
    /// セーブ/ロード時に追加状態（ステージ・音声など）を <see cref="DialogueSaveData.ExtraState"/> へ
    /// 読み書きする拡張点。Phase 2/3 の演出層がこれを実装して完全復元に参加する。
    /// </summary>
    public interface IDialogueSaveContributor
    {
        /// <summary>現在のサブシステム状態を data に書き込む。</summary>
        void Capture(DialogueSaveData data);

        /// <summary>data からサブシステム状態を復元する。</summary>
        void Restore(DialogueSaveData data);
    }
}
