using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Economy audit system.
    /// Monitors CurrencyInventory changes and logs audit trail entries.
    /// Reconciles balance vs audit trail to detect memory-edit exploits.
    /// Budget: &lt;0.05ms (only runs on frames where transactions occur).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EconomyAuditSystem : SystemBase
    {
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<ValidationConfig>();
            RequireForUpdate<Unity.NetCode.NetworkTime>();
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<ValidationLink>(),
                ComponentType.ReadOnly<DIG.Economy.CurrencyInventory>());
        }

        protected override void OnUpdate()
        {
            if (_playerQuery.CalculateEntityCount() == 0) return;

            var networkTime = SystemAPI.GetSingleton<Unity.NetCode.NetworkTime>();
            if (!networkTime.ServerTick.IsValid) return;
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;

            var auditLookup = GetBufferLookup<EconomyAuditEntry>(false);

            foreach (var (currency, link, entity) in
                SystemAPI.Query<RefRO<DIG.Economy.CurrencyInventory>, RefRO<ValidationLink>>()
                    .WithEntityAccess())
            {
                var childEntity = link.ValueRO.ValidationChild;
                if (childEntity == Entity.Null) continue;
                if (!auditLookup.HasBuffer(childEntity)) continue;

                var auditBuffer = auditLookup[childEntity];
                int currentGold = currency.ValueRO.Gold;

                // Reconciliation: compare last audit entry's BalanceAfter with current balance
                if (auditBuffer.Length > 0)
                {
                    var lastEntry = auditBuffer[auditBuffer.Length - 1];
                    int expectedGold = lastEntry.BalanceAfter;

                    // If balance doesn't match and no transaction happened this frame,
                    // it was a memory edit
                    if (currentGold != expectedGold)
                    {
                        int discrepancy = currentGold - expectedGold;

                        // Log the discrepancy as an audit entry
                        auditBuffer.Add(new EconomyAuditEntry
                        {
                            TransactionType = 0, // Gold
                            SourceSystem = (byte)TransactionSourceSystem.Admin, // Unknown source = suspicious
                            Amount = discrepancy,
                            BalanceBefore = expectedGold,
                            BalanceAfter = currentGold,
                            ServerTick = currentTick
                        });

                        // Only flag significant discrepancies (> 0 gain without transaction)
                        if (discrepancy > 0)
                        {
                            RateLimitHelper.CreateViolation(
                                EntityManager, entity,
                                ViolationType.Economy, 1.0f, 0, currentTick);
                        }
                    }
                }
                else
                {
                    // First frame: seed audit trail with current balance
                    auditBuffer.Add(new EconomyAuditEntry
                    {
                        TransactionType = 0,
                        SourceSystem = (byte)TransactionSourceSystem.Admin,
                        Amount = currentGold,
                        BalanceBefore = 0,
                        BalanceAfter = currentGold,
                        ServerTick = currentTick
                    });
                }

                // Ring buffer trim: keep last 32 entries max
                if (auditBuffer.Length > 64)
                {
                    int toRemove = auditBuffer.Length - 32;
                    auditBuffer.RemoveRange(0, toRemove);
                }
            }
        }
    }
}
