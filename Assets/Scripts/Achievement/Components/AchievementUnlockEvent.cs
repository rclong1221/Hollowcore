using Unity.Entities;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Transient entity created by AchievementUnlockSystem when a tier is reached.
    /// Consumed by AchievementRewardSystem, destroyed by AchievementCleanupSystem same frame.
    /// </summary>
    public struct AchievementUnlockEvent : IComponentData
    {
        public ushort AchievementId;
        public Entity PlayerId;
        public byte Tier;
        public uint ServerTick;
    }
}
