using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DIG.Voxel.Core;

namespace DIG.Voxel.Editor
{
    public static class VoxelTestSceneSetup
    {
        [MenuItem("DIG/Voxel/Create Test Scene")]
        public static void CreateTestScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Add directional light (if DefaultGameObjects didn't add one, or to ensure specific setup)
            // DefaultGameObjects adds camera and light usually. But let's verify/update.
            var lights = GameObject.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (lights.Length == 0)
            {
                 var light = new GameObject("Directional Light");
                 var lightComp = light.AddComponent<Light>();
                 lightComp.type = LightType.Directional;
                 light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            
            // Position camera above ground
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(16, 20, 16);
                Camera.main.transform.LookAt(new Vector3(16, 0, 16));
            }
            
            // Add Voxel World Authoring
            var worldObj = new GameObject("Voxel World");
            // Assuming we have VoxelWorldAuthoring or similar. But wait, we used SubScene logic in previous chats?
            // "VoxelWorldAuthoring" was mentioned in EPIC8.md.
            // Let's create a SubScene placeholder since Authoring is the right way.
            // Wait, we don't have VoxelWorldAuthoring component yet? We bootstrapped via Systems.
            // In ChunkSpawnerSystem we didn't use Authoring component.
            // So we just need a SubScene?
            // Actually, systems run automatically. We just need an Entity presence effectively?
            // No, `ChunkStreamingSystem` runs on `OnUpdate` regardless of components?
            // `ChunkStreamingSystem` is a `SystemBase` with `RequireForUpdate`?
            // Checking ChunkStreamingSystem code.. it doesn't have [RequireMatchingQueriesForUpdate].
            // It runs always.
            
            // However, it spawns chunks.
            // So we don't strictly need a GameObject unless we want to hold Config.
            
            // Create Instruction Canvas
            CreateInstructionCanvas();
            
            UnityEngine.Debug.Log("[Voxel Editor] Test scene created! Press Play to generate terrain.");
        }
        
        private static void CreateInstructionCanvas()
        {
            var canvas = new GameObject("InstructionCanvas");
            var canvasComp = canvas.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var text = new GameObject("Instructions");
            text.transform.SetParent(canvas.transform);
            
            var textComp = text.AddComponent<UnityEngine.UI.Text>();
            textComp.text = "Voxel Test Scene\n" +
                "- Terrain generates on Play\n" +
                "- Left Click: Mine\n" +
                "- Right Click: Debug raycast\n" +
                "- Window > DIG > Voxel > Debug Window for stats";
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.color = Color.white;
            textComp.fontSize = 14;
            
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(400, 100);
        }
        
        [MenuItem("DIG/Voxel/Validate Project Setup")]
        public static void ValidateSetup()
        {
            bool valid = true;
            
            // Check layers
            if (LayerMask.NameToLayer("Voxel") == -1)
            {
                UnityEngine.Debug.LogError("[Setup] ❌ 'Voxel' layer not found! Add in Project Settings > Tags and Layers");
                valid = false;
            }
            else
            {
                UnityEngine.Debug.Log("[Setup] ✅ 'Voxel' layer exists");
            }
            
            // Check material registry
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry == null)
            {
                UnityEngine.Debug.LogWarning("[Setup] ⚠️ VoxelMaterialRegistry not found in Resources folder (Assets/Resources/VoxelMaterialRegistry.asset)");
            }
            else
            {
                UnityEngine.Debug.Log("[Setup] ✅ VoxelMaterialRegistry found");
            }
            
            if (valid)
            {
                UnityEngine.Debug.Log("[Setup] ✅ Project setup valid for voxel system!");
            }
        }
    }
}
