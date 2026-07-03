using System;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// プレイヤー設定（音量・文字速度・オート/スキップ）。値が変わると <see cref="Changed"/> を発火する。
    /// Unity 型に依存しないためテスト可能。永続化は <see cref="IDialogueSettingsStore"/> に委ねる。
    /// </summary>
    public sealed class DialogueSettings
    {
        private float _masterVolume = 1f;
        private float _bgmVolume = 1f;
        private float _seVolume = 1f;
        private float _voiceVolume = 1f;
        private float _textSpeed = 0.5f;
        private float _autoAdvanceDelay = 1.5f;
        private bool _skipReadOnly = true;

        /// <summary>いずれかの設定値が変化したときに発火。</summary>
        public event Action Changed;

        public float MasterVolume { get { return _masterVolume; } set { Set(ref _masterVolume, Clamp01(value)); } }
        public float BgmVolume { get { return _bgmVolume; } set { Set(ref _bgmVolume, Clamp01(value)); } }
        public float SeVolume { get { return _seVolume; } set { Set(ref _seVolume, Clamp01(value)); } }
        public float VoiceVolume { get { return _voiceVolume; } set { Set(ref _voiceVolume, Clamp01(value)); } }

        /// <summary>文字送り速度の正規化値（0=遅い, 1=速い）。</summary>
        public float TextSpeed { get { return _textSpeed; } set { Set(ref _textSpeed, Clamp01(value)); } }

        /// <summary>オート進行時、行表示後に次へ進むまでの待ち秒数。</summary>
        public float AutoAdvanceDelay { get { return _autoAdvanceDelay; } set { Set(ref _autoAdvanceDelay, Math.Max(0f, value)); } }

        /// <summary>スキップを既読行のみに限定するか。</summary>
        public bool SkipReadOnly { get { return _skipReadOnly; } set { Set(ref _skipReadOnly, value); } }

        public float EffectiveBgmVolume { get { return _masterVolume * _bgmVolume; } }
        public float EffectiveSeVolume { get { return _masterVolume * _seVolume; } }
        public float EffectiveVoiceVolume { get { return _masterVolume * _voiceVolume; } }

        public void Load(IDialogueSettingsStore store)
        {
            if (store == null) return;
            store.Load(this);
            RaiseChanged();
        }

        public void Save(IDialogueSettingsStore store)
        {
            if (store != null)
                store.Save(this);
        }

        private void Set(ref float field, float value)
        {
            if (field == value) return;
            field = value;
            RaiseChanged();
        }

        private void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler();
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }

    /// <summary>設定の永続化先（PlayerPrefs など）。</summary>
    public interface IDialogueSettingsStore
    {
        void Load(DialogueSettings settings);
        void Save(DialogueSettings settings);
    }

    /// <summary>音量関連の純粋な数値変換。</summary>
    public static class DialogueVolume
    {
        /// <summary>
        /// 線形音量(0..1)を AudioMixer 用のデシベルへ変換する。0 は実質ミュート(-80dB)。
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            if (linear > 1f) linear = 1f;
            return (float)(20.0 * Math.Log10(linear));
        }
    }
}
