using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Items.Definitions
{
    /// <summary>
    /// Scroll wheel behavior options.
    /// </summary>
    public enum ScrollBehavior
    {
        /// <summary>
        /// Scroll cycles through weapon substates (fire modes, etc.)
        /// </summary>
        CycleSubstate = 0,
        
        /// <summary>
        /// Scroll cycles through equipped weapons.
        /// </summary>
        CycleWeapon = 1,
        
        /// <summary>
        /// Scroll controls zoom level.
        /// </summary>
        Zoom = 2,
        
        /// <summary>
        /// Scroll does nothing.
        /// </summary>
        None = 3
    }

    /// <summary>
    /// Defines input bindings for a weapon category or equipment slot.
    /// Enables per-category input configuration without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewInputProfile", menuName = "DIG/Equipment/Input Profile", order = 2)]
    public class InputProfileDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this profile")]
        public string ProfileID;
        
        [Tooltip("Display name for debugging/UI")]
        public string DisplayName;
        
        [Header("Primary Actions")]
        [Tooltip("Primary action input (LMB, trigger, etc.)")]
        public string PrimaryActionName = "Fire";
        
        [Tooltip("Secondary/alternate action input (RMB, aim, etc.)")]
        public string SecondaryActionName = "AltFire";
        
        [Tooltip("Reload/reset action")]
        public string ReloadActionName = "Reload";
        
        [Tooltip("Special ability action")]
        public string SpecialActionName = "Special";
        
        [Header("Modifiers")]
        [Tooltip("Modifier key for combos/alternate actions")]
        public string ModifierKeyName = "Modifier";
        
        [Tooltip("Cancel current action")]
        public string CancelActionName = "Cancel";
        
        [Header("Scroll Behavior")]
        [Tooltip("What scroll wheel does for this profile")]
        public ScrollBehavior ScrollBehavior = ScrollBehavior.CycleWeapon;
        
        [Header("Hold Behaviors")]
        [Tooltip("Define tap vs hold behaviors for specific inputs")]
        public List<HoldBehavior> HoldBehaviors = new List<HoldBehavior>();
        
        [Header("Animation State Mapping")]
        [Tooltip("Animator state index for primary action")]
        public int PrimaryActionStateIndex = 2; // Use/Fire
        
        [Tooltip("Animator state index for secondary action")]
        public int SecondaryActionStateIndex = 3; // Block/Aim
        
        [Tooltip("Animator state index for reload")]
        public int ReloadStateIndex = 7;
        
        /// <summary>
        /// Gets the animator state index for a given action type.
        /// </summary>
        public int GetStateIndexForAction(string actionName)
        {
            if (actionName == PrimaryActionName) return PrimaryActionStateIndex;
            if (actionName == SecondaryActionName) return SecondaryActionStateIndex;
            if (actionName == ReloadActionName) return ReloadStateIndex;
            return 0; // Idle
        }
        
        /// <summary>
        /// Checks if an input should be treated as a hold.
        /// </summary>
        public bool TryGetHoldBehavior(string actionName, out HoldBehavior behavior)
        {
            foreach (var hb in HoldBehaviors)
            {
                if (hb.InputActionName == actionName)
                {
                    behavior = hb;
                    return true;
                }
            }
            behavior = default;
            return false;
        }
    }
}
