using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for EPIC 8 voxel system testing.
    /// Creates all necessary assets with one click for immediate testing.
    /// </summary>
    public static class VoxelQuickSetup
    {
        private const string RESOURCES_PATH = "Assets/Resources";
        private const string MATERIALS_PATH = "Assets/Resources/VoxelMaterials";
        private const string TEXTURES_PATH = "Assets/Textures/Voxel";
        private const string PREFABS_PATH = "Assets/Prefabs/Loot";
        
        #region Priority 0: Complete Demo
        
        [MenuItem("DIG/Quick Setup/Core/Create Complete Demo", priority = 0)]
        static void CreateCompleteDemoSetup()
        {
            int materialsCreated = 0;
            int lootCreated = 0;
            int texturesCreated = 0;
            
            // Step 1: Materials & Loot
            var (materials, loot) = CreateMaterialAndLootSetupInternal();
            materialsCreated = materials.Count;
            lootCreated = loot.Count;
            
            // Step 2: Textures
            var textures = CreateTextureConfigInternal();
            texturesCreated = textures.Length;
            
            // Step 3: Show summary
            EditorUtility.DisplayDialog("Complete Demo Setup",
                $"Epic 8 Demo Setup Complete!\n\n" +
                $"• {materialsCreated} Material Definitions\n" +
                $"• {lootCreated} Loot Prefabs\n" +
                $"• {texturesCreated} Textures + Config\n\n" +
                $"Next Steps:\n" +
                $"1. Create a scene with VoxelWorldAuthoring\n" +
                $"2. Enter Play Mode\n" +
                $"3. Mine voxels to see loot spawn",
                "OK");
            
            UnityEngine.Debug.Log("[VoxelQuickSetup] Complete demo setup finished successfully");
        }
        
        #endregion
        
        #region Priority 10: Individual Setups
        
        [MenuItem("DIG/Quick Setup/Core/Create Material & Loot Setup", priority = 10)]
        static void CreateMaterialAndLootSetup()
        {
            var (materials, loot) = CreateMaterialAndLootSetupInternal();
            
            if (materials.Count > 0)
            {
                Selection.activeObject = materials[0];
                EditorGUIUtility.PingObject(materials[0]);
            }
            
            EditorUtility.DisplayDialog("Material & Loot Setup Complete",
                $"Created:\n" +
                $"• {materials.Count} Material Definitions\n" +
                $"• {loot.Count} Loot Prefabs\n" +
                $"• 1 Material Registry\n\n" +
                $"Location: {MATERIALS_PATH}",
                "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Core/Create Texture Config", priority = 11)]
        static void CreateTextureConfig()
        {
            var textures = CreateTextureConfigInternal();
            
            var config = AssetDatabase.LoadAssetAtPath<VoxelTextureConfig>($"{RESOURCES_PATH}/VoxelTextureConfig.asset");
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            
            EditorUtility.DisplayDialog("Texture Config Setup Complete",
                $"Created:\n" +
                $"• {textures.Length} Procedural Textures\n" +
                $"• 1 VoxelTextureConfig\n" +
                $"• Built Texture2DArray\n\n" +
                $"Texture Location: {TEXTURES_PATH}",
                "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Core/Create Collision Test Objects", priority = 12)]
        static void CreateCollisionTestObjects()
        {
            // Create test sphere
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "VoxelCollisionTestSphere";
            sphere.transform.position = new Vector3(0, 50, 0);
            
            var rb = sphere.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;
            
            var mr = sphere.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mr.material.color = Color.red;
            
            Selection.activeGameObject = sphere;
            
            EditorUtility.DisplayDialog("Collision Test Setup",
                "Created a test sphere at Y=50.\n\n" +
                "Instructions:\n" +
                "1. Ensure you have VoxelWorldAuthoring in scene\n" +
                "2. Enter Play Mode\n" +
                "3. Watch the sphere fall and land on terrain\n\n" +
                "If sphere falls through terrain, collision is broken.",
                "OK");
        }
        
        #endregion
        
        #region Priority 50: Validation
        
        [MenuItem("DIG/Quick Setup/Core/Validate Current Setup", priority = 50)]
        static void ValidateSetup()
        {
            var issues = new List<string>();
            int passed = 0;
            
            // Check Material Registry
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry == null)
            {
                issues.Add("❌ VoxelMaterialRegistry not found in Resources/");
            }
            else if (registry.Materials == null || registry.Materials.Count == 0)
            {
                issues.Add("⚠️ VoxelMaterialRegistry has no materials");
            }
            else
            {
                passed++;
                
                // Check for loot prefabs
                int missingLoot = 0;
                foreach (var mat in registry.Materials)
                {
                    if (mat != null && mat.IsMineable && mat.LootPrefab == null)
                        missingLoot++;
                }
                if (missingLoot > 0)
                    issues.Add($"⚠️ {missingLoot} mineable materials missing LootPrefab");
            }
            
            // Check Texture Config
            var texConfig = Resources.Load<VoxelTextureConfig>("VoxelTextureConfig");
            if (texConfig == null)
            {
                issues.Add("❌ VoxelTextureConfig not found in Resources/");
            }
            else if (texConfig.TextureArray == null)
            {
                issues.Add("⚠️ VoxelTextureConfig has no built Texture2DArray");
            }
            else
            {
                passed++;
            }
            
            // Check World Generation Config
            var worldConfig = Resources.Load<WorldGenerationConfig>("WorldGenerationConfig");
            if (worldConfig == null)
            {
                issues.Add("⚠️ WorldGenerationConfig not found (geology disabled)");
            }
            else
            {
                passed++;
            }
            
            // Build message
            string message;
            if (issues.Count == 0)
            {
                message = $"✅ All {passed} checks passed!\n\nYour voxel setup is ready.";
            }
            else
            {
                message = $"Found {issues.Count} issues:\n\n" + string.Join("\n", issues);
                if (passed > 0)
                    message += $"\n\n✅ {passed} checks passed";
            }
            
            EditorUtility.DisplayDialog("Setup Validation", message, "OK");
        }
        
        #endregion
        
        #region Priority 100: Cleanup
        
        [MenuItem("DIG/Quick Setup/Core/Delete All Quick Setup Assets", priority = 100)]
        static void DeleteAllQuickSetupAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Quick Setup Assets",
                "This will delete:\n\n" +
                "• All VoxelMaterialDefinition assets\n" +
                "• All Loot prefabs in Prefabs/Loot/\n" +
                "• VoxelMaterialRegistry\n" +
                "• VoxelTextureConfig\n" +
                "• Procedural textures in Textures/Voxel/\n\n" +
                "This cannot be undone!",
                "Delete", "Cancel"))
            {
                return;
            }
            
            int deleted = 0;
            
            // Delete materials folder
            if (AssetDatabase.IsValidFolder(MATERIALS_PATH))
            {
                AssetDatabase.DeleteAsset(MATERIALS_PATH);
                deleted++;
            }
            
            // Delete loot prefabs folder
            if (AssetDatabase.IsValidFolder(PREFABS_PATH))
            {
                AssetDatabase.DeleteAsset(PREFABS_PATH);
                deleted++;
            }
            
            // Delete textures folder
            if (AssetDatabase.IsValidFolder(TEXTURES_PATH))
            {
                AssetDatabase.DeleteAsset(TEXTURES_PATH);
                deleted++;
            }
            
            // Delete registry
            AssetDatabase.DeleteAsset($"{RESOURCES_PATH}/VoxelMaterialRegistry.asset");
            
            // Delete texture config
            AssetDatabase.DeleteAsset($"{RESOURCES_PATH}/VoxelTextureConfig.asset");
            
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[VoxelQuickSetup] Deleted quick setup assets");
        }
        
        #endregion
        
        #region Internal Implementation
        
        private static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder(RESOURCES_PATH))
                AssetDatabase.CreateFolder("Assets", "Resources");
            
            if (!AssetDatabase.IsValidFolder(MATERIALS_PATH))
                AssetDatabase.CreateFolder(RESOURCES_PATH, "VoxelMaterials");
            
            if (!AssetDatabase.IsValidFolder("Assets/Textures"))
                AssetDatabase.CreateFolder("Assets", "Textures");
            
            if (!AssetDatabase.IsValidFolder(TEXTURES_PATH))
                AssetDatabase.CreateFolder("Assets/Textures", "Voxel");
            
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            
            if (!AssetDatabase.IsValidFolder(PREFABS_PATH))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Loot");
        }
        
        private static (List<VoxelMaterialDefinition>, List<GameObject>) CreateMaterialAndLootSetupInternal()
        {
            EnsureFoldersExist();
            
            var materials = new List<VoxelMaterialDefinition>();
            var lootPrefabs = new List<GameObject>();
            
            // Material definitions: (name, id, hardness, isMineable, color, needsLoot)
            var materialDefs = new (string name, byte id, float hardness, bool mineable, Color color, bool needsLoot)[]
            {
                ("Air", 0, 0f, false, Color.clear, false),
                ("Stone", 1, 1.0f, true, new Color(0.5f, 0.5f, 0.5f), true),
                ("Dirt", 2, 0.5f, true, new Color(0.55f, 0.35f, 0.2f), true),
                ("Iron", 3, 2.0f, true, new Color(0.6f, 0.45f, 0.35f), true),
                ("Gold", 4, 2.5f, true, new Color(1f, 0.84f, 0f), true),
                ("Copper", 5, 1.8f, true, new Color(0.8f, 0.5f, 0.2f), true),
                ("Granite", 10, 1.5f, true, new Color(0.6f, 0.55f, 0.5f), true),
                ("Basalt", 11, 1.8f, true, new Color(0.25f, 0.25f, 0.3f), true),
                ("Coal", 20, 0.8f, true, new Color(0.1f, 0.1f, 0.1f), true),
                ("Tin", 21, 1.5f, true, new Color(0.7f, 0.7f, 0.75f), true),
                ("Silver", 22, 2.2f, true, new Color(0.85f, 0.85f, 0.9f), true),
                ("Diamond", 23, 4.0f, true, new Color(0.6f, 0.9f, 1f), true),
                ("Mythril", 24, 5.0f, true, new Color(0.4f, 0.8f, 0.95f), true),
            };
            
            foreach (var def in materialDefs)
            {
                string matPath = $"{MATERIALS_PATH}/Mat_{def.name}.asset";
                
                // Check if exists
                var existing = AssetDatabase.LoadAssetAtPath<VoxelMaterialDefinition>(matPath);
                if (existing != null)
                {
                    materials.Add(existing);
                    continue;
                }
                
                // Create material definition
                var mat = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
                mat.MaterialID = def.id;
                mat.MaterialName = def.name;
                mat.Hardness = def.hardness;
                mat.IsMineable = def.mineable;
                mat.DebugColor = def.color;
                mat.MinDropCount = 1;
                mat.MaxDropCount = def.id >= 20 ? 3 : 1; // Ores drop more
                mat.DropChance = 1f;
                mat.TextureArrayIndex = def.id;
                
                // Create loot prefab if needed
                if (def.needsLoot)
                {
                    var lootPrefab = CreateLootPrefab(def.name, def.color);
                    if (lootPrefab != null)
                    {
                        mat.LootPrefab = lootPrefab;
                        lootPrefabs.Add(lootPrefab);
                    }
                }
                
                AssetDatabase.CreateAsset(mat, matPath);
                materials.Add(mat);
            }
            
            // Create registry
            var registry = CreateOrUpdateRegistry(materials);
            
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[VoxelQuickSetup] Created {materials.Count} materials and {lootPrefabs.Count} loot prefabs");
            
            return (materials, lootPrefabs);
        }
        
        private static GameObject CreateLootPrefab(string name, Color color)
        {
            string prefabPath = $"{PREFABS_PATH}/Loot_{name}.prefab";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
                return existing;
            
            // Create loot object
            var loot = GameObject.CreatePrimitive(name.Contains("Gold") || name.Contains("Diamond") ? 
                PrimitiveType.Sphere : PrimitiveType.Cube);
            loot.name = $"Loot_{name}";
            loot.transform.localScale = Vector3.one * 0.3f;
            
            // Add rigidbody
            var rb = loot.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 1f;
            rb.angularDamping = 1f;
            rb.useGravity = true;
            
            // Set material color
            var mr = loot.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Smoothness", name.Contains("Gold") || name.Contains("Diamond") ? 0.8f : 0.3f);
            mat.SetFloat("_Metallic", name.Contains("Iron") || name.Contains("Gold") || name.Contains("Copper") ? 0.8f : 0f);
            mr.sharedMaterial = mat;
            
            // Save material
            string matPath = $"{PREFABS_PATH}/Mat_Loot_{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            
            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(loot, prefabPath);
            Object.DestroyImmediate(loot);
            
            return prefab;
        }
        
        private static VoxelMaterialRegistry CreateOrUpdateRegistry(List<VoxelMaterialDefinition> materials)
        {
            string registryPath = $"{RESOURCES_PATH}/VoxelMaterialRegistry.asset";
            
            var registry = AssetDatabase.LoadAssetAtPath<VoxelMaterialRegistry>(registryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<VoxelMaterialRegistry>();
                AssetDatabase.CreateAsset(registry, registryPath);
            }
            
            registry.Materials = new List<VoxelMaterialDefinition>(materials);
            EditorUtility.SetDirty(registry);
            
            return registry;
        }
        
        private static Texture2D[] CreateTextureConfigInternal()
        {
            EnsureFoldersExist();
            
            // Texture definitions: (name, baseColor, noiseScale, noisiness)
            var textureDefs = new (string name, Color color, float scale, float noisiness)[]
            {
                ("Air", Color.clear, 0.1f, 0f),
                ("Stone", new Color(0.5f, 0.5f, 0.5f), 0.05f, 0.3f),
                ("Dirt", new Color(0.55f, 0.35f, 0.2f), 0.08f, 0.4f),
                ("Iron", new Color(0.6f, 0.45f, 0.35f), 0.06f, 0.5f),
                ("Gold", new Color(1f, 0.84f, 0f), 0.04f, 0.2f),
                ("Copper", new Color(0.8f, 0.5f, 0.2f), 0.05f, 0.4f),
            };
            
            var textures = new List<Texture2D>();
            
            foreach (var def in textureDefs)
            {
                string texPath = $"{TEXTURES_PATH}/Tex_{def.name}.png";
                
                // Check if exists
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (existing != null)
                {
                    textures.Add(existing);
                    continue;
                }
                
                // Generate procedural texture
                var tex = GenerateProceduralTexture(def.name, def.color, def.scale, def.noisiness);
                
                // Save as PNG
                byte[] pngData = tex.EncodeToPNG();
                File.WriteAllBytes(texPath, pngData);
                Object.DestroyImmediate(tex);
                
                AssetDatabase.ImportAsset(texPath);
                
                // Configure import settings
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                
                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                textures.Add(loadedTex);
            }
            
            // Create or update VoxelTextureConfig
            string configPath = $"{RESOURCES_PATH}/VoxelTextureConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<VoxelTextureConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<VoxelTextureConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            
            config.Textures = textures.ToArray();
            config.Rebuild();
            EditorUtility.SetDirty(config);
            
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[VoxelQuickSetup] Created {textures.Count} textures and built Texture2DArray");
            
            return textures.ToArray();
        }
        
        private static Texture2D GenerateProceduralTexture(string name, Color baseColor, float noiseScale, float noisiness)
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
                    float detail = Mathf.PerlinNoise(x * noiseScale * 4, y * noiseScale * 4) * 0.5f;
                    
                    float variation = (noise + detail - 0.75f) * noisiness;
                    
                    Color pixel = baseColor;
                    pixel.r = Mathf.Clamp01(pixel.r + variation);
                    pixel.g = Mathf.Clamp01(pixel.g + variation);
                    pixel.b = Mathf.Clamp01(pixel.b + variation);
                    pixel.a = 1f;
                    
                    tex.SetPixel(x, y, pixel);
                }
            }
            
            tex.Apply();
            return tex;
        }
        
        #endregion
    }
}
