using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using DIG.Player.Components;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Epic 7.6.5: Manages temporary GroupIndex overrides for projectile owner filtering.
    /// Ticks down GroupIndexOverride timers and resets CollisionFilter.GroupIndex when expired.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct GroupIndexOverrideSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.1: Profile GroupIndex override management
            using (CollisionProfilerMarkers.GroupIndexOverride.Auto())
            {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (overrideComp, physicsCollider, entity) 
                in SystemAPI.Query<RefRW<GroupIndexOverride>, RefRW<PhysicsCollider>>()
                    .WithEntityAccess())
            {
                overrideComp.ValueRW.RemainingTime -= deltaTime;
                
                if (overrideComp.ValueRO.RemainingTime <= 0f)
                {
                    // Reset GroupIndex to original value
                    var filter = physicsCollider.ValueRO.Value.Value.GetCollisionFilter();
                    filter.GroupIndex = overrideComp.ValueRO.OriginalGroupIndex;
                    physicsCollider.ValueRW.Value.Value.SetCollisionFilter(filter);
                    
                    // Remove override component
                    ecb.RemoveComponent<GroupIndexOverride>(entity);
                }
                else
                {
                    // Ensure GroupIndex is set to temporary value (in case it was changed externally)
                    var filter = physicsCollider.ValueRO.Value.Value.GetCollisionFilter();
                    if (filter.GroupIndex != overrideComp.ValueRO.TemporaryGroupIndex)
                    {
                        filter.GroupIndex = overrideComp.ValueRO.TemporaryGroupIndex;
                        physicsCollider.ValueRW.Value.Value.SetCollisionFilter(filter);
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            } // End GroupIndexOverride profiler marker
        }
    }
}
