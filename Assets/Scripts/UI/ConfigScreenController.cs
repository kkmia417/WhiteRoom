using System;
using System.Collections.Generic;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed class ConfigScreenController
    {
        private readonly DialogueSettings _settings;
        private readonly IDialogueSettingsStore _store;
        private readonly List<Selectable> _controls = new List<Selectable>();
        private GameObject _root;
        private bool _syncing;

        public ConfigScreenController(DialogueSettings settings, IDialogueSettingsStore store)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _store = store;
        }

        public event Action<bool> VisibilityChanged;
        public bool IsOpen => _root != null && _root.activeSelf;
        public DialogueSettings Settings => _settings;

        public void Open()
        {
            EnsureCreated();
            SyncControls();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            VisibilityChanged?.Invoke(true);
            if (EventSystem.current != null && _controls.Count > 0)
                EventSystem.current.SetSelectedGameObject(_controls[0].gameObject);
        }

        public void Close()
        {
            _settings.Save(_store);
            if (_root != null) _root.SetActive(false);
            VisibilityChanged?.Invoke(false);
        }

        public void SetTextSpeed(float value) => Apply(() => _settings.TextSpeed = value);
        public void SetAutoDelay(float value) => Apply(() => _settings.AutoAdvanceDelay = value);
        public void SetBgmVolume(float value) => Apply(() => _settings.BgmVolume = value);
        public void SetSeVolume(float value) => Apply(() => _settings.SeVolume = value);
        public void SetVoiceVolume(float value) => Apply(() => _settings.VoiceVolume = value);
        public void SetSkipReadOnly(bool value) => Apply(() => _settings.SkipReadOnly = value);

        private void Apply(Action change)
        {
            if (_syncing) return;
            change();
            _settings.Save(_store);
        }

        private void EnsureCreated()
        {
            if (_root != null) return;
            NovelUiFactory.EnsureEventSystem();
            var canvas = NovelUiFactory.EnsureCanvas();
            _root = new GameObject("WhiteRoomConfigScreen", typeof(RectTransform), typeof(Image), typeof(ConfigScreenInputDriver));
            _root.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)_root.transform);
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            _root.GetComponent<ConfigScreenInputDriver>().Configure(this);

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(NovelSafeAreaDriver));
            safe.transform.SetParent(_root.transform, false);
            Stretch((RectTransform)safe.transform);
            safe.GetComponent<NovelSafeAreaDriver>().Configure((RectTransform)safe.transform);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(safe.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.22f, 0.08f);
            panelRect.anchorMax = new Vector2(0.78f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.034f, 0.98f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 28, 28);
            layout.spacing = 14f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateHeading(panel.transform);
            CreateSliderRow(panel.transform, "Text Speed", 0f, 1f, _settings.TextSpeed, SetTextSpeed);
            CreateSliderRow(panel.transform, "Auto Wait", 0f, 5f, _settings.AutoAdvanceDelay, SetAutoDelay);
            CreateSliderRow(panel.transform, "BGM Volume", 0f, 1f, _settings.BgmVolume, SetBgmVolume);
            CreateSliderRow(panel.transform, "SE Volume", 0f, 1f, _settings.SeVolume, SetSeVolume);
            CreateSliderRow(panel.transform, "Voice Volume", 0f, 1f, _settings.VoiceVolume, SetVoiceVolume);
            CreateToggleRow(panel.transform);
            var back = NovelUiFactory.CreateButton("BackButton", panel.transform, "Back", 20f, UiButtonStyle.Default);
            back.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            back.onClick.AddListener(Close);
            _controls.Add(back);
            _root.SetActive(false);
        }

        private void CreateHeading(Transform parent)
        {
            var label = NovelUiFactory.CreateText("Heading", parent, Vector2.zero, Vector2.zero, 34f, FontStyles.Bold);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            label.text = "Config";
            label.alignment = TextAlignmentOptions.Center;
        }

        private void CreateSliderRow(Transform parent, string labelText, float min, float max, float value, Action<float> apply)
        {
            var row = new GameObject(labelText.Replace(" ", string.Empty), typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;
            var label = NovelUiFactory.CreateText("Label", row.transform, new Vector2(0f, 18f), new Vector2(-430f, -6f), 19f, FontStyles.Bold);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var valueLabel = NovelUiFactory.CreateText("Value", row.transform, new Vector2(440f, 18f), new Vector2(-4f, -6f), 17f, FontStyles.Normal);
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(row.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.offsetMin = new Vector2(0f, 4f);
            trackRect.offsetMax = new Vector2(0f, 16f);
            track.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.16f, 1f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            Stretch((RectTransform)fill.transform);
            fill.GetComponent<Image>().color = new Color(0.58f, 0.50f, 0.32f, 1f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(track.transform, false);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(22f, 28f);
            handle.GetComponent<Image>().color = Color.white;
            var slider = track.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.SetValueWithoutNotify(value);
            valueLabel.text = value.ToString("0.0");
            slider.onValueChanged.AddListener(next => { valueLabel.text = next.ToString("0.0"); apply(next); });
            _controls.Add(slider);
        }

        private void CreateToggleRow(Transform parent)
        {
            var toggleObject = new GameObject("SkipReadOnlyToggle", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            toggleObject.transform.SetParent(parent, false);
            toggleObject.GetComponent<LayoutElement>().preferredHeight = 52f;
            toggleObject.GetComponent<Image>().color = new Color(0.08f, 0.095f, 0.105f, 0.98f);
            var toggle = toggleObject.GetComponent<Toggle>();
            var mark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(toggleObject.transform, false);
            var markRect = (RectTransform)mark.transform;
            markRect.anchorMin = new Vector2(0f, 0.5f);
            markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = new Vector2(25f, 0f);
            markRect.sizeDelta = new Vector2(26f, 26f);
            mark.GetComponent<Image>().color = new Color(0.70f, 0.60f, 0.38f, 1f);
            toggle.graphic = mark.GetComponent<Image>();
            toggle.targetGraphic = toggleObject.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(_settings.SkipReadOnly);
            toggle.onValueChanged.AddListener(SetSkipReadOnly);
            var label = NovelUiFactory.CreateText("Label", toggleObject.transform, new Vector2(52f, 8f), new Vector2(-12f, -8f), 19f, FontStyles.Bold);
            label.text = "Skip only previously read text";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            _controls.Add(toggle);
        }

        private void SyncControls()
        {
            _syncing = true;
            // Recreating is unnecessary because each UI event updates settings immediately;
            // reopening in the same runtime therefore already reflects current values.
            _syncing = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    public sealed class ConfigScreenInputDriver : MonoBehaviour
    {
        private ConfigScreenController _controller;
        public void Configure(ConfigScreenController controller) => _controller = controller;
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _controller?.Close();
        }
    }
}
