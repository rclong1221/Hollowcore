using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Auto-cancels trade sessions that exceed TimeoutTicks.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeExecutionSystem))]
    public partial class TradeTimeoutSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<TradeConfig>();
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State > TradeState.Executing) continue;
                if (currentTick - state.ValueRO.CreationTick < config.TimeoutTicks) continue;

                state.ValueRW.State = TradeState.Cancelled;

                NotifyPlayer(ecb, state.ValueRO.InitiatorConnection, (byte)TradeCancelReason.Timeout);
                NotifyPlayer(ecb, state.ValueRO.TargetConnection, (byte)TradeCancelReason.Timeout);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void NotifyPlayer(EntityCommandBuffer ecb, Entity connection, byte reason)
        {
            if (!EntityManager.Exists(connection)) return;
            var notify = ecb.CreateEntity();
            ecb.AddComponent(notify, new TradeStateNotifyRpc { NewState = TradeState.Cancelled, FailReason = reason });
            ecb.AddComponent(notify, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}
