using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DIG.Voxel.Authoring;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor tools for setting up VoxelWorldAuthoring in scenes.
    /// Provides menu items and validation for the voxel world singleton gate.
    /// </summary>
    public static class VoxelWorldSetup
    {
        // ─────────────────────────────────────────────
        //  1) Add Voxel World (gate only, no overrides)
        // ─────────────────────────────────────────────

        [MenuItem("DIG/Voxel World/Add Voxel World to Scene", priority = 0)]
        static void AddVoxelWorldToScene()
        {
            if (FindExistingAuthoring() != null)
            {
                EditorUtility.DisplayDialog("Voxel World Already Exists",
                    "This scene already has a VoxelWorldAuthoring component.\n\n" +
                    "Only one is needed per scene.",
                    "OK");
                Selection.activeGameObject = FindExistingAuthoring().gameObject;
                return;
            }

            var go = CreateVoxelWorldGameObject(overrideSettings: false);

            EditorUtility.DisplayDialog("Voxel World Added",
                "Created 'Voxel World' GameObject with VoxelWorldAuthoring.\n\n" +
                "This enables voxel terrain generation for this scene\n" +
                "using the global Resources/ configuration.\n\n" +
                "Enter Play Mode to see voxel terrain generate.",
                "OK");

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        // ─────────────────────────────────────────────
        //  2) Add Voxel World with Config Overrides
        // ─────────────────────────────────────────────

        [MenuItem("DIG/Voxel World/Add Voxel World with Config Overrides", priority = 1)]
        static void AddVoxelWorldWithOverrides()
        {
            if (FindExistingAuthoring() != null)
            {
                var existing = FindExistingAuthoring();
                if (!existing.OverrideSettings)
                {
                    existing.OverrideSettings = true;
                    EditorUtility.SetDirty(existing);
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                    Selection.activeGameObject = existing.gameObject;

                    EditorUtility.DisplayDialog("Override Settings Enabled",
                        "Enabled Override Settings on the existing VoxelWorldAuthoring.\n\n" +
                        "You can now customize Seed, GroundLevel, ViewDistance,\n" +
                        "and feature toggles in the Inspector.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Already Configured",
                        "This scene already has a VoxelWorldAuthoring with overrides enabled.\n\n" +
                        "Edit the settings in the Inspector.",
                        "OK");
                    Selection.activeGameObject = existing.gameObject;
                }
                return;
            }

            var go = CreateVoxelWorldGameObject(overrideSettings: true);

            EditorUtility.DisplayDialog("Voxel World Added (with Overrides)",
                "Created 'Voxel World' GameObject with per-scene config overrides.\n\n" +
                "Customize settings in the Inspector:\n" +
                "  - Seed, GroundLevel, ViewDistance\n" +
                "  - TerrainNoiseScale, TerrainNoiseAmplitude\n" +
                "  - Feature toggles (Ores, Strata, Caves, Biomes)\n\n" +
                "Enter Play Mode to see voxel terrain generate.",
                "OK");

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        // ─────────────────────────────────────────────
        //  3) Remove Voxel World from Scene
        // ─────────────────────────────────────────────

        [MenuItem("DIG/Voxel World/Remove Voxel World from Scene", priority = 20)]
        static void RemoveVoxelWorldFromScene()
        {
            var existing = FindExistingAuthoring();
            if (existing == null)
            {
                EditorUtility.DisplayDialog("No Voxel World Found",
                    "This scene does not have a VoxelWorldAuthoring component.",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Remove Voxel World?",
                "This will delete the Voxel World GameObject and disable\n" +
                "voxel generation for this scene.\n\n" +
                "This cannot be undone.",
                "Remove", "Cancel"))
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                UnityEngine.Debug.Log("[VoxelWorldSetup] Removed VoxelWorldAuthoring from scene.");
            }
        }

        // ─────────────────────────────────────────────
        //  4) Validate
        // ─────────────────────────────────────────────

        [MenuItem("DIG/Voxel World/Validate Scene Setup", priority = 40)]
        static void ValidateSceneSetup()
        {
            var authoring = FindExistingAuthoring();

            if (authoring == null)
            {
                EditorUtility.DisplayDialog("Voxel World: Not Configured",
                    "No VoxelWorldAuthoring found in this scene.\n\n" +
                    "Voxel terrain will NOT generate in this scene.\n" +
                    "Use 'DIG > Voxel World > Add Voxel World to Scene' to enable it.",
                    "OK");
                return;
            }

            string status = "VoxelWorldAuthoring found.\n\n";
            status += $"Override Settings: {(authoring.OverrideSettings ? "Yes" : "No (using global Resources/)")}\n";

            if (authoring.OverrideSettings)
            {
                status += $"  Seed: {authoring.Seed}\n";
                status += $"  GroundLevel: {authoring.GroundLevel}\n";
                status += $"  ViewDistance: {authoring.ViewDistance} chunks\n";
                status += $"  Ores: {authoring.EnableOres}, Strata: {authoring.EnableStrata}\n";
                status += $"  Caves: {authoring.EnableCaves}, Biomes: {authoring.EnableBiomes}\n";
            }

            status += "\nVoxel terrain will generate when you enter Play Mode.";

            EditorUtility.DisplayDialog("Voxel World: Validation", status, "OK");
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        private static VoxelWorldAuthoring FindExistingAuthoring()
        {
            return Object.FindFirstObjectByType<VoxelWorldAuthoring>();
        }

        private static GameObject CreateVoxelWorldGameObject(bool overrideSettings)
        {
            var go = new GameObject("Voxel World");
            var authoring = go.AddComponent<VoxelWorldAuthoring>();
            authoring.OverrideSettings = overrideSettings;

            Undo.RegisterCreatedObjectUndo(go, "Add Voxel World");
            EditorSceneManager.MarkSceneDirty(go.scene);

            UnityEngine.Debug.Log($"[VoxelWorldSetup] Created VoxelWorldAuthoring (overrides={overrideSettings})");
            return go;
        }
    }
}
