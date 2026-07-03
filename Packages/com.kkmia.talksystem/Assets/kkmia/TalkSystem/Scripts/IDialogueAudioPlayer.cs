namespace kkmia.TalkSystem
{
    /// <summary>
    /// BGM / SE / ボイスの再生先。ディレクターはアセットキーのみを渡し、
    /// クリップ解決・ミキサールーティング・フェードは実装側（AudioSource など）に閉じる。
    /// これによりディレクターは Unity 型に依存せずテスト可能になる。
    /// </summary>
    public interface IDialogueAudioPlayer
    {
        /// <summary>
        /// BGM を変更する。<paramref name="stop"/> が true のときは再生中の BGM を止める。
        /// </summary>
        void PlayBgm(string bgmKey, bool stop, string transition, float duration);

        /// <summary>効果音を 1 回再生する（多重再生可）。</summary>
        void PlaySe(string seKey);

        /// <summary>この行のボイスを再生する。直前のボイスは停止する。</summary>
        void PlayVoice(string voiceKey);

        /// <summary>再生中のボイスを停止する。</summary>
        void StopVoice();

        /// <summary>BGM・ボイスを含め全ての音声を停止する（会話終了時など）。</summary>
        void StopAll();
    }
}
