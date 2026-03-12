using UnityEngine;
using UnityEditor;
using DIG.Voxel.Biomes;
using DIG.Voxel.Core;

namespace DIG.Voxel.Editor
{
    public static class BiomeQuickSetup
    {
        private const string FOLDER_PATH = "Assets/Resources/Biomes";
        
        [MenuItem("DIG/Quick Setup/Generation/Create Biomes", false, 300)]
        public static void CreateBiomes()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FOLDER_PATH))
                AssetDatabase.CreateFolder("Assets/Resources", "Biomes");
                
            // Create Hollow Biomes
            CreateBiome("Hollow_Mushroom", 10, new Color(0.2f, 0.8f, 0.2f), -1, 1, -1, 1);
            CreateBiome("Hollow_Crystal", 11, new Color(0.8f, 0.2f, 1f), -1, 1, -1, 1);
            CreateBiome("Hollow_Magma", 12, new Color(1f, 0.2f, 0f), -1, 1, -1, 1);
            
            // Create Solid Biomes (Distributed by Temp/Hum)
            // Temp -1..0, Hum -1..0: Cold Dry (Tundra)
            CreateBiome("Solid_Tundra", 20, Color.cyan, -1f, 0f, -1f, 0f);
            
            // Temp 0..1, Hum -1..0: Hot Dry (Desert)
            CreateBiome("Solid_Desert", 21, Color.yellow, 0f, 1f, -1f, 0f);
            
            // Temp -1..0, Hum 0..1: Cold Wet (Swamp?)
            CreateBiome("Solid_Taiga", 22, Color.white, -1f, 0f, 0f, 1f);
            
            // Temp 0..1, Hum 0..1: Hot Wet (Jungle)
            CreateBiome("Solid_Jungle", 23, Color.green, 0f, 1f, 0f, 1f);
            
            // Create Registry
            var registry = ScriptableObject.CreateInstance<BiomeRegistry>();
            registry.GlobalNoiseScale = 0.002f;
            AssetDatabase.CreateAsset(registry, $"{FOLDER_PATH}/BiomeRegistry.asset");
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            registry.AutoPopulate(); // Self-reference biomes
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log("Biomes created in " + FOLDER_PATH);
        }
        
        private static void CreateBiome(string name, byte id, Color color, float minT, float maxT, float minH, float maxH)
        {
            var b = ScriptableObject.CreateInstance<BiomeDefinition>();
            b.BiomeName = name;
            b.BiomeID = id;
            b.DebugColor = color;
            b.MinTemperature = minT;
            b.MaxTemperature = maxT;
            b.MinHumidity = minH;
            b.MaxHumidity = maxH;
            
            AssetDatabase.CreateAsset(b, $"{FOLDER_PATH}/{name}.asset");
        }
    }
}
