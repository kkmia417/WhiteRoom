using System.IO;
using kkmia.TalkSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class FeatureTourSceneBuilder
{
    private const string SceneDirectory = "Assets/TalkSystemFeatureTour";
    private const string ScenePath = SceneDirectory + "/FeatureTour.unity";

    [MenuItem("Tools/kkmia/Samples/Create Feature Tour Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(SceneDirectory);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var dialogueView = CreateDialogueUi();
        var manager = CreateDialogueManager(dialogueView);
        CreateEventSystem();
        CreateSampleController();

        Selection.activeObject = manager.gameObject;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Feature Tour", "Created " + ScenePath, "OK");
    }

    private static DialogueView CreateDialogueUi()
    {
        var prefab = LoadAssetByName<GameObject>("VisualNovelDialogueUI");
        if (prefab == null)
            prefab = LoadAssetByName<GameObject>("DialogueUI");

        if (prefab == null)
            throw new FileNotFoundException("Could not find VisualNovelDialogueUI or DialogueUI prefab.");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Feature Tour Dialogue UI";
        return instance.GetComponentInChildren<DialogueView>(true);
    }

    private static DialogueManager CreateDialogueManager(DialogueView view)
    {
        var managerObject = new GameObject("DialogueManager");
        var manager = managerObject.AddComponent<DialogueManager>();
        var csv = LoadAssetByName<TextAsset>("dialogue_feature_tour");

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("csvFile").objectReferenceValue = csv;
        serialized.FindProperty("view").objectReferenceValue = view;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return manager;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void CreateSampleController()
    {
        var controller = new GameObject("FeatureTourSampleController");
        controller.AddComponent<FeatureTourSampleController>();
        controller.AddComponent<FeatureTourDialogueEventRouter>();
    }

    private static T LoadAssetByName<T>(string assetName) where T : Object
    {
        var guids = AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == assetName)
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return null;
    }
}
