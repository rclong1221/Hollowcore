using UnityEngine;
using UnityEditor;
using System.IO;
using DIG.Voxel.Core;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for geology and world generation testing.
    /// Creates sample assets with reasonable defaults for immediate testing.
    /// </summary>
    public static class GeologyQuickSetup
    {
        private const string RESOURCES_PATH = "Assets/Resources";
        private const string GEOLOGY_PATH = "Assets/Resources/Geology";
        
        [MenuItem("DIG/Quick Setup/Generation/Create Complete Geology Setup", priority = 0)]
        static void CreateCompleteSetup()
        {
            EnsureFoldersExist();
            
            // Create all assets
            var strata = CreateDefaultStrataProfile();
            var depthCurve = CreateDefaultDepthCurve();
            var ores = CreateDefaultOres();
            var config = CreateWorldConfig(strata, depthCurve, ores);
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            EditorUtility.DisplayDialog("Geology Setup Complete", 
                $"Created:\n" +
                $"• 1 Strata Profile (4 layers)\n" +
                $"• 1 Depth Value Curve\n" +
                $"• {ores.Length} Ore Definitions\n" +
                $"• 1 World Generation Config\n\n" +
                $"Location: {GEOLOGY_PATH}", "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create Strata Profile Only", priority = 10)]
        static void CreateStrataOnly()
        {
            EnsureFoldersExist();
            var strata = CreateDefaultStrataProfile();
            Selection.activeObject = strata;
            EditorGUIUtility.PingObject(strata);
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create Sample Ores Only", priority = 11)]
        static void CreateOresOnly()
        {
            EnsureFoldersExist();
            var ores = CreateDefaultOres();
            Selection.activeObject = ores[0];
            EditorGUIUtility.PingObject(ores[0]);
            EditorUtility.DisplayDialog("Ores Created", 
                $"Created {ores.Length} ore definitions in {GEOLOGY_PATH}/Ores/", "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create Depth Curve Only", priority = 12)]
        static void CreateDepthCurveOnly()
        {
            EnsureFoldersExist();
            var curve = CreateDefaultDepthCurve();
            Selection.activeObject = curve;
            EditorGUIUtility.PingObject(curve);
        }
        
        private static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder(RESOURCES_PATH))
                AssetDatabase.CreateFolder("Assets", "Resources");
            
            if (!AssetDatabase.IsValidFolder(GEOLOGY_PATH))
                AssetDatabase.CreateFolder(RESOURCES_PATH, "Geology");
            
            if (!AssetDatabase.IsValidFolder($"{GEOLOGY_PATH}/Ores"))
                AssetDatabase.CreateFolder(GEOLOGY_PATH, "Ores");
        }
        
        private static StrataProfile CreateDefaultStrataProfile()
        {
            string path = $"{GEOLOGY_PATH}/DefaultStrataProfile.asset";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<StrataProfile>(path);
            if (existing != null) return existing;
            
            var strata = ScriptableObject.CreateInstance<StrataProfile>();
            strata.NoiseSeed = 42;
            strata.NoiseScale = 0.05f;
            strata.Layers = new StrataProfile.Layer[]
            {
                new StrataProfile.Layer
                {
                    MaterialID = 2, // Dirt
                    DisplayName = "Topsoil",
                    MinDepth = 0,
                    MaxDepth = 5,
                    BlendWidth = 2,
                    NoiseInfluence = 0.1f,
                    DebugColor = new Color(0.4f, 0.3f, 0.2f)
                },
                new StrataProfile.Layer
                {
                    MaterialID = 1, // Stone
                    DisplayName = "Stone",
                    MinDepth = 5,
                    MaxDepth = 50,
                    BlendWidth = 3,
                    NoiseInfluence = 0.2f,
                    DebugColor = Color.gray
                },
                new StrataProfile.Layer
                {
                    MaterialID = 10, // Granite
                    DisplayName = "Granite",
                    MinDepth = 50,
                    MaxDepth = 150,
                    BlendWidth = 5,
                    NoiseInfluence = 0.3f,
                    DebugColor = new Color(0.6f, 0.5f, 0.5f)
                },
                new StrataProfile.Layer
                {
                    MaterialID = 11, // Basalt
                    DisplayName = "Basalt",
                    MinDepth = 150,
                    MaxDepth = 999,
                    BlendWidth = 10,
                    NoiseInfluence = 0.4f,
                    DebugColor = new Color(0.2f, 0.2f, 0.25f)
                }
            };
            
            AssetDatabase.CreateAsset(strata, path);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log($"[GeologyQuickSetup] Created Strata Profile: {path}");
            return strata;
        }
        
        private static DepthValueCurve CreateDefaultDepthCurve()
        {
            string path = $"{GEOLOGY_PATH}/DefaultDepthCurve.asset";
            
            var existing = AssetDatabase.LoadAssetAtPath<DepthValueCurve>(path);
            if (existing != null) return existing;
            
            var curve = ScriptableObject.CreateInstance<DepthValueCurve>();
            curve.MaxDepthReference = 200f;
            
            // Common: abundant shallow, diminishes deep
            curve.CommonOreCurve = new AnimationCurve(
                new Keyframe(0, 1f),
                new Keyframe(50, 0.8f),
                new Keyframe(100, 0.5f),
                new Keyframe(200, 0.2f)
            );
            
            // Uncommon: starts at 20m, peaks at 60m
            curve.UncommonOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(20, 0.1f),
                new Keyframe(60, 1f),
                new Keyframe(120, 0.6f),
                new Keyframe(200, 0.4f)
            );
            
            // Rare: starts at 50m, peaks at 120m
            curve.RareOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(50, 0f),
                new Keyframe(80, 0.3f),
                new Keyframe(120, 1f),
                new Keyframe(200, 0.8f)
            );
            
            // Legendary: only deep, starts at 100m
            curve.LegendaryOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(100, 0f),
                new Keyframe(150, 0.5f),
                new Keyframe(200, 1f)
            );
            
            AssetDatabase.CreateAsset(curve, path);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log($"[GeologyQuickSetup] Created Depth Curve: {path}");
            return curve;
        }
        
        private static OreDefinition[] CreateDefaultOres()
        {
            var ores = new (string name, byte id, float minDepth, float maxDepth, OreRarity rarity, float threshold, Color color)[]
            {
                ("Coal", 20, 5, 60, OreRarity.Common, 0.55f, new Color(0.1f, 0.1f, 0.1f)),
                ("Iron", 3, 10, 80, OreRarity.Common, 0.6f, new Color(0.6f, 0.4f, 0.3f)),
                ("Copper", 5, 15, 100, OreRarity.Uncommon, 0.65f, new Color(0.8f, 0.5f, 0.2f)),
                ("Tin", 21, 20, 90, OreRarity.Uncommon, 0.68f, new Color(0.7f, 0.7f, 0.75f)),
                ("Silver", 22, 40, 150, OreRarity.Rare, 0.75f, new Color(0.85f, 0.85f, 0.9f)),
                ("Gold", 4, 60, 180, OreRarity.Rare, 0.78f, new Color(1f, 0.85f, 0.1f)),
                ("Diamond", 23, 100, 200, OreRarity.Legendary, 0.88f, new Color(0.6f, 0.9f, 1f)),
                ("Mythril", 24, 120, 999, OreRarity.Legendary, 0.92f, new Color(0.4f, 0.8f, 0.95f))
            };
            
            var results = new OreDefinition[ores.Length];
            
            for (int i = 0; i < ores.Length; i++)
            {
                var o = ores[i];
                string path = $"{GEOLOGY_PATH}/Ores/{o.name}Ore.asset";
                
                var existing = AssetDatabase.LoadAssetAtPath<OreDefinition>(path);
                if (existing != null)
                {
                    results[i] = existing;
                    continue;
                }
                
                var ore = ScriptableObject.CreateInstance<OreDefinition>();
                ore.OreName = o.name;
                ore.MaterialID = o.id;
                ore.MinDepth = o.minDepth;
                ore.MaxDepth = o.maxDepth;
                ore.Rarity = o.rarity;
                ore.Threshold = o.threshold;
                ore.DebugColor = o.color;
                ore.NoiseScale = 0.1f;
                ore.DomainWarping = true;
                ore.WarpStrength = 5f;
                
                AssetDatabase.CreateAsset(ore, path);
                results[i] = ore;
            }
            
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[GeologyQuickSetup] Created {ores.Length} ore definitions");
            return results;
        }
        
        private static WorldGenerationConfig CreateWorldConfig(StrataProfile strata, DepthValueCurve depthCurve, OreDefinition[] ores)
        {
            string path = $"{RESOURCES_PATH}/WorldGenerationConfig.asset";
            
            // Check if exists - update rather than replace
            var existing = AssetDatabase.LoadAssetAtPath<WorldGenerationConfig>(path);
            if (existing != null)
            {
                existing.StrataProfile = strata;
                existing.DepthCurve = depthCurve;
                existing.OreDefinitions = ores;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            
            var config = ScriptableObject.CreateInstance<WorldGenerationConfig>();
            config.GroundLevel = 0f;
            config.Seed = 12345;
            config.TerrainNoiseScale = 0.02f;
            config.TerrainNoiseAmplitude = 10f;
            config.EnableStrata = true;
            config.EnableOres = true;
            config.StrataProfile = strata;
            config.DepthCurve = depthCurve;
            config.OreDefinitions = ores;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log($"[GeologyQuickSetup] Created World Config: {path}");
            return config;
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Open Geology Folder", priority = 30)]
        static void OpenGeologyFolder()
        {
            EnsureFoldersExist();
            var folder = AssetDatabase.LoadAssetAtPath<Object>(GEOLOGY_PATH);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Delete All Geology Assets", priority = 32)]
        static void DeleteAllGeologyAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Geology Assets", 
                "This will delete ALL geology assets including:\n\n" +
                "• Strata Profiles\n" +
                "• Ore Definitions\n" +
                "• Depth Curves\n" +
                "• World Generation Config\n\n" +
                "This cannot be undone!", "Delete", "Cancel"))
            {
                return;
            }
            
            // Delete config
            AssetDatabase.DeleteAsset($"{RESOURCES_PATH}/WorldGenerationConfig.asset");
            
            // Delete geology folder
            if (AssetDatabase.IsValidFolder(GEOLOGY_PATH))
            {
                AssetDatabase.DeleteAsset(GEOLOGY_PATH);
            }
            
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[GeologyQuickSetup] Deleted all geology assets");
        }
    }
}
