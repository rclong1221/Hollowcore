using UnityEngine;
using System.Linq;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Central registry of all decorator definitions.
    /// Place in Resources folder for runtime loading.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Decorator Registry")]
    public class DecoratorRegistry : ScriptableObject
    {
        [Header("Decorator Definitions")]
        [Tooltip("All available decorators")]
        public DecoratorDefinition[] Decorators;
        
        [Header("Global Settings")]
        [Tooltip("Maximum decorators per chunk")]
        [Range(1, 100)]
        public int MaxDecoratorsPerChunk = 50;
        
        [Tooltip("Global spawn probability multiplier")]
        [Range(0f, 2f)]
        public float GlobalSpawnMultiplier = 1f;
        
        [Tooltip("Enable decorator spawning")]
        public bool EnableDecorators = true;
        
        /// <summary>
        /// Get decorator by ID.
        /// </summary>
        public DecoratorDefinition GetDecorator(byte id)
        {
            if (Decorators == null) return null;
            
            foreach (var dec in Decorators)
            {
                if (dec != null && dec.DecoratorID == id)
                    return dec;
            }
            return null;
        }
        
        /// <summary>
        /// Get all decorators for a specific surface type.
        /// </summary>
        public DecoratorDefinition[] GetDecoratorsForSurface(SurfaceType surface)
        {
            if (Decorators == null) return System.Array.Empty<DecoratorDefinition>();
            
            return Decorators.Where(d => d != null && d.RequiredSurface == surface).ToArray();
        }
        
#if UNITY_EDITOR
        [ContextMenu("Auto Populate")]
        public void AutoPopulate()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DecoratorDefinition");
            Decorators = guids
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<DecoratorDefinition>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .OrderBy(d => d.DecoratorID)
                .ToArray();
            
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"[DecoratorRegistry] Found {Decorators.Length} decorators");
        }
        
        [ContextMenu("Validate IDs")]
        public void ValidateIDs()
        {
            if (Decorators == null) return;
            
            var usedIDs = new System.Collections.Generic.HashSet<byte>();
            foreach (var dec in Decorators)
            {
                if (dec == null) continue;
                
                if (dec.DecoratorID == 0)
                {
                    UnityEngine.Debug.LogWarning($"[DecoratorRegistry] {dec.name} has ID 0 (reserved)");
                }
                else if (usedIDs.Contains(dec.DecoratorID))
                {
                    UnityEngine.Debug.LogError($"[DecoratorRegistry] Duplicate ID {dec.DecoratorID}: {dec.name}");
                }
                else
                {
                    usedIDs.Add(dec.DecoratorID);
                }
                
                if (dec.Prefab == null)
                {
                    UnityEngine.Debug.LogWarning($"[DecoratorRegistry] {dec.name} has no prefab assigned");
                }
            }
        }
#endif
    }
}
