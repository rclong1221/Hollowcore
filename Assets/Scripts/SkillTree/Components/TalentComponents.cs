using Unity.Entities;

namespace DIG.SkillTree
{
    /// <summary>Tag to identify talent child entities during linking.</summary>
    public struct TalentChildTag : IComponentData { }

    /// <summary>Back-reference from talent child to owning player entity.</summary>
    public struct TalentOwner : IComponentData
    {
        public Entity Owner;
    }

    /// <summary>
    /// EPIC 17.1: Core talent state on child entity — 16 bytes.
    /// Tracks total/spent talent points and respec history.
    /// </summary>
    public struct TalentState : IComponentData
    {
        public int TotalTalentPoints;
        public int SpentTalentPoints;
        public byte ActiveTreeCount;
        public byte RespecCount;
    }

    /// <summary>
    /// EPIC 17.1: Computed passive stat bonuses from all unlocked talent nodes — 48 bytes.
    /// Recalculated by TalentPassiveSystem whenever allocations change.
    /// TalentStatBridgeSystem copies these to AttackStats/DefenseStats/Health.
    /// </summary>
    public struct TalentPassiveStats : IComponentData
    {
        public float BonusMaxHealth;
        public float BonusAttackPower;
        public float BonusSpellPower;
        public float BonusDefense;
        public float BonusArmor;
        public float BonusCritChance;
        public float BonusCritDamage;
        public float BonusMovementSpeed;
        public float BonusCooldownReduction;
        public float BonusResourceRegen;
        public float BonusDamagePercent;
        public float BonusHealingPercent;
    }

    /// <summary>
    /// EPIC 17.1: Individual talent allocation — 8 bytes per entry.
    /// Each entry represents one rank of one node the player has unlocked.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct TalentAllocation : IBufferElementData
    {
        public ushort TreeId;
        public ushort NodeId;
        public int AllocatedTick;
    }

    /// <summary>
    /// EPIC 17.1: Pending allocation request from RPC — 8 bytes per entry.
    /// Processed by TalentAllocationSystem / TalentRespecSystem.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct TalentAllocationRequest : IBufferElementData
    {
        public ushort TreeId;
        public ushort NodeId;
        public TalentRequestType RequestType;
    }

    public enum TalentRequestType : byte
    {
        Allocate = 0,
        Respec = 1
    }

    /// <summary>
    /// EPIC 17.1: Per-tree point tracking — 8 bytes per tree.
    /// Tracks points spent and highest tier unlocked in each tree.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TalentTreeProgress : IBufferElementData
    {
        public ushort TreeId;
        public ushort PointsSpent;
        public ushort HighestTier;
        public ushort Padding;
    }
}
