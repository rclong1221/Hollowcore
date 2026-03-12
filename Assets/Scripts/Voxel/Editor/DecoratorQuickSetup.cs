#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using DIG.Voxel.Decorators;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for decorator system.
    /// </summary>
    public static class DecoratorQuickSetup
    {
        private const string DECORATOR_PATH = "Assets/Resources/Decorators";
        private const string REGISTRY_PATH = "Assets/Resources/DecoratorRegistry.asset";
        
        [MenuItem("DIG/Quick Setup/Decorators/Create Complete Decorator Setup", priority = 100)]
        public static void CreateCompleteSetup()
        {
            CreateFolders();
            CreateSampleDecorators();
            CreateDecoratorRegistry();
            
            UnityEngine.Debug.Log("[DecoratorQuickSetup] Complete decorator setup created!");
            EditorUtility.DisplayDialog("Decorator Setup Complete", 
                "Created:\n• 8 Sample Decorators\n• DecoratorRegistry in Resources\n\nDecorators will spawn in caves when you enter Play Mode.", 
                "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Create Sample Decorators", priority = 110)]
        public static void CreateSampleDecorators()
        {
            CreateFolders();
            
            // Floor decorators
            CreateDecorator("Small Crystal", 1, SurfaceType.Floor, 0.15f, 2f, 3f, 10f, 3000f);
            CreateDecorator("Mushroom Cluster", 2, SurfaceType.Floor, 0.12f, 3f, 2f, 10f, 1000f);
            CreateDecorator("Stalagmite", 3, SurfaceType.Floor, 0.08f, 4f, 5f, 50f, 5000f);
            CreateDecorator("Ore Cluster", 4, SurfaceType.Floor, 0.05f, 5f, 4f, 100f, 5000f);
            
            // Ceiling decorators
            CreateDecorator("Stalactite", 5, SurfaceType.Ceiling, 0.10f, 3f, 5f, 50f, 5000f);
            CreateDecorator("Glowing Spores", 6, SurfaceType.Ceiling, 0.15f, 2f, 2f, 10f, 1500f);
            
            // Wall decorators
            CreateDecorator("Wall Crystal", 7, SurfaceType.WallNorth, 0.08f, 3f, 3f, 30f, 5000f);
            CreateDecorator("Moss Patch", 8, SurfaceType.WallEast, 0.12f, 2f, 2f, 10f, 500f);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            UnityEngine.Debug.Log("[DecoratorQuickSetup] Created 8 sample decorators");
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Create Decorator Registry", priority = 120)]
        public static void CreateDecoratorRegistry()
        {
            CreateFolders();
            
            var registry = ScriptableObject.CreateInstance<DecoratorRegistry>();
            registry.MaxDecoratorsPerChunk = 50;
            registry.GlobalSpawnMultiplier = 1f;
            registry.EnableDecorators = true;
            
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
            
            // Auto-populate
            registry.AutoPopulate();
            
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log("[DecoratorQuickSetup] Created DecoratorRegistry");
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Create Giant Decorators (Hollow Earth)", priority = 130)]
        public static void CreateGiantDecorators()
        {
            CreateFolders();
            
            // Giant hollow earth decorators
            var giantMushroom = CreateDecorator("Giant Mushroom", 20, SurfaceType.Floor, 0.02f, 30f, 20f, 300f, 5000f);
            giantMushroom.IsGiantDecorator = true;
            giantMushroom.MaxHeight = 80f;
            giantMushroom.MinScale = 5f;
            giantMushroom.MaxScale = 15f;
            giantMushroom.ScaleWithCaveSize = true;
            EditorUtility.SetDirty(giantMushroom);
            
            var crystalSpire = CreateDecorator("Crystal Spire", 21, SurfaceType.Floor, 0.03f, 20f, 15f, 500f, 5000f);
            crystalSpire.IsGiantDecorator = true;
            crystalSpire.MaxHeight = 60f;
            crystalSpire.MinScale = 3f;
            crystalSpire.MaxScale = 10f;
            EditorUtility.SetDirty(crystalSpire);
            
            var ancientPillar = CreateDecorator("Ancient Pillar", 22, SurfaceType.Floor, 0.01f, 50f, 30f, 800f, 5000f);
            ancientPillar.IsGiantDecorator = true;
            ancientPillar.MaxHeight = 100f;
            ancientPillar.RandomYRotation = false; // Ancient structures face cardinal directions
            EditorUtility.SetDirty(ancientPillar);
            
            AssetDatabase.SaveAssets();
            
            UnityEngine.Debug.Log("[DecoratorQuickSetup] Created 3 giant decorators for hollow earth");
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Validate Decorator Setup", priority = 200)]
        public static void ValidateSetup()
        {
            var registry = AssetDatabase.LoadAssetAtPath<DecoratorRegistry>(REGISTRY_PATH);
            
            if (registry == null)
            {
                UnityEngine.Debug.LogError("[DecoratorQuickSetup] DecoratorRegistry not found! Run 'Create Decorator Registry' first.");
                return;
            }
            
            registry.ValidateIDs();
            
            int withPrefabs = 0;
            int withoutPrefabs = 0;
            
            foreach (var dec in registry.Decorators)
            {
                if (dec == null) continue;
                if (dec.Prefab != null) withPrefabs++;
                else withoutPrefabs++;
            }
            
            UnityEngine.Debug.Log($"[DecoratorQuickSetup] Validation Complete:\n" +
                     $"  Total Decorators: {registry.Decorators?.Length ?? 0}\n" +
                     $"  With Prefabs: {withPrefabs}\n" +
                     $"  Without Prefabs: {withoutPrefabs}");
            
            if (withoutPrefabs > 0)
            {
                UnityEngine.Debug.LogWarning($"[DecoratorQuickSetup] {withoutPrefabs} decorators have no prefab assigned. " +
                               "Assign prefabs in the Inspector for them to spawn.");
            }
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Open Decorator Folder", priority = 300)]
        public static void OpenDecoratorFolder()
        {
            CreateFolders();
            EditorUtility.RevealInFinder(DECORATOR_PATH);
        }
        
        [MenuItem("DIG/Quick Setup/Decorators/Delete All Decorator Assets", priority = 400)]
        public static void DeleteAllDecoratorAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Decorator Assets",
                "This will delete all decorator definitions and the registry.\n\nAre you sure?",
                "Delete", "Cancel"))
            {
                return;
            }
            
            if (Directory.Exists(DECORATOR_PATH))
            {
                FileUtil.DeleteFileOrDirectory(DECORATOR_PATH);
                FileUtil.DeleteFileOrDirectory(DECORATOR_PATH + ".meta");
            }
            
            if (File.Exists(REGISTRY_PATH))
            {
                AssetDatabase.DeleteAsset(REGISTRY_PATH);
            }
            
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[DecoratorQuickSetup] All decorator assets deleted");
        }
        
        private static void CreateFolders()
        {
            if (!Directory.Exists("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            
            if (!Directory.Exists(DECORATOR_PATH))
                AssetDatabase.CreateFolder("Assets/Resources", "Decorators");
        }
        
        private static DecoratorDefinition CreateDecorator(
            string name, byte id, SurfaceType surface, 
            float probability, float spacing, float minCaveRadius,
            float minDepth, float maxDepth)
        {
            var decorator = ScriptableObject.CreateInstance<DecoratorDefinition>();
            decorator.DecoratorName = name;
            decorator.DecoratorID = id;
            decorator.RequiredSurface = surface;
            decorator.SpawnProbability = probability;
            decorator.MinSpacing = spacing;
            decorator.MinCaveRadius = minCaveRadius;
            decorator.MinDepth = minDepth;
            decorator.MaxDepth = maxDepth;
            decorator.MinScale = 0.8f;
            decorator.MaxScale = 1.2f;
            decorator.RandomYRotation = true;
            decorator.AlignToSurface = true;
            
            string path = $"{DECORATOR_PATH}/{name.Replace(" ", "")}.asset";
            AssetDatabase.CreateAsset(decorator, path);
            
            return decorator;
        }
    }
}
#endif
