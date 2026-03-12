using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles weapon durability degradation.
    /// Weapons degrade with use and can break.
    /// Note: This doesn't auto-run - call WeaponDurabilityHelper from your attack systems.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponDurabilitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // This system just handles the "broken" state transition
            // Actual degradation happens via WeaponDurabilityHelper calls from attack systems
            
            foreach (var (durability, entity) in 
                SystemAPI.Query<RefRW<WeaponDurability>>()
                    .WithEntityAccess())
            {
                // Check if weapon should break
                if (durability.ValueRO.Current <= 0 && !durability.ValueRO.IsBroken)
                {
                    durability.ValueRW.IsBroken = true;
                    
                    // Destroy weapon if configured
                    if (durability.ValueRO.DestroyOnBreak)
                    {
                        // Mark for destruction (ECB pattern)
                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Helper utilities for degrading and repairing weapons.
    /// Call these from your weapon attack/block systems.
    /// </summary>
    public static class WeaponDurabilityHelper
    {
        /// <summary>
        /// Degrades weapon durability from an attack/use.
        /// Returns true if weapon is still usable.
        /// </summary>
        public static bool DegradeOnUse(ref WeaponDurability durability)
        {
            if (durability.IsBroken && durability.DisableOnBreak)
            {
                return false; // Can't use broken weapon
            }
            
            durability.Current = math.max(0, durability.Current - durability.DegradePerUse);
            
            if (durability.Current <= 0)
            {
                durability.IsBroken = true;
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Degrades weapon durability from blocking an attack.
        /// Returns true if weapon is still usable.
        /// </summary>
        public static bool DegradeOnBlock(ref WeaponDurability durability)
        {
            if (durability.IsBroken && durability.DisableOnBreak)
            {
                return false;
            }
            
            durability.Current = math.max(0, durability.Current - durability.DegradePerBlock);
            
            if (durability.Current <= 0)
            {
                durability.IsBroken = true;
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Repairs weapon by a given amount. Returns true if repair succeeded.
        /// </summary>
        public static bool Repair(ref WeaponDurability durability, float amount)
        {
            if (!durability.CanRepair) return false;
            
            float maxRepair = durability.MaxRepairDurability;
            durability.Current = math.min(maxRepair, durability.Current + amount);
            
            if (durability.IsBroken && durability.Current > 0)
            {
                durability.IsBroken = false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Fully repairs weapon to its maximum repairable durability.
        /// </summary>
        public static bool FullRepair(ref WeaponDurability durability)
        {
            if (!durability.CanRepair) return false;
            
            durability.Current = durability.MaxRepairDurability;
            durability.IsBroken = false;
            return true;
        }
        
        /// <summary>
        /// Checks if weapon can be used (not broken or breaking doesn't disable).
        /// </summary>
        public static bool CanUse(in WeaponDurability durability)
        {
            return !durability.IsBroken || !durability.DisableOnBreak;
        }
    }
}
