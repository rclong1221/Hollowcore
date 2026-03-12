using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;
using Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles infection spread and damage.
    /// Infection spreads over time once contracted and deals damage above threshold.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InfectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            foreach (var (infection, health, entity) in 
                SystemAPI.Query<RefRW<PlayerInfection>, RefRW<Health>>()
                    .WithEntityAccess())
            {
                // Only process if infected
                if (infection.ValueRO.Current <= 0) continue;
                
                // Spread infection over time
                if (infection.ValueRO.Current < infection.ValueRO.Max && infection.ValueRO.SpreadRate > 0)
                {
                    infection.ValueRW.Current = math.min(
                        infection.ValueRO.Max,
                        infection.ValueRO.Current + infection.ValueRO.SpreadRate * dt);
                }
                
                // Apply damage when above threshold
                if (infection.ValueRO.IsTakingDamage)
                {
                    // Damage scales with infection level
                    float infectionPercent = infection.ValueRO.Percent;
                    float damageMultiplier = (infectionPercent - infection.ValueRO.DamageThreshold) / (1f - infection.ValueRO.DamageThreshold);
                    float damage = infection.ValueRO.DamageRate * damageMultiplier * dt;
                    
                    health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
                }
                
                // Check for critical infection (near death)
                if (infection.ValueRO.IsCritical)
                {
                    // Could trigger special effects, slowed movement, etc.
                }
            }
        }
    }
    
    /// <summary>
    /// Helper utilities for applying/curing infection.
    /// </summary>
    public static class InfectionHelper
    {
        /// <summary>
        /// Infects the player with a given amount.
        /// </summary>
        public static void Infect(ref PlayerInfection infection, float amount)
        {
            infection.Current = math.min(infection.Max, infection.Current + amount);
        }
        
        /// <summary>
        /// Applies antidote/cure to reduce infection.
        /// </summary>
        public static void Cure(ref PlayerInfection infection, float amount)
        {
            infection.Current = math.max(0, infection.Current - amount);
        }
        
        /// <summary>
        /// Fully cures infection.
        /// </summary>
        public static void FullCure(ref PlayerInfection infection)
        {
            infection.Current = 0;
        }
    }
}
