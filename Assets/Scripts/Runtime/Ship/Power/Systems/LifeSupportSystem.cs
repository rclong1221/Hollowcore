using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Ship.Power
{
    /// <summary>
    /// System that manages life support and updates interior environment zones.
    /// When life support is offline, the interior becomes a vacuum (oxygen required).
    /// When online, the interior is pressurized (no oxygen drain).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ShipPowerAllocationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LifeSupportSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Process each life support system
            foreach (var (lifeSupport, consumer, entity) in
                     SystemAPI.Query<RefRW<LifeSupport>, RefRO<ShipPowerConsumer>>()
                     .WithEntityAccess())
            {
                // Life support is online if:
                // 1. Receiving sufficient power (at least 50% of required)
                // 2. Not critically damaged
                bool hasPower = consumer.ValueRO.CurrentPower >= consumer.ValueRO.RequiredPower * 0.5f;
                bool wasOnline = lifeSupport.ValueRO.IsOnline;
                
                lifeSupport.ValueRW.IsOnline = hasPower && !lifeSupport.ValueRO.IsDamaged;

                // Update interior zone if life support status changed
                if (wasOnline != lifeSupport.ValueRO.IsOnline)
                {
                    Entity zoneEntity = lifeSupport.ValueRO.InteriorZoneEntity;
                    if (zoneEntity != Entity.Null && SystemAPI.HasComponent<EnvironmentZone>(zoneEntity))
                    {
                        var zone = SystemAPI.GetComponentRW<EnvironmentZone>(zoneEntity);
                        
                        if (lifeSupport.ValueRO.IsOnline)
                        {
                            // Life support online: interior is pressurized and safe
                            zone.ValueRW.ZoneType = EnvironmentZoneType.Pressurized;
                            zone.ValueRW.OxygenRequired = false;
                            zone.ValueRW.OxygenDepletionMultiplier = 0f;
                            zone.ValueRW.Temperature = 20f; // Comfortable room temp
                        }
                        else
                        {
                            // Life support offline: interior becomes vacuum-like
                            // Oxygen is required, will drain player tanks
                            zone.ValueRW.ZoneType = EnvironmentZoneType.Vacuum;
                            zone.ValueRW.OxygenRequired = true;
                            zone.ValueRW.OxygenDepletionMultiplier = 0.5f; // Slower than actual vacuum
                            zone.ValueRW.Temperature = -10f; // Gets cold without life support
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that ensures life support systems have power consumer components.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ShipPowerAllocationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LifeSupportInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Ensure LifeSupport entities have ShipPowerConsumer
            foreach (var (lifeSupport, entity) in
                     SystemAPI.Query<RefRO<LifeSupport>>()
                     .WithNone<ShipPowerConsumer>()
                     .WithEntityAccess())
            {
                ecb.AddComponent(entity, new ShipPowerConsumer
                {
                    RequiredPower = lifeSupport.ValueRO.PowerRequired,
                    Priority = PowerPriority.LifeSupport,
                    CurrentPower = 0f,
                    ShipEntity = lifeSupport.ValueRO.ShipEntity
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
