using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Modifies player movement settings when in EVA mode.
    /// Applied as multipliers to PlayerMovementSettings when IsInEVA is true.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct EVAMovementModifier : IComponentData
    {
        /// <summary>
        /// Movement speed multiplier in EVA (default: 0.6 = 60% of normal speed).
        /// Applied to walk, run, sprint speeds.
        /// </summary>
        public float SpeedMultiplier;

        /// <summary>
        /// Jump force multiplier in EVA (default: 0.5 = 50% of normal jump).
        /// Simulates reduced gravity/heavier suit.
        /// </summary>
        public float JumpForceMultiplier;

        /// <summary>
        /// Air control multiplier in EVA (default: 0.3 = 30% of normal).
        /// Less responsive movement in zero-g.
        /// </summary>
        public float AirControlMultiplier;

        /// <summary>
        /// Custom gravity override in EVA (-1 = use default gravity).
        /// Set to 0 for zero-g, or custom value for artificial gravity.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float GravityOverride;

        public static EVAMovementModifier Default => new EVAMovementModifier
        {
            SpeedMultiplier = 0.6f,
            JumpForceMultiplier = 0.5f,
            AirControlMultiplier = 0.3f,
            GravityOverride = -1f // Use default gravity
        };
    }
}
