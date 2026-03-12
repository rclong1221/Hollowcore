using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Jobs;
using DIG.Voxel.Fluids;
using DIG.Voxel.Debug;
using DIG.Voxel.Geology;
using DIG.Voxel.Biomes;

namespace DIG.Voxel.Systems.Generation
{
    /// <summary>
    /// Generates voxel data for chunks using the geology and cave/hollow earth systems.
    /// Supports async job scheduling with configurable concurrency.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkStreamingSystem))]
    public partial class ChunkGenerationSystem : SystemBase
    {
        private const int MAX_CONCURRENT_JOBS = 16; // Increased from 4, rely on time budget
        private const float FRAME_BUDGET_MS = 4.0f; // Max 4ms per frame for generation scheduling
        
        // OPTIMIZATION 10.9.8: Priority Queue Constants
        private const float DIRECTIONAL_BIAS = 0.5f; // Weight for look direction priority (0-1)
        private const int MAX_CANDIDATES_PER_FRAME = 64; // Max chunks to evaluate per frame
        
        // OPTIMIZATION 10.9.14: Job Scheduling Spread
        private const int MAX_COMPLETIONS_PER_FRAME = 4; // Limit completions to prevent frame spikes
        
        // Configuration
        private WorldGenerationConfig _config;
        private WorldStructureConfig _worldStructure;
        private float _groundLevel = 0f;
        private uint _seed = 12345;
        private float _terrainNoiseScale = 0.02f;
        private float _terrainNoiseAmplitude = 10f;
        private bool _geologyInitialized = false;
        private bool _caveSystemInitialized = false;
        private bool _biomeSystemInitialized = false;
        private BiomeRegistry _biomeRegistry;
        
        // Blob Assets (Task 10.7.2)
        private BlobAssetReference<StrataBlob> _strataBlob;
        private BlobAssetReference<CaveParamsBlob> _caveBlob; // Fallback default
        private BlobAssetReference<HollowEarthBlob> _hollowBlob; // Fallback default
        
        // Maps to look up specific blobs for layers if needed, or we might need a BlobArray of Blobs?
        // For simplicity in Phase 1, we will pass arrays of config data via NativeArray for "Layout" lookup,
        // but use Blobs for the heavy params.
        // Actually, to fully optimize, we should put ALL config into a single WorldConfigBlob.
        // For now, let's keep it simple: Use NativeArrays for high-level lookup (as they are small: 10-20 layers),
        // and Blobs for complex profile data.
        
        // Fallback empty arrays for when geology is not configured
        private NativeArray<GeologyService.StrataLayerData> _emptyStrataLayers;
        private NativeArray<GeologyService.OreData> _emptyOres;
        
        // Fallback empty arrays for when cave system is not configured (Restored)
        private NativeArray<CaveGenerationService.LayerData> _emptyLayers;
        private NativeArray<CaveGenerationService.CaveParams> _emptyCaveParams;
        private NativeArray<CaveGenerationService.HollowParams> _emptyHollowParams;
        private NativeArray<BiomeService.BiomeParams> _emptyBiomes;
        
        // Pending generation jobs
        private struct PendingGeneration
        {
            public Entity Entity;
            public int3 ChunkPos;
            public JobHandle Handle;
            public NativeArray<byte> Densities;
            public NativeArray<byte> Materials;
            public NativeArray<float> TerrainHeights;
            public NativeArray<byte> BiomeIDs;
            public NativeArray<float> OreNoiseCache; // Optimization 10.1.11
            public NativeArray<int> DensityStats; // Optimization 10.2.13
            public bool IsHomogeneous; // Optimization 10.4.11
            public byte HomogeneousBiome; // Optimization 10.4.11
        }
        
        // OPTIMIZATION 10.9.8: Generation Candidate for Priority Queue
        private struct GenerationCandidate : System.IComparable<GenerationCandidate>
        {
            public Entity Entity;
            public int3 ChunkPos;
            public float Priority; // Lower = higher priority (closer/in view)
            
            public int CompareTo(GenerationCandidate other)
            {
                return Priority.CompareTo(other.Priority);
            }
        }
        
        private NativeList<PendingGeneration> _pendingJobs;
        private EntityQuery _chunksNeedingGeneration;
        
