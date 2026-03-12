using Unity.Entities;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Buffer on RunState entity. Populated when entering a Shop zone.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ShopInventoryEntry : IBufferElementData
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int Price;                           // In RunCurrency
        public bool IsSoldOut;
    }

    /// <summary>
    /// EPIC 23.5: Transient request component. Created by UI when player buys from shop.
    /// Placed on RunState entity.
    /// </summary>
    public struct ShopPurchaseRequest : IComponentData
    {
        public int ShopSlotIndex;
    }
}
