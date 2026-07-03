using System;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 1 行分の <see cref="DialogueData"/> の音声フィールド（Bgm / Se / Voice）を解釈し、
    /// 音声プレイヤーへ反映する指揮役。<see cref="DialogueManager.LineStarted"/> から
    /// <see cref="Apply(DialogueData)"/> を呼ぶ想定。Unity 型に依存しないためテスト可能。
    /// 背景・立ち絵は別系統（<see cref="DialogueStageDirector"/>）が担当する。
    /// </summary>
    public sealed class DialogueAudioDirector
    {
        private readonly IDialogueAudioPlayer _player;

        public DialogueAudioDirector(IDialogueAudioPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>現在再生中の BGM キー（停止中は空）。セーブの完全復元で参照する。</summary>
        public string CurrentBgmKey { get; private set; } = string.Empty;
        public string CurrentVoiceKey { get; private set; } = string.Empty;

        /// <summary>復元時など、再生状態を外部から設定した際に現在 BGM キーを更新する。</summary>
        public void SetCurrentBgmKey(string key)
        {
            CurrentBgmKey = key ?? string.Empty;
        }

        /// <summary>この行の BGM・SE・ボイスを再生する。</summary>
        public DialogueAudioSnapshot CaptureSnapshot()
        {
            return new DialogueAudioSnapshot
            {
                bgmKey = CurrentBgmKey ?? string.Empty,
                voiceKey = CurrentVoiceKey ?? string.Empty
            };
        }

        public void RestoreSnapshot(DialogueAudioSnapshot snapshot)
        {
            var bgmKey = snapshot != null ? snapshot.bgmKey ?? string.Empty : string.Empty;
            var voiceKey = snapshot != null ? snapshot.voiceKey ?? string.Empty : string.Empty;

            if (string.IsNullOrEmpty(bgmKey))
                _player.PlayBgm(string.Empty, true, string.Empty, 0f);
            else
                _player.PlayBgm(bgmKey, false, string.Empty, 0f);

            if (string.IsNullOrEmpty(voiceKey))
                _player.StopVoice();
            else
                _player.PlayVoice(voiceKey);

            CurrentBgmKey = bgmKey;
            CurrentVoiceKey = voiceKey;
        }

        public void Apply(DialogueData data)
        {
            if (data == null) return;

            ApplyBgm(data);
            ApplySe(data);
            ApplyVoice(data);
        }

        /// <summary>会話終了時などに全ての音声を停止する。</summary>
        public void StopAll()
        {
            CurrentBgmKey = string.Empty;
            CurrentVoiceKey = string.Empty;
            _player.StopAll();
        }

        private void ApplyBgm(DialogueData data)
        {
            var cue = data.GetBgmCue();
            if (!cue.HasValue) return;

            if (!cue.IsClear && CurrentBgmKey == cue.Key)
                return;

            CurrentBgmKey = cue.IsClear ? string.Empty : cue.Key;

            var duration = cue.HasDuration ? Math.Max(0f, cue.Duration) : 0f;
            _player.PlayBgm(cue.Key, cue.IsClear, cue.Transition, duration);
        }

        private void ApplySe(DialogueData data)
        {
            var keys = data.GetSeKeys();
            for (var i = 0; i < keys.Count; i++)
                _player.PlaySe(keys[i]);
        }

        private void ApplyVoice(DialogueData data)
        {
            // ボイスは行に紐づく。Voice 欄が空の行に進んだら前行のボイスを停止する。
            // （BGM と異なり継続をデフォルトにしない。継続が必要なら将来別記法を追加する。）
            if (data.HasVoice)
            {
                CurrentVoiceKey = data.Voice;
                _player.PlayVoice(data.Voice);
            }
            else
            {
                CurrentVoiceKey = string.Empty;
                _player.StopVoice();
            }
        }
    }
}
