namespace kkmia.TalkSystem
{
    /// <summary>
    /// ステージ（背景・立ち絵）の描画先。ディレクターはアセットキーのみを渡し、
    /// スプライト解決と実際の描画・トランジションは実装側（UGUI など）に閉じる。
    /// これによりディレクターは Unity 型に依存せずテスト可能になる。
    /// </summary>
    public interface IDialogueStageView
    {
        /// <summary>
        /// 背景を変更する。<paramref name="clear"/> が true のときは背景を消す。
        /// </summary>
        void SetBackground(string backgroundKey, bool clear, string transition, float duration);

        /// <summary>指定スロットにキャラクターを表示／更新する。</summary>
        void SetCharacter(string slot, string characterKey, string expression, string animation);

        /// <summary>指定スロットのキャラクターを退場させる。</summary>
        void RemoveCharacter(string slot, string characterKey, string animation);

        /// <summary>ステージ上の全キャラクターを消去する。</summary>
        void ClearCharacters();
    }
}
