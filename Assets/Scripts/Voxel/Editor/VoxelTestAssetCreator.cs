using UnityEngine;
using UnityEditor;
using System.IO;
using DIG.Voxel.Core;

namespace DIG.Voxel.Editor
{
    public static class VoxelTestAssetCreator
    {
        [MenuItem("DIG/Voxel/Create Test Material Registry")]
        public static void CreateTestRegistry()
        {
            // Ensure Resources folder exists
            string resourcesPath = "Assets/Resources";
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            // 1. Create Loot Prefabs first
            GameObject stoneLoot = CreateSimpleLootPrefab("Loot_Stone", Color.gray);
            GameObject dirtLoot = CreateSimpleLootPrefab("Loot_Dirt", new Color(0.4f, 0.25f, 0.1f));
            GameObject oreLoot = CreateSimpleLootPrefab("Loot_Iron", new Color(0.8f, 0.6f, 0.4f));

            // 2. Create Registry
            VoxelMaterialRegistry registry = ScriptableObject.CreateInstance<VoxelMaterialRegistry>();
            
            // 3. Create Definitions
            registry.Materials.Add(CreateDefinition(0, "Air", 0f, null));
            registry.Materials.Add(CreateDefinition(1, "Dirt", 0.5f, dirtLoot));
            registry.Materials.Add(CreateDefinition(2, "Stone", 1.0f, stoneLoot));
            registry.Materials.Add(CreateDefinition(3, "IronOre", 2.0f, oreLoot));
            
            // Save Registry
            string regPath = "Assets/Resources/VoxelMaterialRegistry.asset";
            AssetDatabase.CreateAsset(registry, regPath);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            
            // Focus
            Selection.activeObject = registry;
            UnityEngine.Debug.Log($"[Voxel] Created Test Registry at {regPath}");
        }

        private static VoxelMaterialDefinition CreateDefinition(byte id, string name, float hardness, GameObject loot)
        {
            var def = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
            def.MaterialID = id;
            def.MaterialName = name;
            def.Hardness = hardness;
            def.IsMineable = id != 0;
            def.LootPrefab = loot;
            def.DropChance = 1.0f;
            def.MinDropCount = 1;
            def.MaxDropCount = 2;
            
            // Save as sub-asset or separate asset? 
            // Saving as separate asset is cleaner for project view.
            string folder = "Assets/Data/VoxelMaterials";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
                
            string path = $"{folder}/Mat_{name}.asset";
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        [MenuItem("DIG/Voxel/Create Test Loot Prefabs")]
        public static void CreateTestLootMenu()
        {
             CreateSimpleLootPrefab("Loot_Test_Cube", Color.white);
        }

        private static GameObject CreateSimpleLootPrefab(string name, Color color)
        {
            string folder = "Assets/Prefabs/Loot";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = Vector3.one * 0.3f; // Small loot size
            
            // Remove default collider, we need a Rigidbody
            // Actually PrimitiveType.Cube has BoxCollider. Keep it.
            
            // Add Rigidbody
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            
            // Setup Material (URP)
            var renderer = go.GetComponent<MeshRenderer>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            renderer.sharedMaterial = material;
            
            // Save Material asset
            string matPath = $"{folder}/Mat_{name}.mat";
            AssetDatabase.CreateAsset(material, matPath);
            
            // Save Prefab
            string prefabPath = $"{folder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            
            // Destroy scene object
            Object.DestroyImmediate(go);
            
            UnityEngine.Debug.Log($"Created Loot Prefab: {prefabPath}");
            return prefab;
        }
    }
}
