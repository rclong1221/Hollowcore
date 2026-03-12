using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// Modifier key options for input bindings.
    /// </summary>
    public enum ModifierKey
    {
        None = 0,
        Alt = 1,
        Shift = 2,
        Ctrl = 3
    }

    /// <summary>
    /// Defines a single equipment slot in the equipment system.
    /// The system supports any number of slots, each configured via this ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSlotDefinition", menuName = "DIG/Equipment/Slot Definition", order = 0)]
    public class EquipmentSlotDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this slot (e.g., 'MainHand', 'OffHand', 'Helmet')")]
        public string SlotID;
        
        [Tooltip("Display name shown in UI")]
        public string DisplayName;
        
        [Tooltip("Numeric index for legacy compatibility and array indexing")]
        public int SlotIndex;
        
        [Header("Attachment")]
        [Tooltip("Which bone the item attaches to")]
        public HumanBodyBones AttachmentBone = HumanBodyBones.RightHand;
        
        [Tooltip("Custom bone path if HumanBodyBones is insufficient (e.g., 'Spine/Chest/WeaponHolster')")]
        public string FallbackAttachPath;
        
        [Header("Input Binding")]
        [Tooltip("Primary keybind description (e.g., '1-9' for main hand)")]
        public string PrimaryBinding = "1-9";
        
        [Tooltip("Modifier key required to equip to this slot (None, Alt, Shift, Ctrl)")]
        public ModifierKey RequiredModifier = ModifierKey.None;
        
        [Tooltip("If true, this slot consumes numeric keys 1-9 to select items. Overrides PrimaryBinding.")]
        public bool UsesNumericKeys = false;
        
        [Header("Compatibility")]
        [Tooltip("Which weapon categories can be equipped in this slot")]
        public List<WeaponCategoryDefinition> AllowedCategories = new List<WeaponCategoryDefinition>();
        
        [Header("Animator")]
        [Tooltip("Animator parameter prefix for this slot (e.g., 'Slot0', 'Slot1')")]
        public string AnimatorParamPrefix = "Slot0";
        
        [Header("Rendering")]
        [Tooltip("How items in this slot are rendered")]
        public SlotRenderMode RenderMode = SlotRenderMode.AlwaysVisible;
        
        [Header("Suppression Rules")]
        [Tooltip("Rules for hiding/disabling this slot based on other slot states")]
        public List<SuppressionRule> SuppressionRules = new List<SuppressionRule>();
        
        [Tooltip("Priority for conflict resolution (higher wins)")]
        public int Priority = 0;
        
        /// <summary>
        /// Gets the full animator parameter name for ItemID (e.g., "Slot0ItemID").
        /// </summary>
        public string AnimatorItemIDParam => $"{AnimatorParamPrefix}ItemID";
        
        /// <summary>
        /// Gets the full animator parameter name for StateIndex (e.g., "Slot0ItemStateIndex").
        /// </summary>
        public string AnimatorStateIndexParam => $"{AnimatorParamPrefix}ItemStateIndex";
        
        /// <summary>
        /// Gets the full animator trigger name for state changes (e.g., "Slot0ItemStateIndexChange").
        /// </summary>
        public string AnimatorStateChangeTrigger => $"{AnimatorParamPrefix}ItemStateIndexChange";
        
        /// <summary>
        /// Checks if a weapon category is allowed in this slot.
        /// </summary>
        public bool AllowsCategory(WeaponCategoryDefinition category)
        {
            if (AllowedCategories == null || AllowedCategories.Count == 0)
                return true; // No restrictions = allow all
            
            // Check direct match or parent category match
            var current = category;
            while (current != null)
            {
                if (AllowedCategories.Contains(current))
                    return true;
                current = current.ParentCategory;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if the required modifier key is currently held.
        /// </summary>
        public bool IsModifierHeld()
        {
            // EPIC 15.21: Use PlayerInputState Modifiers
            switch (RequiredModifier)
            {
                case ModifierKey.None:
                    return true;
                case ModifierKey.Alt:
                    return global::Player.Systems.PlayerInputState.ModAlt;
                case ModifierKey.Shift:
                    return global::Player.Systems.PlayerInputState.ModShift;
                case ModifierKey.Ctrl:
                    return global::Player.Systems.PlayerInputState.ModCtrl;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks if the input binding was pressed this frame.
        /// Supports single numeric keys "1"-"9" and range notation "1-9".
        /// When using range notation, also sets LastPressedKey to the digit that was pressed.
        /// </summary>
        public bool IsBindingPressed()
        {
            if (string.IsNullOrEmpty(PrimaryBinding)) return false;

            LastPressedKey = 0; // Reset

            // EPIC 15.21: Handle range notation like "1-9" using PlayerInputState
            if (PrimaryBinding == "1-9")
            {
                if (global::Player.Systems.PlayerInputState.EquipSlot1) { LastPressedKey = 1; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot2) { LastPressedKey = 2; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot3) { LastPressedKey = 3; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot4) { LastPressedKey = 4; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot5) { LastPressedKey = 5; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot6) { LastPressedKey = 6; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot7) { LastPressedKey = 7; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot8) { LastPressedKey = 8; return true; }
                if (global::Player.Systems.PlayerInputState.EquipSlot9) { LastPressedKey = 9; return true; }
                return false;
            }

            // Fast path for single numeric digit (only supporting digits 1-9 for now as per plan)
            if (PrimaryBinding.Length == 1 && char.IsDigit(PrimaryBinding[0]))
            {
                int digit = int.Parse(PrimaryBinding);
                if (digit >= 1 && digit <= 9)
                {
                     // EPIC 15.21: Check corresponding PlayerInputState field
                     bool pressed = false;
                     switch (digit) {
                         case 1: pressed = global::Player.Systems.PlayerInputState.EquipSlot1; break;
                         case 2: pressed = global::Player.Systems.PlayerInputState.EquipSlot2; break;
                         case 3: pressed = global::Player.Systems.PlayerInputState.EquipSlot3; break;
                         case 4: pressed = global::Player.Systems.PlayerInputState.EquipSlot4; break;
                         case 5: pressed = global::Player.Systems.PlayerInputState.EquipSlot5; break;
                         case 6: pressed = global::Player.Systems.PlayerInputState.EquipSlot6; break;
                         case 7: pressed = global::Player.Systems.PlayerInputState.EquipSlot7; break;
                         case 8: pressed = global::Player.Systems.PlayerInputState.EquipSlot8; break;
                         case 9: pressed = global::Player.Systems.PlayerInputState.EquipSlot9; break;
                     }
                     
                     if (pressed)
                     {
                         LastPressedKey = digit;
                         return true;
                     }
                }
            }
            return false;
        }

        /// <summary>
        /// The last numeric key that was pressed (1-9) when IsBindingPressed() returned true.
        /// Used to determine which quick slot to equip when using range notation like "1-9".
        /// </summary>
        [System.NonSerialized]
        public int LastPressedKey;
    }
}
