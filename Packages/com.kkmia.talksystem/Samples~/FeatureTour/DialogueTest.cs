using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace kkmia.TalkSystem
{
    public class DialogueTest : MonoBehaviour
    {
        [Header("Startup")]
        public int startDialogueId = 1;

        [Header("Demo Triggers")]
        [SerializeField] private string gameOverTriggerKey = "GameOver";
        [SerializeField] private string endTriggerKey = "End";
        [SerializeField] private bool showDemoOverlay = true;

        private GameObject _overlay;

        private void Start()
        {
            if (DialogueManager.Instance == null)
            {
                Debug.LogError("[TalkSystem Demo] DialogueManager.Instance was not found.");
                return;
            }

            if (showDemoOverlay)
                CreateDemoOverlay();

            StartDialogue(startDialogueId);
        }

        private void Update()
        {
            if (DialogueManager.Instance == null)
                return;

            if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.Space) || DialogueKeyboard.GetKeyDown(DialogueKeyCode.Enter))
                DialogueManager.Instance.RequestNext();
            if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.Alpha1))
                StartDialogue(startDialogueId);
            if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.Alpha2))
                StartTrigger(gameOverTriggerKey);
            if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.Alpha3))
                StartTrigger(endTriggerKey);
            if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.R))
                DialogueManager.Instance.Rollback();
        }

        public void StartDialogue(int id)
        {
            if (DialogueManager.Instance == null)
                return;

            DialogueManager.Instance.StartDialogue(id);
            Debug.Log("[TalkSystem Demo] Started dialogue ID " + id + ".");
        }

        public void StartTrigger(string triggerKey)
        {
            if (DialogueManager.Instance == null)
                return;

            DialogueManager.Instance.StartDialogueForState(triggerKey);
            Debug.Log("[TalkSystem Demo] Started trigger \"" + triggerKey + "\".");
        }

        private void CreateDemoOverlay()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || _overlay != null)
                return;

            _overlay = new GameObject("TalkSystemDemoOverlay", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _overlay.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)_overlay.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(430f, 170f);

            var image = _overlay.GetComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.82f);

            var layout = _overlay.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddText(_overlay.transform,
                "Talk System Demo\nSpace/Enter: next  |  R: rollback\nUse the buttons or number keys to restart demo routes.",
                17f,
                76f);

            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_overlay.transform, false);

            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = false;

            AddButton(row.transform, "Intro [1]", () => StartDialogue(startDialogueId));
            AddButton(row.transform, "GameOver [2]", () => StartTrigger(gameOverTriggerKey));
            AddButton(row.transform, "End [3]", () => StartTrigger(endTriggerKey));
        }

        private static void AddText(Transform parent, string value, float fontSize, float height)
        {
            var textObject = new GameObject("HelpText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var layout = textObject.GetComponent<LayoutElement>();
            layout.preferredHeight = height;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(action);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);

            var text = labelObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 14f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
