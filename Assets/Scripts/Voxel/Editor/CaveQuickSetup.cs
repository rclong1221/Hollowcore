using UnityEngine;
using UnityEditor;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for Cave and Hollow Earth systems.
    /// Creates sample configurations for testing multi-layer worlds.
    /// </summary>
    public static class CaveQuickSetup
    {
        private const string FOLDER_PATH = "Assets/DIG_QuickSetup/CaveSystem";
        private const int MENU_PRIORITY = 100;
        
        #region Complete Setup
        
        [MenuItem("DIG/Quick Setup/Generation/Create Complete Cave Setup", false, MENU_PRIORITY)]
        public static void CreateCompleteCaveSetup()
        {
            CreateOutputFolder();
            
            // Create all assets
            CreateSampleCaveProfiles();
            CreateSampleHollowProfiles();
            CreateSampleWorldLayers();
            CreateWorldStructureConfig();
            
            // Refresh and report
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[CaveQuickSetup] ✅ Complete cave/hollow earth setup created!");
            EditorUtility.DisplayDialog("Cave Setup Complete",
                "Created cave profiles, hollow earth profiles, world layers, and WorldStructureConfig.\n\n" +
                $"Assets saved to: {FOLDER_PATH}\n\n" +
                "Place WorldStructureConfig in Resources folder to use.",
                "OK");
        }
        
        #endregion
        
        #region Individual Assets
        
        [MenuItem("DIG/Quick Setup/Generation/Create Sample Cave Profiles", false, MENU_PRIORITY + 1)]
        public static void CreateSampleCaveProfiles()
        {
            CreateOutputFolder();
            
            // Starter caves (shallow, safe) - Reduced fragmentation
            var starterCave = ScriptableObject.CreateInstance<CaveProfile>();
            starterCave.EnableSwissCheese = true;
            starterCave.CheeseScale = 0.05f;           // Larger scale = bigger, rounder holes
            starterCave.CheeseThreshold = 0.85f;       // Higher = much fewer holes (15% vs 40%)
            starterCave.CheeseMinDepth = 10f;
            starterCave.CheeseMaxDepth = 200f;
            starterCave.EnableSpaghetti = true;
            starterCave.SpaghettiScale = 0.02f;        // Lower scale = smoother, longer tunnels
            starterCave.SpaghettiWidth = 0.03f;        // Narrower = coherent tunnel, not slices
            starterCave.SpaghettiMinDepth = 30f;
            starterCave.SpaghettiMaxDepth = 300f;
            starterCave.EnableNoodles = false;         // Disable for starter - too complex
            starterCave.EnableCaverns = false;         // Disable for starter
            AssetDatabase.CreateAsset(starterCave, $"{FOLDER_PATH}/CaveProfile_Starter.asset");
            
            // Standard caves (mid-game) - Balanced complexity
            var standardCave = ScriptableObject.CreateInstance<CaveProfile>();
            standardCave.EnableSwissCheese = true;
            standardCave.CheeseScale = 0.04f;          // Larger holes
            standardCave.CheeseThreshold = 0.82f;      // Fewer holes
            standardCave.CheeseMinDepth = 50f;
            standardCave.CheeseMaxDepth = 500f;
            standardCave.EnableSpaghetti = true;
            standardCave.SpaghettiScale = 0.015f;      // Smoother tunnels
            standardCave.SpaghettiWidth = 0.04f;       // Coherent width
            standardCave.SpaghettiMinDepth = 100f;
            standardCave.SpaghettiMaxDepth = 600f;
            standardCave.EnableNoodles = true;
            standardCave.NoodleScale = 0.01f;          // Large smooth noodles
            standardCave.NoodleWidth = 0.06f;          // Wider but coherent
            standardCave.NoodleMinDepth = 150f;
            standardCave.NoodleMaxDepth = 700f;
            standardCave.EnableCaverns = true;
            standardCave.CavernScale = 0.015f;         // Large caverns
            standardCave.CavernThreshold = 0.82f;      // Rarer caverns
            standardCave.CavernMinDepth = 200f;
            AssetDatabase.CreateAsset(standardCave, $"{FOLDER_PATH}/CaveProfile_Standard.asset");
            
            // Deep caves (challenging) - Complex but navigable
            var deepCave = ScriptableObject.CreateInstance<CaveProfile>();
            deepCave.EnableSwissCheese = true;
            deepCave.CheeseScale = 0.03f;              // Large pockets
            deepCave.CheeseThreshold = 0.80f;          // Still limited
            deepCave.CheeseMinDepth = 200f;
            deepCave.CheeseMaxDepth = 1000f;
            deepCave.EnableSpaghetti = true;
            deepCave.SpaghettiScale = 0.012f;          // Very smooth tunnels
            deepCave.SpaghettiWidth = 0.05f;           // Traversable width
            deepCave.SpaghettiMinDepth = 300f;
            deepCave.SpaghettiMaxDepth = 1200f;
            deepCave.EnableNoodles = true;
            deepCave.NoodleScale = 0.008f;             // Major highways
            deepCave.NoodleWidth = 0.08f;              // Wide passages
            deepCave.NoodleMinDepth = 400f;
            deepCave.NoodleMaxDepth = 1500f;
            deepCave.EnableCaverns = true;
            deepCave.CavernScale = 0.01f;              // Huge caverns
            deepCave.CavernThreshold = 0.78f;          // More common at depth
            deepCave.CavernMinDepth = 500f;
            deepCave.EnableVerticalShafts = true;
            deepCave.ShaftFrequency = 0.003f;          // Rarer, deliberate shafts
            deepCave.ShaftRadius = 10f;                // Larger shafts
            AssetDatabase.CreateAsset(deepCave, $"{FOLDER_PATH}/CaveProfile_Deep.asset");
            
            UnityEngine.Debug.Log("[CaveQuickSetup] Created 3 sample cave profiles");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create Sample Hollow Profiles", false, MENU_PRIORITY + 2)]
        public static void CreateSampleHollowProfiles()
        {
            CreateOutputFolder();
            
            // Mushroom Forest (entry hollow)
            var mushroom = ScriptableObject.CreateInstance<HollowEarthProfile>();
            mushroom.BiomeName = "Mushroom Forest";
            mushroom.BiomeColor = new Color(0.6f, 0.4f, 0.8f);
            mushroom.AverageHeight = 500f;
            mushroom.HeightVariation = 50f;
            mushroom.FloorWidth = 2000f;
            mushroom.FloorLength = 2000f;
            mushroom.FloorNoiseScale = 0.01f;
            mushroom.FloorAmplitude = 30f;
            mushroom.CeilingNoiseScale = 0.005f;
            mushroom.HasStalactites = true;
            mushroom.StalactiteDensity = 0.3f;
            mushroom.MaxStalactiteLength = 40f;
            mushroom.HasStalagmites = true;
            mushroom.GeneratePillars = true;
            mushroom.PillarFrequency = 0.008f;
            mushroom.MinPillarRadius = 15f;
            mushroom.MaxPillarRadius = 40f;
            mushroom.LightSource = HollowLightingType.Bioluminescence;
            mushroom.AmbientColor = new Color(0.2f, 0.3f, 0.4f);
            mushroom.HasUndergroundLakes = true;
            mushroom.LakeElevation = 15f;
            mushroom.FluidCoverage = 0.2f;
            AssetDatabase.CreateAsset(mushroom, $"{FOLDER_PATH}/HollowProfile_MushroomForest.asset");
            
            // Crystal Cavern
            var crystal = ScriptableObject.CreateInstance<HollowEarthProfile>();
            crystal.BiomeName = "Crystal Cavern";
            crystal.BiomeColor = new Color(0.3f, 0.8f, 0.9f);
            crystal.AverageHeight = 800f;
            crystal.HeightVariation = 80f;
            crystal.FloorWidth = 3000f;
            crystal.FloorLength = 3000f;
            crystal.FloorNoiseScale = 0.008f;
            crystal.FloorAmplitude = 50f;
            crystal.CeilingNoiseScale = 0.004f;
            crystal.HasStalactites = true;
            crystal.StalactiteDensity = 0.5f;
            crystal.MaxStalactiteLength = 80f;
            crystal.HasStalagmites = true;
            crystal.GeneratePillars = true;
            crystal.PillarFrequency = 0.006f;
            crystal.MinPillarRadius = 20f;
            crystal.MaxPillarRadius = 60f;
            crystal.LightSource = HollowLightingType.CrystalLight;
            crystal.AmbientColor = new Color(0.3f, 0.5f, 0.7f);
            crystal.HasCrystalFormations = true;
            crystal.HasUndergroundLakes = true;
            crystal.LakeElevation = 20f;
            crystal.FluidCoverage = 0.15f;
            AssetDatabase.CreateAsset(crystal, $"{FOLDER_PATH}/HollowProfile_CrystalCavern.asset");
            
            // Volcanic Depths
            var volcanic = ScriptableObject.CreateInstance<HollowEarthProfile>();
            volcanic.BiomeName = "Volcanic Depths";
            volcanic.BiomeColor = new Color(0.9f, 0.3f, 0.1f);
            volcanic.AverageHeight = 1000f;
            volcanic.HeightVariation = 100f;
            volcanic.FloorWidth = 4000f;
            volcanic.FloorLength = 4000f;
            volcanic.FloorNoiseScale = 0.006f;
            volcanic.FloorAmplitude = 70f;
            volcanic.CeilingNoiseScale = 0.003f;
            volcanic.HasStalactites = true;
            volcanic.StalactiteDensity = 0.2f;
            volcanic.MaxStalactiteLength = 60f;
            volcanic.HasStalagmites = true;
            volcanic.GeneratePillars = true;
            volcanic.PillarFrequency = 0.005f;
            volcanic.MinPillarRadius = 25f;
            volcanic.MaxPillarRadius = 80f;
            volcanic.LightSource = HollowLightingType.LavaGlow;
            volcanic.AmbientColor = new Color(0.5f, 0.2f, 0.1f);
            volcanic.HasLavaFlows = true;
            volcanic.HasFluidRivers = true;
            volcanic.RiverWidth = 30f;
            volcanic.FluidCoverage = 0.25f;
            volcanic.Temperature = 50f;
            volcanic.FogColor = new Color(0.2f, 0.05f, 0.02f);
            volcanic.FogDensity = 0.03f;
            AssetDatabase.CreateAsset(volcanic, $"{FOLDER_PATH}/HollowProfile_VolcanicDepths.asset");
            
            // The Core (final area)
            var core = ScriptableObject.CreateInstance<HollowEarthProfile>();
            core.BiomeName = "Ancient Core";
            core.BiomeColor = new Color(0.8f, 0.7f, 0.2f);
            core.AverageHeight = 1500f;
            core.HeightVariation = 150f;
            core.FloorWidth = 5000f;
            core.FloorLength = 5000f;
            core.FloorNoiseScale = 0.004f;
            core.FloorAmplitude = 100f;
            core.CeilingNoiseScale = 0.002f;
            core.HasStalactites = true;
            core.StalactiteDensity = 0.4f;
            core.MaxStalactiteLength = 150f;
            core.HasStalagmites = true;
            core.GeneratePillars = true;
            core.PillarFrequency = 0.003f;
            core.MinPillarRadius = 40f;
            core.MaxPillarRadius = 120f;
            core.LightSource = HollowLightingType.ArtificialSun;
            core.AmbientColor = new Color(0.4f, 0.4f, 0.3f);
            core.HasFloatingIslands = true;
            core.HasCrystalFormations = true;
            AssetDatabase.CreateAsset(core, $"{FOLDER_PATH}/HollowProfile_AncientCore.asset");
            
            UnityEngine.Debug.Log("[CaveQuickSetup] Created 4 sample hollow earth profiles");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create Sample World Layers", false, MENU_PRIORITY + 3)]
        public static void CreateSampleWorldLayers()
        {
            CreateOutputFolder();
            
            // Load cave/hollow profiles if they exist
            var starterCave = AssetDatabase.LoadAssetAtPath<CaveProfile>($"{FOLDER_PATH}/CaveProfile_Starter.asset");
            var standardCave = AssetDatabase.LoadAssetAtPath<CaveProfile>($"{FOLDER_PATH}/CaveProfile_Standard.asset");
            var deepCave = AssetDatabase.LoadAssetAtPath<CaveProfile>($"{FOLDER_PATH}/CaveProfile_Deep.asset");
            
            var mushroom = AssetDatabase.LoadAssetAtPath<HollowEarthProfile>($"{FOLDER_PATH}/HollowProfile_MushroomForest.asset");
            var crystal = AssetDatabase.LoadAssetAtPath<HollowEarthProfile>($"{FOLDER_PATH}/HollowProfile_CrystalCavern.asset");
            var volcanic = AssetDatabase.LoadAssetAtPath<HollowEarthProfile>($"{FOLDER_PATH}/HollowProfile_VolcanicDepths.asset");
            var core = AssetDatabase.LoadAssetAtPath<HollowEarthProfile>($"{FOLDER_PATH}/HollowProfile_AncientCore.asset");
            
            // Solid 1: Entry Layer
            var solid1 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            solid1.LayerName = "Entry Caves";
            solid1.LayerIndex = 0;
            solid1.Type = LayerType.Solid;
            solid1.TopDepth = 0f;
            solid1.BottomDepth = -400f;
            solid1.AreaWidth = 2000f;
            solid1.AreaLength = 2000f;
            solid1.CaveProfile = starterCave;
            solid1.TargetPlaytimeMinutes = 45f;
            solid1.DifficultyMultiplier = 1f;
            solid1.DebugColor = new Color(0.5f, 0.4f, 0.3f);
            AssetDatabase.CreateAsset(solid1, $"{FOLDER_PATH}/Layer_01_EntryCaves.asset");
            
            // Hollow 1: Mushroom Forest
            var hollow1 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            hollow1.LayerName = "Mushroom Forest";
            hollow1.LayerIndex = 1;
            hollow1.Type = LayerType.Hollow;
            hollow1.TopDepth = -400f;
            hollow1.BottomDepth = -900f;
            hollow1.AreaWidth = 2000f;
            hollow1.AreaLength = 2000f;
            hollow1.HollowProfile = mushroom;
            hollow1.TargetPlaytimeMinutes = 45f;
            hollow1.DifficultyMultiplier = 1.2f;
            hollow1.DebugColor = new Color(0.6f, 0.4f, 0.8f);
            AssetDatabase.CreateAsset(hollow1, $"{FOLDER_PATH}/Layer_02_MushroomForest.asset");
            
            // Solid 2: Standard Caves
            var solid2 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            solid2.LayerName = "Deep Mines";
            solid2.LayerIndex = 2;
            solid2.Type = LayerType.Solid;
            solid2.TopDepth = -900f;
            solid2.BottomDepth = -1300f;
            solid2.AreaWidth = 3000f;
            solid2.AreaLength = 3000f;
            solid2.CaveProfile = standardCave;
            solid2.TargetPlaytimeMinutes = 30f;
            solid2.DifficultyMultiplier = 1.5f;
            solid2.DebugColor = new Color(0.4f, 0.4f, 0.5f);
            AssetDatabase.CreateAsset(solid2, $"{FOLDER_PATH}/Layer_03_DeepMines.asset");
            
            // Hollow 2: Crystal Cavern
            var hollow2 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            hollow2.LayerName = "Crystal Cavern";
            hollow2.LayerIndex = 3;
            hollow2.Type = LayerType.Hollow;
            hollow2.TopDepth = -1300f;
            hollow2.BottomDepth = -2100f;
            hollow2.AreaWidth = 3000f;
            hollow2.AreaLength = 3000f;
            hollow2.HollowProfile = crystal;
            hollow2.TargetPlaytimeMinutes = 60f;
            hollow2.DifficultyMultiplier = 1.8f;
            hollow2.DebugColor = new Color(0.3f, 0.8f, 0.9f);
            AssetDatabase.CreateAsset(hollow2, $"{FOLDER_PATH}/Layer_04_CrystalCavern.asset");
            
            // Solid 3: Deep Caves
            var solid3 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            solid3.LayerName = "Abyssal Tunnels";
            solid3.LayerIndex = 4;
            solid3.Type = LayerType.Solid;
            solid3.TopDepth = -2100f;
            solid3.BottomDepth = -2500f;
            solid3.AreaWidth = 4000f;
            solid3.AreaLength = 4000f;
            solid3.CaveProfile = deepCave;
            solid3.TargetPlaytimeMinutes = 30f;
            solid3.DifficultyMultiplier = 2f;
            solid3.DebugColor = new Color(0.3f, 0.3f, 0.4f);
            AssetDatabase.CreateAsset(solid3, $"{FOLDER_PATH}/Layer_05_AbyssalTunnels.asset");
            
            // Hollow 3: Volcanic Depths
            var hollow3 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            hollow3.LayerName = "Volcanic Depths";
            hollow3.LayerIndex = 5;
            hollow3.Type = LayerType.Hollow;
            hollow3.TopDepth = -2500f;
            hollow3.BottomDepth = -3500f;
            hollow3.AreaWidth = 4000f;
            hollow3.AreaLength = 4000f;
            hollow3.HollowProfile = volcanic;
            hollow3.TargetPlaytimeMinutes = 60f;
            hollow3.DifficultyMultiplier = 2.5f;
            hollow3.DebugColor = new Color(0.9f, 0.3f, 0.1f);
            AssetDatabase.CreateAsset(hollow3, $"{FOLDER_PATH}/Layer_06_VolcanicDepths.asset");
            
            // Solid 4: Core approach
            var solid4 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            solid4.LayerName = "Core Approach";
            solid4.LayerIndex = 6;
            solid4.Type = LayerType.Solid;
            solid4.TopDepth = -3500f;
            solid4.BottomDepth = -4000f;
            solid4.AreaWidth = 5000f;
            solid4.AreaLength = 5000f;
            solid4.CaveProfile = deepCave;
            solid4.TargetPlaytimeMinutes = 30f;
            solid4.DifficultyMultiplier = 2.8f;
            solid4.DebugColor = new Color(0.2f, 0.2f, 0.3f);
            AssetDatabase.CreateAsset(solid4, $"{FOLDER_PATH}/Layer_07_CoreApproach.asset");
            
            // Hollow 4: The Core
            var hollow4 = ScriptableObject.CreateInstance<WorldLayerDefinition>();
            hollow4.LayerName = "Ancient Core";
            hollow4.LayerIndex = 7;
            hollow4.Type = LayerType.Hollow;
            hollow4.TopDepth = -4000f;
            hollow4.BottomDepth = -5500f;
            hollow4.AreaWidth = 5000f;
            hollow4.AreaLength = 5000f;
            hollow4.HollowProfile = core;
            hollow4.TargetPlaytimeMinutes = 90f;
            hollow4.DifficultyMultiplier = 3f;
            hollow4.DebugColor = new Color(0.8f, 0.7f, 0.2f);
            AssetDatabase.CreateAsset(hollow4, $"{FOLDER_PATH}/Layer_08_AncientCore.asset");
            
            UnityEngine.Debug.Log("[CaveQuickSetup] Created 8 sample world layer definitions");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Create World Structure Config", false, MENU_PRIORITY + 4)]
        public static void CreateWorldStructureConfig()
        {
            CreateOutputFolder();
            
            var config = ScriptableObject.CreateInstance<WorldStructureConfig>();
            config.WorldSeed = 12345;
            config.GroundLevel = 0f;
            config.LayersAboveToLoad = 1;
            config.LayersBelowToLoad = 1;
            config.HorizontalViewDistance = 256f;
            
            // Load existing layer assets
            var layers = new WorldLayerDefinition[8];
            layers[0] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_01_EntryCaves.asset");
            layers[1] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_02_MushroomForest.asset");
            layers[2] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_03_DeepMines.asset");
            layers[3] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_04_CrystalCavern.asset");
            layers[4] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_05_AbyssalTunnels.asset");
            layers[5] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_06_VolcanicDepths.asset");
            layers[6] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_07_CoreApproach.asset");
            layers[7] = AssetDatabase.LoadAssetAtPath<WorldLayerDefinition>($"{FOLDER_PATH}/Layer_08_AncientCore.asset");
            
            config.Layers = layers;
            
            AssetDatabase.CreateAsset(config, $"{FOLDER_PATH}/WorldStructureConfig.asset");
            
            // Also create in Resources if it doesn't exist
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            var resourcesCopy = Object.Instantiate(config);
            resourcesCopy.Layers = layers;
            AssetDatabase.CreateAsset(resourcesCopy, $"{resourcesPath}/WorldStructureConfig.asset");
            
            UnityEngine.Debug.Log("[CaveQuickSetup] Created WorldStructureConfig (also copied to Resources)");
        }
        
        #endregion
        
        #region Utility
        
        [MenuItem("DIG/Quick Setup/Generation/Open Cave System Folder", false, MENU_PRIORITY + 10)]
        public static void OpenCaveFolder()
        {
            if (AssetDatabase.IsValidFolder(FOLDER_PATH))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(FOLDER_PATH);
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
            else
            {
                EditorUtility.DisplayDialog("Folder Not Found",
                    $"Cave system folder not found at:\n{FOLDER_PATH}\n\n" +
                    "Run 'Create Complete Cave Setup' first.",
                    "OK");
            }
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Delete All Cave Assets", false, MENU_PRIORITY + 20)]
        public static void DeleteAllCaveAssets()
        {
            if (!EditorUtility.DisplayDialog("Delete Cave Assets?",
                $"This will delete all cave/hollow earth assets in:\n{FOLDER_PATH}\n\n" +
                "This cannot be undone!",
                "Delete", "Cancel"))
                return;
            
            if (AssetDatabase.IsValidFolder(FOLDER_PATH))
            {
                AssetDatabase.DeleteAsset(FOLDER_PATH);
                AssetDatabase.Refresh();
                UnityEngine.Debug.Log("[CaveQuickSetup] Deleted all cave system assets");
            }
        }
        
        private static void CreateOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/DIG_QuickSetup"))
            {
                AssetDatabase.CreateFolder("Assets", "DIG_QuickSetup");
            }
            if (!AssetDatabase.IsValidFolder(FOLDER_PATH))
            {
                AssetDatabase.CreateFolder("Assets/DIG_QuickSetup", "CaveSystem");
            }
        }
        
        #endregion
    }
}
