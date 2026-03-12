using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;
using Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles hunger and thirst depletion over time.
    /// Players take damage when fully starving/dehydrated.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SurvivalNeedsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            // Process hunger
            foreach (var (hunger, health, entity) in 
                SystemAPI.Query<RefRW<PlayerHunger>, RefRW<Health>>()
                    .WithEntityAccess())
            {
                // Hunger increases over time (0 = full, max = starving)
                if (hunger.ValueRO.Current < hunger.ValueRO.Max)
                {
                    hunger.ValueRW.Current = math.min(
                        hunger.ValueRO.Max,
                        hunger.ValueRO.Current + hunger.ValueRO.IncreaseRate * dt);
                }
                
                // Apply starvation damage when above threshold
                if (hunger.ValueRO.IsStarving)
                {
                    float damage = hunger.ValueRO.StarvationDamage * dt;
                    health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
                }
            }
            
            // Process thirst
            foreach (var (thirst, health, entity) in 
                SystemAPI.Query<RefRW<PlayerThirst>, RefRW<Health>>()
                    .WithEntityAccess())
            {
                // Thirst increases over time (0 = hydrated, max = dehydrated)
                if (thirst.ValueRO.Current < thirst.ValueRO.Max)
                {
                    thirst.ValueRW.Current = math.min(
                        thirst.ValueRO.Max,
                        thirst.ValueRO.Current + thirst.ValueRO.IncreaseRate * dt);
                }
                
                // Apply dehydration damage when above threshold
                if (thirst.ValueRO.IsDehydrated)
                {
                    float damage = thirst.ValueRO.DehydrationDamage * dt;
                    health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
                }
            }
        }
    }
    
    /// <summary>
    /// Helper utilities for modifying hunger/thirst from item usage.
    /// </summary>
    public static class SurvivalNeedsHelper
    {
        /// <summary>
        /// Reduces hunger when player eats food.
        /// </summary>
        public static void Eat(ref PlayerHunger hunger, float amount)
        {
            hunger.Current = math.max(0, hunger.Current - amount);
        }
        
        /// <summary>
        /// Reduces thirst when player drinks.
        /// </summary>
        public static void Drink(ref PlayerThirst thirst, float amount)
        {
            thirst.Current = math.max(0, thirst.Current - amount);
        }
    }
}
