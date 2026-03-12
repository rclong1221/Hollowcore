using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Replenishes tool durability when player is at a recharge station or inside the ship.
    /// Runs on server (authoritative).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ToolDurabilityReplenishSystem : ISystem
    {
        private ComponentLookup<ToolDurability> _durabilityLookup;
        private ComponentLookup<CurrentEnvironmentZone> _zoneLookup;
        private ComponentLookup<ToolOwner> _ownerLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _durabilityLookup = state.GetComponentLookup<ToolDurability>();
            _zoneLookup = state.GetComponentLookup<CurrentEnvironmentZone>(true);
            _ownerLookup = state.GetComponentLookup<ToolOwner>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _durabilityLookup.Update(ref state);
            _zoneLookup.Update(ref state);
            _ownerLookup.Update(ref state);

            var deltaTime = SystemAPI.Time.DeltaTime;

            // Check each tool for replenishment
            foreach (var (tool, usageState, entity) in
                     SystemAPI.Query<RefRO<Tool>, RefRO<ToolUsageState>>()
                     .WithAll<Simulate, ToolDurability>()
                     .WithEntityAccess())
            {
                // Skip if tool is in use
                if (usageState.ValueRO.IsInUse)
                    continue;

                // Get tool owner
                if (!_ownerLookup.HasComponent(entity))
                    continue;

                var ownerEntity = _ownerLookup[entity].OwnerEntity;

                // Check if owner is in pressurized zone (inside ship)
                if (!_zoneLookup.HasComponent(ownerEntity))
                    continue;

                var zone = _zoneLookup[ownerEntity];

                // Only replenish in pressurized areas (ship interior)
                if (zone.ZoneType != EnvironmentZoneType.Pressurized)
                    continue;

                // Get durability
                var durability = _durabilityLookup[entity];

                // Skip if already full
                if (durability.Current >= durability.Max)
                    continue;

                // Replenish at a fixed rate (could be made configurable)
                float replenishRate = 10f; // 10 units per second
                durability.Current += replenishRate * deltaTime;

                if (durability.Current >= durability.Max)
                {
                    durability.Current = durability.Max;
                    durability.IsDepleted = false;
                }
                else if (durability.Current > 0f)
                {
                    durability.IsDepleted = false;
                }

                _durabilityLookup[entity] = durability;
            }
        }
    }

    /// <summary>
    /// Tag component for entities that can recharge tools.
    /// Place on ship recharge stations.
    /// </summary>
    public struct ToolRechargeStation : IComponentData
    {
        /// <summary>
        /// Recharge rate per second.
        /// </summary>
        public float RechargeRate;

        /// <summary>
        /// Radius in which tools can be recharged.
        /// </summary>
        public float Radius;
    }
}
