using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Validation;
using CombatState = DIG.Combat.Components.CombatState;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Receives TradeRequestRpc, validates (self-trade, combat, proximity, session, cooldown),
    /// creates trade session entity. Follows PartyRpcReceiveSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TradeRequestReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;
        private EntityQuery _sessionQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeRequestRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _sessionQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeSessionTag>(),
                ComponentType.ReadOnly<TradeSessionState>());
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            if (_rpcQuery.IsEmpty) return;

            var config = SystemAPI.GetSingleton<TradeConfig>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _rpcQuery.ToComponentDataArray<TradeRequestRpc>(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            // Fix #1: Hoist session array once (was allocating per-call inside IsInActiveSession)
            var sessions = _sessionQuery.ToComponentDataArray<TradeSessionState>(Allocator.Temp);

            // Fix #2: Build ghost ID → Entity lookup (O(1) instead of O(N) per RPC)
            var ghostLookup = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (ghost, entity) in SystemAPI.Query<RefRO<GhostInstance>>().WithEntityAccess())
                ghostLookup.TryAdd(ghost.ValueRO.ghostId, entity);

            // Fix #3: Build player Entity → connection Entity lookup (O(1) instead of O(N) per RPC)
            var playerToConnection = new NativeHashMap<Entity, Entity>(32, Allocator.Temp);
            foreach (var (cmdTarget, connEntity) in SystemAPI.Query<RefRO<CommandTarget>>()
                         .WithAll<NetworkId>()
                         .WithEntityAccess())
            {
                if (cmdTarget.ValueRO.targetEntity != Entity.Null)
                    playerToConnection.TryAdd(cmdTarget.ValueRO.targetEntity, connEntity);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                ecb.DestroyEntity(entities[i]);

                var connection = receives[i].SourceConnection;
                var initiator = ResolvePlayer(connection);
                if (initiator == Entity.Null) continue;

                // --- ANTI-CHEAT: Rate limit check ---
                if (EntityManager.HasComponent<ValidationLink>(initiator))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(initiator).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.TRADE_REQUEST))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, initiator,
                            ViolationType.RateLimit, 0.8f, RpcTypeIds.TRADE_REQUEST, 0);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                if (!ghostLookup.TryGetValue(rpcs[i].TargetGhostId, out var target)) continue;

                // 1. Self-trade prevention
                if (initiator == target) continue;

                // 2. Both must be players
                if (!EntityManager.HasComponent<PlayerTag>(initiator) ||
                    !EntityManager.HasComponent<PlayerTag>(target)) continue;

                // 3. Combat check
                if (EntityManager.HasComponent<CombatState>(initiator) &&
                    EntityManager.GetComponentData<CombatState>(initiator).IsInCombat) continue;
                if (EntityManager.HasComponent<CombatState>(target) &&
                    EntityManager.GetComponentData<CombatState>(target).IsInCombat) continue;

                // 4. Proximity check
                if (!EntityManager.HasComponent<LocalTransform>(initiator) ||
                    !EntityManager.HasComponent<LocalTransform>(target)) continue;
                float distSq = math.distancesq(
                    EntityManager.GetComponentData<LocalTransform>(initiator).Position,
                    EntityManager.GetComponentData<LocalTransform>(target).Position);
                if (distSq > config.ProximityRange * config.ProximityRange) continue;

                // 5. Neither already in active session
                if (IsInActiveSession(sessions, initiator) || IsInActiveSession(sessions, target)) continue;

                // 6. Cooldown check
                if (EntityManager.HasComponent<TradePlayerCooldown>(connection))
                {
                    var cooldown = EntityManager.GetComponentData<TradePlayerCooldown>(connection);
                    if (currentTick - cooldown.LastTradeRequestTick < config.CooldownTicks) continue;
                    EntityManager.SetComponentData(connection, new TradePlayerCooldown { LastTradeRequestTick = currentTick });
                }
                else
                {
                    ecb.AddComponent(connection, new TradePlayerCooldown { LastTradeRequestTick = currentTick });
                }

                // 7. Resolve target connection for RPCs
                if (!playerToConnection.TryGetValue(target, out var targetConnection)) continue;

                // 8. Create trade session entity
                var sessionEntity = ecb.CreateEntity();
                ecb.AddComponent(sessionEntity, new TradeSessionTag());
                ecb.AddComponent(sessionEntity, new TradeSessionState
                {
                    InitiatorEntity = initiator,
                    TargetEntity = target,
                    State = TradeState.Pending,
                    CreationTick = currentTick,
                    LastModifiedTick = currentTick,
                    InitiatorConnection = connection,
                    TargetConnection = targetConnection
                });
                ecb.AddComponent(sessionEntity, new TradeConfirmState());
                ecb.AddBuffer<TradeOffer>(sessionEntity);

                // 9. Notify target player
                var notifyEntity = ecb.CreateEntity();
                ecb.AddComponent(notifyEntity, new TradeSessionNotifyRpc
                {
                    InitiatorGhostId = EntityManager.HasComponent<GhostInstance>(initiator)
                        ? EntityManager.GetComponentData<GhostInstance>(initiator).ghostId
                        : 0
                });
                ecb.AddComponent(notifyEntity, new SendRpcCommandRequest { TargetConnection = targetConnection });

                TradeVisualQueue.Enqueue(new TradeVisualQueue.TradeVisualEvent
                {
                    Type = TradeVisualEventType.TradeRequested,
                    Payload = 0
                });
            }

            entities.Dispose();
            rpcs.Dispose();
            receives.Dispose();
            sessions.Dispose();
            ghostLookup.Dispose();
            playerToConnection.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static bool IsInActiveSession(NativeArray<TradeSessionState> sessions, Entity player)
        {
            for (int s = 0; s < sessions.Length; s++)
            {
                var state = sessions[s];
                if (state.State <= TradeState.Executing &&
                    (state.InitiatorEntity == player || state.TargetEntity == player))
                    return true;
            }
            return false;
        }

        private Entity ResolvePlayer(Entity sourceConnection)
        {
            if (sourceConnection == Entity.Null) return Entity.Null;
            if (!SystemAPI.HasComponent<CommandTarget>(sourceConnection)) return Entity.Null;
            return SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
        }
    }
}
