using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// セーブスロットとサムネイルの永続化先。ファイル・クラウド等を差し替え可能にする。
    /// サムネイルは PNG バイト列で扱い、未対応の実装は null を返してよい。
    /// </summary>
    public interface IDialogueSaveStorage
    {
        bool TryLoad(int slot, out DialogueSaveSlot data);
        void Save(DialogueSaveSlot slot);
        void Delete(int slot);
        bool Exists(int slot);

        /// <summary>保存済みスロット番号を昇順で返す。</summary>
        IEnumerable<int> ListSlots();

        byte[] LoadThumbnail(int slot);
        void SaveThumbnail(int slot, byte[] pngBytes);
    }
}
