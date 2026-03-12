using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for EPIC 9 advanced voxel features.
    /// Creates visual materials, LOD configs, profiler settings, and more.
    /// </summary>
    public static class AdvancedQuickSetup
    {
        private const string RESOURCES_PATH = "Assets/Resources";
        private const string VISUAL_MATERIALS_PATH = "Assets/Resources/VisualMaterials";
        
        #region Priority 0: Complete Advanced Setup
        
        [MenuItem("DIG/Quick Setup/Advanced/Create Complete Advanced Setup", priority = 0)]
        static void CreateCompleteAdvancedSetup()
        {
            int visualMaterials = 0;
            int lodConfigs = 0;
            int profilerConfigs = 0;
            
            // Step 1: Visual Materials
            var visuals = CreateVisualMaterialsInternal();
            visualMaterials = visuals.Count;
            
            // Step 2: LOD Config
            if (CreateLODConfigInternal() != null)
                lodConfigs = 1;
            
            // Step 3: Profiler Config
            if (CreateProfilerConfigInternal() != null)
                profilerConfigs = 1;
            
            // Step 4: Show summary
            EditorUtility.DisplayDialog("Complete Advanced Setup",
                $"Epic 9 Advanced Setup Complete!\n\n" +
                $"• {visualMaterials} Visual Materials\n" +
                $"• {lodConfigs} LOD Config\n" +
                $"• {profilerConfigs} Profiler Config\n\n" +
                $"Next Steps:\n" +
                $"1. Enter Play Mode\n" +
                $"2. Use LOD Visualizer (DIG > Voxel > LOD Visualizer)\n" +
                $"3. Check performance with Profiler Dashboard",
                "OK");
            
            UnityEngine.Debug.Log("[AdvancedQuickSetup] Complete advanced setup finished successfully");
        }
        
        #endregion
        
        #region Priority 10: Visual Materials
        
        [MenuItem("DIG/Quick Setup/Advanced/Create Visual Materials", priority = 10)]
        static void CreateVisualMaterials()
        {
            var materials = CreateVisualMaterialsInternal();
            
            if (materials.Count > 0)
            {
                Selection.activeObject = materials[0];
                EditorGUIUtility.PingObject(materials[0]);
            }
            
            EditorUtility.DisplayDialog("Visual Materials Setup Complete",
                $"Created:\n" +
                $"• {materials.Count} Visual Materials\n\n" +
                $"Location: {VISUAL_MATERIALS_PATH}\n\n" +
                $"These materials add normal maps, smoothness,\n" +
                $"metallic, and detail textures to voxel rendering.",
                "OK");
        }
        
        private static List<VoxelVisualMaterial> CreateVisualMaterialsInternal()
        {
            EnsureFoldersExist();
            
            var materials = new List<VoxelVisualMaterial>();
            
            // Visual material definitions: (name, id, smoothness, metallic, tint, hasDetail)
            var defs = new (string name, byte id, float smoothness, float metallic, Color tint, bool hasDetail)[]
            {
                ("Stone", 1, 0.2f, 0f, new Color(0.5f, 0.5f, 0.5f), true),
                ("Dirt", 2, 0.1f, 0f, new Color(0.55f, 0.35f, 0.2f), true),
                ("Iron", 3, 0.4f, 0.6f, new Color(0.6f, 0.45f, 0.35f), false),
                ("Gold", 4, 0.8f, 0.9f, new Color(1f, 0.84f, 0f), false),
                ("Copper", 5, 0.5f, 0.7f, new Color(0.8f, 0.5f, 0.2f), false),
                ("Granite", 10, 0.3f, 0.1f, new Color(0.6f, 0.55f, 0.5f), true),
                ("Basalt", 11, 0.15f, 0.05f, new Color(0.25f, 0.25f, 0.3f), true),
                ("Coal", 20, 0.1f, 0f, new Color(0.1f, 0.1f, 0.1f), false),
                ("Silver", 22, 0.85f, 0.95f, new Color(0.85f, 0.85f, 0.9f), false),
                ("Diamond", 23, 0.95f, 0.1f, new Color(0.6f, 0.9f, 1f), false),
                ("Mythril", 24, 0.9f, 0.8f, new Color(0.4f, 0.8f, 0.95f), false),
            };
            
            foreach (var def in defs)
            {
                string path = $"{VISUAL_MATERIALS_PATH}/Visual_{def.name}.asset";
                
                // Check if exists
                var existing = AssetDatabase.LoadAssetAtPath<VoxelVisualMaterial>(path);
                if (existing != null)
                {
                    materials.Add(existing);
                    continue;
                }
                
                // Create visual material
                var mat = ScriptableObject.CreateInstance<VoxelVisualMaterial>();
                mat.MaterialID = def.id;
                mat.DisplayName = def.name;
                mat.Smoothness = def.smoothness;
                mat.Metallic = def.metallic;
                mat.Tint = def.tint;
                mat.AOStrength = 0.5f;
                
                if (def.hasDetail)
                {
                    mat.DetailStrength = 0.3f;
                    mat.DetailScale = 8f;
                }
                
                AssetDatabase.CreateAsset(mat, path);
                materials.Add(mat);
            }
            
            // Link visual materials to gameplay materials if they exist
            LinkVisualToGameplayMaterials(materials);
            
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[AdvancedQuickSetup] Created {materials.Count} visual materials");
            
            return materials;
        }
        
        private static void LinkVisualToGameplayMaterials(List<VoxelVisualMaterial> visualMaterials)
        {
            // Try to find gameplay materials and link them
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry == null || registry.Materials == null) return;
            
            foreach (var gameplay in registry.Materials)
            {
                if (gameplay == null) continue;
                
                foreach (var visual in visualMaterials)
                {
                    if (visual.MaterialID == gameplay.MaterialID)
                    {
                        gameplay.VisualMaterial = visual;
                        EditorUtility.SetDirty(gameplay);
                        break;
                    }
                }
            }
        }
        
        #endregion
        
        #region Priority 20: LOD Config
        
        [MenuItem("DIG/Quick Setup/Advanced/Create LOD Config", priority = 20)]
        static void CreateLODConfig()
        {
            var config = CreateLODConfigInternal();
            
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            
            EditorUtility.DisplayDialog("LOD Config Setup Complete",
                $"Created:\n" +
                $"• VoxelLODConfig with 4 LOD levels\n\n" +
                $"Default Configuration:\n" +
                $"• LOD 0: 0-32m, Full detail, Colliders ON\n" +
                $"• LOD 1: 32-64m, Half detail, Colliders ON\n" +
                $"• LOD 2: 64-128m, Quarter detail, Colliders OFF\n" +
                $"• LOD 3: 128m+, Eighth detail, Colliders OFF\n\n" +
                $"Use LOD Visualizer to see boundaries in scene.",
                "OK");
        }
        
        private static VoxelLODConfig CreateLODConfigInternal()
        {
            EnsureFoldersExist();
            
            string path = $"{RESOURCES_PATH}/VoxelLODConfig.asset";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<VoxelLODConfig>(path);
            if (existing != null)
                return existing;
            
            // Create LOD config with balanced presets
            var config = ScriptableObject.CreateInstance<VoxelLODConfig>();
            
            config.Levels = new VoxelLODConfig.LODLevel[]
            {
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 32f, 
                    VoxelStep = 1, 
                    HasCollider = true, 
                    DebugColor = new Color(0, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 64f, 
                    VoxelStep = 2, 
                    HasCollider = true, 
                    DebugColor = new Color(1, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 128f, 
                    VoxelStep = 4, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0.5f, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 256f, 
                    VoxelStep = 8, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0, 0, 0.3f) 
                },
            };
            
            config.UpdateFrequency = 0.5f;
            config.Hysteresis = 2f;
            config.MaxUpdatesPerFrame = 5;
            config.EnableColliderLOD = true;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log("[AdvancedQuickSetup] Created VoxelLODConfig");
            
            return config;
        }
        
        [MenuItem("DIG/Quick Setup/Advanced/Create LOD Config (Performance)", priority = 21)]
        static void CreateLODConfigPerformance()
        {
            EnsureFoldersExist();
            
            string path = $"{RESOURCES_PATH}/VoxelLODConfig.asset";
            
            // Delete existing if any
            if (AssetDatabase.LoadAssetAtPath<VoxelLODConfig>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            
            // Create aggressive LOD config for performance
            var config = ScriptableObject.CreateInstance<VoxelLODConfig>();
            
            config.Levels = new VoxelLODConfig.LODLevel[]
            {
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 16f, 
                    VoxelStep = 1, 
                    HasCollider = true, 
                    DebugColor = new Color(0, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 32f, 
                    VoxelStep = 2, 
                    HasCollider = true, 
                    DebugColor = new Color(1, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 64f, 
                    VoxelStep = 4, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0.5f, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 128f, 
                    VoxelStep = 8, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0, 0, 0.3f) 
                },
            };
            
            config.UpdateFrequency = 1f;
            config.Hysteresis = 4f;
            config.MaxUpdatesPerFrame = 3;
            config.EnableColliderLOD = true;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = config;
            
            EditorUtility.DisplayDialog("Performance LOD Config Created",
                "Created aggressive LOD config for low-end hardware:\n\n" +
                "• Shorter view distances\n" +
                "• Faster LOD falloff\n" +
                "• Higher hysteresis (less updates)\n" +
                "• Fewer updates per frame",
                "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Advanced/Create LOD Config (Quality)", priority = 22)]
        static void CreateLODConfigQuality()
        {
            EnsureFoldersExist();
            
            string path = $"{RESOURCES_PATH}/VoxelLODConfig.asset";
            
            // Delete existing if any
            if (AssetDatabase.LoadAssetAtPath<VoxelLODConfig>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            
            // Create quality LOD config for high-end PCs
            var config = ScriptableObject.CreateInstance<VoxelLODConfig>();
            
            config.Levels = new VoxelLODConfig.LODLevel[]
            {
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 64f, 
                    VoxelStep = 1, 
                    HasCollider = true, 
                    DebugColor = new Color(0, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 128f, 
                    VoxelStep = 2, 
                    HasCollider = true, 
                    DebugColor = new Color(1, 1, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 256f, 
                    VoxelStep = 4, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0.5f, 0, 0.3f) 
                },
                new VoxelLODConfig.LODLevel 
                { 
                    Distance = 512f, 
                    VoxelStep = 8, 
                    HasCollider = false, 
                    DebugColor = new Color(1, 0, 0, 0.3f) 
                },
            };
            
            config.UpdateFrequency = 0.25f;
            config.Hysteresis = 1f;
            config.MaxUpdatesPerFrame = 10;
            config.EnableColliderLOD = true;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = config;
            
            EditorUtility.DisplayDialog("Quality LOD Config Created",
                "Created high-quality LOD config for high-end PCs:\n\n" +
                "• Extended view distances\n" +
                "• More full-detail chunks\n" +
                "• Lower hysteresis (faster updates)\n" +
                "• More updates per frame",
                "OK");
        }
        
        #endregion
        
        #region Priority 30: Profiler Config
        
        [MenuItem("DIG/Quick Setup/Advanced/Create Profiler Config", priority = 30)]
        static void CreateProfilerConfig()
        {
            var config = CreateProfilerConfigInternal();
            
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            
            EditorUtility.DisplayDialog("Profiler Config Created",
                "Created VoxelProfilerConfig with defaults:\n\n" +
                "• Frame Budget: 16.6ms (60 FPS target)\n" +
                "• Warning Threshold: 8ms\n" +
                "• Critical Threshold: 14ms\n" +
                "• Sample History: 60 frames\n\n" +
                "Use Profiler Dashboard (DIG > Voxel > Profiler)\n" +
                "to monitor voxel system performance.",
                "OK");
        }
        
        private static VoxelProfilerConfig CreateProfilerConfigInternal()
        {
            EnsureFoldersExist();
            
            string path = $"{RESOURCES_PATH}/VoxelProfilerConfig.asset";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<VoxelProfilerConfig>(path);
            if (existing != null)
                return existing;
            
            // Create profiler config
            var config = ScriptableObject.CreateInstance<VoxelProfilerConfig>();
            
            config.FrameBudgetMs = 16.6f;
            config.WarningThresholdMs = 8f;
            config.CriticalThresholdMs = 14f;
            config.SampleHistoryCount = 60;
            config.EnableAutoCapture = true;
            config.AutoCaptureThresholdMs = 20f;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log("[AdvancedQuickSetup] Created VoxelProfilerConfig");
            
            return config;
        }
        
        #endregion
        
        #region Priority 50: Validation
        
        [MenuItem("DIG/Quick Setup/Advanced/Validate Advanced Setup", priority = 50)]
        static void ValidateAdvancedSetup()
        {
            var issues = new List<string>();
            int passed = 0;
            
            // Check Visual Materials
            string[] visualPaths = AssetDatabase.FindAssets("t:VoxelVisualMaterial", new[] { VISUAL_MATERIALS_PATH });
            if (visualPaths.Length == 0)
            {
                issues.Add("❌ No Visual Materials found");
            }
            else
            {
                passed++;
            }
            
            // Check LOD Config
            var lodConfig = Resources.Load<VoxelLODConfig>("VoxelLODConfig");
            if (lodConfig == null)
            {
                issues.Add("❌ VoxelLODConfig not found in Resources/");
            }
            else
            {
                passed++;
            }
            
            // Check Profiler Config
            var profilerConfig = Resources.Load<VoxelProfilerConfig>("VoxelProfilerConfig");
            if (profilerConfig == null)
            {
                issues.Add("⚠️ VoxelProfilerConfig not found (profiling won't have thresholds)");
            }
            else
            {
                passed++;
            }
            
            // Check linking to gameplay materials
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry != null && registry.Materials != null)
            {
                int unlinked = 0;
                foreach (var mat in registry.Materials)
                {
                    if (mat != null && mat.VisualMaterial == null && mat.IsMineable)
                        unlinked++;
                }
                if (unlinked > 0)
                    issues.Add($"⚠️ {unlinked} gameplay materials missing visual material link");
            }
            
            // Build message
            string message;
            if (issues.Count == 0)
            {
                message = $"✅ All {passed} checks passed!\n\nYour advanced setup is ready.";
            }
            else
            {
                message = $"Found {issues.Count} issues:\n\n" + string.Join("\n", issues);
                if (passed > 0)
                    message += $"\n\n✅ {passed} checks passed";
            }
            
            EditorUtility.DisplayDialog("Advanced Setup Validation", message, "OK");
        }
        
        #endregion
        
        #region Priority 100: Cleanup
        
        [MenuItem("DIG/Quick Setup/Advanced/Delete All Advanced Assets", priority = 100)]
        static void DeleteAllAdvancedAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Advanced Setup Assets",
                "This will delete:\n\n" +
                "• All Visual Material assets\n" +
                "• VoxelLODConfig\n" +
                "• VoxelProfilerConfig\n\n" +
                "This cannot be undone!",
                "Delete", "Cancel"))
            {
                return;
            }
            
            // Delete visual materials folder
            if (AssetDatabase.IsValidFolder(VISUAL_MATERIALS_PATH))
            {
                AssetDatabase.DeleteAsset(VISUAL_MATERIALS_PATH);
            }
            
            // Delete LOD config
            AssetDatabase.DeleteAsset($"{RESOURCES_PATH}/VoxelLODConfig.asset");
            
            // Delete profiler config
            AssetDatabase.DeleteAsset($"{RESOURCES_PATH}/VoxelProfilerConfig.asset");
            
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[AdvancedQuickSetup] Deleted advanced setup assets");
        }
        
        #endregion
        
        #region Helpers
        
        private static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder(RESOURCES_PATH))
                AssetDatabase.CreateFolder("Assets", "Resources");
            
            if (!AssetDatabase.IsValidFolder(VISUAL_MATERIALS_PATH))
                AssetDatabase.CreateFolder(RESOURCES_PATH, "VisualMaterials");
        }
        
        #endregion
    }
    
    /// <summary>
    /// Configuration for voxel profiler thresholds and behavior.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Profiler Config")]
    public class VoxelProfilerConfig : ScriptableObject
    {
        [Header("Frame Budget")]
        [Tooltip("Target frame time in milliseconds (16.6ms = 60 FPS)")]
        public float FrameBudgetMs = 16.6f;
        
        [Header("Thresholds")]
        [Tooltip("Warning threshold - operations taking longer show yellow")]
        public float WarningThresholdMs = 8f;
        
        [Tooltip("Critical threshold - operations taking longer show red")]
        public float CriticalThresholdMs = 14f;
        
        [Header("Sampling")]
        [Tooltip("Number of samples to keep for averaging")]
        [Range(10, 300)]
        public int SampleHistoryCount = 60;
        
        [Header("Auto Capture")]
        [Tooltip("Enable automatic capture when frame budget exceeded")]
        public bool EnableAutoCapture = true;
        
        [Tooltip("Threshold for auto-capture (ms)")]
        public float AutoCaptureThresholdMs = 20f;
    }
}
