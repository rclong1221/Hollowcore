using Unity.Entities;
using DIG.Shared;
using DIG.Economy;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Buffer element representing one item or currency offer in a trade session.
    /// Lives on the trade session entity. NOT ghost-replicated (server-only).
    /// Capacity 16 = max 8 items per side.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TradeOffer : IBufferElementData
    {
        /// <summary>0 = initiator's offer, 1 = target's offer.</summary>
        public byte OfferSide;
        /// <summary>Item or Currency.</summary>
        public TradeOfferType OfferType;
        /// <summary>Index into player's InventoryItem buffer (for item offers).</summary>
        public byte ItemSlotIndex;
        /// <summary>Which resource type (for validation at execution time).</summary>
        public ResourceType ItemType;
        /// <summary>Item quantity offered.</summary>
        public int Quantity;
        /// <summary>Which currency (for currency offers).</summary>
        public CurrencyType CurrencyType;
        /// <summary>Currency amount offered.</summary>
        public int CurrencyAmount;
    }

    /// <summary>
    /// EPIC 17.3: Distinguishes item vs currency offers.
    /// </summary>
    public enum TradeOfferType : byte
    {
        Item = 0,
        Currency = 1
    }
}
