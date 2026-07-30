using System;
using System.Collections;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="IDialogueAudioPlayer"/> の AudioSource 実装。
    /// BGM（ループ・フェード）、SE（多重ワンショット）、ボイス（行ごと・差し替え）を別チャンネルで扱う。
    /// ボイス用 AudioSource は <see cref="VoiceSource"/> として公開し、リップシンクから参照できる。
    /// </summary>
    public class DialogueAudioPlayer : MonoBehaviour, IDialogueAudioPlayer, IDialoguePresentationIssueSource
    {
        [Header("Database")]
        [SerializeField] private AudioDatabase audioDatabase;

        [Header("Channels")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource voiceSource;

        [Header("Transitions")]
        [Tooltip("フェード秒数が未指定（0）でフェード系トランジションが指定されたときの既定の長さ。")]
        [SerializeField] private float defaultFadeDuration = 0.5f;

        private Coroutine _bgmFade;
        private DialogueSettings _settings;

        public event Action<DialoguePresentationIssueContext> PresentationIssueRaised;

        /// <summary>リップシンク等から参照するためのボイス AudioSource。</summary>
        public AudioSource VoiceSource
        {
            get { return voiceSource; }
        }

        public void BindSettings(DialogueSettings settings)
        {
            if (_settings == settings)
                return;
            if (_settings != null)
                _settings.Changed -= ApplySettingsVolumes;
            _settings = settings;
            if (_settings != null)
                _settings.Changed += ApplySettingsVolumes;
            ApplySettingsVolumes();
        }

        private void OnDestroy()
        {
            if (_settings != null)
                _settings.Changed -= ApplySettingsVolumes;
        }

        public void PlayBgm(string bgmKey, bool stop, string transition, float duration)
        {
            if (bgmSource == null)
            {
                Debug.LogWarning("[DialogueAudioPlayer] bgmSource が未設定です。");
                return;
            }

            var fade = ResolveDuration(duration, transition);

            if (stop)
            {
                FadeBgm(0f, fade, stopAtEnd: true);
                return;
            }

            AudioClip clip;
            if (audioDatabase == null || !audioDatabase.TryGetBgm(bgmKey, out clip))
            {
                RaiseIssue(DialoguePresentationIssueKind.Bgm, bgmKey,
                    "BGM key \"" + bgmKey + "\" could not be resolved.");
                Debug.LogWarning("[DialogueAudioPlayer] BGM キー \"" + bgmKey + "\" を解決できません。");
                return;
            }

            // 同じ BGM が既に鳴っている場合は鳴らし直さない。
            if (bgmSource.isPlaying && bgmSource.clip == clip)
                return;

            bgmSource.clip = clip;
            bgmSource.loop = true;

            if (fade > 0f && isActiveAndEnabled)
            {
                bgmSource.volume = 0f;
                bgmSource.Play();
                FadeBgm(ResolveBgmVolume(), fade, stopAtEnd: false);
            }
            else
            {
                bgmSource.volume = ResolveBgmVolume();
                bgmSource.Play();
            }
        }

        public void PlaySe(string seKey)
        {
            if (seSource == null)
            {
                Debug.LogWarning("[DialogueAudioPlayer] seSource が未設定です。");
                return;
            }

            AudioClip clip;
            if (audioDatabase == null || !audioDatabase.TryGetSe(seKey, out clip))
            {
                RaiseIssue(DialoguePresentationIssueKind.Se, seKey,
                    "SE key \"" + seKey + "\" could not be resolved.");
                Debug.LogWarning("[DialogueAudioPlayer] SE キー \"" + seKey + "\" を解決できません。");
                return;
            }

            seSource.PlayOneShot(clip);
        }

        public void PlayVoice(string voiceKey)
        {
            if (voiceSource == null)
            {
                Debug.LogWarning("[DialogueAudioPlayer] voiceSource が未設定です。");
                return;
            }

            AudioClip clip;
            if (audioDatabase == null || !audioDatabase.TryGetVoice(voiceKey, out clip))
            {
                RaiseIssue(DialoguePresentationIssueKind.Voice, voiceKey,
                    "Voice key \"" + voiceKey + "\" could not be resolved.");
                Debug.LogWarning("[DialogueAudioPlayer] ボイスキー \"" + voiceKey + "\" を解決できません。");
                return;
            }

            voiceSource.Stop();
            voiceSource.loop = false;
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        public void StopVoice()
        {
            if (voiceSource != null)
                voiceSource.Stop();
        }

        public void StopAll()
        {
            StopVoice();
            if (seSource != null)
                seSource.Stop();
            if (bgmSource != null)
                FadeBgm(0f, defaultFadeDuration, stopAtEnd: true);
        }

        /// <summary>Immediately returns every channel to a clean cross-scene baseline.</summary>
        public void ResetPlayback()
        {
            if (_bgmFade != null)
            {
                StopCoroutine(_bgmFade);
                _bgmFade = null;
            }

            ResetSource(bgmSource);
            ResetSource(seSource);
            ResetSource(voiceSource);
            ApplySettingsVolumes();
        }

        private void ApplySettingsVolumes()
        {
            if (_bgmFade != null)
            {
                StopCoroutine(_bgmFade);
                _bgmFade = null;
            }
            if (bgmSource != null) bgmSource.volume = ResolveBgmVolume();
            if (seSource != null) seSource.volume = _settings != null ? _settings.EffectiveSeVolume : 1f;
            if (voiceSource != null) voiceSource.volume = _settings != null ? _settings.EffectiveVoiceVolume : 1f;
        }

        private float ResolveBgmVolume()
        {
            return _settings != null ? _settings.EffectiveBgmVolume : 1f;
        }

        private static void ResetSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 1f;
        }

        private void RaiseIssue(DialoguePresentationIssueKind kind, string key, string message)
        {
            if (PresentationIssueRaised != null)
                PresentationIssueRaised(new DialoguePresentationIssueContext(kind, key, message));
        }

        private float ResolveDuration(float duration, string transition)
        {
            if (duration > 0f) return duration;
            return IsFadeTransition(transition) ? defaultFadeDuration : 0f;
        }

        private static bool IsFadeTransition(string transition)
        {
            return !string.IsNullOrEmpty(transition) &&
                   transition.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FadeBgm(float targetVolume, float duration, bool stopAtEnd)
        {
            if (_bgmFade != null)
                StopCoroutine(_bgmFade);

            if (duration <= 0f || !isActiveAndEnabled)
            {
                bgmSource.volume = targetVolume;
                if (stopAtEnd && Mathf.Approximately(targetVolume, 0f))
                    bgmSource.Stop();
                _bgmFade = null;
                return;
            }

            _bgmFade = StartCoroutine(FadeBgmRoutine(targetVolume, duration, stopAtEnd));
        }

        private IEnumerator FadeBgmRoutine(float targetVolume, float duration, bool stopAtEnd)
        {
            var startVolume = bgmSource.volume;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            bgmSource.volume = targetVolume;
            if (stopAtEnd && Mathf.Approximately(targetVolume, 0f))
                bgmSource.Stop();

            _bgmFade = null;
        }
    }
}
