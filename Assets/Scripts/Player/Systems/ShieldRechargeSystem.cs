using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;
using DIG.Combat.Systems;
using Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles shield regeneration.
    /// Shield recharges after not taking damage for RechargeDelay seconds.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))] // Ensure shield state is fresh before damage
    public partial struct ShieldRechargeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            foreach (var (shield, entity) in 
                SystemAPI.Query<RefRW<PlayerShield>>()
                    .WithEntityAccess())
            {
                // Count down recharge timer
                if (shield.ValueRO.RechargeTimer > 0)
                {
                    shield.ValueRW.RechargeTimer = math.max(0, shield.ValueRO.RechargeTimer - dt);
                }
                
                // Handle broken shield recovery
                if (shield.ValueRO.IsBroken)
                {
                    // Shield stays broken until timer expires
                    if (shield.ValueRO.RechargeTimer <= 0)
                    {
                        shield.ValueRW.IsBroken = false;
                    }
                    continue; // Don't recharge while broken
                }
                
                // Recharge shield if timer expired
                if (shield.ValueRO.RechargeTimer <= 0 && shield.ValueRO.Current < shield.ValueRO.Max)
                {
                    shield.ValueRW.Current = math.min(
                        shield.ValueRO.Max,
                        shield.ValueRO.Current + shield.ValueRO.RechargeRate * dt);
                }
            }
        }
    }
    
    /// <summary>
    /// Helper utilities for shield damage handling.
    /// Call these from your damage application system.
    /// </summary>
    public static class ShieldHelper
    {
        /// <summary>
        /// Applies damage to shield first, returns remaining damage to apply to health.
        /// </summary>
        public static float ApplyDamageToShield(ref PlayerShield shield, float damage)
        {
            if (shield.IsBroken || shield.Current <= 0)
            {
                return damage; // No shield protection
            }
            
            // Calculate how much shield absorbs
            float shieldDamage = damage * shield.AbsorptionRatio;
            float healthDamage = damage * (1f - shield.AbsorptionRatio);
            
            if (shieldDamage >= shield.Current)
            {
                // Shield is depleted
                float overflow = shieldDamage - shield.Current;
                shield.Current = 0;
                
                // Check if shield breaks
                if (shield.CanBreak)
                {
                    shield.IsBroken = true;
                    shield.RechargeTimer = shield.RechargeDelay + shield.BreakPenaltyDelay;
                }
                else
                {
                    shield.RechargeTimer = shield.RechargeDelay;
                }
                
                return healthDamage + overflow;
            }
            else
            {
                // Shield absorbs all damage
                shield.Current -= shieldDamage;
                shield.RechargeTimer = shield.RechargeDelay;
                return healthDamage;
            }
        }
        
        /// <summary>
        /// Instantly restores shield to full.
        /// </summary>
        public static void RestoreShield(ref PlayerShield shield)
        {
            shield.Current = shield.Max;
            shield.IsBroken = false;
            shield.RechargeTimer = 0;
        }
        
        /// <summary>
        /// Restores a portion of shield.
        /// </summary>
        public static void RestoreShield(ref PlayerShield shield, float amount)
        {
            shield.Current = math.min(shield.Max, shield.Current + amount);
            if (shield.IsBroken && shield.Current > 0)
            {
                shield.IsBroken = false;
            }
        }
    }
}
