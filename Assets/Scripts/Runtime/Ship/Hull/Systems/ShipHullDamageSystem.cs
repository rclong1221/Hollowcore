using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;
using DIG.Survival.Explosives;
using DIG.Survival.Tools;

namespace DIG.Ship.Hull.Systems
{
    /// <summary>
    /// Applies damage to ShipHullSection and manages breach state.
    /// Also syncs Hull state to WeldRepairable for tool compatibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))] // Run before Welder
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShipHullDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. Process Explosion Damage
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // We iterate over damage events targeting entities with ShipHullSection
            foreach (var (damageEvent, entity) in
                     SystemAPI.Query<RefRO<ExplosionDamageEvent>>()
                     .WithEntityAccess())
            {
                var target = damageEvent.ValueRO.TargetEntity;
                if (SystemAPI.HasComponent<ShipHullSection>(target))
                {
                    var hullRef = SystemAPI.GetComponentRW<ShipHullSection>(target);
                    float damage = damageEvent.ValueRO.Damage;

                    // Apply damage
                    hullRef.ValueRW.Current -= damage;
                    if (hullRef.ValueRW.Current < 0f) hullRef.ValueRW.Current = 0f;

                    // Check breach condition (e.g., < 20% health or just < max? Let's say < 50%)
                    // Implementation plan says "Current <= 0 or Current <= BreachThreshold"
                    // Let's use a dynamic threshold or a fixed fraction.
                    // For now: 0 HP = Full Breach (Severity 1). < 50% HP = Partial Breach.
                    
                    float fraction = hullRef.ValueRO.Current / hullRef.ValueRO.Max;
                    if (fraction <= 0.5f) // Threshold
                    {
                        hullRef.ValueRW.IsBreached = true;
                        // Severity 0 at 50%, 1 at 0%
                        hullRef.ValueRW.BreachSeverity = 1f - (fraction * 2f); 
                    }
                }
            }

            // 2. Sync ShipHullSection -> WeldRepairable 
            // This ensures the welder sees the latest health (including damage just applied)
            foreach (var (hull, repairable) in
                     SystemAPI.Query<RefRO<ShipHullSection>, RefRW<WeldRepairable>>())
            {
                repairable.ValueRW.CurrentHealth = hull.ValueRO.Current;
                repairable.ValueRW.MaxHealth = hull.ValueRO.Max;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
