using System;
using UnityEngine;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// Defines a rule for suppressing (hiding/disabling) one slot based on another slot's state.
    /// Used for cases like "hide off-hand when main-hand has two-handed weapon".
    /// </summary>
    [Serializable]
    public struct SuppressionRule
    {
        [Tooltip("Which slot to monitor for the condition")]
        public string WatchSlotID;
        
        [Tooltip("What condition triggers suppression")]
        public SuppressionCondition Condition;
        
        [Tooltip("Additional value for the condition (category name, grip type, etc.)")]
        public string ConditionValue;
        
        [Tooltip("What action to take when condition is met")]
        public SuppressionAction Action;
        
        [Tooltip("For Override action: which animation state to use")]
        public int OverrideStateIndex;
    }

    /// <summary>
    /// Defines behavior for hold vs tap input.
    /// </summary>
    [Serializable]
    public struct HoldBehavior
    {
        [Tooltip("Which input action this applies to")]
        public string InputActionName;
        
        [Tooltip("Action index triggered on quick tap")]
        public int TapActionIndex;
        
        [Tooltip("Action index triggered on hold")]
        public int HoldActionIndex;
        
        [Tooltip("How long (seconds) counts as 'hold' vs 'tap'")]
        public float HoldDuration;
    }
}
