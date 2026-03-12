using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Audio.Systems;

namespace Audio.Editor
{
    /// <summary>
    /// Editor utility for creating and setting up the Audio_QA test scene.
    /// </summary>
    public static class AudioQASceneSetup
    {
        [MenuItem("Window/DIG/Audio/Create Audio_QA Scene")]
        public static void CreateAudioQAScene()
        {
            // Confirm if user wants to create a new scene
            if (!EditorUtility.DisplayDialog("Create Audio_QA Scene",
                "This will create a new scene with audio/VFX test setup.\n\n" +
                "Make sure to save your current scene first.",
                "Create", "Cancel"))
            {
                return;
            }

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create ground with different surface materials
            CreateTestGround();

            // Create AudioManager
            var audioManagerGO = new GameObject("AudioManager");
            audioManagerGO.AddComponent<AudioManager>();
            
            // Create VFXManager
            var vfxManagerGO = new GameObject("VFXManager");
            vfxManagerGO.AddComponent<VFXManager>();

            // Create QA Controller
            var qaControllerGO = new GameObject("AudioQAController");
            var qaController = qaControllerGO.AddComponent<Audio.QA.AudioQAController>();
            qaController.AudioManager = audioManagerGO.GetComponent<AudioManager>();
            qaController.VFXManager = vfxManagerGO.GetComponent<VFXManager>();

            // Load registry if it exists
            var registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
            if (registry != null)
            {
                qaController.Registry = registry;
                // Set up material IDs from registry
                if (registry.Materials != null && registry.Materials.Count > 0)
                {
                    var ids = new int[Mathf.Min(registry.Materials.Count, 5)];
                    for (int i = 0; i < ids.Length; i++)
                    {
                        ids[i] = registry.Materials[i].Id;
                    }
                    qaController.TestMaterialIds = ids;
                }
            }

            // Create test position marker
            var testPosGO = new GameObject("TestPosition");
            testPosGO.transform.position = new Vector3(0, 1, 0);
            qaController.TestPosition = testPosGO.transform;

            // Select the QA controller
            Selection.activeGameObject = qaControllerGO;

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[AudioQASceneSetup] Audio_QA scene created. Save it to Assets/Scenes/Audio_QA.unity");
        }

        private static void CreateTestGround()
        {
            // Create parent for ground patches
            var groundParent = new GameObject("GroundPatches");

            // Create multiple ground patches for different surfaces
            string[] surfaceNames = { "Concrete", "Metal", "Wood", "Grass", "Default" };
            float spacing = 5f;
            float startX = -spacing * (surfaceNames.Length - 1) / 2f;

            for (int i = 0; i < surfaceNames.Length; i++)
            {
                var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                patch.name = $"Ground_{surfaceNames[i]}";
                patch.transform.SetParent(groundParent.transform);
                patch.transform.position = new Vector3(startX + i * spacing, -0.5f, 0);
                patch.transform.localScale = new Vector3(4f, 1f, 4f);

                // Add SurfaceMaterialAuthoring if we have sample materials
                var sampleMat = Resources.Load<SurfaceMaterial>($"SurfaceMaterials/{surfaceNames[i]}");
                if (sampleMat != null)
                {
                    var authoring = patch.AddComponent<SurfaceMaterialAuthoring>();
                    // Note: authoring component will need to be configured manually or via baking
                }

                // Create label
                var label = new GameObject($"Label_{surfaceNames[i]}");
                label.transform.SetParent(patch.transform);
                label.transform.localPosition = new Vector3(0, 1.5f, 0);
                // Labels would need TextMeshPro - skip for basic setup
            }
        }

        [MenuItem("Window/DIG/Audio/Validate Audio Setup")]
        public static void ValidateAudioSetup()
        {
            int issues = 0;

            // Check for registry
            var registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
            if (registry == null)
            {
                Debug.LogWarning("[AudioValidation] No SurfaceMaterialRegistry found in Resources folder!");
                issues++;
            }
            else
            {
                Debug.Log($"[AudioValidation] ✓ Registry found with {registry.Materials?.Count ?? 0} materials");
                
                if (registry.DefaultMaterial == null)
                {
                    Debug.LogWarning("[AudioValidation] Registry has no DefaultMaterial assigned!");
                    issues++;
                }

                // Check for materials without clips
                if (registry.Materials != null)
                {
                    foreach (var mat in registry.Materials)
                    {
                        if (mat == null) continue;
                        
                        bool hasAnyClips = 
                            (mat.WalkClips?.Count > 0) || 
                            (mat.RunClips?.Count > 0) || 
                            (mat.CrouchClips?.Count > 0) ||
                            (mat.LandingClips?.Count > 0);
                        
                        if (!hasAnyClips)
                        {
                            Debug.LogWarning($"[AudioValidation] Material '{mat.DisplayName}' (ID={mat.Id}) has no audio clips!");
                            issues++;
                        }
                    }
                }
            }

            // Check for mapping
            var mapping = Resources.Load<SurfaceMaterialMapping>("SurfaceMaterialMapping");
            if (mapping == null)
            {
                Debug.LogWarning("[AudioValidation] No SurfaceMaterialMapping found in Resources folder!");
                issues++;
            }
            else
            {
                Debug.Log($"[AudioValidation] ✓ Mapping found with {mapping.mappings?.Count ?? 0} entries");
            }

            // Summary
            if (issues == 0)
            {
                Debug.Log("[AudioValidation] ✓ All audio setup checks passed!");
            }
            else
            {
                Debug.LogWarning($"[AudioValidation] Found {issues} issue(s). See warnings above.");
            }
        }
    }
}
