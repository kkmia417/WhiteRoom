using System.Collections.Generic;
using kkmia.TalkSystem;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// The stage (background/characters) and audio components that present a dialogue,
    /// as assembled by <see cref="DialoguePresentationFactory"/>.
    /// </summary>
    public sealed class DialoguePresentation
    {
        public DialoguePresentation(DialogueStageView stageView, DialogueStageBinder stageBinder, DialogueAudioPlayer audioPlayer, DialogueAudioBinder audioBinder)
        {
            StageView = stageView;
            StageBinder = stageBinder;
            AudioPlayer = audioPlayer;
            AudioBinder = audioBinder;
        }

        public DialogueStageView StageView { get; }
        public DialogueStageBinder StageBinder { get; }
        public DialogueAudioPlayer AudioPlayer { get; }
        public DialogueAudioBinder AudioBinder { get; }

        public void RegisterSaveContributors(DialogueSaveSystem saveSystem)
        {
            if (saveSystem == null)
                return;

            if (StageBinder != null)
                saveSystem.RegisterContributor(StageBinder);

            if (AudioBinder != null)
                saveSystem.RegisterContributor(AudioBinder);
        }
    }

    /// <summary>
    /// Locates or creates the stage view and audio player, wires their databases,
    /// and attaches the binders that sync them with dialogue playback.
    /// </summary>
    public static class DialoguePresentationFactory
    {
        public static DialoguePresentation Ensure(BackgroundDatabase backgroundDatabase, CharacterExpressionDatabase characterDatabase, AudioDatabase audioDatabase)
        {
            var stageView = EnsureStageView(backgroundDatabase, characterDatabase);
            var stageBinder = EnsureBinder<DialogueStageBinder>(stageView);
            var audioPlayer = EnsureAudioPlayer(audioDatabase);
            var audioBinder = EnsureBinder<DialogueAudioBinder>(audioPlayer);

            return new DialoguePresentation(stageView, stageBinder, audioPlayer, audioBinder);
        }

        private static DialogueStageView EnsureStageView(BackgroundDatabase backgroundDatabase, CharacterExpressionDatabase characterDatabase)
        {
            var existing = Object.FindFirstObjectByType<DialogueStageView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (backgroundDatabase != null)
                    RuntimeFieldBinder.SetPrivateField(existing, "backgroundDatabase", backgroundDatabase);

                if (characterDatabase != null)
                    RuntimeFieldBinder.SetPrivateField(existing, "characterDatabase", characterDatabase);

                return existing;
            }

            var canvas = NovelUiFactory.EnsureCanvas();
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
            RuntimeFieldBinder.SetPrivateField(view, "backgroundDatabase", backgroundDatabase);
            RuntimeFieldBinder.SetPrivateField(view, "characterDatabase", characterDatabase);
            RuntimeFieldBinder.SetPrivateField(view, "backgroundImage", background);
            RuntimeFieldBinder.SetPrivateField(view, "slots", new List<DialogueStageSlotBinding>
            {
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Left, image = left },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Center, image = center },
                new DialogueStageSlotBinding { slot = DialogueStageSlot.Right, image = right }
            });

            return view;
        }

        private static DialogueAudioPlayer EnsureAudioPlayer(AudioDatabase audioDatabase)
        {
            var existing = Object.FindFirstObjectByType<DialogueAudioPlayer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (audioDatabase != null)
                    RuntimeFieldBinder.SetPrivateField(existing, "audioDatabase", audioDatabase);

                return existing;
            }

            var audioObject = new GameObject("DialogueAudio", typeof(DialogueAudioPlayer));
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(audioObject);

            var player = audioObject.GetComponent<DialogueAudioPlayer>();
            RuntimeFieldBinder.SetPrivateField(player, "audioDatabase", audioDatabase);
            RuntimeFieldBinder.SetPrivateField(player, "bgmSource", CreateAudioSource("BgmSource", audioObject.transform));
            RuntimeFieldBinder.SetPrivateField(player, "seSource", CreateAudioSource("SeSource", audioObject.transform));
            RuntimeFieldBinder.SetPrivateField(player, "voiceSource", CreateAudioSource("VoiceSource", audioObject.transform));

            return player;
        }

        private static TBinder EnsureBinder<TBinder>(Component host) where TBinder : Component
        {
            if (host == null)
                return null;

            var binder = host.GetComponent<TBinder>();
            if (binder == null)
                binder = host.gameObject.AddComponent<TBinder>();

            return binder;
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

        private static AudioSource CreateAudioSource(string name, Transform parent)
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
