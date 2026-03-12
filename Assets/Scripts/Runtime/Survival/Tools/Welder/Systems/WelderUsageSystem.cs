using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Handles welder tool usage - repairs ship hull and damages creatures.
    /// Runs on server only (authoritative).
    /// </summary>
    /// <remarks>
    /// Integration points:
    /// - Reads WeldRepairable from target entities
    /// - Reads CreatureHealth from target entities (when implemented)
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolRaycastSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct WelderUsageSystem : ISystem
    {
        private ComponentLookup<WeldRepairable> _weldRepairableLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _weldRepairableLookup = state.GetComponentLookup<WeldRepairable>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _weldRepairableLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Sequential due to writing to target entities
            foreach (var (welder, usageState, durability) in
                     SystemAPI.Query<RefRO<WelderTool>, RefRO<ToolUsageState>, RefRW<ToolDurability>>()
                     .WithAll<Simulate>())
            {
                // Skip if not in use or depleted
                if (!usageState.ValueRO.IsInUse || durability.ValueRO.IsDepleted)
                    continue;

                // Skip if no valid target
                if (!usageState.ValueRO.HasTarget)
                    continue;

                var targetEntity = usageState.ValueRO.TargetEntity;
                if (targetEntity == Entity.Null)
                    continue;

                // Degrade durability while welding
                ref var dur = ref durability.ValueRW;
                dur.Current -= dur.DegradeRatePerSecond * deltaTime;
                if (dur.Current <= 0f)
                {
                    dur.Current = 0f;
                    dur.IsDepleted = true;
                    continue; // Stop processing this frame
                }

                // Check if target is repairable
                if (_weldRepairableLookup.HasComponent(targetEntity))
                {
                    var repairable = _weldRepairableLookup[targetEntity];

                    // Heal the target
                    float healAmount = welder.ValueRO.HealPerSecond * deltaTime;
                    repairable.CurrentHealth = Unity.Mathematics.math.min(
                        repairable.CurrentHealth + healAmount,
                        repairable.MaxHealth
                    );

                    _weldRepairableLookup[targetEntity] = repairable;
                }

                // TODO: Check if target has CreatureHealth and deal damage
                // When creature system is implemented, this would:
                // - Apply welder.DamagePerSecond * DeltaTime damage
            }
        }
    }
}
