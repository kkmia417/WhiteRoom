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
        private void HandleDialogueEvent(DialogueEventContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.EventKey))
                return;

            var eventKey = context.EventKey.Trim();
            if (eventKey.Length == 0)
                return;

            _reachedEventKeys.Add(eventKey);

            switch (eventKey)
            {
                case "scene_start":
                case "load_main":
                    LoadMainScene();
                    break;
                default:
                    Debug.Log($"NovelGameBootstrap: dialogue event '{context.EventKey}' was raised.");
                    break;
            }
        }

        private void LoadMainScene()
        {
            HideTitleMenu();

            if (string.IsNullOrEmpty(mainSceneName)
                || string.Equals(SceneManager.GetActiveScene().name, mainSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            SceneManager.LoadScene(mainSceneName);
        }
    }
}

