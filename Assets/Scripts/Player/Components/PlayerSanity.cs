using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks player sanity/mental state for horror mechanics.
    /// Low sanity causes visual distortions, hallucinations, etc.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerSanity : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Max;
        public float DarknessDrainRate;  // Drain when in darkness
        public float HorrorDrainRate;    // Drain when near horror entities
        public float RecoveryRate;       // When in safe areas/light
        public float RecoveryDelay;
        public float LastDrainTime;
        public float DistortionThreshold;    // Sanity % to begin distortions (0-1)
        public float HallucinationThreshold; // Sanity % to begin hallucinations (0-1)
        [GhostField] public float DistortionIntensity;  // Current effect intensity (0-1)
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public bool IsInsane => Current <= 0;
        public bool IsLow => Percent <= HallucinationThreshold;
        public bool IsUnstable => Percent <= DistortionThreshold;
        
        /// <summary>Calculated intensity for visual effects (0-1, higher = more distortion)</summary>
        public float CalculatedDistortionIntensity => Percent < DistortionThreshold 
            ? 1f - (Percent / DistortionThreshold) 
            : 0f;
        
        public static PlayerSanity Default => new()
        {
            Current = 100f,
            Max = 100f,
            DarknessDrainRate = 0.5f,
            HorrorDrainRate = 2f,
            RecoveryRate = 1f,
            RecoveryDelay = 2f,
            LastDrainTime = 0f,
            DistortionThreshold = 0.5f,
            HallucinationThreshold = 0.25f,
            DistortionIntensity = 0f
        };
    }
}
