using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Rendering;
using DIG.Voxel.Components;
using DIG.Voxel.Core;
using DIG.Voxel.Decorators;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Systems.Meshing
{
    /// <summary>
    /// OPTIMIZATION 10.2.16: Occlusion Culling for Cave Chambers.
    /// Performs a BFS Flood Fill from the camera to determine visible chunks.
    /// Chunks that are unreachable (locked behind solid chunks) are culled.
    /// 
    /// OPTIMIZATION 10.9.3: GC Elimination
    /// Uses persistent NativeHashSet and NativeQueue instead of managed collections.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChunkVisibilitySystem : SystemBase
    {
        // Debug toggle - set true to enable verbose logging  
        public static bool EnableDebugLogs { get; set; } = false;
        
        private EntityQuery _chunkQuery;
        
        // OPTIMIZATION 10.9.3: Persistent native collections
        private NativeHashSet<int3> _visibleChunks;
        private NativeQueue<int3> _bfsQueue;
        
        // OPTIMIZATION 10.9.16: Cache previous camera chunk to skip redundant BFS
        private int3 _lastCameraChunk;
        private bool _hasRunOnce;
        
        // Initial capacity - will grow as needed
        private const int INITIAL_CAPACITY = 512;
        
        protected override void OnCreate()
        {
            _chunkQuery = GetEntityQuery(
                ComponentType.ReadOnly<ChunkPosition>(),
                ComponentType.ReadOnly<ChunkGameObject>() // Targets Hybrid V1 GameObjects
            );
            
            // OPTIMIZATION 10.9.3: Allocate persistent collections once
            _visibleChunks = new NativeHashSet<int3>(INITIAL_CAPACITY, Allocator.Persistent);
            _bfsQueue = new NativeQueue<int3>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            // OPTIMIZATION 10.9.3: Properly dispose persistent collections
            if (_visibleChunks.IsCreated) _visibleChunks.Dispose();
            if (_bfsQueue.IsCreated) _bfsQueue.Dispose();
        }

        protected override void OnUpdate()
        {
            using var _ = VoxelProfilerMarkers.ChunkVisibility.Auto();

            // OPTIMIZATION 10.9.5: Use cached camera data instead of Camera.main
            if (!SystemAPI.HasSingleton<CameraData>()) return;
            var cameraData = SystemAPI.GetSingleton<CameraData>();
            if (!cameraData.IsValid) return;
            
            float3 camPos = cameraData.Position;
            int3 centerChunk = cameraData.ChunkPosition;
            
            // OPTIMIZATION 10.9.16: Early exit if camera hasn't moved chunks
            // But force update occasionally to handle dynamically loaded/unloaded chunks
            if (_hasRunOnce && math.all(centerChunk == _lastCameraChunk) && UnityEngine.Time.frameCount % 60 != 0) 
                return;
                
            _lastCameraChunk = centerChunk;
            _hasRunOnce = true;
            
            // Only update every 5 frames (throttle expensive BFS)
            if (UnityEngine.Time.frameCount % 5 != 0) return;
            
            // OPTIMIZATION 10.9.16: Use ChunkLookup instead of building map
            if (!SystemAPI.HasSingleton<ChunkLookup>()) return;
            var chunkLookup = SystemAPI.GetSingleton<ChunkLookup>();
            if (!chunkLookup.IsInitialized || !chunkLookup.ChunkMap.IsCreated) return;
            
            var chunkMap = chunkLookup.ChunkMap;
            
            // 1. Reset Visibility
            _visibleChunks.Clear();
            _bfsQueue.Clear();
            
            // 2. BFS Flood Fill
            _bfsQueue.Enqueue(centerChunk);
            _visibleChunks.Add(centerChunk);
            
            int viewDistChunks = 8; 
            
            var chunkStatsLookup = GetComponentLookup<ChunkDensityStats>(true);
            
            // BFS loop
            while (_bfsQueue.TryDequeue(out int3 current))
            {
                // Get neighbors
                for (int i = 0; i < 6; i++)
                {
                    int3 neighbor = current + VoxelDirections.IntDirections[i];
                    
                    if (_visibleChunks.Contains(neighbor)) continue;
                    
                    // Check distance
                    int3 delta = neighbor - centerChunk;
                    int maxDist = math.max(math.max(math.abs(delta.x), math.abs(delta.y)), math.abs(delta.z));
                    if (maxDist > viewDistChunks) continue;
                    
                    // Check if neighbor exists
                    if (chunkMap.TryGetValue(neighbor, out Entity neighborEntity))
                    {
                        // Add to visible set
                        _visibleChunks.Add(neighbor);
                        
                        // Check transparency for propagation
                        if (chunkStatsLookup.HasComponent(neighborEntity))
                        {
                            var stats = chunkStatsLookup[neighborEntity];
                            // Only enqueue if not fully solid
                            if (!stats.IsFull)
                            {
                                _bfsQueue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }
            
            // 3. Apply Visibility
            var fluidLookup = GetComponentLookup<DIG.Voxel.Fluids.FluidMeshReference>(true);
            var visibilityLookup = GetComponentLookup<ChunkVisibility>(false);
            
            // Iterate all chunks to set visibility
            // Note: We iterate query here because we need to turn OFF occlusion for chunks not in set
            using var chunkEntities = _chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunkPositions = _chunkQuery.ToComponentDataArray<ChunkPosition>(Allocator.Temp);
            
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                int3 pos = chunkPositions[i].Value;
                bool isVisible = _visibleChunks.Contains(pos);
                Entity entity = chunkEntities[i];
                
                // HYBRID V1: Toggle GameObject
                if (EntityManager.HasComponent<ChunkGameObject>(entity))
                {
                    var chunkGo = EntityManager.GetComponentData<ChunkGameObject>(entity);
                    if (chunkGo != null && chunkGo.Value != null)
                    {
                        if (chunkGo.Value.activeSelf != isVisible)
                            chunkGo.Value.SetActive(isVisible);
                    }
                }
                
                // Toggle Fluid Renderer (Still uses GameObjects for now potentially?)
                if (fluidLookup.HasComponent(entity))
                {
                    var fluidRef = fluidLookup[entity];
                    if (EntityManager.Exists(fluidRef.MeshEntity) && EntityManager.HasComponent<MeshRenderer>(fluidRef.MeshEntity))
                    {
                        var fluidRenderer = EntityManager.GetComponentObject<MeshRenderer>(fluidRef.MeshEntity);
                        if (fluidRenderer != null && fluidRenderer.enabled != isVisible)
                            fluidRenderer.enabled = isVisible;
                    }
                }
                
                // Update Decorator System
                DecoratorInstancingSystem.Instance?.SetChunkOccluded(pos, !isVisible);
                
                // Update Chunk Visibility Component
                if (visibilityLookup.HasComponent(entity))
                {
                    visibilityLookup[entity] = new ChunkVisibility { IsVisible = isVisible };
                }
            }
            
            // DEBUG LOGGING (gated)
            if (EnableDebugLogs && UnityEngine.Time.frameCount % 60 == 0)
            {
               UnityEngine.Debug.Log($"[ChunkVisibility] BFS Visited: {_visibleChunks.Count}, Total Chunks: {chunkEntities.Length}");
            }
        }
    }
}

