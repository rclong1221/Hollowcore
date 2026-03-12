using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Economy.Systems
{
    /// <summary>
    /// EPIC 16.6: Processes CurrencyTransaction buffer entries.
    /// Validates balance (no negative), clamps to 0, clears buffer each frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CurrencyTransactionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (transactions, currency) in
                     SystemAPI.Query<DynamicBuffer<CurrencyTransaction>, RefRW<CurrencyInventory>>())
            {
                if (transactions.Length == 0) continue;

                var inv = currency.ValueRO;

                for (int i = 0; i < transactions.Length; i++)
                {
                    var tx = transactions[i];

                    switch (tx.Type)
                    {
                        case CurrencyType.Gold:
                            inv.Gold = math.max(0, inv.Gold + tx.Amount);
                            break;
                        case CurrencyType.Premium:
                            inv.Premium = math.max(0, inv.Premium + tx.Amount);
                            break;
                        case CurrencyType.Crafting:
                            inv.Crafting = math.max(0, inv.Crafting + tx.Amount);
                            break;
                    }
                }

                currency.ValueRW = inv;
                transactions.Clear();
            }
        }
    }
}
