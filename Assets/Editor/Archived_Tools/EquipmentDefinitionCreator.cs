using UnityEditor;
using UnityEngine;
using DIG.Items.Definitions;
using System.IO;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// Purpose: Creates default equipment definition assets (slots, categories, input profiles).
    /// When to use: One-time during initial project setup.
    /// Safe to remove: After default assets exist and system is stable.
    /// </summary>
    public static class Setup_EquipmentDefaults
    {
        private const string BaseAssetsPath = "Assets/Content/Equipment/Definitions";
        private const string SlotsPath = BaseAssetsPath + "/Slots";
        private const string CategoriesPath = BaseAssetsPath + "/Categories";
        private const string InputProfilesPath = BaseAssetsPath + "/InputProfiles";

        [MenuItem("DIG/Wizards/Utilities/All Default Assets")]
        public static void CreateAllDefaults()
        {
            CreateDefaultSlots();
            CreateDefaultCategories();
            CreateDefaultInputProfiles();
            AssetDatabase.Refresh();
            Debug.Log("[Setup_EquipmentDefaults] Created all default equipment definition assets.");
        }

        [MenuItem("DIG/Wizards/Utilities/Default Slots")]
        public static void CreateDefaultSlots()
        {
            EnsureDirectoryExists(SlotsPath);
            
            // MainHand
            var mainHand = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            mainHand.SlotID = "MainHand";
            mainHand.DisplayName = "Main Hand";
            mainHand.SlotIndex = 0;
            mainHand.AttachmentBone = HumanBodyBones.RightHand;
            mainHand.AnimatorParamPrefix = "Slot0";
            mainHand.RenderMode = SlotRenderMode.AlwaysVisible;
            mainHand.Priority = 10;
            // Main hand uses keys 1-9 by default
            mainHand.UsesNumericKeys = true;
            SaveAsset(mainHand, SlotsPath + "/MainHand.asset");
            
            // OffHand
            var offHand = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            offHand.SlotID = "OffHand";
            offHand.DisplayName = "Off Hand";
            offHand.SlotIndex = 1;
            offHand.AttachmentBone = HumanBodyBones.LeftHand;
            offHand.AnimatorParamPrefix = "Slot1";
            offHand.RenderMode = SlotRenderMode.AlwaysVisible;
            offHand.Priority = 5;
            // Off hand uses Alt + 1-9 by default
            offHand.UsesNumericKeys = true;
            offHand.RequiredModifier = ModifierKey.Alt;
            // Add suppression rule: Hide when MainHand has TwoHanded weapon
            offHand.SuppressionRules.Add(new SuppressionRule
            {
                WatchSlotID = "MainHand",
                Condition = SuppressionCondition.HasTwoHanded,
                Action = SuppressionAction.Hide
            });
            SaveAsset(offHand, SlotsPath + "/OffHand.asset");
            
            Debug.Log("[EquipmentDefinitionCreator] Created default slot definitions.");
        }

        [MenuItem("DIG/Wizards/Utilities/Default Categories")]
        public static void CreateDefaultCategories()
        {
            EnsureDirectoryExists(CategoriesPath);
            
            // Melee (parent category)
            var melee = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            melee.CategoryID = "Melee";
            melee.DisplayName = "Melee Weapon";
            melee.DefaultMovementSetID = 1;
            melee.GripType = GripType.OneHanded;
            melee.UseStyle = UseStyle.ComboChain;
            melee.DefaultComboCount = 3;
            melee.DefaultUseDuration = 0.5f;
            melee.AnimatorSubstateMachine = "Melee";
            SaveAsset(melee, CategoriesPath + "/Melee.asset");
            
            // Sword (inherits from Melee)
            var sword = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            sword.CategoryID = "Sword";
            sword.DisplayName = "Sword";
            sword.ParentCategory = melee;
            sword.DefaultComboCount = 3;
            sword.AnimatorSubstateMachine = "Sword";
            SaveAsset(sword, CategoriesPath + "/Sword.asset");
            
            // Gun (parent category)
            var gun = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            gun.CategoryID = "Gun";
            gun.DisplayName = "Firearm";
            gun.DefaultMovementSetID = 0;
            gun.GripType = GripType.OneHanded;
            gun.UseStyle = UseStyle.SingleUse;
            gun.DefaultUseDuration = 0.1f;
            gun.AnimatorSubstateMachine = "Gun";
            SaveAsset(gun, CategoriesPath + "/Gun.asset");
            
            // Pistol (inherits from Gun)
            var pistol = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            pistol.CategoryID = "Pistol";
            pistol.DisplayName = "Pistol";
            pistol.ParentCategory = gun;
            pistol.GripType = GripType.OneHanded;
            pistol.CanDualWield = true;
            pistol.AnimatorSubstateMachine = "Pistol";
            SaveAsset(pistol, CategoriesPath + "/Pistol.asset");
            
            // Rifle (inherits from Gun, TwoHanded)
            var rifle = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            rifle.CategoryID = "Rifle";
            rifle.DisplayName = "Rifle";
            rifle.ParentCategory = gun;
            rifle.GripType = GripType.TwoHanded;
            rifle.UseStyle = UseStyle.Automatic;
            rifle.AnimatorSubstateMachine = "Rifle";
            SaveAsset(rifle, CategoriesPath + "/Rifle.asset");
            
            // Bow
            var bow = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            bow.CategoryID = "Bow";
            bow.DisplayName = "Bow";
            bow.DefaultMovementSetID = 2;
            bow.GripType = GripType.TwoHanded;
            bow.UseStyle = UseStyle.ChargeRelease;
            bow.DefaultUseDuration = 1.0f;
            bow.AnimatorSubstateMachine = "Bow";
            SaveAsset(bow, CategoriesPath + "/Bow.asset");
            
            // Grenade
            var grenade = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            grenade.CategoryID = "Grenade";
            grenade.DisplayName = "Grenade";
            grenade.DefaultMovementSetID = 0;
            grenade.GripType = GripType.OneHanded;
            grenade.UseStyle = UseStyle.SingleUse;
            grenade.AnimatorSubstateMachine = "Grenade"; 
            SaveAsset(grenade, CategoriesPath + "/Grenade.asset");

            // Katana (inherits from Sword)
            var katana = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            katana.CategoryID = "Katana";
            katana.DisplayName = "Katana";
            katana.ParentCategory = sword;
            katana.GripType = GripType.TwoHanded;
            katana.AnimatorSubstateMachine = "Sword";
            SaveAsset(katana, CategoriesPath + "/Katana.asset");
            
            // Shield
            var shield = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            shield.CategoryID = "Shield";
            shield.DisplayName = "Shield";
            shield.DefaultMovementSetID = 0;
            shield.GripType = GripType.OneHanded;
            shield.UseStyle = UseStyle.Toggle;
            shield.DefaultUseDuration = 0f;
            shield.AnimatorSubstateMachine = "Shield";
            SaveAsset(shield, CategoriesPath + "/Shield.asset");

            // Magic
            var magic = ScriptableObject.CreateInstance<WeaponCategoryDefinition>();
            magic.CategoryID = "Magic";
            magic.DisplayName = "Magic";
            magic.DefaultMovementSetID = 3; // 3 = Magic/Spell in standard Opsive
            magic.GripType = GripType.OneHanded;
            magic.UseStyle = UseStyle.ComboChain;
            magic.DefaultComboCount = 3;
            magic.DefaultUseDuration = 0.5f;
            magic.AnimatorSubstateMachine = "Magic";
            SaveAsset(magic, CategoriesPath + "/Magic.asset");
            
            Debug.Log("[EquipmentDefinitionCreator] Created default category definitions.");
        }

        [MenuItem("DIG/Wizards/Utilities/Default Input Profiles")]
        public static void CreateDefaultInputProfiles()
        {
            EnsureDirectoryExists(InputProfilesPath);
            
            // Standard Combat Profile
            var combat = ScriptableObject.CreateInstance<InputProfileDefinition>();
            combat.ProfileID = "StandardCombat";
            combat.DisplayName = "Standard Combat";
            combat.PrimaryActionName = "Fire";
            combat.SecondaryActionName = "AltFire";
            combat.ReloadActionName = "Reload";
            combat.ScrollBehavior = ScrollBehavior.CycleWeapon;
            combat.PrimaryActionStateIndex = 2; // Use
            combat.SecondaryActionStateIndex = 3; // Block
            combat.ReloadStateIndex = 7;
            SaveAsset(combat, InputProfilesPath + "/StandardCombat.asset");
            
            // Bow Profile (charge-release)
            var bowProfile = ScriptableObject.CreateInstance<InputProfileDefinition>();
            bowProfile.ProfileID = "BowProfile";
            bowProfile.DisplayName = "Bow";
            bowProfile.PrimaryActionName = "Fire";
            bowProfile.SecondaryActionName = "AltFire";
            bowProfile.ScrollBehavior = ScrollBehavior.Zoom;
            bowProfile.PrimaryActionStateIndex = 2;
            bowProfile.HoldBehaviors.Add(new HoldBehavior
            {
                InputActionName = "Fire",
                TapActionIndex = 0,
                HoldActionIndex = 2,
                HoldDuration = 0.2f
            });
            SaveAsset(bowProfile, InputProfilesPath + "/BowProfile.asset");
            
            // Shield Profile (toggle block)
            var shieldProfile = ScriptableObject.CreateInstance<InputProfileDefinition>();
            shieldProfile.ProfileID = "ShieldProfile";
            shieldProfile.DisplayName = "Shield";
            shieldProfile.PrimaryActionName = "AltFire"; // RMB for block
            shieldProfile.SecondaryActionName = "";
            shieldProfile.ScrollBehavior = ScrollBehavior.None;
            shieldProfile.PrimaryActionStateIndex = 3; // Block
            SaveAsset(shieldProfile, InputProfilesPath + "/ShieldProfile.asset");
            
            Debug.Log("[EquipmentDefinitionCreator] Created default input profile definitions.");
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace("\\", "/");
                var folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectoryExists(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void SaveAsset(ScriptableObject asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
            {
                Debug.Log($"[EquipmentDefinitionCreator] Asset already exists: {path}");
                return;
            }
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
