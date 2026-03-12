using UnityEngine;
using UnityEditor;
using DIG.Voxel.Core;
using System.Collections.Generic; // Added for HashSet

namespace DIG.Voxel.Editor
{
    [CustomEditor(typeof(VoxelMaterialDefinition))]
    public class VoxelMaterialEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var material = (VoxelMaterialDefinition)target;
            
            EditorGUILayout.LabelField("Material Preview", EditorStyles.boldLabel);
            
            // Color preview box
            var previewRect = GUILayoutUtility.GetRect(64, 64);
            EditorGUI.DrawRect(previewRect, material.DebugColor);
            
            EditorGUILayout.Space();
            
            // Standard fields
            base.OnInspectorGUI();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Test Spawn Loot"))
            {
                if (Application.isPlaying && material.LootPrefab != null)
                {
                    if (Camera.main != null)
                    {
                         var pos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                         Instantiate(material.LootPrefab, pos, Quaternion.identity);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("No Main Camera found to spawn loot in front of.");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Cannot spawn loot: Not playing or LoopPrefab missing.");
                }
            }
        }
    }
    
    [CustomEditor(typeof(VoxelMaterialRegistry))]
    public class VoxelMaterialRegistryEditor : UnityEditor.Editor
    {
        private bool _showLootSummary = true;

        public override void OnInspectorGUI()
        {
            var registry = (VoxelMaterialRegistry)target;
            
            EditorGUILayout.LabelField("Material Registry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Materials: {registry.Materials?.Count ?? 0}");
            
            EditorGUILayout.Space();
            
            // Quick Actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate IDs"))
            {
                ValidateMaterials(registry);
            }
            if (GUILayout.Button("Auto-Populate"))
            {
                AutoPopulateMaterials(registry);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Loot Summary Foldout
            _showLootSummary = EditorGUILayout.Foldout(_showLootSummary, "Loot Configuration Summary", true);
            if (_showLootSummary && registry.Materials != null)
            {
                DrawLootSummary(registry);
            }
            
            EditorGUILayout.Space();
            
            base.OnInspectorGUI();
        }
        
        private void DrawLootSummary(VoxelMaterialRegistry registry)
        {
            EditorGUI.indentLevel++;
            
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(30));
            EditorGUILayout.LabelField("Loot Prefab", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Drop %", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Count", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            foreach (var mat in registry.Materials)
            {
                if (mat == null) continue;
                
                EditorGUILayout.BeginHorizontal();
                
                // Material Name
                EditorGUILayout.LabelField(mat.MaterialName, GUILayout.Width(100));
                
                // ID
                EditorGUILayout.LabelField(mat.MaterialID.ToString(), GUILayout.Width(30));
                
                // Loot Prefab (with status color)
                var oldColor = GUI.backgroundColor;
                if (mat.LootPrefab == null && mat.IsMineable)
                    GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red
                else if (mat.LootPrefab != null)
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Light green
                
                EditorGUILayout.ObjectField(mat.LootPrefab, typeof(GameObject), false, GUILayout.Width(120));
                GUI.backgroundColor = oldColor;
                
                // Drop Chance
                EditorGUILayout.LabelField($"{mat.DropChance * 100:F0}%", GUILayout.Width(50));
                
                // Count Range
                EditorGUILayout.LabelField($"{mat.MinDropCount}-{mat.MaxDropCount}", GUILayout.Width(60));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void AutoPopulateMaterials(VoxelMaterialRegistry registry)
        {
            var guids = AssetDatabase.FindAssets("t:VoxelMaterialDefinition");
            int added = 0;
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<VoxelMaterialDefinition>(path);
                
                if (mat != null && !registry.Materials.Contains(mat))
                {
                    registry.Materials.Add(mat);
                    added++;
                }
            }
            
            if (added > 0)
            {
                EditorUtility.SetDirty(registry);
                UnityEngine.Debug.Log($"[Material Registry] ✅ Added {added} materials. Total: {registry.Materials.Count}");
            }
            else
            {
                UnityEngine.Debug.Log("[Material Registry] No new materials found to add.");
            }
        }
        
        private void ValidateMaterials(VoxelMaterialRegistry registry)
        {
            if (registry.Materials == null) return;
            
            var usedIds = new HashSet<byte>();
            bool hasErrors = false;
            int missingPrefabs = 0;
            
            foreach (var mat in registry.Materials)
            {
                if (mat == null) continue;
                
                if (usedIds.Contains(mat.MaterialID))
                {
                    UnityEngine.Debug.LogError($"[Material Registry] Duplicate ID: {mat.MaterialID} on {mat.name}");
                    hasErrors = true;
                }
                
                usedIds.Add(mat.MaterialID);
                
                if (mat.LootPrefab == null && mat.IsMineable)
                {
                    UnityEngine.Debug.LogWarning($"[Material Registry] {mat.name} is mineable but has no loot prefab");
                    missingPrefabs++;
                }
            }
            
            if (!hasErrors)
            {
                UnityEngine.Debug.Log($"[Material Registry] ✅ All {registry.Materials.Count} material IDs are unique");
            }
            
            if (missingPrefabs > 0)
            {
                UnityEngine.Debug.LogWarning($"[Material Registry] ⚠️ {missingPrefabs} mineable materials have no loot prefab");
            }
        }
    }
}
