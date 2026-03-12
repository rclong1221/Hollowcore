using Unity.Entities;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Tag to identify achievement child entities. 0 bytes.
    /// </summary>
    public struct AchievementChildTag : IComponentData { }

    /// <summary>
    /// EPIC 17.7: Back-reference to owning player entity. 8 bytes on child.
    /// </summary>
    public struct AchievementOwner : IComponentData
    {
        public Entity Owner;
    }

    /// <summary>
    /// EPIC 17.7: Per-achievement progress entry on child entity. 12 bytes per element.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct AchievementProgress : IBufferElementData
    {
        public ushort AchievementId;
        public int CurrentValue;
        public bool IsUnlocked;
        public byte HighestTierUnlocked;
        public uint UnlockTick;
    }

    /// <summary>
    /// EPIC 17.7: Cumulative stat counters for complex conditions. 48 bytes on child.
    /// </summary>
    public struct AchievementCumulativeStats : IComponentData
    {
        public int TotalKills;
        public int TotalDeaths;
        public int TotalQuestsCompleted;
        public int TotalItemsCrafted;
        public int TotalNPCsInteracted;
        public long TotalDamageDealt;
        public int TotalLootCollected;
        public int HighestKillStreak;
        public int CurrentKillStreak;
        public int ConsecutiveLoginDays;
    }

    /// <summary>
    /// EPIC 17.7: Dirty flags for save system optimization. 4 bytes on child.
    /// Bit 0 = progress changed, Bit 1 = stats changed.
    /// </summary>
    public struct AchievementDirtyFlags : IComponentData
    {
        public uint Flags;
    }
}
