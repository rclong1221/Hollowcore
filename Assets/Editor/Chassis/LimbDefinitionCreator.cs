using Hollowcore.Chassis;
using Hollowcore.Chassis.Definitions;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.Chassis
{
    /// <summary>
    /// Editor utility to create starter limb definition assets.
    /// Creates a basic set of limbs for all 6 chassis slots.
    /// </summary>
    public static class LimbDefinitionCreator
    {
        private const string BasePath = "Assets/Resources/Chassis/Limbs/";

        [MenuItem("Hollowcore/Chassis/Create Starter Limb Set")]
        public static void CreateStarterSet()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Chassis/Limbs"))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Chassis", "Limbs");
            }

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 1,
                DisplayName = "Standard Head",
                Description = "A basic synthetic cranial unit. Adequate sensors, nothing fancy.",
                SlotType = ChassisSlot.Head,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 80f,
                BonusMaxHealth = 10f,
                BonusArmor = 2f
            }, "Limb_CommonHead");

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 2,
                DisplayName = "Standard Torso",
                Description = "Factory-issue chassis core. Keeps you standing.",
                SlotType = ChassisSlot.Torso,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 150f,
                BonusMaxHealth = 25f,
                BonusArmor = 5f,
                BonusStamina = 10f
            }, "Limb_CommonTorso");

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 3,
                DisplayName = "Standard Left Arm",
                Description = "Basic manipulator arm. Gets the job done.",
                SlotType = ChassisSlot.LeftArm,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 100f,
                BonusDamage = 5f,
                BonusAttackSpeed = 0.1f
            }, "Limb_CommonLeftArm");

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 4,
                DisplayName = "Standard Right Arm",
                Description = "Basic manipulator arm. Mirror of the left.",
                SlotType = ChassisSlot.RightArm,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 100f,
                BonusDamage = 5f,
                BonusAttackSpeed = 0.1f
            }, "Limb_CommonRightArm");

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 5,
                DisplayName = "Standard Left Leg",
                Description = "Basic locomotion limb. Steady and reliable.",
                SlotType = ChassisSlot.LeftLeg,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 120f,
                BonusMoveSpeed = 0.5f,
                BonusStamina = 5f,
                FallDamageReduction = 0.05f
            }, "Limb_CommonLeftLeg");

            CreateLimb(new LimbDefinitionSO
            {
                LimbId = 6,
                DisplayName = "Standard Right Leg",
                Description = "Basic locomotion limb. Mirror of the left.",
                SlotType = ChassisSlot.RightLeg,
                Rarity = LimbRarity.Common,
                MaxIntegrity = 120f,
                BonusMoveSpeed = 0.5f,
                BonusStamina = 5f,
                FallDamageReduction = 0.05f
            }, "Limb_CommonRightLeg");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LimbDefinitionCreator] Created 6 starter limb definitions in " + BasePath);
        }

        private static void CreateLimb(LimbDefinitionSO template, string fileName)
        {
            var path = BasePath + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
            if (existing != null)
            {
                Debug.Log($"[LimbDefinitionCreator] '{fileName}' already exists, skipping.");
                return;
            }

            var asset = ScriptableObject.CreateInstance<LimbDefinitionSO>();
            asset.LimbId = template.LimbId;
            asset.DisplayName = template.DisplayName;
            asset.Description = template.Description;
            asset.SlotType = template.SlotType;
            asset.Rarity = template.Rarity;
            asset.MaxIntegrity = template.MaxIntegrity;
            asset.BonusDamage = template.BonusDamage;
            asset.BonusArmor = template.BonusArmor;
            asset.BonusMoveSpeed = template.BonusMoveSpeed;
            asset.BonusMaxHealth = template.BonusMaxHealth;
            asset.BonusAttackSpeed = template.BonusAttackSpeed;
            asset.BonusStamina = template.BonusStamina;
            asset.HeatResistance = template.HeatResistance;
            asset.ToxinResistance = template.ToxinResistance;
            asset.FallDamageReduction = template.FallDamageReduction;
            asset.DistrictAffinityId = template.DistrictAffinityId;
            asset.TemporaryDuration = 45f;

            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
