using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Meshing;
using DIG.Voxel.Debug;
using Collider = Unity.Physics.Collider;
using MeshCollider = Unity.Physics.MeshCollider;

namespace DIG.Voxel.Systems.Physics
{
    /// <summary>
    /// Creates mesh colliders for chunks on the SERVER using async job processing.
    /// 
    /// Key optimization: Marching cubes runs as a background job, doesn't block main thread.
    /// Only 1 chunk is processed at a time to avoid memory pressure.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Generation.ChunkGenerationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [DisableAutoCreation]
    public partial class ChunkPhysicsColliderSystem : SystemBase
    {
        private const byte ISO_LEVEL = 127;
        private EntityQuery _chunksNeedingColliders;
        
        // Async job state
        private bool _jobInProgress;
        private JobHandle _currentJobHandle;
        private Entity _processingEntity;
        private int3 _processingChunkPos;
        
        // Job output buffers (persistent to avoid allocation every frame)
        private NativeArray<byte> _paddedDensities;
        private NativeArray<byte> _paddedMaterials;
        private NativeList<float3> _vertices;
        private NativeList<float3> _normals;
        private NativeList<UnityEngine.Color32> _colors;
        private NativeList<int> _indices;
        private NativeList<int3> _triangles;
        
        protected override void OnCreate()
        {
            _chunksNeedingColliders = GetEntityQuery(
                ComponentType.ReadOnly<ChunkPosition>(),
                ComponentType.ReadOnly<ChunkVoxelData>(),
                ComponentType.Exclude<PhysicsCollider>()
            );
            
            RequireForUpdate(_chunksNeedingColliders);
            
            // Allocate persistent buffers
            const int PADDED_SIZE = 34;
            _paddedDensities = new NativeArray<byte>(PADDED_SIZE * PADDED_SIZE * PADDED_SIZE, Allocator.Persistent);
            _paddedMaterials = new NativeArray<byte>(PADDED_SIZE * PADDED_SIZE * PADDED_SIZE, Allocator.Persistent);
            _vertices = new NativeList<float3>(4096, Allocator.Persistent);
            _normals = new NativeList<float3>(4096, Allocator.Persistent);
            _colors = new NativeList<UnityEngine.Color32>(4096, Allocator.Persistent);
            _indices = new NativeList<int>(8192, Allocator.Persistent);
            _triangles = new NativeList<int3>(4096, Allocator.Persistent);
            
            UnityEngine.Debug.Log($"[ChunkPhysicsCollider] Created in {World.Name} (SERVER async mesh colliders)");
        }
        
        protected override void OnDestroy()
        {
            // Complete any pending job before destroying buffers
            if (_jobInProgress)
            {
                _currentJobHandle.Complete();
            }
            
            // FIX: Safely dispose all Collider Blobs we created to prevent leaks.
            // We must ALSO reset the PhysicsCollider component to default so Unity's ColliderBlobCleanupSystem
            // doesn't try to dispose it again (which would cause InvalidOperationException).
            using var query = EntityManager.CreateEntityQuery(typeof(ChunkPhysicsState), typeof(PhysicsCollider));
            if (!query.IsEmptyIgnoreFilter)
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                var states = query.ToComponentDataArray<ChunkPhysicsState>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    if (states[i].ColliderBlob.IsCreated)
                    {
                        states[i].ColliderBlob.Dispose();
                    }
                    
                    // CRITICAL: Reset PhysicsCollider to null/empty so Unity doesn't double-free
                    if (EntityManager.HasComponent<PhysicsCollider>(entities[i]))
                    {
                        EntityManager.SetComponentData(entities[i], new PhysicsCollider());
                    }
                }
                entities.Dispose();
                states.Dispose();
            }
            
