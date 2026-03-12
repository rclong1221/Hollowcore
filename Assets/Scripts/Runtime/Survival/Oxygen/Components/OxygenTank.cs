using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Oxygen
{
    /// <summary>
    /// Oxygen tank component for entities that consume oxygen.
    /// Depletes when in EVA and OxygenRequired zone. Causes suffocation damage when empty.
    /// </summary>
    /// <remarks>
    /// This is a specialized component (not generic ConsumableResource) for performance.
    /// Oxygen is a hot-path system that runs every frame on all EVA entities.
    /// </remarks>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct OxygenTank : IComponentData
    {
        /// <summary>
        /// Current oxygen level (0 to Max). Depletes in EVA, refills at O2 stations.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum oxygen capacity. Default: 100.
        /// Can be upgraded via equipment.
        /// </summary>
        public float Max;

        /// <summary>
        /// Base depletion rate in units per second when in EVA.
        /// Actual rate = DepletionRatePerSecond * LeakMultiplier.
        /// Default: 1.0 (100 seconds of oxygen at full).
        /// </summary>
        public float DepletionRatePerSecond;

        /// <summary>
        /// Multiplier applied to depletion rate from suit damage/leaks.
        /// 1.0 = no leak, 2.0 = double drain, etc.
        /// Set by SuitIntegrity system.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float LeakMultiplier;

        /// <summary>
        /// Oxygen percentage (0-1) at which warning UI/audio triggers.
        /// Default: 0.25 (25%).
        /// </summary>
        public float WarningThreshold;

        /// <summary>
        /// Oxygen percentage (0-1) at which critical UI/audio triggers.
        /// Default: 0.10 (10%).
        /// </summary>
        public float CriticalThreshold;

        /// <summary>
        /// Damage per second applied when oxygen is depleted (suffocation).
        /// Default: 10 HP/sec.
        /// </summary>
        public float SuffocationDamagePerSecond;

        /// <summary>
        /// Returns current oxygen as a percentage (0-1).
        /// </summary>
        public readonly float Percent => Max > 0 ? Current / Max : 0;

        /// <summary>
        /// Returns true if oxygen is at or below warning threshold.
        /// </summary>
        public readonly bool IsWarning => Percent <= WarningThreshold;

        /// <summary>
        /// Returns true if oxygen is at or below critical threshold.
        /// </summary>
        public readonly bool IsCritical => Percent <= CriticalThreshold;

        /// <summary>
        /// Returns true if oxygen is completely depleted.
        /// </summary>
        public readonly bool IsDepleted => Current <= 0;

        /// <summary>
        /// Creates a default oxygen tank with standard values.
        /// </summary>
        public static OxygenTank Default => new()
        {
            Current = 100f,
            Max = 100f,
            DepletionRatePerSecond = 1f,
            LeakMultiplier = 1f,
            WarningThreshold = 0.25f,
            CriticalThreshold = 0.10f,
            SuffocationDamagePerSecond = 10f
        };
    }

    /// <summary>
    /// Client-side state for oxygen warning UI/audio.
    /// Tracks which warnings have already fired to prevent spam.
    /// </summary>
    public struct OxygenWarningState : IComponentData
    {
        /// <summary>
        /// True if the warning audio/UI has been triggered this EVA session.
        /// Resets when oxygen goes above warning threshold.
        /// </summary>
        public bool WarningTriggered;

        /// <summary>
        /// True if the critical audio/UI has been triggered this EVA session.
        /// Resets when oxygen goes above critical threshold.
        /// </summary>
        public bool CriticalTriggered;

        /// <summary>
        /// True if suffocation audio/effects are active.
        /// Active when oxygen is depleted and taking damage.
        /// </summary>
        public bool SuffocatingActive;
    }

    /// <summary>
    /// Tag component that enables oxygen consumption on this entity.
    /// Remove this tag to make an entity not require oxygen (e.g., robots, vehicles).
    /// </summary>
    public struct OxygenConsumer : IComponentData { }
}
