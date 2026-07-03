using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.Editor
{
    public static class NovelUiPrefabGenerator
    {
        private const string MenuPath = "Tools/WhiteRoom/Create Novel UI Prefabs";
        private const string PrefabDirectory = "Assets/Prefabs/NovelUI";
        private const string DialogueViewPath = PrefabDirectory + "/DialogueView.prefab";
        private const string BacklogViewPath = PrefabDirectory + "/DialogueBacklogView.prefab";
        private const string DefaultFontResourcePath = "Fonts/LogoTypeGothicCondense/LogoTypeGothicCondense";

        [MenuItem(MenuPath)]
        public static void CreateNovelUiPrefabs()
        {
            EnsurePrefabDirectory();
            NovelUiFactory.EnsureFont(null, DefaultFontResourcePath);

            var generated = new List<UnityEngine.Object>();
            var skipped = new List<string>();
            var root = new GameObject("NovelUiPrefabGenerationRoot", typeof(RectTransform));
            root.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var dialogueView = DialogueViewFactory.CreateDefaultDialogueView(root.transform);
                SavePrefabIfMissing(dialogueView.gameObject, DialogueViewPath, generated, skipped);

                var backlogView = DialogueViewFactory.CreateDefaultBacklogView(root.transform);
                SavePrefabIfMissing(backlogView.gameObject, BacklogViewPath, generated, skipped);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (generated.Count > 0)
                Selection.objects = generated.ToArray();

            var message = $"Generated {generated.Count} prefab(s) in {PrefabDirectory}.";
            if (skipped.Count > 0)
                message += "\nSkipped existing prefab(s): " + string.Join(", ", skipped);

            Debug.Log("Novel UI Prefab Generator: " + message);
            EditorUtility.DisplayDialog("Novel UI Prefabs", message, "OK");
        }

        private static void SavePrefabIfMissing(GameObject source, string path, List<UnityEngine.Object> generated, List<string> skipped)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                skipped.Add(path);
                return;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path, out var success);
            if (!success || prefab == null)
            {
                Debug.LogError($"Novel UI Prefab Generator: failed to create prefab at {path}.");
                return;
            }

            generated.Add(prefab);
        }

        private static void EnsurePrefabDirectory()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "NovelUI");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
