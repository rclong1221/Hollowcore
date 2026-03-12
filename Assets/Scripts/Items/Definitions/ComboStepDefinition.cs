using UnityEngine;
using System;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// Defines a single step in a melee combo chain.
    /// This data is baked into ECS Dynamic Buffers for the MeleeSystem to consume.
    /// </summary>
    [Serializable]
    public struct ComboStepDefinition
    {
        [Header("Animation")]
        [Tooltip("The Animation Clip to play for this step. (Baked to Hash/ID)")]
        public AnimationClip Animation;
        
        [Tooltip("The Substate Index in the Animator Condition (e.g. 0=Slash1, 1=Slash2)")]
        public int AnimatorSubStateIndex;

        [Header("Timing (Seconds)")]
        [Tooltip("Total duration of this attack state")]
        public float Duration;
        
        [Tooltip("When the input window opens for chaining the next attack")]
        public float InputWindowStart;
        
        [Tooltip("When the input window closes")]
        public float InputWindowEnd;

        [Header("Combat")]
        [Tooltip("Damage multiplier for this specific hit")]
        public float DamageMultiplier;
        
        [Tooltip("Force applied to target")]
        public float KnockbackForce;
    }
}
