using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Cancels active trades if either player enters combat.
    /// Checks CombatState.IsInCombat every frame (cheap — only active sessions).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeExecutionSystem))]
    public partial class TradeCombatCheckSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State != TradeState.Active && state.ValueRO.State != TradeState.Pending)
                    continue;

                bool initiatorInCombat = EntityManager.HasComponent<CombatState>(state.ValueRO.InitiatorEntity) &&
                                         EntityManager.GetComponentData<CombatState>(state.ValueRO.InitiatorEntity).IsInCombat;
                bool targetInCombat = EntityManager.HasComponent<CombatState>(state.ValueRO.TargetEntity) &&
                                      EntityManager.GetComponentData<CombatState>(state.ValueRO.TargetEntity).IsInCombat;

                if (initiatorInCombat || targetInCombat)
                {
                    state.ValueRW.State = TradeState.Cancelled;

                    NotifyPlayer(ecb, state.ValueRO.InitiatorConnection, (byte)TradeCancelReason.EnteredCombat);
                    NotifyPlayer(ecb, state.ValueRO.TargetConnection, (byte)TradeCancelReason.EnteredCombat);
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
