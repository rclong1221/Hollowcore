using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;
using Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles oxygen depletion in hazard zones (gas, space, smoke).
    /// Separate from BreathState which handles underwater breath.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct OxygenSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (oxygen, health, entity) in 
                SystemAPI.Query<RefRW<PlayerOxygen>, RefRW<Health>>()
                    .WithEntityAccess())
            {
                // Check if in hazard zone (has tag)
                bool inHazard = SystemAPI.HasComponent<InOxygenHazard>(entity);
                
                if (inHazard)
                {
                    // Drain oxygen
                    oxygen.ValueRW.Current = math.max(0, oxygen.ValueRO.Current - oxygen.ValueRO.DrainRate * dt);
                    oxygen.ValueRW.LastDrainTime = time;
                    
                    // Apply suffocation damage when out of oxygen
                    if (oxygen.ValueRO.Current <= 0)
                    {
                        float damage = oxygen.ValueRO.SuffocationDamage * dt;
                        health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
                    }
                }
                else
                {
                    // Recover oxygen when safe (after delay)
                    float timeSinceDrain = time - oxygen.ValueRO.LastDrainTime;
                    if (timeSinceDrain >= oxygen.ValueRO.RecoveryDelay && oxygen.ValueRO.Current < oxygen.ValueRO.Max)
                    {
                        oxygen.ValueRW.Current = math.min(
                            oxygen.ValueRO.Max,
                            oxygen.ValueRO.Current + oxygen.ValueRO.RecoveryRate * dt);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Tag component added when player enters oxygen-depleting hazard zone.
    /// </summary>
    public struct InOxygenHazard : IComponentData
    {
        /// <summary>Multiplier for drain rate in this zone.</summary>
        public float DrainMultiplier;
    }
}