            if (_paddedDensities.IsCreated) _paddedDensities.Dispose();
            if (_paddedMaterials.IsCreated) _paddedMaterials.Dispose();
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_normals.IsCreated) _normals.Dispose();
            if (_colors.IsCreated) _colors.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            if (_triangles.IsCreated) _triangles.Dispose();
        }
        
        protected override void OnUpdate()
        {
            using var _ = VoxelProfilerMarkers.ChunkPhysics.Auto();

            // OPTIMIZATION 10.11.1: Track if we created a collider this frame
            bool colliderCreatedThisFrame = false;

            // Check if previous job completed
            if (_jobInProgress)
            {
                if (_currentJobHandle.IsCompleted)
                {
                    _currentJobHandle.Complete();
                    _jobInProgress = false;
                    
                    // Create collider from job results
                    if (_triangles.Length > 0 && EntityManager.Exists(_processingEntity))
                    {
                        using var createMarker = VoxelProfilerMarkers.ChunkPhysics_CreateCollider.Auto();
                        CreateColliderFromJobResults(_processingEntity, _processingChunkPos);
                        colliderCreatedThisFrame = true;
                    }
                    
                    // Clear buffers for next chunk
                    _vertices.Clear();
                    _normals.Clear();
                    _colors.Clear();
                    _indices.Clear();
                    _triangles.Clear();
                    
                    // OPTIMIZATION 10.11.1: Return early after creating collider
                    // This spreads the work across frames: Frame N creates collider, Frame N+1 schedules next job
                    if (colliderCreatedThisFrame)
                        return;
                }
                else
                {
                    // Job still running, wait for next frame
                    return;
                }
            }
            
            // Find next chunk to process
            var entities = _chunksNeedingColliders.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                if (!EntityManager.HasComponent<ChunkVoxelData>(entity))
                    continue;
                    
                var voxelData = EntityManager.GetComponentData<ChunkVoxelData>(entity);
                var chunkPos = EntityManager.GetComponentData<ChunkPosition>(entity);
                
                if (!voxelData.Data.IsCreated)
                    continue;
                
                // OPTIMIZATION 10.11.3: Skip colliders for distant chunks (LOD >= 2)
                if (EntityManager.HasComponent<ChunkLODState>(entity))
                {
                    var lodState = EntityManager.GetComponentData<ChunkLODState>(entity);
                    if (lodState.CurrentLOD > 1)
                    {
                        // Mark as complete without building collider
                        if (EntityManager.HasComponent<ChunkPhysicsState>(entity))
                        {
                            var state = EntityManager.GetComponentData<ChunkPhysicsState>(entity);
                            // Already has state, just leave it
                        }
                        else
                        {
                            // Add empty physics state to mark as "processed"
                            EntityManager.AddComponentData(entity, new ChunkPhysicsState { ColliderBlob = default });
                        }
                        continue; // Skip to next chunk
                    }
                }
                
                // Start processing this chunk
                StartAsyncMeshGeneration(entity, chunkPos.Value, voxelData.Data);
                break; // Only process one chunk at a time
            }
            
            entities.Dispose();
        }
        
        private void StartAsyncMeshGeneration(Entity entity, int3 chunkPos, BlobAssetReference<VoxelBlob> blob)
        {
            _processingEntity = entity;
            _processingChunkPos = chunkPos;
            
            // Get Neighbors for seamless collisions
            bool hasPosX = false, hasNegX = false, hasPosY = false, hasNegY = false, hasPosZ = false, hasNegZ = false;
            BlobAssetReference<VoxelBlob> posX = default, negX = default, posY = default, negY = default, posZ = default, negZ = default;

            if (EntityManager.HasComponent<ChunkNeighbors>(entity))
            {
                var neighbors = EntityManager.GetComponentData<ChunkNeighbors>(entity);
                
                if (neighbors.PosX != Entity.Null && EntityManager.Exists(neighbors.PosX) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.PosX))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.PosX);
                     if (data.IsValid) { hasPosX = true; posX = data.Data; }
                }
                if (neighbors.NegX != Entity.Null && EntityManager.Exists(neighbors.NegX) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.NegX))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.NegX);
                     if (data.IsValid) { hasNegX = true; negX = data.Data; }
                }
                if (neighbors.PosY != Entity.Null && EntityManager.Exists(neighbors.PosY) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.PosY))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.PosY);
                     if (data.IsValid) { hasPosY = true; posY = data.Data; }
                }
                if (neighbors.NegY != Entity.Null && EntityManager.Exists(neighbors.NegY) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.NegY))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.NegY);
                     if (data.IsValid) { hasNegY = true; negY = data.Data; }
                }
                if (neighbors.PosZ != Entity.Null && EntityManager.Exists(neighbors.PosZ) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.PosZ))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.PosZ);
                     if (data.IsValid) { hasPosZ = true; posZ = data.Data; }
                }
                if (neighbors.NegZ != Entity.Null && EntityManager.Exists(neighbors.NegZ) && EntityManager.HasComponent<ChunkVoxelData>(neighbors.NegZ))
                {
                     var data = EntityManager.GetComponentData<ChunkVoxelData>(neighbors.NegZ);
                     if (data.IsValid) { hasNegZ = true; negZ = data.Data; }
                }
            }
            
            // Schedule Copy Job (Replaces main thread loop)
            var copyJob = new DIG.Voxel.Jobs.CopyPaddedDataJob
            {
                Source = blob,
                PaddedDensities = _paddedDensities,
                PaddedMaterials = _paddedMaterials,
                HasPosX = hasPosX, PosX = posX,
                HasNegX = hasNegX, NegX = negX,
                HasPosY = hasPosY, PosY = posY,
                HasNegY = hasNegY, NegY = negY,
                HasPosZ = hasPosZ, PosZ = posZ,
                HasNegZ = hasNegZ, NegZ = negZ
            };
            
            var copyHandle = copyJob.Schedule();

            // Schedule marching cubes job
            var job = new GenerateMarchingCubesMeshJob
            {
                Densities = _paddedDensities,
                Materials = _paddedMaterials,
                ChunkSize = new int3(32, 32, 32),
                IsoLevel = ISO_LEVEL,
                VertexScale = 1.0f,
                Vertices = _vertices,
                Normals = _normals,
                Colors = _colors,
                Indices = _indices,
                Triangles = _triangles
            };
            
            _currentJobHandle = job.Schedule(copyHandle);
            _jobInProgress = true;
        }
        
        private void CreateColliderFromJobResults(Entity entity, int3 chunkPos)
        {
            var terrainFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = ~0u,
                GroupIndex = 0
            };
            
            var terrainMaterial = new Unity.Physics.Material
            {
                Friction = 0.5f,
                Restitution = 0f,
                CollisionResponse = CollisionResponsePolicy.Collide,
                CustomTags = 0,
                FrictionCombinePolicy = Unity.Physics.Material.CombinePolicy.GeometricMean,
                RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.GeometricMean
            };
            
            BlobAssetReference<Collider> colliderBlob = MeshCollider.Create(
                _vertices.AsArray(),
                _triangles.AsArray(),
                terrainFilter,
                terrainMaterial
            );
            
            // Handle replacement if updating
            if (EntityManager.HasComponent<PhysicsCollider>(entity))
            {
                if (EntityManager.HasComponent<ChunkPhysicsState>(entity))
                {
                    var oldState = EntityManager.GetComponentData<ChunkPhysicsState>(entity);
                    if (oldState.ColliderBlob.IsCreated)
                    {
                         EntityManager.AddComponentData(entity, new ObsoleteChunkCollider { Blob = oldState.ColliderBlob });
                    }
                }
                
                EntityManager.SetComponentData(entity, new PhysicsCollider { Value = colliderBlob });
                EntityManager.SetComponentData(entity, new ChunkPhysicsState { ColliderBlob = colliderBlob });
            }
            else
            {
                EntityManager.AddComponentData(entity, new PhysicsCollider { Value = colliderBlob });
                EntityManager.AddComponentData(entity, new ChunkPhysicsState { ColliderBlob = colliderBlob });
                EntityManager.AddSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
            }
            // PhysicsWorldIndex is ISharedComponentData.
            // EntityManager.AddSharedComponent(entity, component) is the API. 
            // NOTE: Recent Entities versions use AddSharedComponent or AddSharedComponentManaged depending on unmanaged/managed constraint.
            // PhysicsWorldIndex is unmanaged.
            
            float3 worldPos = CoordinateUtils.ChunkToWorldPos(chunkPos);
            EntityManager.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(worldPos, quaternion.identity, new float3(1f, 1f, 1f))
            });
            
            // UnityEngine.Debug.Log($"[ChunkPhysicsCollider] {World.Name}: Created mesh collider for chunk {chunkPos}");
        }
    }
}
