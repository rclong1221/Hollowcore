using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DIG.Voxel.Core;

namespace DIG.Voxel.Editor
{
    public class MaterialValidator : EditorWindow
    {
        [MenuItem("DIG/Voxel/Validate Materials")]
        static void Validate()
        {
            var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (registry == null)
            {
                // Try to find it by type if exact path fails
                var guids = AssetDatabase.FindAssets("t:VoxelMaterialRegistry");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    registry = AssetDatabase.LoadAssetAtPath<VoxelMaterialRegistry>(path);
                }
            }
            
            if (registry == null)
            {
                UnityEngine.Debug.LogError("[Validator] VoxelMaterialRegistry not found in Resources or Assets!");
                return;
            }
            
            UnityEngine.Debug.Log($"[Validator] Validating registry: {registry.name}");
            
            var errors = new List<string>();
            var warnings = new List<string>();
            var usedIds = new HashSet<byte>();
            
            foreach (var mat in registry.Materials)
            {
                if (mat == null)
                {
                    errors.Add("Null entry in registry material list");
                    continue;
                }
                
                // Duplicate ID check
                if (usedIds.Contains(mat.MaterialID))
                    errors.Add($"{mat.name}: Duplicate MaterialID {mat.MaterialID}");
                usedIds.Add(mat.MaterialID);
                
                // Missing loot
                if (mat.IsMineable && mat.LootPrefab == null)
                    warnings.Add($"{mat.name}: Mineable but no LootPrefab assigned");
                
                // Texture check (if applicable)
                // Assuming Materials use standard textures
            }
            
            // Check AIR material (ID 0)
            if (!usedIds.Contains(0))
            {
                errors.Add("Missing MaterialID 0 (Air)");
            }
            
            // Report
            if (errors.Count == 0 && warnings.Count == 0)
            {
                UnityEngine.Debug.Log($"[Validator] ✅ All {registry.Materials.Count} materials valid!");
            }
            else
            {
                foreach (var e in errors)
                    UnityEngine.Debug.LogError($"[Validator] ❌ {e}");
                foreach (var w in warnings)
                    UnityEngine.Debug.LogWarning($"[Validator] ⚠️ {w}");
            }
        }
    }
}
