using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Hollowcore.Chassis.Definitions
{
    [CreateAssetMenu(fileName = "NewLimb", menuName = "Hollowcore/Chassis/Limb Definition")]
    public class LimbDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int LimbId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ChassisSlot SlotType;
        public LimbRarity Rarity;

        [Header("Stats")]
        public float MaxIntegrity = 100f;
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;
        public float ToxinResistance;
        public float FallDamageReduction;

        [Header("District Memory")]
        [Tooltip("District this limb has affinity for (-1 = none)")]
        public int DistrictAffinityId = -1;
        [Tooltip("Bonus multiplier when in affinity district (0.05 = 5%)")]
        public float AffinityBonusMultiplier = 0.05f;

        [Header("Special")]
        [Tooltip("Ability unlocked while this limb is equipped (0 = none)")]
        public int SpecialAbilityId;
        [Tooltip("Visual prefab for the limb mesh")]
        public GameObject VisualPrefab;
        [Tooltip("Visual prefab for the destroyed stump")]
        public GameObject StumpPrefab;

        [Header("Rip Settings")]
        [Tooltip("If from an enemy rip: how long temporary limbs last")]
        public float TemporaryDuration = 45f;
        [Tooltip("Whether this limb can carry curses/instabilities")]
        public bool CanBeCursed;

        public LimbStatBlock ToStatBlock()
        {
            return new LimbStatBlock
            {
                BonusDamage = BonusDamage,
                BonusArmor = BonusArmor,
                BonusMoveSpeed = BonusMoveSpeed,
                BonusMaxHealth = BonusMaxHealth,
                BonusAttackSpeed = BonusAttackSpeed,
                BonusStamina = BonusStamina,
                HeatResistance = HeatResistance,
                ToxinResistance = ToxinResistance,
                FallDamageReduction = FallDamageReduction
            };
        }

        public LimbInstance ToLimbInstance(LimbDurability durability = LimbDurability.Permanent)
        {
            return new LimbInstance
            {
                LimbDefinitionId = LimbId,
                SlotType = SlotType,
                CurrentIntegrity = MaxIntegrity,
                MaxIntegrity = MaxIntegrity,
                Rarity = Rarity,
                DurabilityType = durability,
                ElapsedTime = 0f,
                ExpirationTime = durability == LimbDurability.Temporary ? TemporaryDuration : 0f,
                DistrictAffinityId = DistrictAffinityId,
                DisplayName = new FixedString64Bytes(DisplayName ?? name)
            };
        }

        public static BlobAssetReference<LimbDefinitionDatabase> BakeDatabaseToBlob(
            LimbDefinitionSO[] allDefinitions)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var db = ref builder.ConstructRoot<LimbDefinitionDatabase>();
            var defArray = builder.Allocate(ref db.Definitions, allDefinitions.Length);

            for (int i = 0; i < allDefinitions.Length; i++)
            {
                var so = allDefinitions[i];
                defArray[i].LimbId = so.LimbId;
                builder.AllocateString(ref defArray[i].DisplayName, so.DisplayName ?? so.name);
                defArray[i].SlotType = so.SlotType;
                defArray[i].Rarity = so.Rarity;
                defArray[i].MaxIntegrity = so.MaxIntegrity;
                defArray[i].BonusDamage = so.BonusDamage;
                defArray[i].BonusArmor = so.BonusArmor;
                defArray[i].BonusMoveSpeed = so.BonusMoveSpeed;
                defArray[i].BonusMaxHealth = so.BonusMaxHealth;
                defArray[i].BonusAttackSpeed = so.BonusAttackSpeed;
                defArray[i].BonusStamina = so.BonusStamina;
                defArray[i].HeatResistance = so.HeatResistance;
                defArray[i].ToxinResistance = so.ToxinResistance;
                defArray[i].FallDamageReduction = so.FallDamageReduction;
                defArray[i].DistrictAffinityId = so.DistrictAffinityId;
                defArray[i].AffinityBonusMultiplier = so.AffinityBonusMultiplier;
                defArray[i].SpecialAbilityId = so.SpecialAbilityId;
                defArray[i].TemporaryDuration = so.TemporaryDuration;
                defArray[i].CanBeCursed = so.CanBeCursed;

                // Empty memory entries for now (populated in EPIC 1.5)
                builder.Allocate(ref defArray[i].MemoryEntries, 0);
            }

            var result = builder.CreateBlobAssetReference<LimbDefinitionDatabase>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (LimbId < 0)
                Debug.LogError($"[LimbDefinitionSO] '{name}': LimbId must be >= 0", this);

            if (MaxIntegrity <= 0f)
                Debug.LogError($"[LimbDefinitionSO] '{name}': MaxIntegrity must be > 0", this);

            if (string.IsNullOrWhiteSpace(DisplayName))
                Debug.LogWarning($"[LimbDefinitionSO] '{name}': DisplayName is empty", this);

            if (DistrictAffinityId >= 0 && AffinityBonusMultiplier <= 0f)
                Debug.LogWarning($"[LimbDefinitionSO] '{name}': Has district affinity but AffinityBonusMultiplier is 0", this);

            if (TemporaryDuration < 0f)
                Debug.LogError($"[LimbDefinitionSO] '{name}': TemporaryDuration cannot be negative", this);

            if (SlotType == ChassisSlot.Head && VisualPrefab == null)
                Debug.LogWarning($"[LimbDefinitionSO] '{name}': Head limb should have a VisualPrefab", this);
        }
#endif
    }
}
