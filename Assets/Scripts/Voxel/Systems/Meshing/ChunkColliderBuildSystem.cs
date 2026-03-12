using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Meshing;
using DIG.Voxel.Debug;
using DIG.Voxel.Jobs;
using Collider = Unity.Physics.Collider;
using MeshCollider = Unity.Physics.MeshCollider;

namespace DIG.Voxel.Systems.Meshing
{
    /// <summary>
    /// OPTIMIZATION 10.11.2: Dedicated system for building physics colliders.
    /// 
    /// Separates expensive MeshCollider.Create() from visual mesh finalization.
    /// Visual mesh appears immediately, collider follows 1-N frames later.
    /// 
    /// Key features:
    /// - Processes chunks tagged with ChunkNeedsCollider
    /// - Limits to 1 collider creation per frame (configurable)
    /// - Prioritizes chunks closest to player
    /// - Runs AFTER ChunkMeshingSystem in PresentationSystemGroup
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]

    public partial class ChunkColliderBuildSystem : SystemBase
    {
        private const int MAX_COLLIDERS_PER_FRAME = 8;
        private const byte ISO_LEVEL = 127;
        
        // Debug logging
        private static bool s_EnableDebugLogs = false;
        public static bool EnableDebugLogs
        {
            get => s_EnableDebugLogs;
            set => s_EnableDebugLogs = value;
        }
        
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<ChunkNeedsCollider>();
            _ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            if (s_EnableDebugLogs) UnityEngine.Debug.Log($"[ChunkColliderBuild] OnUpdate in {World.Name}");
            
            VoxelProfiler.BeginSample("ColliderBuild");
            
            // Collect candidates
            // We verify the VoxelBlob validity here on main thread
            using var candidates = new NativeList<ColliderCandidate>(Allocator.Temp);
            
            foreach (var (chunkPos, voxelData, entity) in 
                SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxelData>>()
                .WithAll<ChunkNeedsCollider>()
                .WithNone<ChunkNeedsCleanup>() // Fix ArgumentException (don't bake dying entities)
                .WithEntityAccess())
            {
                if (!voxelData.ValueRO.IsValid) continue;
                
                candidates.Add(new ColliderCandidate
                {
                    Entity = entity,
                    ChunkPos = chunkPos.ValueRO.Value,
                    VoxelData = voxelData.ValueRO.Data,
                    
                    // Capture neighbor data for padding
                    NegX = GetNeighborData(entity, new int3(-1, 0, 0)),
                    PosX = GetNeighborData(entity, new int3(1, 0, 0)),
                    NegY = GetNeighborData(entity, new int3(0, -1, 0)),
                    PosY = GetNeighborData(entity, new int3(0, 1, 0)),
                    NegZ = GetNeighborData(entity, new int3(0, 0, -1)),
                    PosZ = GetNeighborData(entity, new int3(0, 0, 1))
                });
                
                if (candidates.Length >= MAX_COLLIDERS_PER_FRAME) break;
            }

            if (s_EnableDebugLogs && candidates.Length > 0)
            {
                UnityEngine.Debug.Log($"[ChunkColliderBuild] Scheduling {candidates.Length} collider jobs");
            }
            
            var ecb = _ecbSystem.CreateCommandBuffer();
            var physicsLookup = SystemAPI.GetComponentLookup<ChunkPhysicsState>(isReadOnly: true);
            var needsColliderLookup = SystemAPI.GetComponentLookup<ChunkNeedsCollider>(isReadOnly: true);
            var physicsColliderLookup = SystemAPI.GetComponentLookup<PhysicsCollider>(isReadOnly: true);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(isReadOnly: true);

            // Schedule jobs for each candidate
            JobHandle dependency = Dependency;
            
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                
                // 1. Allocate Containers (TempJob - must be disposed by last job)
                var paddedDensities = new NativeArray<byte>(VoxelConstants.PADDED_VOLUME, Allocator.TempJob);
                var paddedMaterials = new NativeArray<byte>(VoxelConstants.PADDED_VOLUME, Allocator.TempJob);
                
                var vertices = new NativeList<float3>(Allocator.TempJob);
                var indices = new NativeList<int>(Allocator.TempJob);
                var triangles = new NativeList<int3>(Allocator.TempJob);
                var normals = new NativeList<float3>(Allocator.TempJob);
                var colors = new NativeList<Color32>(Allocator.TempJob);
                
                // 2. Padding Job
                var paddingJob = new CopyPaddedDataJob
                {
                    PaddedDensities = paddedDensities,
                    PaddedMaterials = paddedMaterials,
                    Source = candidate.VoxelData,
                    // Pass pre-fetched neighbor blobs
                    HasNegX = candidate.NegX.IsCreated, NegX = candidate.NegX,
                    HasPosX = candidate.PosX.IsCreated, PosX = candidate.PosX,
                    HasNegY = candidate.NegY.IsCreated, NegY = candidate.NegY,
                    HasPosY = candidate.PosY.IsCreated, PosY = candidate.PosY,
                    HasNegZ = candidate.NegZ.IsCreated, NegZ = candidate.NegZ,
                    HasPosZ = candidate.PosZ.IsCreated, PosZ = candidate.PosZ
                };
                var paddingHandle = paddingJob.Schedule(dependency);

                // 3. Meshing Job
                var meshJob = new GenerateMarchingCubesMeshJob
                {
                    Densities = paddedDensities,
                    Materials = paddedMaterials,
                    ChunkSize = new int3(VoxelConstants.CHUNK_SIZE),
                    IsoLevel = ISO_LEVEL,
                    VertexScale = 1.0f,
                    VoxelStep = 1,
                    Vertices = vertices,
                    Indices = indices,
                    Triangles = triangles,
                    Normals = normals,
                    Colors = colors
                };
                var meshHandle = meshJob.Schedule(paddingHandle);
                
                // 4. Baking Job
                var bakeJob = new BakeColliderJob
                {
                    Entity = candidate.Entity,
                    ChunkPos = candidate.ChunkPos,
                    Vertices = vertices,
                    Triangles = triangles,
                    ECB = ecb,
                    PhysicsLookup = physicsLookup,
                    NeedsColliderLookup = needsColliderLookup,
                    PhysicsColliderLookup = physicsColliderLookup,
                    LocalToWorldLookup = localToWorldLookup
                };
                dependency = bakeJob.Schedule(meshHandle);
                
                // Manual Disposal Chain (Robust for NativeList)
                paddedDensities.Dispose(dependency);
                paddedMaterials.Dispose(dependency);
                vertices.Dispose(dependency);
                indices.Dispose(dependency);
                triangles.Dispose(dependency);
                normals.Dispose(dependency);
                colors.Dispose(dependency);
            }
            
            _ecbSystem.AddJobHandleForProducer(dependency);
            Dependency = dependency;
            
            VoxelProfiler.EndSample("ColliderBuild");
        }
        
        private BlobAssetReference<VoxelBlob> GetNeighborData(Entity entity, int3 offset)
        {
            var neighbors = EntityManager.GetComponentData<ChunkNeighbors>(entity);
            Entity neighborEntity = Entity.Null;
            
            if (offset.x == -1) neighborEntity = neighbors.NegX;
            else if (offset.x == 1) neighborEntity = neighbors.PosX;
            else if (offset.y == -1) neighborEntity = neighbors.NegY;
            else if (offset.y == 1) neighborEntity = neighbors.PosY;
            else if (offset.z == -1) neighborEntity = neighbors.NegZ;
            else if (offset.z == 1) neighborEntity = neighbors.PosZ;
            
            // Fix InvalidOperationException (don't access blobs of dying neighbors)
            if (EntityManager.Exists(neighborEntity) && 
                EntityManager.HasComponent<ChunkVoxelData>(neighborEntity) &&
                !EntityManager.HasComponent<ChunkNeedsCleanup>(neighborEntity))
            {
                 var data = EntityManager.GetComponentData<ChunkVoxelData>(neighborEntity);
                 if (data.IsValid) return data.Data;
            }
            return default;
        }

        private struct ColliderCandidate
        {
            public Entity Entity;
            public int3 ChunkPos;
            public BlobAssetReference<VoxelBlob> VoxelData;
            
            public BlobAssetReference<VoxelBlob> NegX, PosX, NegY, PosY, NegZ, PosZ;
        }

        [BurstCompile]
        private struct BakeColliderJob : IJob
        {
            public Entity Entity;
            public int3 ChunkPos;

            public NativeList<float3> Vertices;
            public NativeList<int3> Triangles;

            public EntityCommandBuffer ECB;
            [ReadOnly] public ComponentLookup<ChunkPhysicsState> PhysicsLookup;
            [ReadOnly] public ComponentLookup<ChunkNeedsCollider> NeedsColliderLookup;
            [ReadOnly] public ComponentLookup<PhysicsCollider> PhysicsColliderLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            public void Execute()
            {
                // Guard: entity may have been destroyed or critical components removed
                if (!NeedsColliderLookup.HasComponent(Entity))
                    return;

                // Additional guard: ensure entity has required components for ECB operations
                bool hasPhysicsCollider = PhysicsColliderLookup.HasComponent(Entity);
                bool hasPhysicsState = PhysicsLookup.HasComponent(Entity);
                bool hasLocalToWorld = LocalToWorldLookup.HasComponent(Entity);

                // Always disable the tag processed
                ECB.SetComponentEnabled<ChunkNeedsCollider>(Entity, false);

                if (Vertices.Length == 0)
                {
                    // Empty mesh - clear collider
                    if (hasPhysicsState)
                    {
                        var old = PhysicsLookup[Entity];
                        if (old.ColliderBlob.IsCreated)
                            ECB.AddComponent(Entity, new ObsoleteChunkCollider { Blob = old.ColliderBlob });
                        ECB.SetComponent(Entity, new ChunkPhysicsState());
                    }
                    if (hasPhysicsCollider)
                    {
                        ECB.SetComponent(Entity, new PhysicsCollider());
                    }
                    return;
                }

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
                    CollisionResponse = CollisionResponsePolicy.Collide
                };

                // Expensive Bake
                BlobAssetReference<Collider> colliderBlob = MeshCollider.Create(
                    Vertices.AsArray(),
                    Triangles.AsArray(),
                    terrainFilter,
                    terrainMaterial
                );

                // Handle old blob disposal and assign new state
                if (hasPhysicsState)
                {
                    var old = PhysicsLookup[Entity];
                    if (old.ColliderBlob.IsCreated)
                        ECB.AddComponent(Entity, new ObsoleteChunkCollider { Blob = old.ColliderBlob });
                    ECB.SetComponent(Entity, new ChunkPhysicsState { ColliderBlob = colliderBlob });
                }
                else
                {
                    // First time - add the component
                    ECB.AddComponent(Entity, new ChunkPhysicsState { ColliderBlob = colliderBlob });
                }

                // Assign PhysicsCollider
                if (hasPhysicsCollider)
                {
                    ECB.SetComponent(Entity, new PhysicsCollider { Value = colliderBlob });
                }
                else
                {
                    // First time - add the component
                    ECB.AddComponent(Entity, new PhysicsCollider { Value = colliderBlob });
                }

                // Add PhysicsWorldIndex (AddSharedComponent is safe even if it exists)
                ECB.AddSharedComponent(Entity, new PhysicsWorldIndex { Value = 0 });

                // LocalToWorld update
                if (hasLocalToWorld)
                {
                    float3 worldPos = CoordinateUtils.ChunkToWorldPos(ChunkPos);
                    ECB.SetComponent(Entity, new LocalToWorld
                    {
                        Value = float4x4.TRS(worldPos, quaternion.identity, new float3(1f, 1f, 1f))
                    });
                }
            }
        }

        protected override void OnDestroy()
        {
            // NOTE: Do NOT dispose ChunkPhysicsState.ColliderBlob here.
            // It's the same blob reference as PhysicsCollider.Value, and Unity Physics
            // has its own ColliderBlobCleanupSystem that handles disposal on shutdown.
            // Manual disposal here causes double-free errors.
        }
    }
}
