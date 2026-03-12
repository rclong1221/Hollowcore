using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Player.Components;

namespace Player.Systems
{
    // T7: God Mode Logic
    // Listens for WillDieEvent and cancels it if GodMode is enabled
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DeathTransitionSystem))] // Run AFTER transition system starts the event? 
    // NO! DeathTransitionSystem starts it, then waits for next frame.
    // So GodModeSystem can run anywhere in between.
    // Ideally it runs before DeathTransitionSystem on the NEXT frame.
    [UpdateBefore(typeof(DeathTransitionSystem))]
    public partial struct GodModeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (willDie, godMode, entity) in 
                     SystemAPI.Query<RefRW<WillDieEvent>, RefRO<GodMode>>()
                     .WithAll<WillDieEvent>() // Only iterate if enabled? Query includes enabled by default
                     .WithEntityAccess())
            {
                if (godMode.ValueRO.Enabled && !willDie.ValueRO.Cancelled)
                {
                    willDie.ValueRW.Cancelled = true;
                    // UnityEngine.Debug.Log($"GodMode saved {entity.Index}");
                }
            }
        }
    }
}
