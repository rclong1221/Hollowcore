using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DIG.Core.Zones;
using DIG.Core.Spawning;
using DIG.Core.Surfaces;
using DIG.Environment.Platforms;
using Player.Authoring;
using DIG.Player.Abilities;
using DIG.UI.Tutorial;

namespace DIG.Editor.Tools
{
    public class OpsiveMigrationTool : EditorWindow
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Opsive";

        [MenuItem("Tools/DIG/Migrate Opsive Prefabs")]
        public static void ShowWindow()
        {
            GetWindow<OpsiveMigrationTool>("Opsive Migration");
        }

        private void OnGUI()
        {
            GUILayout.Label("Opsive Prefab Migration", EditorStyles.boldLabel);
            GUILayout.Label($"Target Folder: {PREFAB_PATH}", EditorStyles.helpBox);

            if (GUILayout.Button("Migrate All Prefabs"))
            {
                MigratePrefabs();
            }
        }

        private static void MigratePrefabs()
        {
            if (!Directory.Exists(PREFAB_PATH))
            {
                Debug.LogError($"Directory not found: {PREFAB_PATH}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_PATH });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Migrating Prefabs", Path.GetFileName(path), (float)count / guids.Length);
                
                MigrateSinglePrefab(path);
                count++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Debug.Log($"Migration Complete. Processed {count} prefabs.");
        }

        private static void MigrateSinglePrefab(string assetPath)
        {
            bool modified = false;
            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                // Iterate all components in the hierarchy (checking recursively)
                // We use GetComponentsInChildren<Component> to get everything, then check types by name string
                // to avoid compile dependencies on Opsive assemblies if they aren't explicitly referenced.
                
                Component[] allComponents = contents.GetComponentsInChildren<Component>(true);
                
                foreach (Component comp in allComponents)
                {
                    if (comp == null) continue; // Missing script
                    
                    Type type = comp.GetType();
                    string typeName = type.Name;

                    if (typeName == "SphericalGravityZone" || typeName == "GravityZone")
                    {
                        ReplaceGravityZone(comp);
                        modified = true;
                    }
                    else if (typeName == "MovingPlatform")
                    {
                        ReplaceMovingPlatform(comp);
                        modified = true;
                    }
                    else if (typeName == "SpawnPoint")
                    {
                        ReplaceSpawnPoint(comp);
                        modified = true;
                    }
                    else if (typeName == "SurfaceIdentifier")
                    {
                        ReplaceSurfaceIdentifier(comp);
                        modified = true;
                    }
                    else if (typeName == "DemoTextTrigger")
                    {
                        ReplaceDemoTextTrigger(comp);
                        modified = true;
                    }
                    else if (typeName == "StateTrigger")
                    {
                        // opsive's StateTrigger is almost exclusively used for Ability/Attribute zones
                        // Migrate ALL of them to ensure they get the correct Layer/Trigger setup
                        ReplaceAbilityUnlock(comp);
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                    Debug.Log($"Migrated: {assetPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to migrate {assetPath}: {e.Message}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ----------------------------------------------------------------------------------
        // REPLACEMENT HELPERS
        // Using SerializedObject to read legacy values safely
        // ----------------------------------------------------------------------------------

        private static void ReplaceGravityZone(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            SerializedObject so = new SerializedObject(oldComp);
            
            // Try read values
            float radius = 10f;

            SerializedProperty propRadius = so.FindProperty("m_Radius");
            if (propRadius == null) propRadius = so.FindProperty("Radius");
            if (propRadius != null) radius = propRadius.floatValue;

            SerializedProperty propForce = so.FindProperty("m_GravityForce"); // Opsive usually vector or float?
             // Checking common naming... assuming simple mapping for now.
             // If manual check needed, we'll log it.
            
            // Remove old
            DestroyImmediate(oldComp, true);
            
            // Add new
            var newComp = go.AddComponent<DIG.Environment.Gravity.GravityZoneAuthoring>();
            newComp.Radius = radius;
            newComp.Strength = 9.81f; // Default standard gravity
            
            SetLayerAndTrigger(go);
        }

        private static void ReplaceMovingPlatform(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            
            DestroyImmediate(oldComp, true);
            
            // Add WaypointLogic
            var wp = go.AddComponent<WaypointPlatform>();
            wp.Speed = 3f;
            wp.WaitTime = 1f;

            // Add Authoring
            go.AddComponent<MovingPlatformAuthoring>();
        }

        private static void ReplaceSpawnPoint(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            DestroyImmediate(oldComp, true);
            go.AddComponent<SpawnPointAuthoring>();
            SetLayerAndTrigger(go);
        }

        private static void ReplaceSurfaceIdentifier(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            SerializedObject so = new SerializedObject(oldComp);
            
            // Reading Opsive SurfaceType is hard because it's an Object reference or custom struct.
            // We will define based on GameObject Name as a heuristic.
            
            string name = go.name.ToLower();
            DIG.Core.Surfaces.SurfaceType newType = DIG.Core.Surfaces.SurfaceType.Default;
            
            if (name.Contains("wood")) newType = DIG.Core.Surfaces.SurfaceType.Wood;
            else if (name.Contains("metal")) newType = DIG.Core.Surfaces.SurfaceType.Metal;
            else if (name.Contains("stone") || name.Contains("rock")) newType = DIG.Core.Surfaces.SurfaceType.Stone;
            else if (name.Contains("dirt") || name.Contains("grass")) newType = DIG.Core.Surfaces.SurfaceType.Grass;
            else if (name.Contains("water")) newType = DIG.Core.Surfaces.SurfaceType.Water;
            else if (name.Contains("flesh")) newType = DIG.Core.Surfaces.SurfaceType.Flesh;

            DestroyImmediate(oldComp, true);
            
            var newComp = go.AddComponent<SurfaceIdentifierAuthoring>();
            newComp.Type = newType;
        }

        private static void SetLayerAndTrigger(GameObject go)
        {
            // Ensure "Zones" layer exists (LayerSetupTool should have created it, but we can fallback or query)
            int zoneLayer = LayerMask.NameToLayer("Zones");
            if (zoneLayer != -1)
            {
                go.layer = zoneLayer;
            }

            // Force IsTrigger on all colliders
            var cols = go.GetComponents<Collider>();
            foreach (var c in cols) c.isTrigger = true;
        }

        private static void ReplaceDemoTextTrigger(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            SerializedObject so = new SerializedObject(oldComp);
            
            string header = "Tutorial";
            string text = "";
            
            SerializedProperty propHead = so.FindProperty("m_Header");
            if (propHead != null) header = propHead.stringValue;
            
            SerializedProperty propText = so.FindProperty("m_Text");
            if (propText != null) text = propText.stringValue;

            DestroyImmediate(oldComp, true);
            
            var newComp = go.AddComponent<TutorialTriggerAuthoring>();
            newComp.Header = header;
            newComp.Message = text;
            
            SetLayerAndTrigger(go);
        }

        private static void ReplaceAbilityUnlock(Component oldComp)
        {
            GameObject go = oldComp.gameObject;
            DestroyImmediate(oldComp, true);
            
            var newComp = go.AddComponent<AbilityUnlockAuthoring>();
            // Default to jetpack or infer? 
            // Opsive StateTrigger usually sets a state name string. 
            // Hard to map "Jetpack" string to Enum index automatically without robust parsing.
            // Defaulting to 0 (None) or 1 (Jetpack) is safer.
            // We'll leave it for designer to select the enum.
            
            SetLayerAndTrigger(go);
        }
    }
}
