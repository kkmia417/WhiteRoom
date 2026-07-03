namespace kkmia.TalkSystem
{
    /// <summary>
    /// 立ち絵（キャラクター）の描画バックエンド。<see cref="DialogueStageView"/> はこれが割り当てられている場合、
    /// スプライト描画の代わりにここへ委譲する。Live2D / Spine / プレハブモデル等へ差し替えるための抽象。
    /// メソッド群は <see cref="IDialogueStageView"/> のキャラクター系と同一シグネチャ。
    /// </summary>
    public interface IDialogueCharacterBackend
    {
        void SetCharacter(string slot, string characterKey, string expression, string animation);
        void RemoveCharacter(string slot, string characterKey, string animation);
        void ClearCharacters();
    }

    /// <summary>
    /// リップシンクの口の開き具合（0..1）を受け取る対象。立ち絵モデルが実装し、
    /// Live2D のパラメータや口画像などに反映する。
    /// </summary>
    public interface IDialogueLipSyncTarget
    {
        void SetMouthOpen(float openness);
    }
}
