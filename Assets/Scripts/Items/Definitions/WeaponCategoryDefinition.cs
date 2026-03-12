using System;
using System.Collections.Generic;
using UnityEngine;
using DIG.Combat.Definitions;
using DIG.Combat.Resolvers;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// Defines a weapon category (e.g., Sword, Rifle, Shield).
    /// Replaces the hardcoded AnimationWeaponType enum with data-driven configuration.
    /// Supports inheritance for shared behavior (e.g., Katana -> Sword -> Melee).
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponCategory", menuName = "DIG/Equipment/Weapon Category", order = 1)]
    public class WeaponCategoryDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this category (e.g., 'Sword', 'AssaultRifle')")]
        public string CategoryID;
        
        [Tooltip("Display name shown in UI")]
        public string DisplayName;
        
        [Tooltip("Parent category for inheritance (e.g., Katana's parent is Sword)")]
        public WeaponCategoryDefinition ParentCategory;
        
        [Header("Animation")]
        [Tooltip("Animator movement set ID for this category")]
        public int DefaultMovementSetID = 0;
        
        [Tooltip("Name of the Animator sub-state machine for this category")]
        public string AnimatorSubstateMachine;
        
        [Header("Grip & Handling")]
        [Tooltip("How this weapon is gripped")]
        public GripType GripType = GripType.OneHanded;
        
        [Tooltip("Can this weapon be held in both hands simultaneously (dual-wield)?")]
        public bool CanDualWield = false;
        
        [Tooltip("What off-hand categories are allowed with this weapon")]
        public List<WeaponCategoryDefinition> AllowedOffHandCategories = new List<WeaponCategoryDefinition>();
        
        [Header("Use Behavior")]
        [Tooltip("How the primary action is used")]
        public UseStyle UseStyle = UseStyle.SingleUse;
        
        [Tooltip("Number of attacks in combo chain (for ComboChain UseStyle)")]
        public int DefaultComboCount = 1;
        
        [Tooltip("Duration of use action in seconds")]
        public float DefaultUseDuration = 0.5f;
        
        [Tooltip("Freeze character movement during use?")]
        public bool LockMovementOnUse = false;
        
        [Header("Equip/Unequip")]
        [Tooltip("Duration of equip animation in seconds")]
        public float DefaultEquipDuration = 0.3f;
        
        [Tooltip("Duration of unequip animation in seconds")]
        public float DefaultUnequipDuration = 0.3f;
        
        [Header("Input")]
        [Tooltip("Input profile for this category (if different from slot default)")]
        public InputProfileDefinition InputProfile;
        
        [Header("Combat Resolution")]
        [Tooltip("How this weapon category resolves combat (physics, stat-based, hybrid)")]
        public CombatResolverType ResolverType = CombatResolverType.PhysicsHitbox;
        
        [Tooltip("Optional damage formula override (null = use resolver default)")]
        public DamageFormula DamageFormula;
        
        [Tooltip("Whether this weapon can critically hit")]
        public bool CanCrit = true;
        
        [Tooltip("Base damage range (min-max) for this category")]
        public Vector2 BaseDamageRange = new Vector2(10f, 15f);
        
        [Header("Custom Data")]
        [Tooltip("Extensible key-value pairs for custom properties")]
        public List<CustomDataEntry> CustomData = new List<CustomDataEntry>();
        
        /// <summary>
        /// Checks if this category inherits from the specified category.
        /// </summary>
        public bool InheritsFrom(WeaponCategoryDefinition ancestor)
        {
            var current = ParentCategory;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.ParentCategory;
            }
            return false;
        }
        
        /// <summary>
        /// Gets a value with inheritance fallback.
        /// </summary>
        public int GetMovementSetID()
        {
            if (DefaultMovementSetID != 0)
                return DefaultMovementSetID;
            return ParentCategory != null ? ParentCategory.GetMovementSetID() : 0;
        }
    }
    
    /// <summary>
    /// Key-value pair for custom data storage.
    /// </summary>
    [Serializable]
    public struct CustomDataEntry
    {
        public string Key;
        public string Value;
    }
}
