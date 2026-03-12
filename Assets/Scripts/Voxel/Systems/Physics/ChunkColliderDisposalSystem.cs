using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Physics
{
    /// <summary>
    /// Safely disposes old physics colliders after the physics simulation
    /// has finished using them for the frame.
    /// This prevents InvalidOperationException race conditions during re-meshing.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct ChunkColliderDisposalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObsoleteChunkCollider>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (obsolete, entity) in SystemAPI.Query<RefRO<ObsoleteChunkCollider>>().WithEntityAccess())
            {
                if (obsolete.ValueRO.Blob.IsCreated)
                {
                    obsolete.ValueRO.Blob.Dispose();
                }
                ecb.RemoveComponent<ObsoleteChunkCollider>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        public void OnDestroy(ref SystemState state)
        {
            // Clean up any remaining obsolete blobs on shutdown
            foreach (var obsolete in SystemAPI.Query<RefRO<ObsoleteChunkCollider>>())
            {
                try
                {
                    if (obsolete.ValueRO.Blob.IsCreated)
                    {
                        obsolete.ValueRO.Blob.Dispose();
                    }
                }
                catch { }
            }

            // CRITICAL: Also dispose all ChunkPhysicsState blobs on shutdown
            // These are OUR colliders that we track separately from PhysicsCollider.
            // We dispose ChunkPhysicsState.ColliderBlob (which is the same reference as PhysicsCollider.Value)
            // BEFORE Unity Physics' ColliderBlobCleanupSystem runs to prevent leaks.
            foreach (var physicsState in SystemAPI.Query<RefRO<ChunkPhysicsState>>())
            {
                try
                {
                    if (physicsState.ValueRO.ColliderBlob.IsCreated)
                    {
                        physicsState.ValueRO.ColliderBlob.Dispose();
                    }
                }
                catch { }
            }
        }
    }
}
