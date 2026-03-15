using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    public enum LimbRarity : byte
    {
        Junk = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5
    }

    public enum LimbDurability : byte
    {
        Temporary = 0,
        DistrictLife = 1,
        Permanent = 2
    }

    /// <summary>
    /// Runtime state for an equipped or world-placed limb entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct LimbInstance : IComponentData
    {
        /// <summary>Reference to the ScriptableObject definition (baked as blob or hash).</summary>
        [GhostField] public int LimbDefinitionId;

        /// <summary>Which chassis slot this limb fits.</summary>
        [GhostField] public ChassisSlot SlotType;

        /// <summary>Current integrity (0 = destroyed).</summary>
        [GhostField] public float CurrentIntegrity;

        /// <summary>Max integrity from definition.</summary>
        [GhostField] public float MaxIntegrity;

        [GhostField] public LimbRarity Rarity;
        [GhostField] public LimbDurability DurabilityType;

        /// <summary>For Temporary limbs: elapsed time since equip. Destroyed when >= ExpirationTime.</summary>
        [GhostField(Quantization = 100)] public float ElapsedTime;
        [GhostField(Quantization = 100)] public float ExpirationTime;

        /// <summary>District affinity ID for memory bonus (-1 = no affinity).</summary>
        [GhostField] public int DistrictAffinityId;

        /// <summary>Display name for UI.</summary>
        [GhostField] public FixedString64Bytes DisplayName;
    }

    /// <summary>
    /// Stat contributions from a single limb. Read by ChassisStatAggregatorSystem.
    /// </summary>
    public struct LimbStatBlock : IComponentData
    {
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;
        public float ToxinResistance;
        public float FallDamageReduction;
    }
}
