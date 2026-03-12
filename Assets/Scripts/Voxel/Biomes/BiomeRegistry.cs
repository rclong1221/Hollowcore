using UnityEngine;
using System.Linq;

namespace DIG.Voxel.Biomes
{
    /// <summary>
    /// Registry of all defined biomes in the world.
    /// Used to initialize the BiomeService.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Biome Registry")]
    public class BiomeRegistry : ScriptableObject
    {
        [Header("Biome Definitions")]
        public BiomeDefinition[] Biomes;
        
        [Header("Global Settings")]
        [Tooltip("Scale of temperature/humidity noise")]
        public float GlobalNoiseScale = 0.001f;
        
        [Tooltip("Default biome when no conditions match")]
        public BiomeDefinition FallbackBiome;
        
        [ContextMenu("Auto Populate")]
        public void AutoPopulate()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BiomeDefinition");
            Biomes = guids
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<BiomeDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(b => b.BiomeID)
                .ToArray();
            
            if (FallbackBiome == null && Biomes.Length > 0)
                FallbackBiome = Biomes[0];
                
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
