using UnityEngine;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Defines a procedural structure that can spawn in caves or hollow earth.
    /// Structures are larger than decorators and may include voxel data modifications.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Structure Definition")]
    public class StructureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this structure (1-255)")]
        public byte StructureID;
        
        [Tooltip("Display name")]
        public string StructureName;
        
        [Header("Content")]
        [Tooltip("Prefab to instantiate (for visual-only structures)")]
        public GameObject Prefab;
        
        [Tooltip("Size in voxels (for clearance checks)")]
        public Vector3Int Size = new Vector3Int(10, 10, 10);
        
        [Header("Placement")]
        [Tooltip("Chance to spawn per valid location (very low for rare structures)")]
        [Range(0f, 0.01f)]
        public float Rarity = 0.001f;
        
        [Tooltip("Minimum depth (absolute Y value)")]
        public float MinDepth = 50f;
        
        [Tooltip("Maximum depth (absolute Y value)")]
        public float MaxDepth = 5000f;
        
        [Header("Requirements")]
        [Tooltip("Requires open cave space?")]
        public bool RequiresCaveSpace = true;
        
        [Tooltip("Minimum clearance around structure (meters)")]
        [Range(0f, 50f)]
        public float MinClearance = 10f;
        
        [Tooltip("Only spawn in specific biomes (empty = any)")]
        public byte[] AllowedBiomeIDs;
        
        [Header("Hollow Earth Placement")]
        [Tooltip("Spawn on hollow earth floor?")]
        public bool PlaceOnHollowFloor = true;
        
        [Tooltip("Minimum hollow layer height required")]
        [Range(0f, 500f)]
        public float MinHollowHeight = 100f;
        
        [Header("Rotation")]
        [Tooltip("Apply random Y-axis rotation")]
        public bool RandomYRotation = true;
        
        [Tooltip("Rotation snap increment (0 = continuous, 90 = 4 directions)")]
        [Range(0f, 90f)]
        public float RotationSnap = 0f;
        
        /// <summary>
        /// Check if this structure can spawn in the given biome.
        /// </summary>
        public bool CanSpawnInBiome(byte biomeID)
        {
            if (AllowedBiomeIDs == null || AllowedBiomeIDs.Length == 0)
                return true;
            
            foreach (var allowed in AllowedBiomeIDs)
            {
                if (allowed == biomeID)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get random Y rotation respecting snap settings.
        /// </summary>
        public float GetRandomRotation(uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            
            if (!RandomYRotation)
                return 0f;
            
            if (RotationSnap > 0)
            {
                int steps = (int)(360f / RotationSnap);
                return random.NextInt(0, steps) * RotationSnap;
            }
            
            return random.NextFloat() * 360f;
        }
    }
}
