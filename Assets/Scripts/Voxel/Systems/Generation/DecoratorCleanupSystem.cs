using Unity.Entities;
using DIG.Voxel.Components;
using DIG.Voxel.Systems; // For ChunkNeedsCleanup

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// System that returns pooled decorators when chunks are unloaded.
    /// Runs after ChunkMemoryCleanupSystem adds ChunkNeedsCleanup tags.
    /// 
    /// OPTIMIZATION 10.5.9: Part of the object pooling system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class DecoratorCleanupSystem : SystemBase
    {
        private EntityQuery _cleanupQuery;
        
        protected override void OnCreate()
        {
            _cleanupQuery = GetEntityQuery(
                ComponentType.ReadOnly<ChunkPosition>(),
                ComponentType.ReadOnly<ChunkDecorated>(),
                ComponentType.ReadOnly<ChunkNeedsCleanup>()
            );
            
            RequireForUpdate(_cleanupQuery);
        }
        
        protected override void OnUpdate()
        {
            // Get all chunks being cleaned up that have decorators
            var entities = _cleanupQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in entities)
            {
                var chunkPos = EntityManager.GetComponentData<ChunkPosition>(entity).Value;
                
                // Return all decorators for this chunk to the pool
                DecoratorPool.Instance.ReturnChunk(chunkPos);
            }
            
            entities.Dispose();
        }
    }
}
