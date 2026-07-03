using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 立ち絵スロット名と描画先 Image の対応。
    /// </summary>
    [Serializable]
    public sealed class DialogueStageSlotBinding
    {
        public string slot = DialogueStageSlot.Center;
        public Image image;
    }

    /// <summary>
    /// <see cref="IDialogueStageView"/> の UGUI 実装。背景と立ち絵スロットを Image で描画し、
    /// アセットキーを <see cref="BackgroundDatabase"/> / <see cref="CharacterExpressionDatabase"/> で解決する。
    /// フェードは単純な alpha 補間で、より高度な演出は後続フェーズで差し替え可能。
    /// </summary>
    public class DialogueStageView : MonoBehaviour, IDialogueStageView, IDialoguePresentationIssueSource
    {
        [Header("Databases")]
        [SerializeField] private BackgroundDatabase backgroundDatabase;
        [SerializeField] private CharacterExpressionDatabase characterDatabase;

        [Header("Background")]
        [SerializeField] private Image backgroundImage;

        [Header("Character Slots")]
        [SerializeField] private List<DialogueStageSlotBinding> slots = new List<DialogueStageSlotBinding>();

        [Header("Character Backend (任意)")]
        [Tooltip("IDialogueCharacterBackend を実装したコンポーネント（Live2D/Spine/モデル）。設定時は立ち絵描画をここへ委譲する。")]
        [SerializeField] private MonoBehaviour characterBackend;

        private IDialogueCharacterBackend _backend;

        [Header("Transitions")]
        [Tooltip("トランジション秒数が未指定（0）でフェード系が指定されたときに使う既定の長さ。")]
        [SerializeField] private float defaultFadeDuration = 0.25f;

        private readonly Dictionary<Image, Coroutine> _fades = new Dictionary<Image, Coroutine>();

        public event Action<DialoguePresentationIssueContext> PresentationIssueRaised;

        private void Awake()
        {
            _backend = characterBackend as IDialogueCharacterBackend;
            if (characterBackend != null && _backend == null)
                Debug.LogWarning("[DialogueStageView] characterBackend が IDialogueCharacterBackend を実装していません。");
        }

        /// <summary>立ち絵バックエンドを実行時に差し替える（Live2D/Spine など）。</summary>
        public void SetCharacterBackend(IDialogueCharacterBackend backend)
        {
            _backend = backend;
        }

        public void SetBackground(string backgroundKey, bool clear, string transition, float duration)
        {
            if (backgroundImage == null)
            {
                Debug.LogWarning("[DialogueStageView] backgroundImage が未設定です。");
                return;
            }

            var fade = ResolveDuration(duration, transition);

            if (clear)
            {
                FadeOut(backgroundImage, fade);
                return;
            }

            Sprite sprite;
            if (backgroundDatabase == null || !backgroundDatabase.TryGetSprite(backgroundKey, out sprite))
            {
                RaiseIssue(DialoguePresentationIssueKind.Background, backgroundKey,
                    "Background key \"" + backgroundKey + "\" could not be resolved.");
                Debug.LogWarning("[DialogueStageView] 背景キー \"" + backgroundKey + "\" を解決できません。");
                return;
            }

            backgroundImage.sprite = sprite;
            FadeIn(backgroundImage, fade);
        }

        public void SetCharacter(string slot, string characterKey, string expression, string animation)
        {
            if (_backend != null)
            {
                _backend.SetCharacter(slot, characterKey, expression, animation);
                return;
            }

            var image = FindSlotImage(slot);
            if (image == null)
            {
                RaiseIssue(DialoguePresentationIssueKind.StageSlot, slot,
                    "Stage slot \"" + slot + "\" is not bound to an Image.");
                Debug.LogWarning("[DialogueStageView] スロット \"" + slot + "\" に対応する Image がありません。");
                return;
            }

            Sprite sprite;
            if (characterDatabase == null || !characterDatabase.TryGetSprite(characterKey, expression, out sprite))
            {
                RaiseIssue(DialoguePresentationIssueKind.Character, characterKey,
                    "Character key \"" + characterKey + "\" expression \"" + expression + "\" could not be resolved.");
                Debug.LogWarning("[DialogueStageView] 立ち絵 \"" + characterKey + "\" (" + expression + ") を解決できません。");
                return;
            }

            image.sprite = sprite;
            if (IsFadeAnimation(animation))
                FadeIn(image, ResolveDuration(0f, animation));
            else
                SetAlpha(image, 1f, true);
        }

        public void RemoveCharacter(string slot, string characterKey, string animation)
        {
            if (_backend != null)
            {
                _backend.RemoveCharacter(slot, characterKey, animation);
                return;
            }

            var image = FindSlotImage(slot);
            if (image == null) return;

            if (IsFadeAnimation(animation))
                FadeOut(image, ResolveDuration(0f, animation));
            else
                SetAlpha(image, 0f, false);
        }

        public void ClearCharacters()
        {
            if (_backend != null)
            {
                _backend.ClearCharacters();
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var binding = slots[i];
                if (binding != null && binding.image != null)
                    SetAlpha(binding.image, 0f, false);
            }
        }

        private Image FindSlotImage(string slot)
        {
            var key = string.IsNullOrEmpty(slot) ? DialogueStageSlot.Center : slot;
            for (var i = 0; i < slots.Count; i++)
            {
                var binding = slots[i];
                if (binding != null && binding.slot == key)
                    return binding.image;
            }

            return null;
        }

        private void RaiseIssue(DialoguePresentationIssueKind kind, string key, string message)
        {
            if (PresentationIssueRaised != null)
                PresentationIssueRaised(new DialoguePresentationIssueContext(kind, key, message));
        }

        private float ResolveDuration(float duration, string modifier)
        {
            if (duration > 0f) return duration;
            return IsFadeAnimation(modifier) ? defaultFadeDuration : 0f;
        }

        private static bool IsFadeAnimation(string modifier)
        {
            if (string.IsNullOrEmpty(modifier)) return false;
            return modifier.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   modifier.IndexOf("crossfade", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FadeIn(Image image, float duration)
        {
            image.enabled = true;
            if (duration <= 0f || !isActiveAndEnabled)
            {
                SetAlpha(image, 1f, true);
                return;
            }

            StartFade(image, 1f, duration, false);
        }

        private void FadeOut(Image image, float duration)
        {
            if (duration <= 0f || !isActiveAndEnabled)
            {
                SetAlpha(image, 0f, false);
                return;
            }

            StartFade(image, 0f, duration, true);
        }

        private void StartFade(Image image, float targetAlpha, float duration, bool disableAtEnd)
        {
            Coroutine running;
            if (_fades.TryGetValue(image, out running) && running != null)
                StopCoroutine(running);

            _fades[image] = StartCoroutine(FadeRoutine(image, targetAlpha, duration, disableAtEnd));
        }

        private IEnumerator FadeRoutine(Image image, float targetAlpha, float duration, bool disableAtEnd)
        {
            var startAlpha = image.color.a;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                ApplyAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            ApplyAlpha(image, targetAlpha);
            if (disableAtEnd && Mathf.Approximately(targetAlpha, 0f))
                image.enabled = false;

            _fades[image] = null;
        }

        private void SetAlpha(Image image, float alpha, bool enabled)
        {
            Coroutine running;
            if (_fades.TryGetValue(image, out running) && running != null)
            {
                StopCoroutine(running);
                _fades[image] = null;
            }

            ApplyAlpha(image, alpha);
            image.enabled = enabled;
        }

        private static void ApplyAlpha(Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