        // OPTIMIZATION 10.9.8: Cached camera direction for directional bias
        private float3 _cachedCameraForward;
        private bool _enableDebugLogs = false;
        
        protected override void OnCreate()
        {
            RequireForUpdate<VoxelWorldEnabled>();

            NativeCollectionPool.RegisterUser();
            _pendingJobs = new NativeList<PendingGeneration>(MAX_CONCURRENT_JOBS, Allocator.Persistent);
            
            _chunksNeedingGeneration = GetEntityQuery(
                ComponentType.ReadOnly<ChunkPosition>(),
                ComponentType.ReadOnly<ChunkVoxelData>()
            );
            
            // Create empty fallback arrays (must be valid for job scheduling)
            _emptyStrataLayers = new NativeArray<GeologyService.StrataLayerData>(0, Allocator.Persistent);
            _emptyOres = new NativeArray<GeologyService.OreData>(0, Allocator.Persistent);
            _emptyLayers = new NativeArray<CaveGenerationService.LayerData>(0, Allocator.Persistent);
            _emptyCaveParams = new NativeArray<CaveGenerationService.CaveParams>(0, Allocator.Persistent);
            _emptyHollowParams = new NativeArray<CaveGenerationService.HollowParams>(0, Allocator.Persistent);
            _emptyBiomes = new NativeArray<BiomeService.BiomeParams>(0, Allocator.Persistent);
            
            // Load world generation config
            LoadConfig();
        }
        
        private void LoadConfig()
        {
            // Load basic generation config
            _config = Resources.Load<WorldGenerationConfig>("WorldGenerationConfig");
            
            if (_config != null)
            {
                _groundLevel = _config.GroundLevel;
                _seed = _config.Seed;
                _terrainNoiseScale = _config.TerrainNoiseScale;
                _terrainNoiseAmplitude = _config.TerrainNoiseAmplitude;
                
                // Initialize geology service
                _config.InitializeGeologyService();
                _geologyInitialized = GeologyService.IsInitialized;
                
                UnityEngine.Debug.Log($"[ChunkGeneration] Loaded WorldGenerationConfig: Seed={_seed}, Geology={_geologyInitialized}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ChunkGeneration] WorldGenerationConfig not found in Resources. Using defaults.");
            }
            
            // Initialize Fluid Service (Fixing Task 10.14.3)
            var fluidRegistry = Resources.Load<FluidRegistry>("Fluids/FluidRegistry");
            if (fluidRegistry != null)
            {
                FluidService.Initialize(fluidRegistry.Fluids);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ChunkGeneration] FluidRegistry not found in Resources/Fluids/FluidRegistry. Fluids disabled.");
            }
            
            // Load world structure config (for caves/hollow earth)
            _worldStructure = Resources.Load<WorldStructureConfig>("WorldStructureConfig");
            
            if (_worldStructure != null)
            {
                CaveGenerationService.Initialize(_worldStructure);
                _caveSystemInitialized = CaveGenerationService.IsInitialized;
                
                // Use world structure seed if no basic config
                if (_config == null)
                {
                    _seed = _worldStructure.WorldSeed;
                    _groundLevel = _worldStructure.GroundLevel;
                }
                
                UnityEngine.Debug.Log($"[ChunkGeneration] Loaded WorldStructureConfig: {_worldStructure.Layers?.Length ?? 0} layers, " +
                    $"{_worldStructure.HollowLayerCount} hollow, {_worldStructure.SolidLayerCount} solid, CaveSystem={_caveSystemInitialized}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ChunkGeneration] WorldStructureConfig not found in Resources. Cave/Hollow Earth disabled.");
            }
            
            // Load biome registry
            _biomeRegistry = Resources.Load<BiomeRegistry>("Biomes/BiomeRegistry");
            if (_biomeRegistry != null)
            {
                BiomeService.Initialize(_biomeRegistry);
                _biomeSystemInitialized = BiomeService.IsInitialized;
                UnityEngine.Debug.Log($"[ChunkGeneration] Loaded BiomeRegistry: {_biomeRegistry.Biomes?.Length ?? 0} biomes");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ChunkGeneration] BiomeRegistry not found in Resources/Biomes/BiomeRegistry. Biomes disabled.");
            }

