using UnityEngine;
using UnityEditor;
using DIG.Voxel.Fluids;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Quick setup menu items for Fluid systems.
    /// Creates sample fluid definitions for testing.
    /// </summary>
    public static class FluidQuickSetup
    {
        private const string FOLDER_PATH = "Assets/Resources/Fluids";
        private const int MENU_PRIORITY = 200;
        
        [MenuItem("DIG/Quick Setup/Generation/Create Fluid Definitions", false, MENU_PRIORITY)]
        public static void CreateFluidDefinitions()
        {
            // Check for existing assets and warn user
            var existingRegistry = AssetDatabase.LoadAssetAtPath<FluidRegistry>($"{FOLDER_PATH}/FluidRegistry.asset");
            var existingAssets = AssetDatabase.FindAssets("t:FluidDefinition", new[] { FOLDER_PATH });

            if (existingRegistry != null || existingAssets.Length > 0)
            {
                string warning = "Existing fluid assets found:\n\n";

                if (existingRegistry != null)
                {
                    int fluidCount = existingRegistry.Fluids?.Length ?? 0;
                    warning += $"• FluidRegistry.asset ({fluidCount} fluids)\n";
                }

                foreach (var guid in existingAssets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = System.IO.Path.GetFileName(path);
                    warning += $"• {fileName}\n";
                }

                warning += "\nThis will OVERWRITE all existing fluid assets. Continue?";

                if (!EditorUtility.DisplayDialog("Overwrite Existing Fluids?", warning, "Overwrite", "Cancel"))
                {
                    return;
                }
            }
            else
            {
                // No existing assets - confirm creation
                if (!EditorUtility.DisplayDialog("Create Fluid Definitions?",
                    "This will create 5 fluid definitions and a FluidRegistry:\n\n" +
                    "• Water (ID 1)\n" +
                    "• Oil (ID 2)\n" +
                    "• Lava (ID 3)\n" +
                    "• Toxic Gas (ID 4)\n" +
                    "• Acid (ID 5)\n\n" +
                    $"Location: {FOLDER_PATH}",
                    "Create", "Cancel"))
                {
                    return;
                }
            }

            CreateOutputFolder();
            
            // Water
            var water = ScriptableObject.CreateInstance<FluidDefinition>();
            water.FluidID = 1;
            water.FluidName = "Water";
            water.Type = FluidType.Water;
            water.FluidColor = new Color(0.2f, 0.4f, 0.8f, 0.6f);
            water.Transparency = 0.6f;
            water.Reflectivity = 0.4f;
            water.Viscosity = 1f;
            water.Density = 1f;
            water.SpreadRate = 8;
            water.DamageType = FluidDamageType.Drowning;
            water.DamagePerSecond = 5f;
            water.DamageStartDepth = 2f;
            AssetDatabase.CreateAsset(water, $"{FOLDER_PATH}/Fluid_Water.asset");
            
            // Lava
            var lava = ScriptableObject.CreateInstance<FluidDefinition>();
            lava.FluidID = 3;
            lava.FluidName = "Lava";
            lava.Type = FluidType.Lava;
            lava.FluidColor = new Color(1f, 0.3f, 0.1f, 0.9f);
            lava.Transparency = 0.1f;
            lava.Reflectivity = 0.2f;
            lava.IsEmissive = true;
            lava.EmissionColor = new Color(2f, 0.5f, 0.1f);
            lava.Viscosity = 0.1f;
            lava.Density = 3f;
            lava.SpreadRate = 1;
            lava.DamageType = FluidDamageType.Burning;
            lava.DamagePerSecond = 50f;
            lava.DamageStartDepth = 0f;
            lava.CoolsToSolid = true;
            lava.CooledMaterialID = 10;  // Obsidian
            lava.CoolingTemperature = 500f;
            AssetDatabase.CreateAsset(lava, $"{FOLDER_PATH}/Fluid_Lava.asset");
            
            // Oil
            var oil = ScriptableObject.CreateInstance<FluidDefinition>();
            oil.FluidID = 2;
            oil.FluidName = "Oil";
            oil.Type = FluidType.Oil;
            oil.FluidColor = new Color(0.1f, 0.08f, 0.05f, 0.8f);
            oil.Transparency = 0.2f;
            oil.Reflectivity = 0.6f;
            oil.Viscosity = 0.5f;
            oil.Density = 0.8f;
            oil.SpreadRate = 4;
            oil.IsPressurized = true;
            oil.PressureLevel = 5f;
            oil.EruptionRadius = 15f;
            oil.IsFlammable = true;
            oil.DamageType = FluidDamageType.None;
            AssetDatabase.CreateAsset(oil, $"{FOLDER_PATH}/Fluid_Oil.asset");
            
            // Toxic Gas
            var gas = ScriptableObject.CreateInstance<FluidDefinition>();
            gas.FluidID = 4;
            gas.FluidName = "Toxic Gas";
            gas.Type = FluidType.ToxicGas;
            gas.FluidColor = new Color(0.3f, 0.6f, 0.2f, 0.4f);
            gas.Transparency = 0.7f;
            gas.Reflectivity = 0f;
            gas.Viscosity = 3f;
            gas.Density = 0.5f;
            gas.SpreadRate = 16;
            gas.DamageType = FluidDamageType.Toxic;
            gas.DamagePerSecond = 10f;
            gas.DamageStartDepth = 0f;
            gas.IsToxic = true;
            AssetDatabase.CreateAsset(gas, $"{FOLDER_PATH}/Fluid_ToxicGas.asset");
            
            // Acid
            var acid = ScriptableObject.CreateInstance<FluidDefinition>();
            acid.FluidID = 5;
            acid.FluidName = "Acid";
            acid.Type = FluidType.Acid;
            acid.FluidColor = new Color(0.5f, 0.9f, 0.2f, 0.7f);
            acid.Transparency = 0.3f;
            acid.Reflectivity = 0.5f;
            acid.IsEmissive = true;
            acid.EmissionColor = new Color(0.3f, 0.5f, 0.1f);
            acid.Viscosity = 0.8f;
            acid.Density = 1.2f;
            acid.SpreadRate = 6;
            acid.DamageType = FluidDamageType.Corrosive;
            acid.DamagePerSecond = 25f;
            acid.DamageStartDepth = 0f;
            AssetDatabase.CreateAsset(acid, $"{FOLDER_PATH}/Fluid_Acid.asset");

            // Create the FluidRegistry and populate it with all definitions
            var registry = ScriptableObject.CreateInstance<FluidRegistry>();
            registry.Fluids = new FluidDefinition[] { water, oil, lava, gas, acid };
            registry.GlobalWaterLevel = 0f;
            AssetDatabase.CreateAsset(registry, $"{FOLDER_PATH}/FluidRegistry.asset");

            AssetDatabase.Refresh();

            UnityEngine.Debug.Log("[FluidQuickSetup] Created 5 fluid definitions and FluidRegistry: Water, Lava, Oil, Toxic Gas, Acid");
            EditorUtility.DisplayDialog("Fluids Created",
                "Created 5 fluid definitions:\n\n" +
                "• Water (ID 1) - Drowning damage\n" +
                "• Oil (ID 2) - Pressurized, flammable\n" +
                "• Lava (ID 3) - Burning damage, cools to solid\n" +
                "• Toxic Gas (ID 4) - Toxic damage\n" +
                "• Acid (ID 5) - Corrosive damage\n\n" +
                $"Saved to: {FOLDER_PATH}",
                "OK");
        }
        
        [MenuItem("DIG/Quick Setup/Generation/Open Fluids Folder", false, MENU_PRIORITY + 1)]
        public static void OpenFluidsFolder()
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
                    $"Fluids folder not found at:\n{FOLDER_PATH}\n\n" +
                    "Run 'Create Fluid Definitions' first.",
                    "OK");
            }
        }
        
        private static void CreateOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(FOLDER_PATH))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Fluids");
            }
        }
    }
}
