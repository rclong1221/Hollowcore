using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Provides Burst-compatible geology lookup data for voxel generation jobs.
    /// This is a bridge between ScriptableObjects (managed) and Burst jobs (unmanaged).
    /// </summary>
    public static class GeologyService
    {
        // Cached references (managed side)
        private static StrataProfile _currentStrata;
        private static OreDefinition[] _currentOres;
        private static DepthValueCurve _currentDepthCurve;
        private static uint _seed;
        
        // Blittable data for jobs
        public static NativeArray<StrataLayerData> StrataLayers;
        public static NativeArray<OreData> Ores;
        public static bool IsInitialized { get; private set; }
        
        /// <summary>
        /// Blittable strata layer for Burst jobs.
        /// </summary>
        public struct StrataLayerData
        {
            public byte MaterialID;
            public float MinDepth;
            public float MaxDepth;
            public float BlendWidth;
            public float NoiseInfluence;
        }
        
        /// <summary>
        /// Blittable ore data for Burst jobs.
        /// </summary>
        public struct OreData
        {
            public byte MaterialID;
            public float MinDepth;
            public float MaxDepth;
            public float Threshold;
            public float NoiseScale;
            public float WarpStrength;
            public bool DomainWarping;
            public OreRarity Rarity;
        }
        
        /// <summary>
        /// Initialize the geology service with profile data.
        /// Call this from ChunkGenerationSystem.OnCreate or a bootstrap system.
        /// </summary>
        private static int _referenceCount;

        /// <summary>
        /// Initialize the geology service with profile data.
        /// Call this from ChunkGenerationSystem.OnCreate or a bootstrap system.
        /// </summary>
        public static void Initialize(StrataProfile strata, OreDefinition[] ores, DepthValueCurve depthCurve, uint seed)
        {
            _referenceCount++;
            if (IsInitialized) return;
            
            _currentStrata = strata;
            _currentOres = ores;
            _currentDepthCurve = depthCurve;
            _seed = seed;
            
            // Build strata layers
            if (strata != null && strata.Layers != null && strata.Layers.Length > 0)
            {
                StrataLayers = new NativeArray<StrataLayerData>(strata.Layers.Length, Allocator.Persistent);
                for (int i = 0; i < strata.Layers.Length; i++)
                {
                    var layer = strata.Layers[i];
                    StrataLayers[i] = new StrataLayerData
                    {
                        MaterialID = layer.MaterialID,
                        MinDepth = layer.MinDepth,
                        MaxDepth = layer.MaxDepth,
                        BlendWidth = layer.BlendWidth,
                        NoiseInfluence = layer.NoiseInfluence
                    };
                }
            }
            else
            {
                // Default single layer
                StrataLayers = new NativeArray<StrataLayerData>(1, Allocator.Persistent);
                StrataLayers[0] = new StrataLayerData
                {
                    MaterialID = 1, // Stone
                    MinDepth = 0,
                    MaxDepth = 999,
                    BlendWidth = 0,
                    NoiseInfluence = 0
                };
            }
            
            // Build ore data
            if (ores != null && ores.Length > 0)
            {
                Ores = new NativeArray<OreData>(ores.Length, Allocator.Persistent);
                for (int i = 0; i < ores.Length; i++)
                {
                    var ore = ores[i];
                    if (ore == null) continue;
                    
                    Ores[i] = new OreData
                    {
                        MaterialID = ore.MaterialID,
                        MinDepth = ore.MinDepth,
                        MaxDepth = ore.MaxDepth,
                        Threshold = ore.Threshold,
                        NoiseScale = ore.NoiseScale,
                        WarpStrength = ore.WarpStrength,
                        DomainWarping = ore.DomainWarping,
                        Rarity = ore.Rarity
                    };
                }
            }
            else
            {
                Ores = new NativeArray<OreData>(0, Allocator.Persistent);
            }
            
            IsInitialized = true;
            UnityEngine.Debug.Log($"[GeologyService] Initialized with {StrataLayers.Length} strata layers and {Ores.Length} ore types");
        }
        
        /// <summary>
        /// Dispose native collections. Call from system OnDestroy.
        /// </summary>
        public static void Dispose()
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                if (StrataLayers.IsCreated) StrataLayers.Dispose();
                if (Ores.IsCreated) Ores.Dispose();
                IsInitialized = false;
                _referenceCount = 0;
            }
        }
        
        /// <summary>
        /// Get seed for noise generation.
        /// </summary>
        public static uint GetSeed() => _seed;
    }
    
    /// <summary>
    /// Static methods for geology lookup. Called from Burst jobs and inlined automatically.
    /// </summary>
    public static class GeologyLookup
    {
        /// <summary>
        /// Get the strata material for a given depth and position.
        /// </summary>
        public static byte GetStrataMaterial(
            float depth, 
            in float3 worldPos, 
            in NativeArray<GeologyService.StrataLayerData> layers,
            uint seed)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                
                // Calculate noise offset for layer boundaries
                float noiseOffset = 0f;
                if (layer.NoiseInfluence > 0)
                {
                    noiseOffset = noise.snoise(worldPos * 0.05f + seed) * layer.NoiseInfluence * 10f;
                }
                
                float adjustedMin = layer.MinDepth + noiseOffset;
                float adjustedMax = layer.MaxDepth + noiseOffset;
                
                if (depth >= adjustedMin && depth < adjustedMax)
                    return layer.MaterialID;
            }
            
            return 1; // Default stone
        }
        
        /// <summary>
        /// Check for ore spawning and return ore material if found.
        /// Returns 0 if no ore should spawn.
        /// </summary>
        public static byte GetOreMaterial(
            in float3 worldPos,
            float depth,
            byte hostMaterial,
            in NativeArray<GeologyService.OreData> ores,
            uint seed,
            float cachedNoise) // Optimization 10.1.11
        {
            for (int i = 0; i < ores.Length; i++)
            {
                var ore = ores[i];
                
                // Depth check
                if (depth < ore.MinDepth || depth > ore.MaxDepth)
                    continue;
                
                // Optimization 10.1.11: Use cached noise instead of recalculating
                // We ignore domain warping for cached optimization to save peformance
                float oreNoise = cachedNoise; 
                
                if (oreNoise > ore.Threshold)
                    return ore.MaterialID;
            }
            
            return 0; // No ore
        }
    }
}
