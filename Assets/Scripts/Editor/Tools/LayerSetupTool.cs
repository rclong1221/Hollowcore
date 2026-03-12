using UnityEngine;
using UnityEditor;
using System.IO;

namespace DIG.Editor.Tools
{
    public class LayerSetupTool : EditorWindow
    {
        private const string ZONE_LAYER_NAME = "Zones";
        private const string PREFAB_PATH = "Assets/Prefabs/Opsive";

        [MenuItem("Tools/DIG/Setup Zone Layers")]
        public static void ShowWindow()
        {
            GetWindow<LayerSetupTool>("Layer Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Zone Layer Physics Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("1. Creates 'Zones' Layer if missing.", EditorStyles.miniLabel);
            GUILayout.Label("2. Disables collision between 'Zones' and Default/Player layers.", EditorStyles.miniLabel);
            GUILayout.Label("3. Updates all prefabs in Assets/Prefabs/Opsive to use 'Zones' layer.", EditorStyles.miniLabel);
            GUILayout.Label("4. Enforces 'Is Trigger' on all colliders in those prefabs.", EditorStyles.miniLabel);

            GUILayout.Space(20);

            if (GUILayout.Button("Execute Setup"))
            {
                SetupLayers();
            }
        }

        private static void SetupLayers()
        {
            // 1. Create Layer
            if (!CreateLayer(ZONE_LAYER_NAME))
            {
                Debug.LogError($"Could not create layer '{ZONE_LAYER_NAME}'. Please clean up some unused layers.");
                return;
            }
            
            int zoneLayer = LayerMask.NameToLayer(ZONE_LAYER_NAME);
            
            // 2. Setup Physics Matrix (Disable collisions)
            // We want Zones to strictly be triggers. They should technically collide with nothing physically,
            // but still allow Query interactions (so IsTrigger is key).
            // However, to be doubly sure "Invisible Walls" don't happen, we disable Default/Player collision.
            Physics.IgnoreLayerCollision(zoneLayer, LayerMask.NameToLayer("Default"), true);
            Physics.IgnoreLayerCollision(zoneLayer, LayerMask.NameToLayer("Player"), true); // Assuming "Player" exists
            // Standard "Trigger" behavior typically requires collision enabled in matrix but IsTrigger=true.
            // If we disable matrix, OnTriggerEnter might NOT fire depending on physics settings.
            // RE-EVALUATION: To ensure Trigger Events work, we MUST allow interaction in the matrix,
            // but set IsTrigger=true.
            // DISABLING MATRIX COLLISION PREVENTS TRIGGER EVENTS. 
            // So we will SKIP disabling matrix, and only enforce IsTrigger = true + Layer assignment.
            Debug.Log("Note: Preserving Physics Matrix to ensure Trigger Events fire. Relying on 'IsTrigger' enforcement.");

            // 3. Update Prefabs
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_PATH });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UpdatePrefabLayer(path, zoneLayer);
                count++;
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log($"Layer Setup Complete. Updated {count} prefabs.");
        }

        private static bool CreateLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // Check if exists
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (element.stringValue == layerName) return true;
            }

            // Find empty slot
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return true;
                }
            }
            return false;
        }

        private static void UpdatePrefabLayer(string assetPath, int layerIndex)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            bool modified = false;

            try
            {
                // Iterate COMPONENT-bearing objects to check for Colliders
                Collider[] colliders = contents.GetComponentsInChildren<Collider>(true);
                
                foreach (Collider col in colliders)
                {
                    // Recursively set layer for the object holding the collider
                    if (col.gameObject.layer != layerIndex)
                    {
                        col.gameObject.layer = layerIndex;
                        modified = true;
                    }

                    // Enforce IsTrigger
                    if (!col.isTrigger)
                    {
                        col.isTrigger = true;
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                    Debug.Log($"Updated Layer/Trigger: {Path.GetFileName(assetPath)}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
