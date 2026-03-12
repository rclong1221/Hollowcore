using Unity.Entities;
using Unity.NetCode;

namespace DIG.Economy
{
    /// <summary>
    /// EPIC 16.6: Player's currency balances.
    /// Ghost-replicated so clients can display current gold/premium/crafting.
    /// Safe to add to player entity (~12 bytes, within 16KB budget).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CurrencyInventory : IComponentData
    {
        [GhostField] public int Gold;
        [GhostField] public int Premium;
        [GhostField] public int Crafting;
    }
}
