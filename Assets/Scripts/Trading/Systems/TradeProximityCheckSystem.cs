using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Continuous proximity check. Every 30 ticks, verifies traders
    /// are within range. Uses 1.5x hysteresis to avoid flicker.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeExecutionSystem))]
    public partial class TradeProximityCheckSystem : SystemBase
    {
        private const uint CheckInterval = 30;

        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!netTime.ServerTick.IsValid) return;
            if (netTime.ServerTick.TickIndexForValidTick % CheckInterval != 0) return;

            var config = SystemAPI.GetSingleton<TradeConfig>();
            float maxDistSq = config.ProximityRange * config.ProximityRange * 2.25f; // 1.5x hysteresis
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State != TradeState.Active) continue;

                if (!EntityManager.HasComponent<LocalTransform>(state.ValueRO.InitiatorEntity) ||
                    !EntityManager.HasComponent<LocalTransform>(state.ValueRO.TargetEntity))
                    continue;

                float distSq = math.distancesq(
                    EntityManager.GetComponentData<LocalTransform>(state.ValueRO.InitiatorEntity).Position,
                    EntityManager.GetComponentData<LocalTransform>(state.ValueRO.TargetEntity).Position);

                if (distSq > maxDistSq)
                {
                    state.ValueRW.State = TradeState.Cancelled;
                    NotifyPlayer(ecb, state.ValueRO.InitiatorConnection, (byte)TradeCancelReason.TooFar);
                    NotifyPlayer(ecb, state.ValueRO.TargetConnection, (byte)TradeCancelReason.TooFar);
                }
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
