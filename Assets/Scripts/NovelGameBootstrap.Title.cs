using System;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    public sealed partial class NovelGameBootstrap
    {
        private void EnsureUiFontAsset()
        {
            if (uiFontAsset != null)
            {
                _runtimeUiFontAsset = uiFontAsset;
                return;
            }

            if (_runtimeUiFontAsset != null || string.IsNullOrEmpty(uiFontResourcePath))
                return;

            var font = Resources.Load<Font>(uiFontResourcePath);
            if (font == null)
            {
                Debug.LogWarning($"NovelGameBootstrap: UI font was not found at Resources/{uiFontResourcePath}.");
                return;
            }

            _runtimeUiFontAsset = TMP_FontAsset.CreateFontAsset(font);
            _runtimeUiFontAsset.name = font.name + " TMP Runtime";
        }

        private void ShowTitleMenu()
        {
            EnsureEventSystem();

            if (_titleMenuRoot == null)
                _titleMenuRoot = CreateTitleMenu();

            RefreshTitleMenuButtons();
            _titleMenuRoot.SetActive(true);
            RefreshSaveLoadLauncherVisibility();
        }

        private void HideTitleMenu()
        {
            if (_titleMenuRoot != null)
                _titleMenuRoot.SetActive(false);

            RefreshSaveLoadLauncherVisibility();
        }

        private void RefreshTitleMenuButtons()
        {
            if (_titleMenuRoot == null)
                return;

            if (_continueButton != null)
                _continueButton.interactable = HasContinueSave();

            if (_quickLoadButton != null)
                _quickLoadButton.interactable = HasSave(DialogueSaveSystem.QuickSaveSlot);
        }

        private GameObject CreateTitleMenu()
        {
            var canvas = EnsureDialogueCanvas();
            var root = new GameObject("WhiteRoomTitleMenu", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.018f, 0.02f, 0.022f, 0.96f);

            var menuObject = new GameObject("Menu", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuObject.transform.SetParent(root.transform, false);

            var menuRect = (RectTransform)menuObject.transform;
            menuRect.anchorMin = new Vector2(0.08f, 0.18f);
            menuRect.anchorMax = new Vector2(0.42f, 0.78f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            var layout = menuObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTitleLabel(menuObject.transform, "WhiteRoom", 64f, 96f, FontStyles.Bold);
            CreateTitleSpacer(menuObject.transform, 18f);
            CreateTitleButton(menuObject.transform, "New Game", StartNewGame);
            _continueButton = CreateTitleButton(menuObject.transform, "Continue", () => ContinueLatest());
            CreateTitleButton(menuObject.transform, "Load Game", OpenLoadScreen);
            _quickLoadButton = CreateTitleButton(menuObject.transform, "Quick Load", () => QuickLoad());

            root.SetActive(false);
            return root;

        private static TextMeshProUGUI CreateTitleLabel(Transform parent, string textValue, float fontSize, float height, FontStyles style)
        {
            var textObject = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var layout = textObject.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return text;
        }

        private static void CreateTitleSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);

            var layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static Button CreateTitleButton(Transform parent, string labelText, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(labelText.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.24f, 0.96f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(action);

            var colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.24f, 0.96f);
            colors.highlightedColor = new Color(0.24f, 0.30f, 0.32f, 1f);
            colors.pressedColor = new Color(0.12f, 0.16f, 0.18f, 1f);
            colors.disabledColor = new Color(0.09f, 0.10f, 0.11f, 0.55f);
            button.colors = colors;

            var label = CreateText("Label", buttonObject.transform, new Vector2(18f, 9f), new Vector2(-18f, -9f), 24f, FontStyles.Bold);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return button;
        }
    }
}

