using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor helper to create a small Audio_QA scene with an AudioManager and FootstepTester.
/// The created scene will be saved at `Assets/Scenes/Audio_QA.unity`.
/// </summary>
public static class CreateAudioQAScene
{
    [MenuItem("Tools/Audio/Create Audio_QA Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Audio_QA";

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 1.6f, -3f);

        // AudioManager
        var audioMgrGO = new GameObject("AudioManager");
        audioMgrGO.AddComponent<Audio.Systems.AudioManager>();

        // FootstepTester
        var testerGO = new GameObject("FootstepTester");
        var tester = testerGO.AddComponent<Audio.Systems.FootstepTester>();
        testerGO.transform.position = Vector3.zero;

        // Attempt to auto-assign a SurfaceMaterialRegistry if one exists in the project
#if UNITY_EDITOR
        var guids = AssetDatabase.FindAssets("t:SurfaceMaterialRegistry");
        if (guids != null && guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var registry = AssetDatabase.LoadAssetAtPath(path, typeof(Audio.Systems.SurfaceMaterialRegistry)) as Audio.Systems.SurfaceMaterialRegistry;
            if (registry != null)
            {
                var mgr = audioMgrGO.GetComponent<Audio.Systems.AudioManager>();
                mgr.Registry = registry;
                Debug.Log($"Assigned SurfaceMaterialRegistry from {path} to AudioManager.");
            }
        }
#endif

        // Save scene
        var scenePath = "Assets/Scenes/Audio_QA.unity";
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"Audio_QA scene created at {scenePath}. Open it from the Project window to run QA.");
    }
}
