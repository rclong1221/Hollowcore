using System.Collections.Concurrent;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Telemetry entry for external logging/analytics.
    /// </summary>
    public struct ValidationTelemetryEntry
    {
        public int NetworkId;
        public float ViolationScore;
        public byte PenaltyLevel;
        public byte WarningCount;
        public byte ConsecutiveKicks;
        public uint ServerTick;
    }

    /// <summary>
    /// EPIC 17.11: Static queue for async telemetry export.
    /// Follows DamageVisualQueue / LevelUpVisualQueue pattern.
    /// </summary>
    public static class ValidationTelemetryQueue
    {
        public static readonly ConcurrentQueue<ValidationTelemetryEntry> Queue =
            new ConcurrentQueue<ValidationTelemetryEntry>();
    }

    /// <summary>
    /// EPIC 17.11: Bridges validation state changes to telemetry queue.
    /// Only enqueues when a violation just occurred (LastViolationTick == currentTick)
    /// or a penalty was escalated (PenaltyLevel != None). Avoids per-frame spam during decay.
    /// Budget: &lt;0.01ms.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ValidationTelemetryBridgeSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ValidationConfig>();
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.ServerTick.IsValid) return;
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;

            foreach (var (state, owner) in
                SystemAPI.Query<RefRO<PlayerValidationState>, RefRO<ValidationOwner>>()
                    .WithAll<ValidationChildTag>())
            {
                // Only enqueue on actual state changes:
                // - A violation happened THIS tick
                // - A penalty was escalated (will be consumed by PenaltyExecutionSystem)
                bool violationThisTick = state.ValueRO.LastViolationTick == currentTick;
                bool penaltyPending = state.ValueRO.PenaltyLevel != (byte)PenaltyLevel.None;

                if (!violationThisTick && !penaltyPending) continue;

                // Resolve network ID for telemetry
                int netId = 0;
                var playerEntity = owner.ValueRO.Owner;
                if (EntityManager.Exists(playerEntity) &&
                    EntityManager.HasComponent<GhostOwner>(playerEntity))
                {
                    netId = EntityManager.GetComponentData<GhostOwner>(playerEntity).NetworkId;
                }

                ValidationTelemetryQueue.Queue.Enqueue(new ValidationTelemetryEntry
                {
                    NetworkId = netId,
                    ViolationScore = state.ValueRO.ViolationScore,
                    PenaltyLevel = state.ValueRO.PenaltyLevel,
                    WarningCount = state.ValueRO.WarningCount,
                    ConsecutiveKicks = state.ValueRO.ConsecutiveKicks,
                    ServerTick = currentTick
                });
            }
        }
    }
}
