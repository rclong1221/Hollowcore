using Unity.Collections;
using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Aggregates ViolationEvent transient entities into per-player scores.
    /// Applies decay, checks thresholds, escalates penalty levels.
    /// Budget: &lt;0.02ms (violation events are sparse).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementValidationSystem))]
    [UpdateAfter(typeof(EconomyAuditSystem))]
    [UpdateAfter(typeof(CooldownValidationSystem))]
    public partial class ViolationAccumulatorSystem : SystemBase
    {
        private EntityQuery _violationQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<ValidationConfig>();
            RequireForUpdate<Unity.NetCode.NetworkTime>();
            _violationQuery = GetEntityQuery(ComponentType.ReadOnly<ViolationEvent>());
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<ValidationConfig>();
            var networkTime = SystemAPI.GetSingleton<Unity.NetCode.NetworkTime>();
            if (!networkTime.ServerTick.IsValid) return;
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;
            float dt = SystemAPI.Time.DeltaTime;

            // Process violation events
            if (_violationQuery.CalculateEntityCount() > 0)
            {
                var events = _violationQuery.ToComponentDataArray<ViolationEvent>(Allocator.Temp);
                var eventEntities = _violationQuery.ToEntityArray(Allocator.Temp);

                var linkLookup = GetComponentLookup<ValidationLink>(true);
                var stateLookup = GetComponentLookup<PlayerValidationState>(false);

                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    var player = evt.PlayerEntity;

                    if (!EntityManager.Exists(player)) continue;
                    if (!linkLookup.HasComponent(player)) continue;

                    var child = linkLookup[player].ValidationChild;
                    if (child == Entity.Null) continue;
                    if (!stateLookup.HasComponent(child)) continue;

                    var state = stateLookup[child];

                    // Apply weighted severity
                    float weight = GetWeight(config, (ViolationType)evt.ViolationType);
                    state.ViolationScore += evt.Severity * weight;
                    state.LastViolationTick = currentTick;

                    stateLookup[child] = state;
                }

                // Batch destroy all violation event entities in one structural change
                EntityManager.DestroyEntity(eventEntities);

                events.Dispose();
                eventEntities.Dispose();
            }

            // Decay and threshold checks for all validation children
            foreach (var (stateRef, owner) in
                SystemAPI.Query<RefRW<PlayerValidationState>, RefRO<ValidationOwner>>()
                    .WithAll<ValidationChildTag>())
            {
                ref var state = ref stateRef.ValueRW;

                // Decay violation score
                if (state.ViolationScore > 0f)
                {
                    state.ViolationScore -= config.ViolationDecayRate * dt;
                    if (state.ViolationScore < 0f)
                        state.ViolationScore = 0f;
                }

                // Skip if no pending penalty needed
                if (state.PenaltyLevel != (byte)PenaltyLevel.None) continue;

                // Threshold checks (escalating)
                if (state.ViolationScore >= config.KickThreshold)
                {
                    state.PenaltyLevel = (byte)PenaltyLevel.Kick;
                }
                else if (state.ViolationScore >= config.WarnThreshold)
                {
                    state.PenaltyLevel = (byte)PenaltyLevel.Warn;
                }

                // Ban escalation from consecutive kicks
                if (state.ConsecutiveKicks >= config.ConsecutiveKicksForBan)
                {
                    state.PenaltyLevel = (byte)PenaltyLevel.TempBan;
                }
            }
        }

        private static float GetWeight(in ValidationConfig config, ViolationType type)
        {
            switch (type)
            {
                case ViolationType.RateLimit: return config.RateLimitWeight;
                case ViolationType.Movement: return config.MovementWeight;
                case ViolationType.Economy: return config.EconomyWeight;
                case ViolationType.Cooldown: return config.CooldownWeight;
                default: return 1f;
            }
        }
    }
}
