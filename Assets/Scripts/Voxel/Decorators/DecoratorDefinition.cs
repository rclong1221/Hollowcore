using UnityEngine;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Surface type for decorator placement.
    /// </summary>
    public enum SurfaceType : byte
    {
        Floor = 0,
        Ceiling = 1,
        WallNorth = 2,
        WallSouth = 3,
        WallEast = 4,
        WallWest = 5
    }
    
    /// <summary>
    /// OPTIMIZATION 10.5.11: LOD importance for distance-based decorator culling.
    /// </summary>
    public enum DecoratorLODImportance : byte
    {
        /// <summary>Low importance - only spawn within 4 chunks of player.</summary>
        Low = 0,
        /// <summary>Medium importance - spawn within 8 chunks of player.</summary>
        Medium = 1,
        /// <summary>High importance - spawn within 16 chunks of player.</summary>
        High = 2,
        /// <summary>Critical - always spawn regardless of distance (large landmarks).</summary>
        Critical = 3
    }

    /// <summary>
    /// Defines a single decorator type that can spawn in caves and hollow earth.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Decorator Definition")]
    public class DecoratorDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this decorator (1-255)")]
        public byte DecoratorID;
        
        [Tooltip("Display name")]
        public string DecoratorName;
        
        [Header("Prefab")]
        [Tooltip("Main prefab to instantiate")]
        public GameObject Prefab;
        
        [Tooltip("Random variations (optional)")]
        public GameObject[] Variations;
        
        [Header("Placement")]
        [Tooltip("Which surface type this decorator attaches to")]
        public SurfaceType RequiredSurface = SurfaceType.Floor;
        
        [Tooltip("Minimum distance between instances of this decorator")]
        [Range(0.5f, 50f)]
        public float MinSpacing = 2f;
        
        [Tooltip("Spawn chance per valid surface point (0-1)")]
        [Range(0f, 1f)]
        public float SpawnProbability = 0.1f;
        
        [Header("Size Constraints")]
        [Tooltip("Minimum cave/hollow radius to spawn")]
        [Range(1f, 100f)]
        public float MinCaveRadius = 3f;
        
        [Tooltip("Scale decorator based on cave size?")]
        public bool ScaleWithCaveSize = false;
        
        [Range(0.1f, 5f)]
        public float MinScale = 0.8f;
        
        [Range(0.1f, 5f)]
        public float MaxScale = 1.2f;
        
        [Header("Depth Constraints")]
        [Tooltip("Minimum depth (negative Y) where this spawns")]
        public float MinDepth = 10f;
        
        [Tooltip("Maximum depth (negative Y) where this spawns")]
        public float MaxDepth = 5000f;
        
        [Header("Biome Restrictions")]
        [Tooltip("Leave empty for all biomes, or specify allowed biomes")]
        public byte[] AllowedBiomeIDs;
        
        [Header("Rotation")]
        [Tooltip("Apply random Y-axis rotation")]
        public bool RandomYRotation = true;
        
        [Tooltip("Rotate to match surface normal")]
        public bool AlignToSurface = true;
        
        [Header("Hollow Earth Settings")]
        [Tooltip("For giant decorators in hollow earth layers")]
        public bool IsGiantDecorator = false;
        
        [Tooltip("Maximum height for tall decorators (mushrooms, crystals)")]
        [Range(1f, 200f)]
        public float MaxHeight = 50f;
        
        [Header("LOD Settings")]
        [Tooltip("Importance level for distance-based culling. High = always spawn, Low = only near player")]
        public DecoratorLODImportance LODImportance = DecoratorLODImportance.Medium;
        
        [Tooltip("Maximum distance (chunks) at which this decorator spawns. 0 = use LODImportance defaults")]
        [Range(0, 32)]
        public int CustomMaxChunkDistance = 0;
        
        [Header("GPU Instancing")]
        [Tooltip("Use GPU Instancing instead of individual GameObjects. Best for many identical small decorators.")]
        public bool UseGPUInstancing = false;
        
        [Tooltip("Mesh to use for GPU Instancing (if different from prefab). Leave null to extract from prefab.")]
        public Mesh InstancedMesh;
        
        [Tooltip("Material for GPU Instancing. Must have 'Enable GPU Instancing' checked.")]
        public Material InstancedMaterial;
        
        /// <summary>
        /// Get a random prefab (main or variation).
        /// </summary>
        public GameObject GetRandomPrefab(uint seed)
        {
            if (Variations == null || Variations.Length == 0)
                return Prefab;
            
            var random = new Unity.Mathematics.Random(seed);
            int index = random.NextInt(0, Variations.Length + 1);
            
            if (index == 0)
                return Prefab;
            
            return Variations[index - 1] ?? Prefab;
        }
        
        /// <summary>
        /// Check if this decorator can spawn in the given biome.
        /// </summary>
        public bool CanSpawnInBiome(byte biomeID)
        {
            if (AllowedBiomeIDs == null || AllowedBiomeIDs.Length == 0)
                return true; // No restrictions
            
            foreach (var allowed in AllowedBiomeIDs)
            {
                if (allowed == biomeID)
                    return true;
            }
            return false;
        }
    }
}
