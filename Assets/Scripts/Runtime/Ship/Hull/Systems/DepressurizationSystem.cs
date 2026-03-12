using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Ship.Power;
using DIG.Survival.Environment;

namespace DIG.Ship.Hull.Systems
{
    /// <summary>
    /// Updates interior environment zones based on hull breach status.
    /// Overrides LifeSupport safety if hull is breached.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Ship.Power.LifeSupportSystem))] // Run after LifeSupport to override it
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DepressurizationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 1. Identify all ships with active breaches
            var breachedShips = new NativeHashSet<Entity>(16, Allocator.Temp);
            
            foreach (var hull in SystemAPI.Query<RefRO<ShipHullSection>>())
            {
                if (hull.ValueRO.IsBreached)
                {
                    breachedShips.Add(hull.ValueRO.ShipEntity);
                }
            }

            // 2. Override LifeSupport zones for breached ships
            if (!breachedShips.IsEmpty)
            {
                foreach (var lifeSupport in SystemAPI.Query<RefRO<LifeSupport>>())
                {
                    if (breachedShips.Contains(lifeSupport.ValueRO.ShipEntity))
                    {
                        var zoneEntity = lifeSupport.ValueRO.InteriorZoneEntity;
                        if (SystemAPI.Exists(zoneEntity) && SystemAPI.HasComponent<EnvironmentZone>(zoneEntity))
                        {
                            var zone = SystemAPI.GetComponentRW<EnvironmentZone>(zoneEntity);
                            
                            // Force Vacuum condition regardless of power
                            zone.ValueRW.ZoneType = EnvironmentZoneType.Vacuum;
                            zone.ValueRW.OxygenRequired = true;
                            
                            // Optional: Could adjust temperature or radiation too
                        }
                    }
                }
            }

            breachedShips.Dispose();
        }
    }
}
