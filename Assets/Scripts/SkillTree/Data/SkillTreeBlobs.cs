using Unity.Entities;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Singleton holding BlobAsset reference to all skill tree definitions.
    /// Created by SkillTreeBootstrapSystem from Resources/SkillTreeDatabase.
    /// </summary>
    public struct SkillTreeRegistrySingleton : IComponentData
    {
        public BlobAssetReference<SkillTreeRegistryBlob> Registry;
    }

    public struct SkillTreeRegistryBlob
    {
        public BlobArray<SkillTreeBlob> Trees;
        public int TalentPointsPerLevel;
        public int RespecBaseCost;
        public float RespecCostMultiplier;
        public int RespecCostCap;
    }

    public struct SkillTreeBlob
    {
        public int TreeId;
        public BlobString Name;
        public int MaxPoints;
        public byte ClassRestriction;
        public BlobArray<SkillNodeBlob> Nodes;
    }

    public struct SkillNodeBlob
    {
        public int NodeId;
        public int Tier;
        public int PointCost;
        public int TierPointsRequired;
        public SkillNodeType NodeType;
        public int MaxRanks;
        public SkillPassiveBonus PassiveBonus;
        public int AbilityTypeId;
        public int PrereqNodeId0;
        public int PrereqNodeId1;
        public int PrereqNodeId2;
        public float EditorX;
        public float EditorY;
    }

    public struct SkillPassiveBonus
    {
        public SkillBonusType BonusType;
        public float Value;
    }

    public enum SkillNodeType : byte
    {
        Passive = 0,
        ActiveAbility = 1,
        Keystone = 2,
        Gateway = 3
    }

    public enum SkillBonusType : byte
    {
        None = 0,
        MaxHealth = 1,
        AttackPower = 2,
        SpellPower = 3,
        Defense = 4,
        Armor = 5,
        CritChance = 6,
        CritDamage = 7,
        MovementSpeed = 8,
        CooldownReduction = 9,
        ResourceRegen = 10,
        DamagePercent = 11,
        HealingPercent = 12,
        ElementalDamage = 13,
        StatusDuration = 14,
        LifeSteal = 15,
        DodgeChance = 16,
        BlockChance = 17,
        AttackSpeed = 18
    }
}
