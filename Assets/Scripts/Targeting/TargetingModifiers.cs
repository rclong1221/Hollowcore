using Unity.Entities;

namespace DIG.Targeting
{
    /// <summary>
    /// Runtime modifiers for targeting stats.
    /// Applied by skills, items, buffs to adjust targeting behavior.
    /// Targeting system combines these with base TargetingConfig values.
    /// </summary>
    public struct TargetingModifiers : IComponentData
    {
        /// <summary>
        /// Added to MaxTargetRange from config.
        /// </summary>
        public float RangeModifier;
        
        /// <summary>
        /// Added to AimAssistStrength from config.
        /// </summary>
        public float AimAssistModifier;
        
        /// <summary>
        /// If true, skip line-of-sight checks (e.g., wall-hack skill).
        /// </summary>
        public bool IgnoreLineOfSight;
        
        /// <summary>
        /// Override target priority. -1 means use config default.
        /// Cast to TargetPriority when >= 0.
        /// </summary>
        public sbyte PriorityOverride;
        
        /// <summary>
        /// Create default modifiers (no changes).
        /// </summary>
        public static TargetingModifiers Default => new TargetingModifiers
        {
            RangeModifier = 0f,
            AimAssistModifier = 0f,
            IgnoreLineOfSight = false,
            PriorityOverride = -1
        };
    }
}
