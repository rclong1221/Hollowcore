using Unity.Entities;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Buffer on RunState entity. Generated options awaiting player selection.
    /// Cleared after selection or timeout.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingRewardChoice : IBufferElementData
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int SlotIndex;                       // Which choice slot (0, 1, 2...)
        public bool IsSelected;
    }

    /// <summary>
    /// EPIC 23.5: Transient request component. Created by UI when player picks a reward.
    /// Placed on RunState entity.
    /// </summary>
    public struct RewardSelectionRequest : IComponentData
    {
        public int SlotIndex;
    }
}
