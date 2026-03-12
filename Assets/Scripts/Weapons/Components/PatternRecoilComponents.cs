using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.5: Component linking a weapon to its recoil pattern asset.
    /// Stored on weapon entities to reference pattern data via index.
    /// </summary>
    public struct PatternRecoil : IComponentData
    {
        /// <summary>
        /// Index into the RecoilPatternRegistry.
        /// </summary>
        public int PatternIndex;

        /// <summary>
        /// Override multiplier for pattern strength.
        /// </summary>
        public float PatternStrengthMultiplier;
    }

    /// <summary>
    /// EPIC 15.5: Runtime state for pattern-based recoil.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PatternRecoilState : IComponentData
    {
        /// <summary>
        /// Current shot index in the pattern (0 = first shot).
        /// </summary>
        [GhostField]
        public int CurrentShotIndex;

        /// <summary>
        /// Time since last shot fired.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceLastShot;

        /// <summary>
        /// Accumulated aim offset from pattern (degrees).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float2 AccumulatedOffset;

        /// <summary>
        /// Target aim offset (smoothly interpolated).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float2 TargetOffset;

        /// <summary>
        /// Visual kick offset for FEEL (purely cosmetic).
        /// </summary>
        public float2 VisualKick;

        /// <summary>
        /// Whether currently in recovery mode.
        /// </summary>
        [GhostField]
        public bool IsRecovering;

        /// <summary>
        /// Random seed for this burst (for deterministic randomness).
        /// </summary>
        public float RandomSeed;

        /// <summary>
        /// Reset to initial state (new burst).
        /// </summary>
        public void Reset()
        {
            CurrentShotIndex = 0;
            TimeSinceLastShot = 0f;
            AccumulatedOffset = float2.zero;
            TargetOffset = float2.zero;
            VisualKick = float2.zero;
            IsRecovering = false;
        }
    }
}
