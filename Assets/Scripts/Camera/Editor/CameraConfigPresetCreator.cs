using UnityEditor;
using UnityEngine;
using System.IO;

namespace DIG.CameraSystem.Editor
{
    /// <summary>
    /// Editor utility for creating camera configuration preset assets.
    /// Provides menu items and methods to create standard camera configs.
    /// </summary>
    public static class CameraConfigPresetCreator
    {
        private const string CONFIG_FOLDER = "Assets/Resources/CameraConfigs";

        /// <summary>
        /// Ensure the config folder exists.
        /// </summary>
        private static void EnsureConfigFolder()
        {
            if (!AssetDatabase.IsValidFolder(CONFIG_FOLDER))
            {
                // Create parent folders if needed
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                AssetDatabase.CreateFolder("Assets/Resources", "CameraConfigs");
            }
        }

        /// <summary>
        /// Create and save a CameraConfig asset.
        /// </summary>
        private static CameraConfig CreateAndSaveConfig(CameraConfig config, string fileName)
        {
            EnsureConfigFolder();

            string path = $"{CONFIG_FOLDER}/{fileName}.asset";

            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<CameraConfig>(path);
            if (existing != null)
            {
                Debug.Log($"[CameraConfigPresetCreator] Config already exists at {path}");
                return existing;
            }

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CameraConfigPresetCreator] Created config at {path}");
            return config;
        }

        // ============================================================
        // MENU ITEMS
        // ============================================================

        [MenuItem("DIG/Camera/Create Preset Configs/All Presets")]
        public static void CreateAllPresets()
        {
            CreateDIGPreset();
            CreateARPGPreset();
            CreateTopDownPreset();
            CreateRotatableIsometricPreset();

            Debug.Log("[CameraConfigPresetCreator] Created all camera config presets.");
            EditorUtility.DisplayDialog("Camera Configs Created",
                "Created all camera configuration presets in:\n" + CONFIG_FOLDER,
                "OK");
        }

        [MenuItem("DIG/Camera/Create Preset Configs/DIG (Third-Person)")]
        public static CameraConfig CreateDIGPreset()
        {
            var config = CameraConfig.CreateDIGPreset();
            return CreateAndSaveConfig(config, "CameraConfig_DIG");
        }

        [MenuItem("DIG/Camera/Create Preset Configs/ARPG (Isometric)")]
        public static CameraConfig CreateARPGPreset()
        {
            var config = CameraConfig.CreateARPGPreset();
            return CreateAndSaveConfig(config, "CameraConfig_ARPG");
        }

        [MenuItem("DIG/Camera/Create Preset Configs/Top-Down")]
        public static CameraConfig CreateTopDownPreset()
        {
            var config = CameraConfig.CreateTopDownPreset();
            return CreateAndSaveConfig(config, "CameraConfig_TopDown");
        }

        [MenuItem("DIG/Camera/Create Preset Configs/Rotatable Isometric")]
        public static CameraConfig CreateRotatableIsometricPreset()
        {
            var config = CameraConfig.CreateRotatableIsometricPreset();
            return CreateAndSaveConfig(config, "CameraConfig_RotatableIso");
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Get or create a DIG preset config.
        /// </summary>
        public static CameraConfig GetOrCreateDIGPreset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraConfig>($"{CONFIG_FOLDER}/CameraConfig_DIG.asset");
            if (existing != null) return existing;
            return CreateDIGPreset();
        }

        /// <summary>
        /// Get or create an ARPG preset config.
        /// </summary>
        public static CameraConfig GetOrCreateARPGPreset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraConfig>($"{CONFIG_FOLDER}/CameraConfig_ARPG.asset");
            if (existing != null) return existing;
            return CreateARPGPreset();
        }

        /// <summary>
        /// Get or create a Top-Down preset config.
        /// </summary>
        public static CameraConfig GetOrCreateTopDownPreset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraConfig>($"{CONFIG_FOLDER}/CameraConfig_TopDown.asset");
            if (existing != null) return existing;
            return CreateTopDownPreset();
        }

        /// <summary>
        /// Get or create a Rotatable Isometric preset config.
        /// </summary>
        public static CameraConfig GetOrCreateRotatableIsometricPreset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraConfig>($"{CONFIG_FOLDER}/CameraConfig_RotatableIso.asset");
            if (existing != null) return existing;
            return CreateRotatableIsometricPreset();
        }

        /// <summary>
        /// Get the config folder path.
        /// </summary>
        public static string GetConfigFolder() => CONFIG_FOLDER;
    }
}
