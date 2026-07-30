using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomFavoriteVoicePlayModeTests
    {
        private const string FavoriteVoicePlayerPrefsKey = "WhiteRoom.FavoriteVoices.Json";

        private sealed class RecordingAudioPlayer : IDialogueAudioPlayer
        {
            public readonly List<string> Calls = new List<string>();
            public void PlayBgm(string bgmKey, bool stop, string transition, float duration) { }
            public void PlaySe(string seKey) { }
            public void PlayVoice(string voiceKey) => Calls.Add("play:" + voiceKey);
            public void StopVoice() => Calls.Add("stop");
            public void StopAll() => Calls.Add("stop-all");
        }

        [UnityTest]
        public IEnumerator FavoriteListSupportsPlayStopRemoveBackAndEmptyState()
        {
            PlayerPrefs.DeleteKey(FavoriteVoicePlayerPrefsKey);
            var rows = CsvLoader.Parse<DialogueData>(new TextAsset(
                "Id,Speaker,Text,NextId,Voice\n" +
                "1,Alice,Localized favorite line,-1,voice_001\n"));
            var audio = new RecordingAudioPlayer();
            var serviceType = RequireType("WhiteRoom.Novel.FavoriteVoiceService, Assembly-CSharp");
            var service = Activator.CreateInstance(
                serviceType,
                new Func<DialogueData>(() => rows[1]),
                new Func<int, DialogueData>(id => rows.ContainsKey(id) ? rows[id] : null),
                new Func<string, bool>(key => key == "voice_001"),
                audio,
                null,
                null);
            var screenType = RequireType("WhiteRoom.Novel.FavoriteVoiceScreenController, Assembly-CSharp");
            var screen = Activator.CreateInstance(screenType, service, null);

            try
            {
                var added = Invoke(service, "AddCurrent");
                Assert.That(Get(added, "Succeeded"), Is.EqualTo(true));

                Invoke(screen, "Open");
                yield return null;
                Assert.That(Get(screen, "IsOpen"), Is.EqualTo(true));
                Assert.That(Get(screen, "VisibleItemCount"), Is.EqualTo(1));
                Assert.That(Get(screen, "IsEmptyStateVisible"), Is.EqualTo(false));

                var play = GameObject.Find("PlayButton").GetComponent<Button>();
                var stop = GameObject.Find("StopButton").GetComponent<Button>();
                var remove = GameObject.Find("RemoveButton").GetComponent<Button>();
                var back = GameObject.Find("BackButton").GetComponent<Button>();
                Assert.That(play.IsInteractable(), Is.True);
                Assert.That(stop, Is.Not.Null);
                Assert.That(remove, Is.Not.Null);
                Assert.That(back, Is.Not.Null);

                play.onClick.Invoke();
                stop.onClick.Invoke();
                CollectionAssert.AreEqual(
                    new[] { "stop", "stop", "play:voice_001", "stop" },
                    audio.Calls,
                    "Open stops background voice, Play restarts one voice, and Stop ends it.");

                remove.onClick.Invoke();
                yield return null;
                Assert.That(Get(screen, "VisibleItemCount"), Is.EqualTo(0));
                Assert.That(Get(screen, "IsEmptyStateVisible"), Is.EqualTo(true));

                back.onClick.Invoke();
                yield return null;
                Assert.That(Get(screen, "IsOpen"), Is.EqualTo(false));
                Assert.That(audio.Calls[audio.Calls.Count - 1], Is.EqualTo("stop"));
            }
            finally
            {
                var root = GameObject.Find("WhiteRoomFavoriteVoiceScreen");
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                var canvas = GameObject.Find("NovelDialogueCanvas");
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas);
                PlayerPrefs.DeleteKey(FavoriteVoicePlayerPrefsKey);
                PlayerPrefs.Save();
            }
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object Get(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, null);
        }
    }
}
