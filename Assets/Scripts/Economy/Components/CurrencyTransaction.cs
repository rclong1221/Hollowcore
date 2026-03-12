using Unity.Entities;

namespace DIG.Economy
{
    /// <summary>
    /// EPIC 16.6: Transient currency transaction request.
    /// Processed and cleared same frame by CurrencyTransactionSystem.
    /// Buffer on player entity — transient (not ghost-replicated).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CurrencyTransaction : IBufferElementData
    {
        /// <summary>Which currency to modify.</summary>
        public CurrencyType Type;

        /// <summary>Amount to add (positive) or remove (negative).</summary>
        public int Amount;

        /// <summary>Source entity (e.g., loot pickup, vendor, quest reward).</summary>
        public Entity Source;
    }
}
