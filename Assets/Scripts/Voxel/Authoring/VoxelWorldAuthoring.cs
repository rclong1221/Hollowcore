using Unity.Entities;
using UnityEngine;
using DIG.Voxel.Components;

namespace DIG.Voxel.Authoring
{
    /// <summary>
    /// Drop this component anywhere in the scene to enable voxel terrain generation.
    /// Without this component, all voxel systems remain dormant.
    ///
    /// Works in both the main scene (runtime bootstrap) and SubScenes (baker).
    /// Optionally enable "Override Settings" to provide per-scene configuration
    /// instead of using the global Resources/ defaults.
    /// </summary>
    [AddComponentMenu("DIG/Voxel/Voxel World")]
    public class VoxelWorldAuthoring : MonoBehaviour
    {
        [Header("Scene Override Settings")]
        [Tooltip("When enabled, the settings below override the global Resources/ configs for this scene.")]
        public bool OverrideSettings = false;

        [Header("Terrain")]
        [Tooltip("World generation seed for deterministic terrain.")]
        public uint Seed = 12345;

        [Tooltip("Y coordinate of the ground surface.")]
        public float GroundLevel = 0f;

        [Header("Streaming")]
        [Tooltip("Horizontal streaming distance in chunks (1 chunk = 32m). Default 4 = ~128m.")]
        [Range(1, 16)]
        public int ViewDistance = 4;

        [Header("Terrain Noise")]
        [Tooltip("Scale of terrain height Perlin noise.")]
        public float TerrainNoiseScale = 0.02f;

        [Tooltip("Amplitude of terrain height variation in voxels.")]
        public float TerrainNoiseAmplitude = 10f;

        [Header("Feature Toggles")]
        [Tooltip("Enable ore generation.")]
        public bool EnableOres = true;

        [Tooltip("Enable strata (rock layer) variation.")]
        public bool EnableStrata = true;

        [Tooltip("Enable cave and hollow earth generation.")]
        public bool EnableCaves = true;

        [Tooltip("Enable biome system.")]
        public bool EnableBiomes = true;
    }

    /// <summary>
    /// Baker for SubScene usage. If the authoring is inside a SubScene, this handles it.
    /// </summary>
    public class VoxelWorldAuthoringBaker : Baker<VoxelWorldAuthoring>
    {
        public override void Bake(VoxelWorldAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<VoxelWorldEnabled>(entity);

            if (authoring.OverrideSettings)
            {
                AddComponent(entity, new VoxelWorldSettings
                {
                    Seed = authoring.Seed,
                    GroundLevel = authoring.GroundLevel,
                    ViewDistance = authoring.ViewDistance,
                    TerrainNoiseScale = authoring.TerrainNoiseScale,
                    TerrainNoiseAmplitude = authoring.TerrainNoiseAmplitude,
                    EnableOres = authoring.EnableOres,
                    EnableStrata = authoring.EnableStrata,
                    EnableCaves = authoring.EnableCaves,
                    EnableBiomes = authoring.EnableBiomes
                });
            }
        }
    }

    /// <summary>
    /// Runtime bootstrap system that finds VoxelWorldAuthoring MonoBehaviours in the scene
    /// and creates the VoxelWorldEnabled singleton entity. This allows the authoring component
    /// to work when placed anywhere in the scene hierarchy (not just SubScenes).
    /// Runs once on the first frame, then disables itself.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class VoxelWorldBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Don't gate on VoxelWorldEnabled — we're the system that creates it
        }

        protected override void OnUpdate()
        {
            // If the singleton already exists (e.g. from a SubScene baker), we're done
            if (SystemAPI.HasSingleton<VoxelWorldEnabled>())
            {
                Enabled = false;
                return;
            }

            // Look for the MonoBehaviour in the scene
            var authoring = Object.FindFirstObjectByType<VoxelWorldAuthoring>();
            if (authoring == null)
            {
                // No authoring found — voxels stay disabled. Check again next frame
                // in case the scene is still loading.
                return;
            }

            // Create the gate singleton entity
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponent<VoxelWorldEnabled>(entity);
            EntityManager.SetName(entity, "VoxelWorldEnabled");

            // Add settings overrides if configured
            if (authoring.OverrideSettings)
            {
                EntityManager.AddComponentData(entity, new VoxelWorldSettings
                {
                    Seed = authoring.Seed,
                    GroundLevel = authoring.GroundLevel,
                    ViewDistance = authoring.ViewDistance,
                    TerrainNoiseScale = authoring.TerrainNoiseScale,
                    TerrainNoiseAmplitude = authoring.TerrainNoiseAmplitude,
                    EnableOres = authoring.EnableOres,
                    EnableStrata = authoring.EnableStrata,
                    EnableCaves = authoring.EnableCaves,
                    EnableBiomes = authoring.EnableBiomes
                });
            }

            UnityEngine.Debug.Log("[VoxelWorldBootstrap] Created VoxelWorldEnabled singleton from scene MonoBehaviour.");
            Enabled = false;
        }
    }
}
