using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles ability charge regeneration.
    /// Charges recharge over time when not at maximum.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AbilityChargeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            foreach (var (charges, entity) in 
                SystemAPI.Query<RefRW<AbilityCharges>>()
                    .WithEntityAccess())
            {
                // Skip if at max charges
                if (charges.ValueRO.CurrentCharges >= charges.ValueRO.MaxCharges)
                {
                    charges.ValueRW.RechargeProgress = 0f;
                    continue;
                }
                
                // Calculate recharge speed
                float rechargeRate = 1f / charges.ValueRO.RechargeTime;
                
                if (charges.ValueRO.ParallelRecharge)
                {
                    // All missing charges recharge at once
                    int missingCharges = charges.ValueRO.MaxCharges - charges.ValueRO.CurrentCharges;
                    charges.ValueRW.RechargeProgress += rechargeRate * missingCharges * dt;
                    
                    // Grant charges
                    while (charges.ValueRO.RechargeProgress >= 1f && 
                           charges.ValueRO.CurrentCharges < charges.ValueRO.MaxCharges)
                    {
                        charges.ValueRW.CurrentCharges++;
                        charges.ValueRW.RechargeProgress -= 1f;
                    }
                }
                else
                {
                    // Sequential recharge (one at a time)
                    charges.ValueRW.RechargeProgress += rechargeRate * dt;
                    
                    if (charges.ValueRO.RechargeProgress >= 1f)
                    {
                        charges.ValueRW.CurrentCharges++;
                        charges.ValueRW.RechargeProgress = 0f;
                    }
                }
                
                // Clamp to valid range
                charges.ValueRW.CurrentCharges = math.min(
                    charges.ValueRO.CurrentCharges, 
                    charges.ValueRO.MaxCharges);
                charges.ValueRW.RechargeProgress = math.clamp(charges.ValueRO.RechargeProgress, 0f, 1f);
            }
        }
    }
    
    /// <summary>
    /// Helper utilities for consuming and granting ability charges.
    /// </summary>
    public static class AbilityChargeHelper
    {
        /// <summary>
        /// Attempts to consume a charge. Returns true if successful.
        /// </summary>
        public static bool TryConsumeCharge(ref AbilityCharges charges)
        {
            if (charges.CurrentCharges > 0)
            {
                charges.CurrentCharges--;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Adds charges (e.g., from pickup or buff).
        /// </summary>
        public static void AddCharges(ref AbilityCharges charges, int amount)
        {
            int max = charges.AllowOvercharge ? charges.EffectiveMax : charges.MaxCharges;
            charges.CurrentCharges = math.min(max, charges.CurrentCharges + amount);
        }
        
        /// <summary>
        /// Instantly refills all charges.
        /// </summary>
        public static void Refill(ref AbilityCharges charges)
        {
            charges.CurrentCharges = charges.MaxCharges;
            charges.RechargeProgress = 0f;
        }
    }
}
