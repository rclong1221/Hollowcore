using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;
using DIG.Ship.LocalSpace;
using DIG.Ship.Power;

namespace Player.Systems
{
    /// <summary>
    /// Fallback system that sets CurrentEnvironmentZone based on InShipLocalSpace attachment.
    /// This is a workaround when physics-based zone detection isn't set up.
    /// </summary>
    /// <remarks>
    /// When a player is attached to a ship (via airlock), they should automatically
    /// be in the ship's interior zone. This system handles that case without requiring
    /// physics trigger volumes.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifeSupportSystem))]
    [UpdateBefore(typeof(DIG.Survival.Oxygen.OxygenDepletionSystem))]
    // run on both client and server for prediction
    public partial struct ShipLocalSpaceZoneSystem : ISystem
    {
        private ComponentLookup<EnvironmentZone> _zoneLookup;
        private ComponentLookup<LifeSupport> _lifeSupportLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _zoneLookup = state.GetComponentLookup<EnvironmentZone>(true);
            _lifeSupportLookup = state.GetComponentLookup<LifeSupport>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _zoneLookup.Update(ref state);
            _lifeSupportLookup.Update(ref state);

            // Find the ship's interior zone entity (from LifeSupport)
            Entity lifeSupportZoneEntity = Entity.Null;
            EnvironmentZone zoneData = default;
            bool foundZone = false;

            foreach (var lifeSupport in SystemAPI.Query<RefRO<LifeSupport>>())
            {
                var zoneEntity = lifeSupport.ValueRO.InteriorZoneEntity;
                if (zoneEntity != Entity.Null && _zoneLookup.HasComponent(zoneEntity))
                {
                    lifeSupportZoneEntity = zoneEntity;
                    zoneData = _zoneLookup[zoneEntity];
                    foundZone = true;
                    break;
                }
            }

            if (!foundZone)
                return;

            // Update CurrentEnvironmentZone for all entities attached to a ship
            foreach (var (localSpace, currentZone) in
                     SystemAPI.Query<RefRO<InShipLocalSpace>, RefRW<CurrentEnvironmentZone>>())
            {
                // Only update if attached to ship
                if (!localSpace.ValueRO.IsAttached)
                    continue;

                // Set zone to ship's interior zone
                currentZone.ValueRW = new CurrentEnvironmentZone
                {
                    ZoneEntity = lifeSupportZoneEntity,
                    ZoneType = zoneData.ZoneType,
                    OxygenRequired = zoneData.OxygenRequired,
                    OxygenDepletionMultiplier = zoneData.OxygenDepletionMultiplier,
                    RadiationRate = zoneData.RadiationRate
                };
            }
        }
    }
}
