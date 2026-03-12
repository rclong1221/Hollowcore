using Unity.Entities;
using Unity.Burst;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// Links Player entities to their TargetingModule child entities at runtime.
    /// 
    /// This system runs once after baking to establish the bidirectional relationship
    /// between Player entities and their targeting module child entities.
    /// 
    /// Architecture:
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │  Player Entity                    TargetingModule Entity                │
    /// │  ┌────────────────────────┐       ┌─────────────────────────────────┐  │
    /// │  │ TargetingModuleLink    │──────▶│ TargetingModuleTag              │  │
    /// │  │   .TargetingModule     │       │ TargetingModuleOwner            │  │
    /// │  │                        │◀──────│   .Owner                        │  │
    /// │  │ PlayerTag              │       │                                 │  │
    /// │  │ TargetingState         │       │ AimAssistState                  │  │
    /// │  │ CameraTargetLockState  │       │ PartTargetingState              │  │
    /// │  │ ... (other player data)│       │ PredictiveAimState              │  │
    /// │  │                        │       │ MultiLockState                  │  │
    /// │  │                        │       │ LockedTargetElement buffer      │  │
    /// │  │                        │       │ OverTheShoulderState            │  │
    /// │  └────────────────────────┘       └─────────────────────────────────┘  │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Unity.Entities.BeginInitializationEntityCommandBufferSystem))]
    public partial struct TargetingModuleLinkSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetingModuleTag>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            // Find unlinked targeting modules (Owner == Entity.Null)
            foreach (var (owner, moduleTag, moduleEntity) in 
                SystemAPI.Query<RefRW<TargetingModuleOwner>, RefRO<TargetingModuleTag>>()
                    .WithEntityAccess())
            {
                if (owner.ValueRO.Owner != Entity.Null)
                    continue; // Already linked
                
                // Find the parent player via LinkedEntityGroup
                // The module is a child of the player prefab, so it's in the player's LinkedEntityGroup
                Entity playerEntity = FindParentPlayer(ref state, moduleEntity);
                
                if (playerEntity == Entity.Null)
                    continue;
                
                // Link module -> player
                owner.ValueRW.Owner = playerEntity;
                
                // Link player -> module
                if (SystemAPI.HasComponent<TargetingModuleLink>(playerEntity))
                {
                    var link = SystemAPI.GetComponentRW<TargetingModuleLink>(playerEntity);
                    link.ValueRW.TargetingModule = moduleEntity;
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        private Entity FindParentPlayer(ref SystemState state, Entity moduleEntity)
        {
            // Search through all players with LinkedEntityGroup to find one containing this module
            foreach (var (playerTag, link, linkedGroup, playerEntity) in 
                SystemAPI.Query<RefRO<PlayerTag>, RefRO<TargetingModuleLink>, DynamicBuffer<LinkedEntityGroup>>()
                    .WithEntityAccess())
            {
                // Already linked - skip
                if (link.ValueRO.TargetingModule != Entity.Null)
                    continue;
                
                // Check if this player's LinkedEntityGroup contains the module
                for (int i = 0; i < linkedGroup.Length; i++)
                {
                    if (linkedGroup[i].Value == moduleEntity)
                    {
                        return playerEntity;
                    }
                }
            }
            
            return Entity.Null;
        }
    }
}
