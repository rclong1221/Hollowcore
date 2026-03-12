using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Handles voluntary trade cancellation and disconnect detection.
    /// Sets State=Cancelled and notifies both players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeRequestReceiveSystem))]
    public partial class TradeCancelReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;
        private EntityQuery _sessionQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeCancelRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _sessionQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeSessionTag>(),
                ComponentType.ReadWrite<TradeSessionState>());
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            if (_rpcQuery.IsEmpty && _sessionQuery.IsEmpty) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Handle voluntary cancellation RPCs
            if (!_rpcQuery.IsEmpty)
            {
                var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
                var rpcs = _rpcQuery.ToComponentDataArray<TradeCancelRpc>(Allocator.Temp);
                var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    ecb.DestroyEntity(entities[i]);

                    var connection = receives[i].SourceConnection;
                    var player = ResolvePlayer(connection);
                    if (player == Entity.Null) continue;

                    CancelSessionForPlayer(ecb, player, rpcs[i].Reason);
                }

                entities.Dispose();
                rpcs.Dispose();
                receives.Dispose();
            }

            // 2. Handle disconnected players (connection entity destroyed)
            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State > TradeState.Executing) continue;

                bool initiatorGone = !EntityManager.Exists(state.ValueRO.InitiatorConnection) ||
                                     !EntityManager.HasComponent<NetworkId>(state.ValueRO.InitiatorConnection);
                bool targetGone = !EntityManager.Exists(state.ValueRO.TargetConnection) ||
                                  !EntityManager.HasComponent<NetworkId>(state.ValueRO.TargetConnection);

                if (initiatorGone || targetGone)
                {
                    state.ValueRW.State = TradeState.Cancelled;

                    Entity survivingConnection = initiatorGone ? state.ValueRO.TargetConnection : state.ValueRO.InitiatorConnection;
                    if (EntityManager.Exists(survivingConnection))
                    {
                        var notify = ecb.CreateEntity();
                        ecb.AddComponent(notify, new TradeStateNotifyRpc
                        {
                            NewState = TradeState.Cancelled,
                            FailReason = (byte)TradeCancelReason.Disconnect
                        });
                        ecb.AddComponent(notify, new SendRpcCommandRequest { TargetConnection = survivingConnection });
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void CancelSessionForPlayer(EntityCommandBuffer ecb, Entity player, TradeCancelReason reason)
        {
            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State > TradeState.Executing) continue;
                if (state.ValueRO.InitiatorEntity != player && state.ValueRO.TargetEntity != player) continue;

                state.ValueRW.State = TradeState.Cancelled;

                // Notify both players
                NotifyPlayer(ecb, state.ValueRO.InitiatorConnection, TradeState.Cancelled, (byte)reason);
                NotifyPlayer(ecb, state.ValueRO.TargetConnection, TradeState.Cancelled, (byte)reason);
                return;
            }
        }

        private void NotifyPlayer(EntityCommandBuffer ecb, Entity connection, TradeState newState, byte reason)
        {
            if (!EntityManager.Exists(connection)) return;
            var notify = ecb.CreateEntity();
            ecb.AddComponent(notify, new TradeStateNotifyRpc { NewState = newState, FailReason = reason });
            ecb.AddComponent(notify, new SendRpcCommandRequest { TargetConnection = connection });
        }

        private Entity ResolvePlayer(Entity sourceConnection)
        {
            if (sourceConnection == Entity.Null) return Entity.Null;
            if (!SystemAPI.HasComponent<CommandTarget>(sourceConnection)) return Entity.Null;
            return SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
        }
    }
}