            // Create Blob Assets after all configs are loaded
            CreateBlobAssets();
        }
        
        protected override void OnDestroy()
        {
            // Complete and dispose any pending jobs on destroy
            for (int i = 0; i < _pendingJobs.Length; i++)
            {
                var pending = _pendingJobs[i];
                pending.Handle.Complete();
                if (pending.Densities.IsCreated) pending.Densities.Dispose();
                if (pending.Materials.IsCreated) pending.Materials.Dispose();
                if (pending.TerrainHeights.IsCreated) pending.TerrainHeights.Dispose();
                if (pending.BiomeIDs.IsCreated) pending.BiomeIDs.Dispose();
                if (pending.OreNoiseCache.IsCreated) pending.OreNoiseCache.Dispose();
                if (pending.DensityStats.IsCreated) pending.DensityStats.Dispose();
            }
            if (_pendingJobs.IsCreated) _pendingJobs.Dispose();
            
            // Dispose fallback arrays
            if (_emptyStrataLayers.IsCreated) _emptyStrataLayers.Dispose();
            if (_emptyOres.IsCreated) _emptyOres.Dispose();
            if (_emptyLayers.IsCreated) _emptyLayers.Dispose();
            if (_emptyCaveParams.IsCreated) _emptyCaveParams.Dispose();
            if (_emptyHollowParams.IsCreated) _emptyHollowParams.Dispose();
            if (_emptyBiomes.IsCreated) _emptyBiomes.Dispose();
            
            // Dispose Blobs
            if (_strataBlob.IsCreated) _strataBlob.Dispose();
            if (_caveBlob.IsCreated) _caveBlob.Dispose();
            if (_hollowBlob.IsCreated) _hollowBlob.Dispose();
            
            // Dispose services - wrap in try-catch as jobs may still reference data during shutdown
            try { GeologyService.Dispose(); } catch (System.InvalidOperationException) { }
            try { CaveGenerationService.Dispose(); } catch (System.InvalidOperationException) { }
            try { BiomeService.Dispose(); } catch (System.InvalidOperationException) { }
            try { FluidService.Dispose(); } catch (System.InvalidOperationException) { }
            
            NativeCollectionPool.UnregisterUser();
        }
        
        // Player tracking
        private float3 _playerPos;
        private bool _settingsOverridesApplied = false;

        /// <summary>
        /// Apply VoxelWorldSettings overrides if present.
        /// Called lazily on first OnUpdate (after subscene entities exist).
        /// </summary>
        private void ApplySettingsOverrides()
        {
            if (SystemAPI.HasSingleton<VoxelWorldSettings>())
            {
                var settings = SystemAPI.GetSingleton<VoxelWorldSettings>();
                _seed = settings.Seed;
                _groundLevel = settings.GroundLevel;
                _terrainNoiseScale = settings.TerrainNoiseScale;
                _terrainNoiseAmplitude = settings.TerrainNoiseAmplitude;

                // Re-initialize geology with overridden seed if config exists
                if (_config != null)
                {
                    _config.InitializeGeologyService();
                    _geologyInitialized = settings.EnableOres && GeologyService.IsInitialized;
                }
                else
                {
                    _geologyInitialized = false;
                }

                if (!settings.EnableCaves)
                    _caveSystemInitialized = false;

                if (!settings.EnableBiomes)
                    _biomeSystemInitialized = false;

                if (!settings.EnableStrata)
                    _geologyInitialized = false;

                // Recreate blob assets with potentially updated config
                CreateBlobAssets();

                UnityEngine.Debug.Log($"[ChunkGeneration] Applied VoxelWorldSettings overrides: Seed={_seed}, Ground={_groundLevel}");
            }
            _settingsOverridesApplied = true;
        }

        protected override void OnUpdate()
        {
            // Lazy-load settings overrides on first update (after subscene entities exist)
            if (!_settingsOverridesApplied)
                ApplySettingsOverrides();

            VoxelProfiler.BeginSample("GenerationSystem");
            VoxelProfiler.BeginSample("Generation.Total");

            // Update player position for LOD (Use Camera fallback like StreamingSystem)
            _playerPos = GetViewerPosition();
            
            // 0. Process Completed Jobs (Completion Phase)
            // OPTIMIZATION 10.9.14: Limit completions per frame to prevent spikes
            if (_pendingJobs.Length > 0)
            {
                VoxelProfiler.BeginSample("Generation.Complete");
                int completionsThisFrame = 0;
                for (int i = _pendingJobs.Length - 1; i >= 0 && completionsThisFrame < MAX_COMPLETIONS_PER_FRAME; i--)
                {
                    var pending = _pendingJobs[i];
                    
                    if (pending.Handle.IsCompleted)
                    {
                        pending.Handle.Complete();
                        
                        // Verify entity still exists
                        if (EntityManager.Exists(pending.Entity))
                        {
                            if (EntityManager.HasComponent<ChunkFluidData>(pending.Entity))
                            {
                                var fluidData = EntityManager.GetComponentData<ChunkFluidData>(pending.Entity);
                                if (fluidData.HasFluid > 0)
                                {
                                    // Optimization 10.3.12: Start active
                                    EntityManager.AddComponentData(pending.Entity, new ChunkHasActiveFluid { TimeSinceLastChange = 0 });
                                }
                            }
                            
                            // Optimization 10.3.17: Visibility Tracking
                            EntityManager.AddComponentData(pending.Entity, new ChunkVisibility { IsVisible = true }); // Default to visible to prevent pop-in logic issues
                            
                            // Create blob from completed job data
                            var blob = VoxelBlobBuilder.Create(pending.Densities, pending.Materials);
                            EntityManager.SetComponentData(pending.Entity, new ChunkVoxelData { Data = blob });
                            
                            // Trigger collider build (Server/Authority)
                            if (EntityManager.HasComponent<ChunkNeedsCollider>(pending.Entity))
                            {
                                if (_enableDebugLogs) UnityEngine.Debug.Log($"[ChunkGenerationSystem] Tagging new chunk {pending.Entity} for collider update in {World.Name}");
                                EntityManager.SetComponentEnabled<ChunkNeedsCollider>(pending.Entity, true);
                            }
                            
                            // Optimization 10.2.13: Add Density Stats Component
                            var stats = new ChunkDensityStats
                            {
                                SolidCount = pending.DensityStats[0],
                                AirCount = pending.DensityStats[1],
                                SurfaceCount = pending.DensityStats[2]

                            };
                            EntityManager.AddComponentData(pending.Entity, stats);
                            
                            // Optimization 10.4.11: Add Deferred ChunkBiome Component
                            EntityManager.AddComponentData(pending.Entity, new ChunkBiome 
                            { 
                                BiomeID = pending.HomogeneousBiome, 
                                IsHomogeneous = pending.IsHomogeneous 
                            });
                        }
                        
                        // Cleanup
                        // OPTIMIZATION 10.9.15: Return to pool
                        NativeCollectionPool.ReturnArray(pending.Densities);
                        NativeCollectionPool.ReturnArray(pending.Materials);
                        NativeCollectionPool.ReturnArray(pending.TerrainHeights);
                        NativeCollectionPool.ReturnArray(pending.BiomeIDs);
                        NativeCollectionPool.ReturnArray(pending.OreNoiseCache);
                        NativeCollectionPool.ReturnArray(pending.DensityStats);
                        
                        // Remove from list
                        _pendingJobs.RemoveAtSwapBack(i);
                        completionsThisFrame++;
                    }
                }
                VoxelProfiler.EndSample("Generation.Complete");
            }
            
            // ========== PHASE 2: Schedule new jobs ==========
            var availableSlots = MAX_CONCURRENT_JOBS - _pendingJobs.Length;
            
            // OPTIMIZATION 10.9.17: Check frame budget before scheduling
            bool hasBudget = FrameBudgetSystem.Instance == null || 
                             FrameBudgetSystem.Instance.HasBudget(1.0f);
            
            // 1. Schedule New Generation Jobs (if slots available and budget permits)
            if (availableSlots > 0 && hasBudget)
            {
                VoxelProfiler.BeginSample("Generation.Schedule");
                
                // OPTIMIZATION 10.9.8: Get camera data for priority calculation
                _cachedCameraForward = float3.zero;
                if (SystemAPI.HasSingleton<CameraData>())
                {
                    var cameraData = SystemAPI.GetSingleton<CameraData>();
                    if (cameraData.IsValid)
                    {
                        _cachedCameraForward = cameraData.Forward;
                    }
                }
                else if (UnityEngine.Camera.main != null)
                {
                    _cachedCameraForward = UnityEngine.Camera.main.transform.forward;
                }
                
                // OPTIMIZATION 10.9.8: Collect candidates and sort by priority
                using var candidates = new NativeList<GenerationCandidate>(MAX_CANDIDATES_PER_FRAME, Allocator.Temp);
                int3 viewerChunk = CoordinateUtils.WorldToChunkPos(_playerPos);
                
                int candidateCount = 0;
                foreach (var (position, voxelData, entity) in 
                    SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxelData>>()
                        .WithEntityAccess())
                {
                    // Limit candidates per frame
                    if (candidateCount >= MAX_CANDIDATES_PER_FRAME) break;
                    
                    // Check if chunk has valid data
                    if (voxelData.ValueRO.Data.IsCreated && voxelData.ValueRO.Data.Value.Densities.Length > 0)
                        continue;
                        
                    // Check if already processing
                    if (IsChunkProcessing(entity)) continue;
                    
                    // OPTIMIZATION 10.9.8: Calculate priority (lower = higher priority)
                    float priority = CalculateGenerationPriority(position.ValueRO.Value, viewerChunk);
                    
                    candidates.Add(new GenerationCandidate
                    {
                        Entity = entity,
                        ChunkPos = position.ValueRO.Value,
                        Priority = priority
                    });
                    
                    candidateCount++;
                }
                
                // OPTIMIZATION 10.9.8: Sort candidates by priority (closest/in-view first)
                if (candidates.Length > 1)
                {
                    candidates.Sort();
                }
                
                // Schedule top N candidates
                int scheduled = 0;
                for (int i = 0; i < candidates.Length && scheduled < availableSlots; i++)
                {
                    var candidate = candidates[i];
                    ScheduleGeneration(candidate.Entity, candidate.ChunkPos);
                    scheduled++;
                }
                
                VoxelProfiler.EndSample("Generation.Schedule");
            }
            
            VoxelProfiler.EndSample("GenerationSystem");
            VoxelProfiler.EndSample("Generation.Total");
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.8: Calculate generation priority for a chunk.
        /// Lower value = higher priority.
        /// Factors: distance to viewer, direction bias (chunks in look direction prioritized).
        /// </summary>
        private float CalculateGenerationPriority(int3 chunkPos, int3 viewerChunk)
        {
            // Base priority: distance to viewer
            int3 delta = chunkPos - viewerChunk;
            float distanceSq = math.lengthsq((float3)delta);
            
            // Directional bias: chunks in look direction get lower priority value (higher priority)
            float directionalBonus = 0f;
            if (math.lengthsq(_cachedCameraForward) > 0.01f)
            {
                float3 chunkDir = math.normalizesafe((float3)delta);
                float dotProduct = math.dot(chunkDir, _cachedCameraForward);
                // dotProduct: 1 = in front, -1 = behind
                // Convert to bonus: in front = -1 (lower priority value), behind = +1
                directionalBonus = (1f - dotProduct) * DIRECTIONAL_BIAS * 10f;
            }
            
            // Priority: distance + directional penalty (lower = higher priority)
            return distanceSq + directionalBonus;
        }

        private void CreateBlobAssets()
        {
            // Create Strata Blob
            if (_config != null && _config.StrataProfile != null)
            {
                if (_strataBlob.IsCreated) _strataBlob.Dispose();
                _strataBlob = BlobAssetBuilder.CreateStrataBlob(_config.StrataProfile);
            }
            
            // Note: Cave and Hollow Blobs implementation deferred to improve architecture.
            // Currently using efficient NativeArrays via CaveGenerationService.
        }
        
        private void ScheduleGeneration(Entity entity, int3 chunkPos)
        {
            VoxelProfiler.BeginSample("Generation.Schedule");
            int3 worldOrigin = chunkPos * VoxelConstants.CHUNK_SIZE;
            
            // Calculate LOD Step based on distance
            float dist = math.distance(new float3(worldOrigin), _playerPos);
            int step = 1;
            if (dist > 128) step = 4;
            else if (dist > 64) step = 2;
            
            // Allocate with Persistent since we expect it to live across frames
            // Standard voxel data (32 KB each)
            // OPTIMIZATION 10.9.15: Use NativeCollectionPool
            var densities = NativeCollectionPool.GetArray<byte>(VoxelConstants.VOXELS_PER_CHUNK);
            var materials = NativeCollectionPool.GetArray<byte>(VoxelConstants.VOXELS_PER_CHUNK);
            
            // Optimization 10.8.2: Column Data (1 KB each)
            var terrainHeights = NativeCollectionPool.GetArray<float>(VoxelConstants.CHUNK_SIZE_SQ);
            var biomeIDs = NativeCollectionPool.GetArray<byte>(VoxelConstants.CHUNK_SIZE_SQ);
            // Optimization 10.1.11
            var oreNoiseCache = NativeCollectionPool.GetArray<float>(VoxelConstants.VOXELS_PER_CHUNK); 
            
            // Optimization 10.2.12: Hollow Layer Early-Out
            // Check if chunk intersects any layer that actually has caves/hollows
            bool useCaves = _caveSystemInitialized;
            if (useCaves)
            {
                float chunkMinY = worldOrigin.y;
                float chunkMaxY = worldOrigin.y + VoxelConstants.CHUNK_SIZE_WORLD;
                bool intersectsValidLayer = false;
                
                var layers = CaveGenerationService.Layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    // Check overlap: chunkMax > layerBottom AND chunkMin < layerTop
                    if (chunkMaxY > layer.BottomDepth && chunkMinY < layer.TopDepth)
                    {
                        // Layer is valid if it has configured caves or is a hollow layer
                        if (layer.CaveParamsIndex >= 0 || layer.HollowParamsIndex >= 0)
                        {
                            intersectsValidLayer = true;
                            break;
                        }
                    }
                }
                useCaves = intersectsValidLayer;
            }
            
            // Optimization 10.4.11: Biome Caching & Homogeneity Check
            bool isHomogeneous = false;
            byte homogeneousBiome = 0;

            if (_biomeSystemInitialized)
            {
                // Calculate biome at 5 points (Center + Corners) to detect uniformity
                var candidates = BiomeService.SolidLayerBiomes.AsArray();
                float noiseScale = _biomeRegistry.GlobalNoiseScale;

                // Center (16, 16) - Use as predominant biome
                float3 center = new float3(worldOrigin.x + 16, 0, worldOrigin.z + 16);
                byte bCenter = BiomeLookup.GetBiomeAt(center, noiseScale, _seed, candidates);

                // Corners (Check for boundary crossing)
                byte b1 = BiomeLookup.GetBiomeAt(new float3(worldOrigin.x, 0, worldOrigin.z), noiseScale, _seed, candidates);
                byte b2 = BiomeLookup.GetBiomeAt(new float3(worldOrigin.x + 31, 0, worldOrigin.z), noiseScale, _seed, candidates);
                byte b3 = BiomeLookup.GetBiomeAt(new float3(worldOrigin.x, 0, worldOrigin.z + 31), noiseScale, _seed, candidates);
                byte b4 = BiomeLookup.GetBiomeAt(new float3(worldOrigin.x + 31, 0, worldOrigin.z + 31), noiseScale, _seed, candidates);

                if (bCenter == b1 && bCenter == b2 && bCenter == b3 && bCenter == b4)
                {
                    isHomogeneous = true;
                    homogeneousBiome = bCenter;
                }
            }
            else
            {
                 isHomogeneous = true;
                 homogeneousBiome = 0;
            }

            // 1. Schedule Pre-Pass (Column Data)
            var columnJob = new GenerateColumnDataJob
            {
                ChunkWorldOrigin = worldOrigin,
                Seed = _seed,
                TerrainNoiseScale = _terrainNoiseScale,
                TerrainNoiseAmplitude = _terrainNoiseAmplitude,
                GroundLevel = _groundLevel,
                UseBiomes = _biomeSystemInitialized,
                SolidLayerBiomes = _biomeSystemInitialized ? BiomeService.SolidLayerBiomes.AsArray() : _emptyBiomes,
                BiomeNoiseScale = _biomeRegistry != null ? _biomeRegistry.GlobalNoiseScale : 0.01f,
                IsBiomeHomogeneous = isHomogeneous,
                HomogeneousBiomeID = homogeneousBiome,
                VoxelStep = step,
                TerrainHeights = terrainHeights,
                BiomeIDs = biomeIDs
            };
            int columnJobLength = VoxelConstants.CHUNK_SIZE_SQ / (step * step);
            var columnHandle = columnJob.Schedule(columnJobLength, 64);

            // 1.5 Schedule Ore Noise Cache (Optimization 10.1.11)
            // Modified for Task 10.7.6 Integration: Uses LOD Step
            var oreNoiseJob = new GenerateOreNoiseJob
            {
                ChunkWorldOrigin = worldOrigin,
                Seed = _seed,
                NoiseScale = 0.05f, // Standard scale for cache
                OreNoiseCache = oreNoiseCache,
                VoxelStep = step
            };
            int oreJobLength = VoxelConstants.VOXELS_PER_CHUNK / (step * step * step);
            var oreNoiseHandle = oreNoiseJob.Schedule(oreJobLength, 256);
            var prePassHandle = JobHandle.CombineDependencies(columnHandle, oreNoiseHandle);
            
            // 2. Schedule Main Pass (Voxel Data)
            var voxelJob = new GenerateVoxelDataJob
            {
                ChunkWorldOrigin = worldOrigin,
                GroundLevel = _groundLevel,
                Seed = _seed,
                TerrainNoiseScale = _terrainNoiseScale,
                TerrainNoiseAmplitude = _terrainNoiseAmplitude,
                
                StrataBlob = _strataBlob,
                Ores = _geologyInitialized ? GeologyService.Ores : _emptyOres,
                OreNoiseCache = oreNoiseCache,
                // Biome data
                UseBiomes = _biomeSystemInitialized,
                Biomes = _biomeSystemInitialized ? BiomeService.AllBiomes : _emptyBiomes,
                SolidLayerBiomes = _biomeSystemInitialized ? BiomeService.SolidLayerBiomes.AsArray() : _emptyBiomes,
                BiomeNoiseScale = _biomeRegistry != null ? _biomeRegistry.GlobalNoiseScale : 0.01f,
                
                // Cave/Hollow Earth data
                UseCaves = useCaves,
                WorldLayers = _caveSystemInitialized ? CaveGenerationService.Layers : _emptyLayers,
                CaveParams = _caveSystemInitialized ? CaveGenerationService.CaveParamsArray : _emptyCaveParams,
                HollowParams = _caveSystemInitialized ? CaveGenerationService.HollowParamsArray : _emptyHollowParams,
                
                // Output
                Densities = densities,
                Materials = materials,
                
                // Pre-pass inputs
                TerrainHeights = terrainHeights,
                BiomeIDs = biomeIDs,
                VoxelStep = step
            };
            
            // Schedule dependent on pre-pass (column + ore noise)
            int voxelJobLength = VoxelConstants.VOXELS_PER_CHUNK / (step * step * step);
            var handle = voxelJob.Schedule(voxelJobLength, 256, prePassHandle);
            
            // Optimization 10.2.13: Schedule Histogram Job
            // OPTIMIZATION 10.9.15: Use NativeCollectionPool
            var stats = NativeCollectionPool.GetArray<int>(3);
            var statsJob = new CalculateChunkStatsJob
            {
                Densities = densities,
                Stats = stats
            };
            var statsHandle = statsJob.Schedule(handle);
            
            _pendingJobs.Add(new PendingGeneration
            {
                Entity = entity,
                ChunkPos = chunkPos,
                Handle = statsHandle, // Wait for Stats too
                Densities = densities,
                Materials = materials,
                TerrainHeights = terrainHeights,
                BiomeIDs = biomeIDs,
                OreNoiseCache = oreNoiseCache,
                DensityStats = stats,
                IsHomogeneous = isHomogeneous,
                HomogeneousBiome = homogeneousBiome
            });
        }

        private float3 GetViewerPosition()
        {
            if (UnityEngine.Camera.main != null)
            {
                return (float3)UnityEngine.Camera.main.transform.position;
            }
            // Fallback for Editor Scene View
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView != null && 
                UnityEditor.SceneView.lastActiveSceneView.camera != null)
            {
                return (float3)UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
            }
            #endif
            return float3.zero;
        }

        private bool IsChunkProcessing(Entity entity)
        {
            for (int i = 0; i < _pendingJobs.Length; i++)
            {
                if (_pendingJobs[i].Entity == entity)
                    return true;
            }
            return false;
        }
    }
}
