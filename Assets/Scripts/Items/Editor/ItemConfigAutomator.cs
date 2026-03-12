using UnityEngine;
using UnityEditor;
using DIG.Items.Authoring;
using DIG.Weapons.Authoring;
using DIG.Items.Definitions;
using System.Collections.Generic;

namespace DIG.Items.Editor.Migration
{
    /// <summary>
    /// [MIGRATION] Tool - EPIC 14.5
    /// 
    /// Purpose: Updates ItemAnimationConfigAuthoring on weapon prefabs based on WeaponAuthoring data.
    /// Assigns WeaponCategoryDefinition assets based on AnimatorItemID.
    /// When to use: After modifying WeaponAuthoring components or adding new weapons.
    /// </summary>
    public class Migration_ItemConfigs : EditorWindow
    {
        [MenuItem("DIG/Migration/Update Item Animation Configs")]
        public static void UpdateConfigs()
        {
            // Find all prefabs with WeaponAuthoring
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int updatedCount = 0;
            int totalCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // Check for WeaponAuthoring
                WeaponAuthoring weaponAuth = prefab.GetComponent<WeaponAuthoring>();
                if (weaponAuth == null) continue;

                totalCount++;

                // Check/Add ItemAnimationConfigAuthoring
                ItemAnimationConfigAuthoring configAuth = prefab.GetComponent<ItemAnimationConfigAuthoring>();
                bool added = false;
                if (configAuth == null)
                {
                    configAuth = prefab.AddComponent<ItemAnimationConfigAuthoring>();
                    added = true;
                }

                bool changed = false;

                // Configure based on WeaponAuthoring
                int itemID = weaponAuth.AnimatorItemID;
                
                // Update AnimatorItemID if 0
                if (configAuth.AnimatorItemID == 0)
                {
                    configAuth.AnimatorItemID = itemID;
                    changed = true;
                }

                // Determine CategoryID and MovementSetID based on legacy ranges
                string derivedCategoryID = DetermineCategoryID(itemID);
                int derivedMovementSet = DetermineMovementSetID(itemID);

                // Find and assign Category asset if not already set
                if (configAuth.Category == null)
                {
                    var category = FindCategoryAsset(derivedCategoryID);
                    if (category != null)
                    {
                        configAuth.Category = category;
                        changed = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[ItemConfigAutomator] Could not find WeaponCategoryDefinition '{derivedCategoryID}' for '{prefab.name}'. Create it first.");
                    }
                }

                if (configAuth.MovementSetID != derivedMovementSet)
                {
                    configAuth.MovementSetID = derivedMovementSet;
                    changed = true;
                }
                
                // Copy ComboCount/Durations from WeaponAuthoring if applicable
                if (weaponAuth.Type == WeaponType.Melee)
                {
                    if (configAuth.ComboCount == 0 && weaponAuth.ComboCount > 0)
                    {
                        configAuth.ComboCount = weaponAuth.ComboCount;
                        changed = true;
                    }
                    if (configAuth.UseDuration == 0 && weaponAuth.ComboWindow > 0)
                    {
                        configAuth.UseDuration = weaponAuth.ComboWindow;
                        changed = true;
                    }
                }
                else if (weaponAuth.Type == WeaponType.Bow)
                {
                     if (configAuth.UseDuration == 0 && weaponAuth.BowDrawTime > 0)
                     {
                         configAuth.UseDuration = weaponAuth.BowDrawTime;
                         changed = true;
                     }
                }

                // Set IsTwoHanded based on weapon type
                bool shouldBeTwoHanded = DetermineIsTwoHanded(itemID, weaponAuth.Type);
                if (configAuth.IsTwoHanded != shouldBeTwoHanded)
                {
                    configAuth.IsTwoHanded = shouldBeTwoHanded;
                    changed = true;
                }

                if (added || changed)
                {
                    EditorUtility.SetDirty(prefab);
                    updatedCount++;
                    Debug.Log($"[ItemConfigAutomator] Updated '{prefab.name}': ID={itemID} Category={derivedCategoryID} Set={derivedMovementSet} TwoHanded={shouldBeTwoHanded}" + (added ? " [ADDED COMPONENT]" : ""));
                }
            }

            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[ItemConfigAutomator] Complete. Updated {updatedCount}/{totalCount} weapon prefabs.");
            }
            else
            {
                Debug.Log($"[ItemConfigAutomator] Complete. No updates needed for {totalCount} weapon prefabs.");
            }
        }

        private static WeaponCategoryDefinition FindCategoryAsset(string categoryID)
        {
            string[] guids = AssetDatabase.FindAssets($"t:WeaponCategoryDefinition {categoryID}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<WeaponCategoryDefinition>(path);
                if (asset != null && asset.CategoryID == categoryID)
                    return asset;
            }
            return null;
        }

        private static string DetermineCategoryID(int animatorItemID)
        {
            if (animatorItemID >= 61 && animatorItemID <= 65) return "Magic";
            if (animatorItemID == 26) return "Shield";
            if (animatorItemID >= 23 && animatorItemID <= 25) return "Melee";
            if (animatorItemID == 4) return "Bow";
            return "Gun";
        }

        private static int DetermineMovementSetID(int animatorItemID)
        {
            if (animatorItemID >= 61 && animatorItemID <= 65) return 0; // Magic
            if (animatorItemID >= 23 && animatorItemID <= 25) return 1; // Melee
            if (animatorItemID == 4) return 2; // Bow
            return 0; // Gun
        }

        /// <summary>
        /// Determines if weapon requires both hands based on WeaponType.
        /// </summary>
        private static bool DetermineIsTwoHanded(int animatorItemID, WeaponType weaponType)
        {
            if (weaponType == WeaponType.Shootable)
            {
                return animatorItemID >= 3;
            }
            if (weaponType == WeaponType.Bow) return true;
            if (animatorItemID >= 61 && animatorItemID <= 65) return false;
            if (animatorItemID == 29) return true; // Greatsword
            if (weaponType == WeaponType.Melee) return false;
            return false;
        }
    }
}
