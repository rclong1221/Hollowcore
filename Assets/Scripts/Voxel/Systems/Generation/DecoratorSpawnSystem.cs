using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// System that spawns decorators on chunk surfaces.
    /// Runs after chunk generation. Client-side only (decorators are visual, not networked).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Voxel.Systems.ChunkLookupSystem))]
    public partial class DecoratorSpawnSystem : SystemBase
    {
        private struct PendingDecoration
        {
            public int3 ChunkPos;
            public Entity ChunkEntity;
            public JobHandle DetectionHandle;
            public NativeList<DecoratorService.SurfacePoint> Surfaces;
            public bool Phase2Ready;
            public JobHandle PlacementHandle;
            public NativeList<DecoratorService.DecoratorPlacement> Placements;
        }
        
        // OPTIMIZATION 10.5.10 + 10.5.13: Queued placement for batched instantiation with priority
        private struct QueuedPlacement
        {
            public int3 ChunkPos;
            public DecoratorService.DecoratorPlacement Placement;
            public float DistanceSq; // OPTIMIZATION 10.5.13: Distance to player for priority sorting
        }
        
        // Use managed List since PendingDecoration contains NativeList (not blittable)
        private List<PendingDecoration> _pendingJobs;
        private EntityQuery _newChunksQuery;
        private const int MAX_CONCURRENT_JOBS = 4;
        private const int MAX_SURFACES_PER_CHUNK = 8192; // Increased to 8192 to handle complex caves
        
        // OPTIMIZATION 10.5.10 + 10.5.13: Prioritized instantiation with frame budget
        private List<QueuedPlacement> _instantiationQueue;
        private bool _queueDirty = false; // Track if queue needs re-sorting
        private const int MAX_INSTANTIATIONS_PER_FRAME = 15;
        
        // Cave sampling indices (computed based on CHUNK_SIZE = 32)
        // 0, 31, 992, 31744, 32767, 16384, 16, 528, 16912
        private static readonly int[] _caveSampleIndices = new int[] 
        { 
             0, 31, 992, 31744, 32767, 16384, 16, 528, 16912 
        };
        
        protected override void OnCreate()
        {
            _pendingJobs = new List<PendingDecoration>(MAX_CONCURRENT_JOBS);
            _instantiationQueue = new List<QueuedPlacement>(256);
            
            // Load decorator registry
            var registry = Resources.Load<DecoratorRegistry>("DecoratorRegistry");
            if (registry != null)
            {
                DecoratorService.Initialize(registry);
                
                // OPTIMIZATION 10.5.9: Register prefabs with pool
                // OPTIMIZATION 10.5.12: Register GPU instanced decorators
                if (registry.Decorators != null)
                {
                    foreach (var dec in registry.Decorators)
                    {
                        if (dec == null) continue;
                        
                        // GPU Instancing path
                        if (dec.UseGPUInstancing)
                        {
                            // Get mesh from decorator's InstancedMesh or extract from prefab
                            Mesh mesh = dec.InstancedMesh;
                            Material mat = dec.InstancedMaterial;
                            
                            if (mesh == null && dec.Prefab != null)
                            {
                                var meshFilter = dec.Prefab.GetComponent<MeshFilter>();
                                if (meshFilter != null) mesh = meshFilter.sharedMesh;
                            }
                            
                            if (mat == null && dec.Prefab != null)
                            {
                                var renderer = dec.Prefab.GetComponent<MeshRenderer>();
                                if (renderer != null) mat = renderer.sharedMaterial;
                            }
                            
                            if (mesh != null && mat != null)
                            {
                                DecoratorInstancingSystem.Instance.RegisterForInstancing(dec.DecoratorID, mesh, mat);
                            }
                        }
                        
                        // Pool path (for non-instanced or as fallback)
                        if (dec.Prefab != null)
                        {
                            DecoratorPool.Instance.RegisterPrefab(dec.DecoratorID, dec.Prefab);
                        }
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[DecoratorSpawnSystem] DecoratorRegistry not found in Resources. Decorators disabled.");
                Enabled = false;
                return;
            }
            
            // Query for chunks that just finished generation and need decoration
            _newChunksQuery = GetEntityQuery(
                ComponentType.ReadOnly<ChunkPosition>(),
                ComponentType.ReadOnly<ChunkVoxelData>(),
                ComponentType.Exclude<ChunkDecorated>() // Tag to prevent re-processing
            );
        }
        
        protected override void OnDestroy()
        {
            // Complete all pending jobs
            for (int i = 0; i < _pendingJobs.Count; i++)
            {
                var pending = _pendingJobs[i];
                pending.DetectionHandle.Complete();
                pending.PlacementHandle.Complete();
                if (pending.Surfaces.IsCreated) pending.Surfaces.Dispose();
                if (pending.Placements.IsCreated) pending.Placements.Dispose();
            }
            _pendingJobs.Clear();
            
            // Manage lifecycle via reference counting
            DecoratorService.Dispose();
        }
        
        protected override void OnUpdate()
        {
            if (!DecoratorService.IsInitialized) return;
            
            // OPTIMIZATION 10.5.10: Process queued instantiations with frame budget
            ProcessInstantiationQueue();
            
            // Check completed jobs
            ProcessCompletedJobs();
            
            // Schedule new jobs for undecorated chunks
            ScheduleNewJobs();
        }
        
        private void ProcessCompletedJobs()
        {
            for (int i = _pendingJobs.Count - 1; i >= 0; i--)
            {
                var pending = _pendingJobs[i];
                
                // Phase 1: Surface Detection
                if (!pending.Phase2Ready)
                {
                    if (pending.DetectionHandle.IsCompleted)
                    {
                        pending.DetectionHandle.Complete();
                        
                        // Schedule placement job
                        if (pending.Surfaces.Length > 0)
                        {
                            pending.Placements = new NativeList<DecoratorService.DecoratorPlacement>(
                                DecoratorService.MaxDecoratorsPerChunk, Allocator.Persistent);
                            
                            // Calculate non-zero seed
                            uint chunkSeed = (uint)(pending.ChunkPos.x * 73856093 ^ pending.ChunkPos.y * 19349663 ^ pending.ChunkPos.z * 83492791);
                            if (chunkSeed == 0) chunkSeed = 1;

                            var placementJob = new DecoratorPlacementJob
                            {
                                Surfaces = pending.Surfaces.AsArray(),
                                DecoratorParams = DecoratorService.DecoratorParamsArray,
                                FloorDecorators = DecoratorService.FloorDecorators,
                                CeilingDecorators = DecoratorService.CeilingDecorators,
                                WallDecorators = DecoratorService.WallDecorators,
                                Seed = chunkSeed,
                                ChunkDepth = pending.ChunkPos.y * VoxelConstants.CHUNK_SIZE,
                                MaxDecorators = DecoratorService.MaxDecoratorsPerChunk,
                                Placements = pending.Placements
                            };
                            
                            pending.PlacementHandle = placementJob.Schedule();
                            pending.Phase2Ready = true;
                            _pendingJobs[i] = pending;
                        }
                        else
                        {
                            // No surfaces, mark as done
                            MarkChunkDecorated(pending.ChunkEntity);
                            pending.Surfaces.Dispose();
                            _pendingJobs.RemoveAt(i);
                        }
                    }
                }
                // Phase 2: Placement complete - instantiate prefabs
                else if (pending.PlacementHandle.IsCompleted)
                {
                    pending.PlacementHandle.Complete();
                    
                    // Instantiate decorators (main thread)
                    InstantiateDecorators(pending.ChunkPos, pending.Placements);
                    
                    // Cleanup
                    MarkChunkDecorated(pending.ChunkEntity);
                    pending.Surfaces.Dispose();
                    pending.Placements.Dispose();
                    _pendingJobs.RemoveAt(i);
                }
            }
        }
        
        private void ScheduleNewJobs()
        {
            if (_pendingJobs.Count >= MAX_CONCURRENT_JOBS) return;
            
            var entities = _newChunksQuery.ToEntityArray(Allocator.Temp);
            
            for (int i = 0; i < entities.Length && _pendingJobs.Count < MAX_CONCURRENT_JOBS; i++)
            {
                var entity = entities[i];
                
                // Get chunk data
                var chunkPos = EntityManager.GetComponentData<ChunkPosition>(entity).Value;
                var voxelData = EntityManager.GetComponentData<ChunkVoxelData>(entity);
                
                // Skip if blob is not valid
                if (!voxelData.IsValid)
                    continue;
                
                // OPTIMIZATION 10.5.7: Early-out for solid/empty chunks
                if (!HasCaveSurfaces(ref voxelData.Data.Value))
                {
                    // No surfaces possible - mark as done without scheduling job
                    MarkChunkDecorated(entity);
                    continue;
                }
                
                // OPTIMIZATION 10.5.6: No data copy - pass blob reference directly to job
                int voxelCount = VoxelConstants.CHUNK_SIZE * VoxelConstants.CHUNK_SIZE * VoxelConstants.CHUNK_SIZE;
                var surfaces = new NativeList<DecoratorService.SurfacePoint>(MAX_SURFACES_PER_CHUNK, Allocator.Persistent);
                
                // Schedule surface detection with blob reference
                var detectionJob = new SurfaceDetectionJob
                {
                    ChunkWorldOrigin = chunkPos * VoxelConstants.CHUNK_SIZE,
                    VoxelData = voxelData.Data,
                    ChunkSize = VoxelConstants.CHUNK_SIZE,
                    Depth = chunkPos.y * VoxelConstants.CHUNK_SIZE,
                    Surfaces = surfaces.AsParallelWriter()
                };
                
                var handle = detectionJob.Schedule(voxelCount, 256);
                
                _pendingJobs.Add(new PendingDecoration
                {
                    ChunkPos = chunkPos,
                    ChunkEntity = entity,
                    DetectionHandle = handle,
                    Surfaces = surfaces,
                    Phase2Ready = false
                });
                
                // Mark chunk as processing (add tag early to prevent re-scheduling)
                EntityManager.AddComponent<ChunkDecorated>(entity);
            }
            
            entities.Dispose();
        }
        
        private void InstantiateDecorators(int3 chunkPos, NativeList<DecoratorService.DecoratorPlacement> placements)
        {
            // OPTIMIZATION 10.5.10 + 10.5.13: Queue placements with distance for priority sorting
            var playerPos = GetPlayerChunkPosition();
            float3 chunkCenter = new float3(chunkPos) * Core.VoxelConstants.CHUNK_SIZE + Core.VoxelConstants.CHUNK_SIZE * 0.5f;
            float3 playerWorldPos = new float3(playerPos) * Core.VoxelConstants.CHUNK_SIZE;
            float distSq = math.distancesq(chunkCenter, playerWorldPos);
            
            for (int i = 0; i < placements.Length; i++)
            {
                _instantiationQueue.Add(new QueuedPlacement
                {
                    ChunkPos = chunkPos,
                    Placement = placements[i],
                    DistanceSq = distSq
                });
            }
            
            _queueDirty = true; // Mark for re-sort
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.10 + 10.5.13: Process instantiation queue with frame budget and priority.
        /// Sorts by distance and spawns closest decorators first.
        /// </summary>
        private void ProcessInstantiationQueue()
        {
            if (_instantiationQueue.Count == 0) return;
            
            // OPTIMIZATION 10.5.13: Sort by distance (closest first) if dirty
            if (_queueDirty)
            {
                _instantiationQueue.Sort((a, b) => a.DistanceSq.CompareTo(b.DistanceSq));
                _queueDirty = false;
            }
            
            int spawned = 0;
            
            // OPTIMIZATION 10.5.11: Get player position for LOD filtering
            var playerPos = GetPlayerChunkPosition();
            
            // Process from front (closest first due to sort)
            while (_instantiationQueue.Count > 0 && spawned < MAX_INSTANTIATIONS_PER_FRAME)
            {
                var queued = _instantiationQueue[0];
                _instantiationQueue.RemoveAt(0);
                var placement = queued.Placement;
                
                // OPTIMIZATION 10.5.17: Hierarchical chunk-based culling
                // Skip entire chunk if it's occluded (before any per-decorator checks)
                if (DecoratorInstancingSystem.Instance.IsChunkOccluded(queued.ChunkPos))
                    continue;
                
                // OPTIMIZATION 10.5.11: LOD distance check
                if (!ShouldSpawnAtDistance(placement.DecoratorID, queued.ChunkPos, playerPos))
                    continue;
                
                var definition = DecoratorService.GetDefinition(placement.DecoratorID);
                
                if (definition == null)
                    continue;
                
                // Calculate rotation
                Quaternion rotation;
                if (definition.AlignToSurface)
                {
                    var up = (Vector3)placement.Normal;
                    var forward = math.abs(up.y) > 0.9f 
                        ? new float3(0, 0, 1) 
                        : math.normalize(math.cross(up, new float3(0, 1, 0)));
                    rotation = Quaternion.LookRotation(forward, up);
                    rotation *= Quaternion.Euler(0, math.degrees(placement.YRotation), 0);
                }
                else
                {
                    rotation = Quaternion.Euler(0, math.degrees(placement.YRotation), 0);
                }
                
                // OPTIMIZATION 10.5.12 + 10.5.16: GPU Instancing path with occlusion support
                if (definition.UseGPUInstancing && 
                    DecoratorInstancingSystem.Instance.IsInstancedDecorator(placement.DecoratorID))
                {
                    DecoratorInstancingSystem.Instance.AddInstance(
                        placement.DecoratorID,
                        placement.Position,
                        rotation,
                        placement.Scale,
                        queued.ChunkPos // OPTIMIZATION 10.5.16: Pass chunk for occlusion culling
                    );
                }
                // OPTIMIZATION 10.5.9: Object pool path
                else if (definition.Prefab != null)
                {
                    DecoratorPool.Instance.Get(
                        placement.DecoratorID, 
                        queued.ChunkPos, 
                        placement.Position, 
                        rotation, 
                        placement.Scale
                    );
                }
                
                spawned++;
            }
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.11: Check if decorator should spawn based on distance.
        /// </summary>
        private bool ShouldSpawnAtDistance(byte decoratorID, int3 chunkPos, int3 playerChunkPos)
        {
            if (decoratorID >= DecoratorService.DecoratorParamsArray.Length)
                return false;
            
            var param = DecoratorService.DecoratorParamsArray[decoratorID];
            byte maxDist = param.MaxChunkDistance;
            
            // Critical always spawns
            if (maxDist >= 255) return true;
            
            // Calculate chunk distance (Manhattan distance for performance)
            int dist = math.abs(chunkPos.x - playerChunkPos.x) +
                       math.abs(chunkPos.y - playerChunkPos.y) +
                       math.abs(chunkPos.z - playerChunkPos.z);
            
            return dist <= maxDist;
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.11: Get player chunk position for LOD filtering.
        /// Uses Camera.main position since decorator system is client-side visual only.
        /// </summary>
        private int3 GetPlayerChunkPosition()
        {
            // Use camera position (client-side, so Camera.main is reliable)
            if (Camera.main != null)
            {
                var pos = Camera.main.transform.position;
                return new int3(
                    (int)math.floor(pos.x / Core.VoxelConstants.CHUNK_SIZE),
                    (int)math.floor(pos.y / Core.VoxelConstants.CHUNK_SIZE),
                    (int)math.floor(pos.z / Core.VoxelConstants.CHUNK_SIZE)
                );
            }
            
            return int3.zero;
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.7: Quick check if chunk has cave surfaces.
        /// Returns false if chunk is all solid or all air (no surfaces possible).
        /// Uses sampling to avoid checking all 32,768 voxels.
        /// </summary>
        private bool HasCaveSurfaces(ref VoxelBlob blob)
        {
            const byte SOLID_THRESHOLD = 128;
            int chunkSize = VoxelConstants.CHUNK_SIZE;
            int totalVoxels = chunkSize * chunkSize * chunkSize;
            
            // Sample strategy: check corners, center, and random samples
            // If all samples are the same type (solid or air), likely no surfaces
            
            bool hasSolid = false;
            bool hasAir = false;
            
            // Static indices to avoid allocation
            // Check 8 corners + center
            // (Defined at class level to avoid allocation)
            
            foreach (int idx in _caveSampleIndices)
            {
                if (idx >= 0 && idx < totalVoxels)
                {
                    if (blob.Densities[idx] < SOLID_THRESHOLD)
                        hasSolid = true;
                    else
                        hasAir = true;
                    
                    // If we found both types, there are surfaces
                    if (hasSolid && hasAir)
                        return true;
                }
            }
            
            // Do a sparse scan if samples were all same type
            // Check every 4th voxel on each axis (skip factor = 4 means 1/64 of voxels)
            const int SKIP = 4;
            for (int z = 0; z < chunkSize; z += SKIP)
            {
                for (int y = 0; y < chunkSize; y += SKIP)
                {
                    for (int x = 0; x < chunkSize; x += SKIP)
                    {
                        int idx = x + y * chunkSize + z * chunkSize * chunkSize;
                        if (blob.Densities[idx] < SOLID_THRESHOLD)
                            hasSolid = true;
                        else
                            hasAir = true;
                        
                        if (hasSolid && hasAir)
                            return true;
                    }
                }
            }
            
            // All samples were same type - no surfaces
            return false;
        }
        
        private void MarkChunkDecorated(Entity entity)
        {
            // Guard: Entity may have been destroyed (chunk unloaded) before job completed
            if (!EntityManager.Exists(entity)) return;
            
            if (!EntityManager.HasComponent<ChunkDecorated>(entity))
            {
                EntityManager.AddComponent<ChunkDecorated>(entity);
            }
        }
    }
    
    /// <summary>
    /// Tag component to mark chunks that have been processed for decorators.
    /// </summary>
    public struct ChunkDecorated : IComponentData { }
}
