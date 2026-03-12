using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Epic 7.6.4: Ticks down CollisionGracePeriod timers and removes expired components.
    /// Runs in PredictedFixedStepSimulationSystemGroup before collision systems.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerProximityCollisionSystem))]
    [BurstCompile]
    public partial struct CollisionGracePeriodSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.1: Profile grace period timer updates
            using (CollisionProfilerMarkers.GracePeriod.Auto())
            {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (gracePeriod, entity) in SystemAPI.Query<RefRW<CollisionGracePeriod>>()
                .WithAll<Simulate>()
                .WithEntityAccess())
            {
                gracePeriod.ValueRW.RemainingTime -= deltaTime;
                
                if (gracePeriod.ValueRO.RemainingTime <= 0f)
                {
                    // Grace period expired - remove component
                    ecb.RemoveComponent<CollisionGracePeriod>(entity);
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            } // End GracePeriod profiler marker
        }
    }
}
