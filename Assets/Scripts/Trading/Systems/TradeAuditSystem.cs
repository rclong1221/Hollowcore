using Unity.Entities;
using Unity.NetCode;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Logs completed/failed trades to the TradeAuditLog ring buffer.
    /// 256-entry ring buffer on the TradeConfig singleton entity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeExecutionSystem))]
    public partial class TradeAuditSystem : SystemBase
    {
        private const int MaxAuditEntries = 256;

        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var configEntity = SystemAPI.GetSingletonEntity<TradeConfig>();
            if (!EntityManager.HasBuffer<TradeAuditLog>(configEntity)) return;
            if (!EntityManager.HasComponent<TradeAuditState>(configEntity)) return;

            var auditLog = EntityManager.GetBuffer<TradeAuditLog>(configEntity);
            var auditState = EntityManager.GetComponentData<TradeAuditState>(configEntity);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRO<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State != TradeState.Completed && state.ValueRO.State != TradeState.Failed)
                    continue;

                // Get ghost IDs
                int initGhostId = EntityManager.HasComponent<GhostInstance>(state.ValueRO.InitiatorEntity)
                    ? EntityManager.GetComponentData<GhostInstance>(state.ValueRO.InitiatorEntity).ghostId : 0;
                int targetGhostId = EntityManager.HasComponent<GhostInstance>(state.ValueRO.TargetEntity)
                    ? EntityManager.GetComponentData<GhostInstance>(state.ValueRO.TargetEntity).ghostId : 0;

                // Summarize offers
                byte itemCount = 0;
                int goldDelta = 0, premiumDelta = 0, craftingDelta = 0;

                if (EntityManager.HasBuffer<TradeOffer>(sessionEntity))
                {
                    var offers = EntityManager.GetBuffer<TradeOffer>(sessionEntity, true);
                    for (int o = 0; o < offers.Length; o++)
                    {
                        var offer = offers[o];
                        if (offer.OfferType == TradeOfferType.Item)
                        {
                            itemCount++;
                        }
                        else
                        {
                            int sign = offer.OfferSide == 0 ? -1 : 1; // From A's perspective
                            switch (offer.CurrencyType)
                            {
                                case DIG.Economy.CurrencyType.Gold: goldDelta += sign * offer.CurrencyAmount; break;
                                case DIG.Economy.CurrencyType.Premium: premiumDelta += sign * offer.CurrencyAmount; break;
                                case DIG.Economy.CurrencyType.Crafting: craftingDelta += sign * offer.CurrencyAmount; break;
                            }
                        }
                    }
                }

                var entry = new TradeAuditLog
                {
                    InitiatorGhostId = initGhostId,
                    TargetGhostId = targetGhostId,
                    Timestamp = currentTick,
                    ItemCount = itemCount,
                    GoldDelta = goldDelta,
                    PremiumDelta = premiumDelta,
                    CraftingDelta = craftingDelta,
                    ResultCode = state.ValueRO.State == TradeState.Completed ? (byte)0 : (byte)1
                };

                // Write-index modulo ring buffer: overwrite in-place, no RemoveAt(0) shift
                int idx = auditState.NextWriteIndex % MaxAuditEntries;
                if (auditLog.Length < MaxAuditEntries)
                    auditLog.Add(entry);
                else
                    auditLog[idx] = entry;

                auditState.NextWriteIndex = idx + 1;
                auditState.TotalWritten++;
            }

            EntityManager.SetComponentData(configEntity, auditState);
        }
    }
}
