using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// WhiteRoom-owned, safe-area-aware chapter title treatment. It contains no
    /// progression rules and never intercepts input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NovelChapterTitleView : MonoBehaviour
    {
        public const string ObjectName = "NovelChapterTitleOverlay";
        public const string SafeAreaName = "SafeArea";
        public const string PanelName = "ChapterTitlePanel";
        public const string OrdinalName = "ChapterOrdinal";
        public const string TitleName = "ChapterTitle";

        private static readonly Vector2 PanelBasePosition = new Vector2(-58f, -58f);
        private static readonly Vector2 PanelSize = new Vector2(360f, 184f);

        private CanvasGroup _group;
        private RectTransform _panelRect;
        private Image _backdrop;
        private Image _accent;
        private RectTransform _ruleRect;
        private Image _rule;
        private TMP_Text _ordinal;
        private TMP_Text _title;

        public CanvasGroup Group => _group;
        public RectTransform PanelRect => _panelRect;
        public TMP_Text OrdinalText => _ordinal;
        public TMP_Text TitleText => _title;
        public bool IsVisible => gameObject.activeSelf && _group != null && _group.alpha > 0f;

        public static NovelChapterTitleView Ensure(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return null;

            var existing = canvasRoot.Find(ObjectName);
            if (existing != null)
            {
                var existingView = existing.GetComponent<NovelChapterTitleView>();
                if (existingView != null)
                {
                    existingView.CacheReferences();
                    existing.SetAsLastSibling();
                    return existingView;
                }
            }

            var root = new GameObject(
                ObjectName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(NovelChapterTitleView));
            root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)root.transform);

            var safeArea = new GameObject(SafeAreaName, typeof(RectTransform), typeof(NovelSafeAreaDriver));
            safeArea.transform.SetParent(root.transform, false);
            var safeRect = (RectTransform)safeArea.transform;
            Stretch(safeRect);
            safeArea.GetComponent<NovelSafeAreaDriver>().Configure(safeRect);

            var panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(safeArea.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = PanelBasePosition;
            panelRect.sizeDelta = PanelSize;
            var backdrop = panel.GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.045f, 0.085f, 0.58f);
            backdrop.raycastTarget = false;

            var accentObject = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accentObject.transform.SetParent(panel.transform, false);
            var accentRect = (RectTransform)accentObject.transform;
            accentRect.anchorMin = new Vector2(1f, 0f);
            accentRect.anchorMax = Vector2.one;
            accentRect.pivot = Vector2.one;
            accentRect.offsetMin = new Vector2(-6f, 0f);
            accentRect.offsetMax = Vector2.zero;
            var accent = accentObject.GetComponent<Image>();
            accent.color = new Color(0.38f, 0.67f, 0.94f, 1f);
            accent.raycastTarget = false;

            var eyebrow = CreateText(
                "ChapterEyebrow",
                panel.transform,
                new Vector2(-28f, -18f),
                new Vector2(300f, 24f),
                13f,
                FontStyles.Normal);
            eyebrow.text = "WHITE ROOM  /  CH.";
            eyebrow.color = new Color(0.62f, 0.72f, 0.84f, 0.92f);
            eyebrow.characterSpacing = 4f;

            var ordinal = CreateText(
                OrdinalName,
                panel.transform,
                new Vector2(-28f, -46f),
                new Vector2(260f, 38f),
                22f,
                FontStyles.Bold);

            var title = CreateText(
                TitleName,
                panel.transform,
                new Vector2(-28f, -88f),
                new Vector2(304f, 58f),
                38f,
                FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 38f;

            var ruleObject = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            ruleObject.transform.SetParent(panel.transform, false);
            var ruleRect = (RectTransform)ruleObject.transform;
            ruleRect.anchorMin = Vector2.one;
            ruleRect.anchorMax = Vector2.one;
            ruleRect.pivot = new Vector2(1f, 0.5f);
            ruleRect.anchoredPosition = new Vector2(-28f, -160f);
            ruleRect.sizeDelta = new Vector2(304f, 2f);
            var rule = ruleObject.GetComponent<Image>();
            rule.color = new Color(0.38f, 0.67f, 0.94f, 0.72f);
            rule.raycastTarget = false;

            var view = root.GetComponent<NovelChapterTitleView>();
            view.CacheReferences();
            view.HideImmediate();
            root.transform.SetAsLastSibling();
            return view;
        }

        public void Prepare(string ordinal, string title, NovelDialogueMotionController.StageTransitionMood mood)
        {
            CacheReferences();
            if (_ordinal != null)
                _ordinal.text = ordinal ?? string.Empty;
            if (_title != null)
                _title.text = title ?? string.Empty;
            ApplyMood(mood);
            gameObject.SetActive(true);
            SetReveal(0f);
        }

        public void SetReveal(float t)
        {
            t = Mathf.Clamp01(t);
            if (_group != null)
                _group.alpha = t;
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = Vector2.LerpUnclamped(
                    PanelBasePosition + new Vector2(52f, 0f),
                    PanelBasePosition,
                    t);
                _panelRect.localScale = Vector3.LerpUnclamped(
                    Vector3.one * 0.985f,
                    Vector3.one,
                    t);
            }
            if (_ruleRect != null)
                _ruleRect.localScale = new Vector3(Mathf.Lerp(0.08f, 1f, t), 1f, 1f);
        }

        public void ShowImmediate(string ordinal, string title, NovelDialogueMotionController.StageTransitionMood mood)
        {
            Prepare(ordinal, title, mood);
            SetReveal(1f);
        }

        public void BeginExit()
        {
            CacheReferences();
            gameObject.SetActive(true);
            if (_group != null)
                _group.alpha = 1f;
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = PanelBasePosition;
                _panelRect.localScale = Vector3.one;
            }
            if (_ruleRect != null)
                _ruleRect.localScale = Vector3.one;
        }

        public void SetExit(float t)
        {
            t = Mathf.Clamp01(t);
            if (_group != null)
                _group.alpha = 1f - t;
            if (_panelRect != null)
                _panelRect.anchoredPosition = PanelBasePosition + new Vector2(18f * t, -6f * t);
        }

        public void HideImmediate()
        {
            CacheReferences();
            if (_group != null)
                _group.alpha = 0f;
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = PanelBasePosition;
                _panelRect.localScale = Vector3.one;
            }
            if (_ruleRect != null)
                _ruleRect.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void ApplyMood(NovelDialogueMotionController.StageTransitionMood mood)
        {
            var backdrop = new Color(0.02f, 0.045f, 0.085f, 0.58f);
            var accent = new Color(0.38f, 0.67f, 0.94f, 1f);
            if (mood == NovelDialogueMotionController.StageTransitionMood.Sterile)
            {
                backdrop = new Color(0.035f, 0.07f, 0.10f, 0.52f);
                accent = new Color(0.68f, 0.88f, 1f, 1f);
            }
            else if (mood == NovelDialogueMotionController.StageTransitionMood.Alarm)
            {
                backdrop = new Color(0.10f, 0.015f, 0.025f, 0.56f);
                accent = new Color(0.96f, 0.25f, 0.28f, 1f);
            }
            else if (mood == NovelDialogueMotionController.StageTransitionMood.Neutral)
            {
                backdrop = new Color(0.025f, 0.03f, 0.05f, 0.58f);
                accent = new Color(0.58f, 0.65f, 0.78f, 1f);
            }

            if (_backdrop != null)
                _backdrop.color = backdrop;
            if (_accent != null)
                _accent.color = accent;
            if (_rule != null)
                _rule.color = new Color(accent.r, accent.g, accent.b, 0.72f);
            if (_ordinal != null)
                _ordinal.color = accent;
            if (_title != null)
                _title.color = new Color(0.96f, 0.98f, 1f, 1f);
        }

        private void CacheReferences()
        {
            _group = GetComponent<CanvasGroup>();
            var panel = transform.Find(SafeAreaName + "/" + PanelName);
            _panelRect = panel as RectTransform;
            _backdrop = panel != null ? panel.GetComponent<Image>() : null;
            _accent = panel != null ? panel.Find("Accent")?.GetComponent<Image>() : null;
            _ruleRect = panel != null ? panel.Find("Rule") as RectTransform : null;
            _rule = _ruleRect != null ? _ruleRect.GetComponent<Image>() : null;
            _ordinal = panel != null ? panel.Find(OrdinalName)?.GetComponent<TMP_Text>() : null;
            _title = panel != null ? panel.Find(TitleName)?.GetComponent<TMP_Text>() : null;

            if (_group != null)
            {
                _group.interactable = false;
                _group.blocksRaycasts = false;
            }
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            FontStyles style)
        {
            var text = NovelUiFactory.CreateText(name, parent, Vector2.zero, Vector2.zero, fontSize, style);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            text.alignment = TextAlignmentOptions.TopRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
