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
        private void EnsureDialoguePresentation(DialogueSaveSystem saveSystem)
        {
            _stageView = EnsureDialogueStageView();
            _stageBinder = EnsureDialogueStageBinder(_stageView);
            _audioPlayer = EnsureDialogueAudioPlayer();
            _audioBinder = EnsureDialogueAudioBinder(_audioPlayer);

            ConnectPresentationIssues();

            if (saveSystem == null)
                return;

            if (_stageBinder != null)
                saveSystem.RegisterContributor(_stageBinder);

            if (_audioBinder != null)
                saveSystem.RegisterContributor(_audioBinder);
        }

        private DialogueStageView EnsureDialogueStageView()
        {
            var existing = FindFirstObjectByType<DialogueStageView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigureStageDatabases(existing);
                return existing;
            }

            var canvas = EnsureDialogueCanvas();
            var stageObject = new GameObject("DialogueStage", typeof(RectTransform), typeof(DialogueStageView));
            stageObject.transform.SetParent(canvas.transform, false);
            stageObject.transform.SetAsFirstSibling();

            var rect = (RectTransform)stageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = CreateStageImage("Background", stageObject.transform, Vector2.zero, Vector2.one, false);
            var left = CreateStageImage("LeftCharacter", stageObject.transform, new Vector2(0.04f, 0.08f), new Vector2(0.42f, 0.94f), true);
            var center = CreateStageImage("CenterCharacter", stageObject.transform, new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.94f), true);
            var right = CreateStageImage("RightCharacter", stageObject.transform, new Vector2(0.58f, 0.08f), new Vector2(0.96f, 0.94f), true);

            var view = stageObject.GetComponent<DialogueStageView>();
            SetPrivateField(view, "backgroundDatabase", backgroundDatabase);
            SetPrivateField(view, "characterDatabase", characterDatabase);
            SetPrivateField(view, "backgroundImage", background);
            SetPrivateField(view, "slots", new List<DialogueStageSlotBinding>
            {
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Left, image = left },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Center, image = center },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Right, image = right }
            });

            return view;
        }

        private DialogueStageBinder EnsureDialogueStageBinder(DialogueStageView stageView)
        {
            if (stageView == null)
                return null;

            var binder = stageView.GetComponent<DialogueStageBinder>();
            if (binder == null)
                binder = stageView.gameObject.AddComponent<DialogueStageBinder>();

            return binder;
        }

        private DialogueAudioPlayer EnsureDialogueAudioPlayer()
        {
            var existing = FindFirstObjectByType<DialogueAudioPlayer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigureAudioDatabase(existing);
                return existing;
            }

            var audioObject = new GameObject("DialogueAudio", typeof(DialogueAudioPlayer));
            DontDestroyOnLoad(audioObject);

            var player = audioObject.GetComponent<DialogueAudioPlayer>();
            SetPrivateField(player, "audioDatabase", audioDatabase);
            SetPrivateField(player, "bgmSource", CreateDialogueAudioSource("BgmSource", audioObject.transform));
            SetPrivateField(player, "seSource", CreateDialogueAudioSource("SeSource", audioObject.transform));
            SetPrivateField(player, "voiceSource", CreateDialogueAudioSource("VoiceSource", audioObject.transform));

            return player;
        }

        private DialogueAudioBinder EnsureDialogueAudioBinder(DialogueAudioPlayer audioPlayer)
        {
            if (audioPlayer == null)
                return null;

            var binder = audioPlayer.GetComponent<DialogueAudioBinder>();
            if (binder == null)
                binder = audioPlayer.gameObject.AddComponent<DialogueAudioBinder>();

            return binder;
        }

        private void ConfigureStageDatabases(DialogueStageView stageView)
        {
            if (stageView == null)
                return;

            if (backgroundDatabase != null)
                SetPrivateField(stageView, "backgroundDatabase", backgroundDatabase);

            if (characterDatabase != null)
                SetPrivateField(stageView, "characterDatabase", characterDatabase);
        }

        private void ConfigureAudioDatabase(DialogueAudioPlayer audioPlayer)
        {
            if (audioPlayer != null && audioDatabase != null)
                SetPrivateField(audioPlayer, "audioDatabase", audioDatabase);
        }

        private void ConnectPresentationIssues()
        {
            DisconnectPresentationIssues();

            var stageIssues = _stageView as IDialoguePresentationIssueSource;
            if (stageIssues != null)
                stageIssues.PresentationIssueRaised += HandlePresentationIssue;

            var audioIssues = _audioPlayer as IDialoguePresentationIssueSource;
            if (audioIssues != null)
                audioIssues.PresentationIssueRaised += HandlePresentationIssue;
        }

        private void DisconnectPresentationIssues()
        {
            var stageIssues = _stageView as IDialoguePresentationIssueSource;
            if (stageIssues != null)
                stageIssues.PresentationIssueRaised -= HandlePresentationIssue;

            var audioIssues = _audioPlayer as IDialoguePresentationIssueSource;
            if (audioIssues != null)
                audioIssues.PresentationIssueRaised -= HandlePresentationIssue;
        }

        private void HandlePresentationIssue(DialoguePresentationIssueContext context)
        {
            if (context == null)
                return;

            Debug.LogWarning($"NovelGameBootstrap: presentation issue {context.Kind} '{context.Key}': {context.Message}");
        }

        private static Image CreateStageImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;

            return image;
        }

        private static AudioSource CreateDialogueAudioSource(string name, Transform parent)
        {
            var sourceObject = new GameObject(name, typeof(AudioSource));
            sourceObject.transform.SetParent(parent, false);

            var source = sourceObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            return source;
        }
    }
}

