using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.7: Singleton component for editor-to-ECS live tuning communication.
    /// LiveTuningModule writes override values; LiveTuningApplySystem reads them.
    /// Only meaningful in UNITY_EDITOR builds — zero runtime cost in player builds.
    /// </summary>
    public struct LiveTuningOverrides : IComponentData
    {
        /// <summary>Override for ZoneDifficultyMultiplier. 0 = no override, >0 = use this value.</summary>
        public float DifficultyMultiplierOverride;

        /// <summary>Override for spawn rate. 0 = no override.</summary>
        public float SpawnRateOverride;

        /// <summary>If true, sets SpawnBudget to 0 each frame to pause spawning.</summary>
        public byte PauseSpawning; // byte for blittable (1 = true)

        /// <summary>One-shot: grant this much run currency, then reset to 0.</summary>
        public int GrantRunCurrency;

        /// <summary>One-shot: grant this much meta currency, then reset to 0.</summary>
        public int GrantMetaCurrency;

        /// <summary>One-shot: force transition to this phase. None = no override.</summary>
        public RunPhase ForcePhase;
    }
}
