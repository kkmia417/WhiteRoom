using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Locates or creates the dialogue window and backlog views: reuses an instance already
    /// in the scene, instantiates the configured prefab, or builds a plain fallback UI.
    /// </summary>
    public static class DialogueViewFactory
    {
        public static DialogueView EnsureDialogueView(DialogueView prefab)
        {
            var existingView = Object.FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            if (existingView != null)
            {
                EnsureViewBinder(existingView);
                return existingView;
            }

            var canvas = NovelUiFactory.EnsureCanvas();
            if (prefab != null)
            {
                var prefabView = Object.Instantiate(prefab, canvas.transform);
                prefabView.gameObject.SetActive(false);
                EnsureViewBinder(prefabView);
                return prefabView;
            }

            return CreateDefaultDialogueView(canvas.transform, true);
        }

        public static DialogueBacklogView EnsureBacklogView(DialogueBacklogView prefab)
        {
            var existingBacklog = Object.FindFirstObjectByType<DialogueBacklogView>(FindObjectsInactive.Include);
            if (existingBacklog != null)
                return existingBacklog;

            var canvas = NovelUiFactory.EnsureCanvas();
            if (prefab != null)
            {
                var prefabBacklog = Object.Instantiate(prefab, canvas.transform);
                prefabBacklog.gameObject.SetActive(true);
                prefabBacklog.Close();
                return prefabBacklog;
            }

            return CreateDefaultBacklogView(canvas.transform);
        }

        public static DialogueView CreateDefaultDialogueView(Transform parent, bool startInactive = false)
        {
            var root = CreateDialogueRoot(parent);
            root.SetActive(!startInactive);

            var speaker = NovelUiFactory.CreateText("SpeakerText", root.transform, new Vector2(28f, -18f), new Vector2(-28f, -56f), 24f, FontStyles.Bold);
            var body = NovelUiFactory.CreateText("BodyText", root.transform, new Vector2(28f, -70f), new Vector2(-148f, 26f), 26f, FontStyles.Normal);
            var nextButton = CreateNextButton(root.transform);
            var choicesContainer = CreateChoicesContainer(root.transform);
            var choiceButtonPrefab = CreateChoiceButtonPrefab(choicesContainer);
            var typewriter = body.gameObject.AddComponent<TypewriterEffect>();
            var view = root.AddComponent<DialogueView>();

            RuntimeFieldBinder.SetPrivateField(view, "speakerText", (TMP_Text)speaker);
            RuntimeFieldBinder.SetPrivateField(view, "bodyText", (TMP_Text)body);
            RuntimeFieldBinder.SetPrivateField(view, "nextButton", nextButton);
            RuntimeFieldBinder.SetPrivateField(view, "dialogWindow", root.GetComponent<Image>());
            RuntimeFieldBinder.SetPrivateField(view, "typewriter", typewriter);
            RuntimeFieldBinder.SetPrivateField(view, "choicesContainer", choicesContainer);
            RuntimeFieldBinder.SetPrivateField(view, "choiceButtonPrefab", choiceButtonPrefab);
            EnsureViewBinder(view);

            return view;
        }

        public static DialogueBacklogView CreateDefaultBacklogView(Transform parent)
        {
            var backlogObject = new GameObject("DialogueBacklog", typeof(RectTransform), typeof(DialogueBacklogView));
            backlogObject.transform.SetParent(parent, false);

            var rootRect = (RectTransform)backlogObject.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panel = CreateBacklogPanel(backlogObject.transform);
            var content = NovelUiFactory.CreateVerticalScrollList(panel.transform, new Vector2(24f, 24f), new Vector2(-24f, -72f), 8f);
            var rowPrefab = CreateBacklogRowPrefab(backlogObject.transform);
            var backlogView = backlogObject.GetComponent<DialogueBacklogView>();

            RuntimeFieldBinder.SetPrivateField(backlogView, "panel", panel);
            RuntimeFieldBinder.SetPrivateField(backlogView, "contentContainer", content);
            RuntimeFieldBinder.SetPrivateField(backlogView, "rowPrefab", rowPrefab);

            panel.SetActive(false);
            return backlogView;
        }

        private static GameObject CreateDialogueRoot(Transform parent)
        {
            var root = new GameObject("DialogueWindow", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.04f, 0.04f);
            rect.anchorMax = new Vector2(0.96f, 0.34f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

            return root;
        }

        private static Button CreateNextButton(Transform parent)
        {
            var buttonObject = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 22f);
            rect.sizeDelta = new Vector2(108f, 44f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.24f, 0.30f, 0.95f);

            var label = NovelUiFactory.CreateText("Label", buttonObject.transform, new Vector2(12f, 6f), new Vector2(-12f, -6f), 20f, FontStyles.Bold);
            label.text = "Next";
            label.alignment = TextAlignmentOptions.Center;

            return buttonObject.GetComponent<Button>();
        }

        private static Transform CreateChoicesContainer(Transform parent)
        {
            var containerObject = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            containerObject.transform.SetParent(parent, false);

            var rect = (RectTransform)containerObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 190f);
            rect.sizeDelta = new Vector2(520f, 150f);

            var layout = containerObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return containerObject.transform;
        }

        private static Button CreateChoiceButtonPrefab(Transform parent)
        {
            var button = NovelUiFactory.CreateButton(
                "ChoiceButtonPrefab",
                parent,
                string.Empty,
                18f,
                new UiButtonStyle(
                    new Color(0.12f, 0.14f, 0.18f, 0.92f),
                    new Color(0.18f, 0.21f, 0.27f, 1f),
                    new Color(0.09f, 0.11f, 0.15f, 1f),
                    new Color(0.07f, 0.08f, 0.10f, 0.55f)),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(14f, 4f));

            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(480f, 42f);

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 42f;
            layout.minHeight = 42f;

            button.gameObject.SetActive(false);
            return button;
        }

        private static GameObject CreateBacklogPanel(Transform parent)
        {
            var panel = new GameObject("BacklogPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.12f, 0.12f);
            rect.anchorMax = new Vector2(0.88f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panel.GetComponent<Image>();
            image.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);

            var title = NovelUiFactory.CreateText("Title", panel.transform, new Vector2(26f, -50f), new Vector2(-26f, 12f), 24f, FontStyles.Bold);
            title.text = "Backlog";

            return panel;
        }

        private static DialogueBacklogRow CreateBacklogRowPrefab(Transform parent)
        {
            var rowObject = new GameObject("BacklogRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(DialogueBacklogRow));
            rowObject.transform.SetParent(parent, false);
            rowObject.SetActive(false);

            var rect = (RectTransform)rowObject.transform;
            rect.sizeDelta = new Vector2(900f, 86f);

            var image = rowObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.095f, 0.11f, 0.88f);

            var layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 86f;
            layout.preferredHeight = 86f;

            var speaker = NovelUiFactory.CreateText("Speaker", rowObject.transform, new Vector2(18f, -30f), new Vector2(-18f, 4f), 18f, FontStyles.Bold);
            var body = NovelUiFactory.CreateText("Body", rowObject.transform, new Vector2(18f, 8f), new Vector2(-18f, -34f), 18f, FontStyles.Normal);
            body.overflowMode = TextOverflowModes.Ellipsis;

            var row = rowObject.GetComponent<DialogueBacklogRow>();
            RuntimeFieldBinder.SetPrivateField(row, "speakerText", (TMP_Text)speaker);
            RuntimeFieldBinder.SetPrivateField(row, "bodyText", (TMP_Text)body);

            return row;
        }

        private static void EnsureViewBinder(DialogueView view)
        {
            if (view != null && view.GetComponent<DialogueViewBinder>() == null)
                view.gameObject.AddComponent<DialogueViewBinder>();
        }
    }
}
