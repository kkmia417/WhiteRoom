using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>Builds and drives the ending result overlay.</summary>
    public sealed class EndingResultScreenController
    {
        private static readonly UiButtonStyle ButtonStyle = new UiButtonStyle(
            new Color(0.65f, 0.55f, 0.34f, 0.98f),
            new Color(0.78f, 0.68f, 0.45f, 1f),
            new Color(0.46f, 0.38f, 0.22f, 1f),
            new Color(0.20f, 0.20f, 0.20f, 0.55f));

        private readonly Action _confirm;
        private GameObject _root;
        private TMP_Text _typeLabel;
        private TMP_Text _nameLabel;
        private TMP_Text _firstReachLabel;

        public EndingResultScreenController(Action confirm)
        {
            _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        }

        public bool IsVisible => _root != null && _root.activeSelf;
        public EndingResultInfo CurrentResult { get; private set; }

        public void Show(EndingResultInfo result)
        {
            if (result == null)
                return;

            EnsureCreated();
            CurrentResult = result;
            _typeLabel.text = result.Type;
            _nameLabel.text = result.DisplayName;
            _firstReachLabel.text = result.IsFirstReach ? "NEW ENDING" : "REACHED AGAIN";
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            CurrentResult = null;
            if (_root != null)
                _root.SetActive(false);
        }

        private void EnsureCreated()
        {
            if (_root != null)
                return;

            NovelUiFactory.EnsureEventSystem();
            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject("EndingResultScreen", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0.012f, 0.014f, 0.018f, 0.94f);

            var panel = new GameObject("Result", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.22f, 0.20f);
            panelRect.anchorMax = new Vector2(0.78f, 0.80f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _typeLabel = CreateLabel(panel.transform, "EndingType", 36f, 64f, FontStyles.Bold);
            _nameLabel = CreateLabel(panel.transform, "EndingName", 52f, 112f, FontStyles.Bold);
            _firstReachLabel = CreateLabel(panel.transform, "ReachStatus", 20f, 44f, FontStyles.Normal);
            var button = NovelUiFactory.CreateButton(
                "ReturnToTitleButton",
                panel.transform,
                "Titleへ戻る",
                24f,
                ButtonStyle);
            var buttonLayout = button.gameObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = 62f;
            buttonLayout.preferredHeight = 62f;
            button.onClick.AddListener(() => _confirm());

            _root.SetActive(false);
        }

        private static TMP_Text CreateLabel(Transform parent, string name, float fontSize, float height, FontStyles style)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var element = holder.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            var text = holder.GetComponent<TextMeshProUGUI>();
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
