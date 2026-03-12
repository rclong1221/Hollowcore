using UnityEngine;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Shared configuration for voxel world generation.
    ///
    /// This provides a single source of truth for:
    /// - WorldSeed (for future noise-based terrain)
    /// - Generation mode (sphere, flat, noise)
    /// - Parameters for each mode
    ///
    /// In multiplayer, server and all clients should use the same
    /// configuration so base terrain is deterministic across processes.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Voxel/Voxel World Config", fileName = "VoxelWorldConfig")]
    public class VoxelWorldConfig : ScriptableObject
    {
        public enum GenerationMode
        {
            Sphere,
            FlatPlane,
            PerlinNoise
        }

        [Header("Global")]
        public int WorldSeed = 12345;

        [Header("Generation")]
        public GenerationMode Mode = GenerationMode.Sphere;

        [Header("Sphere Settings")]
        public float SphereRadius = 50f;

        [Header("Flat Plane Settings")]
        public float PlaneY = 0f;

        [Header("Perlin Noise Settings")]
        public float PerlinScale = 0.02f;
        public float PerlinThreshold = 0.0f;

        private static VoxelWorldConfig active;

        /// <summary>
        /// Global access to the active world config.
        /// Attempts to load a VoxelWorldConfig asset from Resources.
        /// If none is found, uses a default in-memory instance.
        /// </summary>
        public static VoxelWorldConfig Active
        {
            get
            {
                if (active == null)
                {
                    // Look for an asset in Resources/VoxelWorldConfig.asset
                    active = UnityEngine.Resources.Load<VoxelWorldConfig>("VoxelWorldConfig");

                    if (active == null)
                    {
                        active = CreateInstance<VoxelWorldConfig>();
                        UnityEngine.Debug.LogWarning("[VoxelWorldConfig] No asset found in Resources. Using default in-memory config.");
                    }
                }

                return active;
            }
        }
    }
}
