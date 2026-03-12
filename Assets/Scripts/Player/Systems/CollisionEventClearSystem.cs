using Unity.Burst;
using Unity.Entities;
using DIG.Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Clears collision event buffers each frame after consumers (audio/VFX systems) have processed them.
    /// Epic 7.3.7: Runs at the end of PresentationSystemGroup to ensure all consumers have read events.
    /// 
    /// Consumer systems should read CollisionEvent buffers earlier in PresentationSystemGroup.
    /// This system clears all buffers to prepare for the next frame's collision events.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct CollisionEventClearSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCollisionState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear all collision event buffers
            // This runs after audio/VFX systems have consumed the events
            new ClearCollisionEventBuffersJob().ScheduleParallel();
        }
    }
    
    /// <summary>
    /// Burst-compiled job to clear collision event buffers in parallel.
    /// Each entity's buffer is processed independently, making this safe for ScheduleParallel.
    /// </summary>
    [BurstCompile]
    partial struct ClearCollisionEventBuffersJob : IJobEntity
    {
        void Execute(DynamicBuffer<CollisionEvent> eventBuffer)
        {
            eventBuffer.Clear();
        }
    }
}
