using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics; // Added for PhysicsCollider cleanup
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Systems;
using DIG.Voxel.Geology;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Systems.Generation
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ChunkStreamingSystem : SystemBase
    {
        // Configuration (defaults, overridable via VoxelWorldSettings)
        private const int DEFAULT_VIEW_DISTANCE = 4; // Chunks radius (approx 128m)
        private const int CHUNKS_TO_PROCESS_PER_FRAME = 2; // Throttle

        private int _viewDistance = DEFAULT_VIEW_DISTANCE;
        private int _unloadDistance = DEFAULT_VIEW_DISTANCE + 2;
        
        // Vertical range (Dynamic based on layers)
        // Task 10.7.3: Layer-Based Streaming
        private const int LAYER_BUFFER = 1;
        private const int MAX_VERTICAL_CHUNKS = 16; // ~500m vertical limit
        
        private EntityArchetype _chunkArchetype;
        private NativeHashMap<int3, Entity> _loadedChunks;
        
        // Batches
        private NativeList<int3> _chunksToSpawn;
        private NativeList<Entity> _entitiesToUnload;
        private NativeList<int3> _coordsToUnload;
        
        // OPTIMIZATION 10.9.4: Pre-computed spiral pattern
        // Offsets are sorted by distance from origin, enabling closest-first loading
        private NativeArray<int3> _spiralPattern;
        private int _spiralPatternLength;
        private bool _settingsApplied;

        // OPTIMIZATION 10.9.4: Viewer position caching
        // Skip recalculation if viewer hasn't moved to a new chunk
        private int3 _cachedViewerChunk;
        private bool _hasViewerChunkCache;
        private int _spiralIndex; // Current position in spiral for incremental loading

        // Debug logging toggle
        private static bool s_EnableDebugLogs = false;
        public static bool EnableDebugLogs
        {
            get => s_EnableDebugLogs;
            set => s_EnableDebugLogs = value;
        }

        private EndSimulationEntityCommandBufferSystem _ecbSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<VoxelWorldEnabled>();

            _chunkArchetype = EntityManager.CreateArchetype(
                typeof(ChunkPosition),
                typeof(ChunkVoxelData),
                typeof(ChunkMeshState),
                typeof(ChunkColliderState),
                typeof(ChunkLODState),
                typeof(ChunkNeighbors),
                typeof(ChunkNeedsRemesh),
                typeof(ChunkNeedsCollider),
                typeof(Unity.Transforms.LocalTransform),
                typeof(Unity.Transforms.LocalToWorld)
            );
            
            _loadedChunks = new NativeHashMap<int3, Entity>(1024, Allocator.Persistent);
            _chunksToSpawn = new NativeList<int3>(CHUNKS_TO_PROCESS_PER_FRAME, Allocator.Persistent);
            _entitiesToUnload = new NativeList<Entity>(CHUNKS_TO_PROCESS_PER_FRAME, Allocator.Persistent);
            _coordsToUnload = new NativeList<int3>(CHUNKS_TO_PROCESS_PER_FRAME, Allocator.Persistent);
            
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            // OPTIMIZATION 10.9.4: Pre-compute spiral pattern at startup
            GenerateSpiralPattern();

            // Create Stats Singleton
            var entity = EntityManager.CreateEntity(typeof(VoxelStreamingStats));
            EntityManager.SetName(entity, "VoxelStreamingStats");
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.4: Generate pre-computed spiral pattern sorted by distance.
        /// This eliminates the nested loops in OnUpdate.
        /// </summary>
        /// <summary>
        /// Apply VoxelWorldSettings overrides if present, then regenerate the spiral.
        /// Called lazily on first OnUpdate (after subscene entities exist).
        /// </summary>
        private void ApplySettings()
        {
            if (SystemAPI.HasSingleton<VoxelWorldSettings>())
            {
                var settings = SystemAPI.GetSingleton<VoxelWorldSettings>();
                if (settings.ViewDistance > 0)
                {
                    _viewDistance = settings.ViewDistance;
                    _unloadDistance = _viewDistance + 2;
                }
            }

            GenerateSpiralPattern();
            _settingsApplied = true;
        }

        private void GenerateSpiralPattern()
        {
            // Dispose previous spiral if regenerating
            if (_spiralPattern.IsCreated)
                _spiralPattern.Dispose();

            // Calculate total offsets: (2*viewDistance+1)^2 for XZ plane, extended vertically
            int diameter = 2 * _viewDistance + 1;
            int maxOffsets = diameter * diameter * (2 * MAX_VERTICAL_CHUNKS + 1);

            var offsets = new NativeList<int3>(maxOffsets, Allocator.Temp);
            var distances = new NativeList<float>(maxOffsets, Allocator.Temp);

            // Generate all offsets within view distance
            for (int dy = -MAX_VERTICAL_CHUNKS; dy <= MAX_VERTICAL_CHUNKS; dy++)
            {
                for (int dx = -_viewDistance; dx <= _viewDistance; dx++)
                {
                    for (int dz = -_viewDistance; dz <= _viewDistance; dz++)
                    {
                        // Chebyshev distance for horizontal, separate vertical handling
                        int horizDist = math.max(math.abs(dx), math.abs(dz));
                        if (horizDist <= _viewDistance)
                        {
                            int3 offset = new int3(dx, dy, dz);
                            offsets.Add(offset);

                            // Priority: horizontal distance first, then vertical
                            // This ensures chunks on the same horizontal plane load before vertical
                            float dist = horizDist * 1000 + math.abs(dy);
                            distances.Add(dist);
                        }
                    }
                }
            }

            // Sort by distance using simple insertion sort (done once at startup)
            for (int i = 1; i < offsets.Length; i++)
            {
                int3 keyOffset = offsets[i];
                float keyDist = distances[i];
                int j = i - 1;

                while (j >= 0 && distances[j] > keyDist)
                {
                    offsets[j + 1] = offsets[j];
                    distances[j + 1] = distances[j];
                    j--;
                }
                offsets[j + 1] = keyOffset;
                distances[j + 1] = keyDist;
            }

            // Copy to persistent array
            _spiralPatternLength = offsets.Length;
            _spiralPattern = new NativeArray<int3>(_spiralPatternLength, Allocator.Persistent);
            for (int i = 0; i < _spiralPatternLength; i++)
            {
                _spiralPattern[i] = offsets[i];
            }

            offsets.Dispose();
            distances.Dispose();
        }

        protected override void OnUpdate()
        {
            // Lazy-load settings on first update (after subscene entities exist)
            if (!_settingsApplied)
                ApplySettings();

            using var _ = VoxelProfilerMarkers.ChunkStreaming.Auto();

            var ecb = _ecbSystem.CreateCommandBuffer();

            _chunksToSpawn.Clear();
            _entitiesToUnload.Clear();
            _coordsToUnload.Clear();

            float3 viewerPos = GetViewerPosition();
            int3 viewerChunk = CoordinateUtils.WorldToChunkPos(viewerPos);
            
            // OPTIMIZATION 10.9.4: Check if viewer moved to a new chunk
            bool viewerMoved = !_hasViewerChunkCache || !viewerChunk.Equals(_cachedViewerChunk);
            if (viewerMoved)
            {
                _cachedViewerChunk = viewerChunk;
                _hasViewerChunkCache = true;
                _spiralIndex = 0; // Reset spiral iteration when viewer moves
            }
            
            int opsPerformed = 0;
            
            // Get player layer for vertical culling
            float playerDepth = -viewerPos.y;
            int playerLayer = CaveGenerationService.GetLayerIndexAt(playerDepth);
            if (playerLayer == -1) playerLayer = 0; // Default to surface
            
            // 1. Unload Phase
            if (viewerMoved || (UnityEngine.Time.frameCount % 5 == 0))
            {
                using var keys = _loadedChunks.GetKeyArray(Allocator.Temp);
                foreach (var chunkPos in keys)
                {
                    if (opsPerformed >= CHUNKS_TO_PROCESS_PER_FRAME) break;
                    
                    int dist = math.max(
                        math.max(math.abs(chunkPos.x - viewerChunk.x), math.abs(chunkPos.y - viewerChunk.y)), 
                        math.abs(chunkPos.z - viewerChunk.z)
                    );
                    
                    if (dist > _unloadDistance)
                    {
                        var entity = _loadedChunks[chunkPos];
                        _entitiesToUnload.Add(entity);
                        _coordsToUnload.Add(chunkPos);
                        
                        // We remove from local map immediately so we don't try to unload again next frame
                        // But deletion happens at end of frame via ECB
                        _loadedChunks.Remove(chunkPos);
                        
                        opsPerformed++;
                    }
                }
            }
            
            // 2. Load Phase
            if (opsPerformed < CHUNKS_TO_PROCESS_PER_FRAME)
            {
                int startIndex = _spiralIndex;
                int checkedCount = 0;
                
                while (_spiralIndex < _spiralPatternLength && 
                       opsPerformed < CHUNKS_TO_PROCESS_PER_FRAME &&
                       checkedCount < _spiralPatternLength)
                {
                    int3 offset = _spiralPattern[_spiralIndex];
                    int3 checkPos = viewerChunk + offset;
                    
                    // Vertical culling based on layer
                    float targetDepth = -checkPos.y * VoxelConstants.CHUNK_SIZE;
                    int targetLayer = CaveGenerationService.GetLayerIndexAt(targetDepth);
                    
                    bool shouldLoad = true;
                    
                    if (targetLayer == -1)
                    {
                        // Sky or void
                         if (checkPos.y > 0 && playerLayer == 0) { } 
                         else if (math.abs(offset.y) > _viewDistance) shouldLoad = false;
                    }
                    else
                    {
                        if (math.abs(targetLayer - playerLayer) > LAYER_BUFFER) shouldLoad = false;
                    }
                    
                    if (math.abs(offset.y) > MAX_VERTICAL_CHUNKS) shouldLoad = false;
                    
                    if (shouldLoad && !_loadedChunks.ContainsKey(checkPos))
                    {
                        _chunksToSpawn.Add(checkPos);
                        
                        // Optimization: We must reserve the spot in _loadedChunks immediately
                        // otherwise we might try to spawn it again next frame before ECB playback.
                        // However, we don't have the real Entity yet. 
                        // Strategy: We use Entity.Null for now, or we rely on creating entity immediately via EntityManager.createEntity (batch)
                        // but setting components via ECB? No, that defeats the purpose of avoiding sync points.
                        
                        // Actually, CreateEntity(Archetype, Count) is VERY FAST (1 struct change).
                        // The slow part is AddComponent on dynamic lists.
                        // BUT, to completely avoid structural changes in update, strict ECB usage is preferred.
                        // To solve the "double spawn" issue, we can track "PendingSpawns" in a HashSet?
                        
                        // For simplicity and robustness given the profiler data:
                        // The previous implementation used CreateEntity(Archetype, Count) which is FINE.
                        // The Unload used AddComponent<ChunkNeedsCleanup> which logic suggests is maybe where the cost is if the array is large,
                        // or just the generic overhead.
                        
                        // I will switch to ECB for EVERYTHING.
                        // Pending spawns logic:
                        // I will add to _chunksToSpawn. I will NOT add to _loadedChunks yet.
                        // BUT next frame, I need to know it's pending.
                        // I can add to a _pendingChunks HashSet? 
                        
                        // Wait, simpler: process CreateEntity via EntityManager (1 struct change).
                        // Process AddComponent via ECB.
                        // This removes the component addition struct change.
                        // Actually, I'll stick to full ECB for Unload.
                        // For Spawn, I'll use EntityManager.CreateEntity so I can update _loadedChunks immediately.
                        // The cost of 1 CreateEntity batch is negligible compared to the random access/logic.
                        
                        opsPerformed++;
                    }
                    
                    _spiralIndex++;
                    checkedCount++;
                    
                    if (_spiralIndex >= _spiralPatternLength)
                    {
                        _spiralIndex = 0;
                        if (checkedCount > 0 && opsPerformed == 0) break;
                    }
                }
            }

            // 3. Batch Execution
            
            // Batch Unload - Using ECB to avoid mid-frame structural changes
            if (_entitiesToUnload.Length > 0)
            {
                // Previously: EntityManager.AddComponent<ChunkNeedsCleanup>
                // Now:
                for (int i = 0; i < _entitiesToUnload.Length; i++)
                {
                    ecb.AddComponent<ChunkNeedsCleanup>(_entitiesToUnload[i]);
                }
            }
            
            // Batch Spawn - Keeping EntityManager for immediate Entity ID access
            // This causes 1 structural change, which is acceptable.
            if (_chunksToSpawn.Length > 0)
            {
                var newEntities = EntityManager.CreateEntity(_chunkArchetype, _chunksToSpawn.Length, Allocator.Temp);
                
                for (int i = 0; i < _chunksToSpawn.Length; i++)
                {
                    Entity entity = newEntities[i];
                    int3 chunkPos = _chunksToSpawn[i];
                    
                    EntityManager.SetComponentData(entity, new ChunkPosition { Value = chunkPos });
                    float3 worldPos = CoordinateUtils.ChunkToWorldPos(chunkPos);
                    EntityManager.SetComponentData(entity, Unity.Transforms.LocalTransform.FromPosition(worldPos));
                    EntityManager.SetComponentData(entity, new ChunkMeshState { IsDirty = true });
                    EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(entity, true);
                    
                     // Removed SetName for performance in Release
                    if (EnableDebugLogs) 
                    {
                        var name = new FixedString64Bytes("Chunk_");
                        name.Append(chunkPos.x); name.Append('_'); name.Append(chunkPos.y); name.Append('_'); name.Append(chunkPos.z);
                        EntityManager.SetName(entity, name);
                    }

                    _loadedChunks.Add(chunkPos, entity);
                }
                newEntities.Dispose();
            }
            
            // Update Stats
            if (SystemAPI.HasSingleton<VoxelStreamingStats>())
            {
                 float memPerChunkMB = (VoxelConstants.CHUNK_SIZE * VoxelConstants.CHUNK_SIZE * VoxelConstants.CHUNK_SIZE * 2) / (1024f * 1024f); 
                SystemAPI.SetSingleton(new VoxelStreamingStats
                {
                    LoadedChunks = _loadedChunks.Count,
                    ChunksToSpawnQueue = _chunksToSpawn.Length,
                    ChunksToUnloadQueue = _entitiesToUnload.Length,
                    EstimatedMemoryMB = _loadedChunks.Count * memPerChunkMB
                });
            }
        }

        private float3 GetViewerPosition()
        {
            if (UnityEngine.Camera.main != null)
            {
                return (float3)UnityEngine.Camera.main.transform.position;
            }
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView != null && 
                UnityEditor.SceneView.lastActiveSceneView.camera != null)
            {
                return (float3)UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
            }
            #endif
            return float3.zero;
        }

        private void CleanupChunkResources(Entity e)
        {
            if (EntityManager.HasComponent<ChunkVoxelData>(e))
            {
                var data = EntityManager.GetComponentData<ChunkVoxelData>(e);
                try
                {
                    if (data.IsValid)
                    {
                        data.Data.Dispose();
                    }
                }
                catch (System.InvalidOperationException)
                {
                    // BlobAssetReference already disposed - ignore
                }
            }
            
            if (EntityManager.HasComponent<ChunkGameObject>(e))
            {
               var managed = EntityManager.GetComponentData<ChunkGameObject>(e);
               if (managed != null)
               {
                   if (managed.MeshFilter != null && managed.MeshFilter.sharedMesh != null)
                   {
                       UnityEngine.Object.Destroy(managed.MeshFilter.sharedMesh);
                   }
                   if (managed.Value != null)
                   {
                       // OPTIMIZATION 10.13.4: Return to pool instead of destroying
                       DIG.Voxel.Systems.Meshing.ChunkMeshPool.Return(managed.Value);
                   }
               }
            }

            // NOTE: Do NOT dispose PhysicsCollider here.
            // ChunkColliderBuildSystem uses the ObsoleteChunkCollider pattern for runtime disposal,
            // and ChunkColliderDisposalSystem.OnDestroy handles shutdown cleanup.
            // Manual disposal here causes double-free errors.
        }

        protected override void OnDestroy()
        {
            // Dispose all persistent native collections to prevent memory leaks
            if (_loadedChunks.IsCreated) _loadedChunks.Dispose();
            if (_chunksToSpawn.IsCreated) _chunksToSpawn.Dispose();
            if (_entitiesToUnload.IsCreated) _entitiesToUnload.Dispose();
            if (_coordsToUnload.IsCreated) _coordsToUnload.Dispose();
            if (_spiralPattern.IsCreated) _spiralPattern.Dispose();
        }
    }
}
