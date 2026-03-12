using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Static service for decorator data in Burst jobs.
    /// Follows GeologyService/BiomeService pattern.
    /// </summary>
    public static class DecoratorService
    {
        /// <summary>
        /// Burst-compatible decorator parameters.
        /// </summary>
        public struct DecoratorParams
        {
            public byte DecoratorID;
            public SurfaceType RequiredSurface;
            public float MinSpacing;
            public float SpawnProbability;
            public float MinCaveRadius;
            public float MinDepth;
            public float MaxDepth;
            public float MinScale;
            public float MaxScale;
            public byte Flags; // Packed booleans
            public byte MaxChunkDistance; // OPTIMIZATION 10.5.11: LOD distance
            
            // Flag accessors
            public bool ScaleWithCaveSize => (Flags & 1) != 0;
            public bool RandomYRotation => (Flags & 2) != 0;
            public bool AlignToSurface => (Flags & 4) != 0;
            public bool IsGiantDecorator => (Flags & 8) != 0;
        }
        
        /// <summary>
        /// Surface point detected in a chunk.
        /// </summary>
        public struct SurfacePoint
        {
            public float3 Position;
            public float3 Normal;
            public SurfaceType Type;
            public byte BiomeID;
            public float CaveRadius;
        }
        
        /// <summary>
        /// Placement decision for a decorator.
        /// </summary>
        public struct DecoratorPlacement
        {
            public byte DecoratorID;
            public float3 Position;
            public float3 Normal;
            public float Scale;
            public float YRotation;
            public uint RandomSeed;
        }
        
        // Native arrays for Burst access
        public static NativeArray<DecoratorParams> DecoratorParamsArray;
        public static NativeList<byte> FloorDecorators;
        public static NativeList<byte> CeilingDecorators;
        public static NativeList<byte> WallDecorators;
        
        private static DecoratorRegistry _registry;
        private static bool _isInitialized;
        private static int _maxDecoratorsPerChunk;
        private static float _globalSpawnMultiplier;
        
        private static int _referenceCount; // Reference counting for shared worlds
        
        public static bool IsInitialized => _isInitialized;
        public static int MaxDecoratorsPerChunk => _maxDecoratorsPerChunk;
        public static float GlobalSpawnMultiplier => _globalSpawnMultiplier;
        
        /// <summary>
        /// Initialize decorator service from registry.
        /// </summary>
        public static void Initialize(DecoratorRegistry registry)
        {
            _referenceCount++;
            
            if (_isInitialized)
            {
                return;
            }
            
            // Safety: Ensure clean state if we somehow got here but data exists (e.g. static reload issue)
             if (DecoratorParamsArray.IsCreated) DisposeInternal();
            
            if (registry == null || registry.Decorators == null || registry.Decorators.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[DecoratorService] No decorators configured");
                return;
            }
            
            _registry = registry;
            _maxDecoratorsPerChunk = registry.MaxDecoratorsPerChunk;
            _globalSpawnMultiplier = registry.GlobalSpawnMultiplier;
            
            // Find max ID
            int maxId = 0;
            foreach (var dec in registry.Decorators)
            {
                if (dec != null && dec.DecoratorID > maxId)
                    maxId = dec.DecoratorID;
            }
            
            // Allocate arrays
            DecoratorParamsArray = new NativeArray<DecoratorParams>(maxId + 1, Allocator.Persistent);
            FloorDecorators = new NativeList<byte>(32, Allocator.Persistent);
            CeilingDecorators = new NativeList<byte>(32, Allocator.Persistent);
            WallDecorators = new NativeList<byte>(32, Allocator.Persistent);
            
            // Fill data
            foreach (var dec in registry.Decorators)
            {
                if (dec == null) continue;
                
                byte flags = 0;
                if (dec.ScaleWithCaveSize) flags |= 1;
                if (dec.RandomYRotation) flags |= 2;
                if (dec.AlignToSurface) flags |= 4;
                if (dec.IsGiantDecorator) flags |= 8;
                
                var param = new DecoratorParams
                {
                    DecoratorID = dec.DecoratorID,
                    RequiredSurface = dec.RequiredSurface,
                    MinSpacing = dec.MinSpacing,
                    SpawnProbability = dec.SpawnProbability * _globalSpawnMultiplier,
                    MinCaveRadius = dec.MinCaveRadius,
                    MinDepth = dec.MinDepth,
                    MaxDepth = dec.MaxDepth,
                    MinScale = dec.MinScale,
                    MaxScale = dec.MaxScale,
                    Flags = flags,
                    MaxChunkDistance = GetMaxChunkDistance(dec)
                };
                
                DecoratorParamsArray[dec.DecoratorID] = param;
                
                // Add to surface lists
                switch (dec.RequiredSurface)
                {
                    case SurfaceType.Floor:
                        FloorDecorators.Add(dec.DecoratorID);
                        break;
                    case SurfaceType.Ceiling:
                        CeilingDecorators.Add(dec.DecoratorID);
                        break;
                    default: // Walls
                        WallDecorators.Add(dec.DecoratorID);
                        break;
                }
            }
            
            _isInitialized = true;
            UnityEngine.Debug.Log($"[DecoratorService] Initialized with {registry.Decorators.Length} decorators");
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.11: Calculate max chunk distance based on LOD importance.
        /// </summary>
        private static byte GetMaxChunkDistance(DecoratorDefinition dec)
        {
            // Custom override
            if (dec.CustomMaxChunkDistance > 0)
                return (byte)dec.CustomMaxChunkDistance;
            
            // Default distances based on importance
            switch (dec.LODImportance)
            {
                case DecoratorLODImportance.Low: return 4;
                case DecoratorLODImportance.Medium: return 8;
                case DecoratorLODImportance.High: return 16;
                case DecoratorLODImportance.Critical: return 255; // Always spawn
                default: return 8;
            }
        }
        
        /// <summary>
        /// Get the managed decorator definition by ID.
        /// Use for prefab instantiation (main thread only).
        /// </summary>
        public static DecoratorDefinition GetDefinition(byte id)
        {
            return _registry?.GetDecorator(id);
        }
        
        /// <summary>
        /// Enable cleanup by reference counting.
        /// </summary>
        public static void Dispose()
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                DisposeInternal();
                _referenceCount = 0;
            }
        }

        private static void DisposeInternal()
        {
            if (DecoratorParamsArray.IsCreated) DecoratorParamsArray.Dispose();
            if (FloorDecorators.IsCreated) FloorDecorators.Dispose();
            if (CeilingDecorators.IsCreated) CeilingDecorators.Dispose();
            if (WallDecorators.IsCreated) WallDecorators.Dispose();
            _isInitialized = false;
        }
    }
}
