using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Executes penalties (warn/kick/ban) on players whose
    /// PenaltyLevel has been escalated by ViolationAccumulatorSystem.
    /// Budget: &lt;0.01ms (penalties are very rare).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class PenaltyExecutionSystem : SystemBase
    {
        private EntityQuery _connectionQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<ValidationConfig>();
            RequireForUpdate<NetworkTime>();
            _connectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<NetworkStreamInGame>());
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<ValidationConfig>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.ServerTick.IsValid) return;
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;

            // Build NetworkId → connection entity map
            var connEntities = _connectionQuery.ToEntityArray(Allocator.Temp);
            var connIds = _connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            var connMap = new NativeParallelHashMap<int, Entity>(connEntities.Length, Allocator.Temp);
            for (int i = 0; i < connEntities.Length; i++)
                connMap.TryAdd(connIds[i].Value, connEntities[i]);
            connEntities.Dispose();
            connIds.Dispose();

            // Check simulation tick rate for cooldown conversion
            uint tickRate = 30;
            if (SystemAPI.HasSingleton<ClientServerTickRate>())
                tickRate = (uint)SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;
            if (tickRate == 0) tickRate = 30;
            uint warnCooldownTicks = (uint)(config.WarnCooldownSeconds * tickRate);

            foreach (var (stateRef, owner) in
                SystemAPI.Query<RefRW<PlayerValidationState>, RefRO<ValidationOwner>>()
                    .WithAll<ValidationChildTag>())
            {
                ref var state = ref stateRef.ValueRW;
                var penalty = (PenaltyLevel)state.PenaltyLevel;
                if (penalty == PenaltyLevel.None) continue;

                var playerEntity = owner.ValueRO.Owner;
                if (!EntityManager.Exists(playerEntity)) continue;

                // Resolve connection entity and network ID from GhostOwner
                Entity connectionEntity = Entity.Null;
                int netId = -1;
                if (EntityManager.HasComponent<GhostOwner>(playerEntity))
                {
                    netId = EntityManager.GetComponentData<GhostOwner>(playerEntity).NetworkId;
                    connMap.TryGetValue(netId, out connectionEntity);
                }

                switch (penalty)
                {
                    case PenaltyLevel.Warn:
                        // Throttle warnings
                        if (currentTick - state.LastWarningTick < warnCooldownTicks)
                        {
                            state.PenaltyLevel = (byte)PenaltyLevel.None;
                            break;
                        }
                        state.WarningCount++;
                        state.LastWarningTick = currentTick;
                        state.PenaltyLevel = (byte)PenaltyLevel.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[Validation] WARNING issued to player entity {playerEntity.Index} (score: {state.ViolationScore:F1}, warnings: {state.WarningCount})");
#endif
                        break;

                    case PenaltyLevel.Kick:
                        state.ConsecutiveKicks++;
                        state.ViolationScore = 0f;
                        state.PenaltyLevel = (byte)PenaltyLevel.None;

                        if (connectionEntity != Entity.Null &&
                            !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connectionEntity))
                        {
                            EntityManager.AddComponentData(connectionEntity,
                                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.ClosedByRemote });
                        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[Validation] KICKED player entity {playerEntity.Index} (consecutive kicks: {state.ConsecutiveKicks})");
#endif
                        break;

                    case PenaltyLevel.TempBan:
                        state.ViolationScore = 0f;
                        state.PenaltyLevel = (byte)PenaltyLevel.None;

                        if (netId >= 0)
                        {
                            BanListManager.AddTempBan(
                                netId,
                                config.TempBanDurationMinutes,
                                "Anti-cheat: excessive violations",
                                state.ViolationScore);
                        }

                        if (connectionEntity != Entity.Null &&
                            !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connectionEntity))
                        {
                            EntityManager.AddComponentData(connectionEntity,
                                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.ClosedByRemote });
                        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[Validation] TEMP BANNED player entity {playerEntity.Index} for {config.TempBanDurationMinutes} minutes");
#endif
                        break;

                    case PenaltyLevel.PermaBan:
                        state.ViolationScore = 0f;
                        state.PenaltyLevel = (byte)PenaltyLevel.None;

                        if (netId >= 0)
                        {
                            BanListManager.AddPermaBan(
                                netId,
                                "Anti-cheat: permanent ban",
                                state.ViolationScore);
                        }

                        if (connectionEntity != Entity.Null &&
                            !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connectionEntity))
                        {
                            EntityManager.AddComponentData(connectionEntity,
                                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.ClosedByRemote });
                        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[Validation] PERMANENTLY BANNED player entity {playerEntity.Index}");
#endif
                        break;
                }
            }

            connMap.Dispose();
        }
    }
}
