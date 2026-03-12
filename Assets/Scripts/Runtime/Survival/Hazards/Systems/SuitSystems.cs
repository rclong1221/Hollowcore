using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Survival.Oxygen;
using DIG.Survival.Tools;

namespace DIG.Survival.Hazards
{
    /// <summary>
    /// Damage event for suit damage. Consumed by SuitDamageSystem.
    /// </summary>
    public struct SuitDamageEvent : IComponentData
    {
        /// <summary>
        /// Entity with suit to damage.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Amount of damage to suit.
        /// </summary>
        public float Damage;
    }

    /// <summary>
    /// Applies damage to suit integrity when entity takes damage.
    /// A fraction of incoming damage transfers to suit.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SuitDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            // Process suit damage events
            foreach (var (suitDamage, entity) in
                     SystemAPI.Query<RefRO<SuitDamageEvent>>()
                     .WithEntityAccess())
            {
                var evt = suitDamage.ValueRO;

                // Find target's suit integrity
                if (SystemAPI.HasComponent<SuitIntegrity>(evt.TargetEntity))
                {
                    var suit = SystemAPI.GetComponentRW<SuitIntegrity>(evt.TargetEntity);
                    ref var suitRef = ref suit.ValueRW;

                    // Apply damage (already scaled by DamageTransfer when event was created)
                    suitRef.Current = math.max(0f, suitRef.Current - evt.Damage);

                    // Update crack level based on integrity
                    float percent = suitRef.Percent;
                    if (percent < 0.25f)
                        suitRef.CrackLevel = 3; // Critical
                    else if (percent < 0.5f)
                        suitRef.CrackLevel = 2; // Major
                    else if (percent < 0.75f)
                        suitRef.CrackLevel = 1; // Minor
                    else
                        suitRef.CrackLevel = 0; // None
                }
            }
        }
    }

    /// <summary>
    /// Cleans up suit damage events after processing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SuitDamageEventCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<SuitDamageEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Applies oxygen leak based on suit integrity.
    /// Damaged suits consume oxygen faster.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(SuitDamageSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SuitLeakSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            // Update oxygen consumption modifier based on suit integrity
            foreach (var (suit, oxygenTank) in
                     SystemAPI.Query<RefRO<SuitIntegrity>, RefRW<OxygenTank>>()
                     .WithAll<Simulate, HasSuit>())
            {
                var suitData = suit.ValueRO;
                ref var oxygen = ref oxygenTank.ValueRW;

                // Leak multiplier increases consumption when suit is damaged
                // Full suit = 1.0x, Empty suit = 3.0x
                oxygen.LeakMultiplier = suitData.LeakMultiplier;
            }
        }
    }

    /// <summary>
    /// Handles suit repair using welder tool.
    /// Must be stationary and holding welder.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SuitRepairSystem : ISystem
    {
        private ComponentLookup<Tool> _toolLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _toolLookup = state.GetComponentLookup<Tool>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _toolLookup.Update(ref state);

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (suit, repairRequest, activeTool) in
                     SystemAPI.Query<RefRW<SuitIntegrity>, RefRW<SuitRepairRequest>, RefRO<ActiveTool>>()
                     .WithAll<Simulate, HasSuit>())
            {
                ref var suitRef = ref suit.ValueRW;
                ref var repair = ref repairRequest.ValueRW;

                // Check if holding welder
                bool hasWelder = false;
                if (activeTool.ValueRO.ToolEntity != Entity.Null &&
                    _toolLookup.HasComponent(activeTool.ValueRO.ToolEntity))
                {
                    var tool = _toolLookup[activeTool.ValueRO.ToolEntity];
                    hasWelder = tool.ToolType == ToolType.Welder;
                }

                if (!hasWelder || !repair.IsRepairing)
                {
                    repair.IsRepairing = false;
                    continue;
                }

                // Repair suit over time
                if (suitRef.Current < suitRef.Max)
                {
                    float repairAmount = repair.RepairRate * deltaTime;
                    suitRef.Current = math.min(suitRef.Max, suitRef.Current + repairAmount);

                    // Update crack level based on new integrity
                    float percent = suitRef.Percent;
                    if (percent < 0.25f)
                        suitRef.CrackLevel = 3;
                    else if (percent < 0.5f)
                        suitRef.CrackLevel = 2;
                    else if (percent < 0.75f)
                        suitRef.CrackLevel = 1;
                    else
                        suitRef.CrackLevel = 0;
                }
            }
        }
    }
}
