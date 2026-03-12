using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using DIG.Voxel.Components;
using DIG.Voxel.Core;

namespace DIG.Voxel.Systems
{
    // Task 8.14.10: Dirty Flag for Cleanup
    public struct ChunkNeedsCleanup : IComponentData { }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct ChunkMemoryCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChunkNeedsCleanup>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            // Iterate all entities marked for cleanup
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<ChunkNeedsCleanup>>().WithEntityAccess())
            {
                // 1. Dispose Voxel Data
                if (state.EntityManager.HasComponent<ChunkVoxelData>(entity))
                {
                    var data = state.EntityManager.GetComponentData<ChunkVoxelData>(entity);
                    try { if (data.IsValid) data.Data.Dispose(); } catch { }
                }

                // 2. Physics Collider - Do NOT dispose manually
                // Unity Physics' ColliderBlobCleanupSystem handles PhysicsCollider blob disposal
                // when entities are destroyed. Manual disposal here causes double-free.
                
                // 3. Destroy GameObject & Mesh
                if (state.EntityManager.HasComponent<ChunkGameObject>(entity))
                {
                    var managed = state.EntityManager.GetComponentData<ChunkGameObject>(entity);
                    if (managed != null)
                    {
                        if (managed.MeshFilter != null && managed.MeshFilter.sharedMesh != null)
                        {
                            UnityEngine.Object.Destroy(managed.MeshFilter.sharedMesh);
                        }
                        
                        if (managed.Value != null) 
                        {
                            UnityEngine.Object.Destroy(managed.Value);
                        }
                    }
                }

                // 4. Destroy Entity (Deferred)
                ecb.DestroyEntity(entity);
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            // Shutdown cleanup: Dispose all remaining VoxelBlob resources
            foreach (var (voxelData, entity) in SystemAPI.Query<RefRO<ChunkVoxelData>>().WithEntityAccess())
            {
                try
                {
                    if (voxelData.ValueRO.IsValid)
                    {
                        var data = state.EntityManager.GetComponentData<ChunkVoxelData>(entity);
                        data.Data.Dispose();
                    }
                }
                catch { }
            }

            // NOTE: Do NOT dispose PhysicsCollider blobs here.
            // Unity Physics has its own ColliderBlobCleanupSystem that handles PhysicsCollider disposal.
            // Disposing them here causes double-free errors on shutdown.
            //
            // ChunkPhysicsState.ColliderBlob is the SAME blob as PhysicsCollider.Value (same reference),
            // so we must not dispose either - let Unity Physics handle it.
        }
    }
    
}
