using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Receives TradeConfirmRpc, sets confirm flags.
    /// When both players confirmed, transitions to Executing state.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeOfferReceiveSystem))]
    public partial class TradeConfirmReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeConfirmRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            if (_rpcQuery.IsEmpty) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                ecb.DestroyEntity(entities[i]);

                var connection = receives[i].SourceConnection;
                var player = ResolvePlayer(connection);
                if (player == Entity.Null) continue;

                foreach (var (state, confirmState, sessionEntity) in
                         SystemAPI.Query<RefRW<TradeSessionState>, RefRW<TradeConfirmState>>()
                             .WithAll<TradeSessionTag>()
                             .WithEntityAccess())
                {
                    if (state.ValueRO.State != TradeState.Active) continue;

                    if (state.ValueRO.InitiatorEntity == player)
                        confirmState.ValueRW.InitiatorConfirmed = true;
                    else if (state.ValueRO.TargetEntity == player)
                        confirmState.ValueRW.TargetConfirmed = true;
                    else
                        continue;

                    // Notify both about confirm state change
                    // (Client can show "Waiting for other player..." or "Both confirmed")

                    if (confirmState.ValueRO.InitiatorConfirmed && confirmState.ValueRO.TargetConfirmed)
                    {
                        state.ValueRW.State = TradeState.Executing;

                        var notifyInit = ecb.CreateEntity();
                        ecb.AddComponent(notifyInit, new TradeStateNotifyRpc { NewState = TradeState.Executing, FailReason = 0 });
                        ecb.AddComponent(notifyInit, new SendRpcCommandRequest { TargetConnection = state.ValueRO.InitiatorConnection });

                        var notifyTarget = ecb.CreateEntity();
                        ecb.AddComponent(notifyTarget, new TradeStateNotifyRpc { NewState = TradeState.Executing, FailReason = 0 });
                        ecb.AddComponent(notifyTarget, new SendRpcCommandRequest { TargetConnection = state.ValueRO.TargetConnection });
                    }
                    break;
                }
            }

            entities.Dispose();
            receives.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private Entity ResolvePlayer(Entity sourceConnection)
        {
            if (sourceConnection == Entity.Null) return Entity.Null;
            if (!SystemAPI.HasComponent<CommandTarget>(sourceConnection)) return Entity.Null;
            return SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
        }
    }
}
