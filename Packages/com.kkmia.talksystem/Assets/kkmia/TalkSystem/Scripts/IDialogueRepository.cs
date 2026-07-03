using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 会話データへのアクセスインターフェース。
    /// 実装クラスはCSVやデータベース等からDialogueDataを取得します。
    /// </summary>
    public interface IDialogueRepository
    {
        /// <summary>
        /// 指定したIDの会話データを取得します。
        /// </summary>
        /// <param name="id">会話データのID</param>
        /// <returns>一致するデータ。存在しない場合はnull</returns>
        DialogueData Get(int id);

        /// <summary>
        /// 登録されている全ての会話データを取得します。
        /// </summary>
        IEnumerable<DialogueData> GetAll();

        /// <summary>
        /// 指定されたTriggerKeyを持つ最初の会話データを取得します。
        /// </summary>
        /// <param name="key">TriggerKey</param>
        /// <returns>一致するデータ。存在しない場合はnull</returns>
        DialogueData GetByTriggerKey(string key);
    }
}