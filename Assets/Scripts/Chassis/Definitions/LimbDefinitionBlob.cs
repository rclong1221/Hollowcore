using Unity.Entities;

namespace Hollowcore.Chassis.Definitions
{
    public struct LimbDefinitionBlob
    {
        public int LimbId;
        public BlobString DisplayName;
        public ChassisSlot SlotType;
        public LimbRarity Rarity;

        public float MaxIntegrity;
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;
        public float ToxinResistance;
        public float FallDamageReduction;

        public int DistrictAffinityId;
        public float AffinityBonusMultiplier;

        public int SpecialAbilityId;
        public float TemporaryDuration;
        public bool CanBeCursed;

        public BlobArray<LimbMemoryEntryBlob> MemoryEntries;
    }

    public struct LimbMemoryEntryBlob
    {
        public int DistrictId;
        public byte BonusType;
        public float BonusValue;
    }

    /// <summary>
    /// Database blob containing all limb definitions. Singleton entity.
    /// </summary>
    public struct LimbDefinitionDatabase
    {
        public BlobArray<LimbDefinitionBlob> Definitions;
    }

    /// <summary>
    /// Singleton component referencing the baked database.
    /// </summary>
    public struct LimbDatabaseReference : IComponentData
    {
        public BlobAssetReference<LimbDefinitionDatabase> Value;
    }
}
