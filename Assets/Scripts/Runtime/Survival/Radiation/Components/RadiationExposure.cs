using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Radiation
{
    /// <summary>
    /// Tracks radiation exposure for an entity.
    /// Accumulates in radioactive zones, decays naturally over time.
    /// Causes damage when above threshold.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RadiationExposure : IComponentData
    {
        /// <summary>
        /// Current radiation level (0+). Can exceed DamageThreshold.
        /// Higher values = more damage.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum "safe" radiation level. Below this, no damage is taken.
        /// Radiation can exceed this value.
        /// Default: 100.
        /// </summary>
        public float DamageThreshold;

        /// <summary>
        /// Natural decay rate per second when NOT in a radioactive zone.
        /// Default: 5.0 (takes 20 seconds to decay from 100 to 0).
        /// </summary>
        public float DecayRatePerSecond;

        /// <summary>
        /// Current accumulation rate from the zone the entity is in.
        /// Set by RadiationAccumulationSystem based on CurrentEnvironmentZone.
        /// 0 when not in a radioactive zone.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentAccumulationRate;

        /// <summary>
        /// Damage per second applied when Current > DamageThreshold.
        /// Damage scales linearly: DamagePerSecond * (Current - DamageThreshold) / 100.
        /// Default: 5 HP/sec at 100 over threshold.
        /// </summary>
        public float DamagePerSecond;

        /// <summary>
        /// Warning threshold (0-1 of DamageThreshold) to trigger warning UI.
        /// Default: 0.5 (warn at 50% of damage threshold).
        /// </summary>
        public float WarningThreshold;

        /// <summary>
        /// Returns true if currently accumulating radiation.
        /// </summary>
        public readonly bool IsAccumulating => CurrentAccumulationRate > 0;

        /// <summary>
        /// Returns true if radiation is causing damage.
        /// </summary>
        public readonly bool IsTakingDamage => Current > DamageThreshold;

        /// <summary>
        /// Returns current radiation as a percentage of damage threshold (can exceed 1.0).
        /// </summary>
        public readonly float PercentOfThreshold => DamageThreshold > 0 ? Current / DamageThreshold : 0;

        /// <summary>
        /// Creates default radiation exposure settings.
        /// </summary>
        public static RadiationExposure Default => new()
        {
            Current = 0f,
            DamageThreshold = 100f,
            DecayRatePerSecond = 5f,
            CurrentAccumulationRate = 0f,
            DamagePerSecond = 5f,
            WarningThreshold = 0.5f
        };
    }

    /// <summary>
    /// Client-side state for radiation warning UI/audio.
    /// </summary>
    public struct RadiationWarningState : IComponentData
    {
        /// <summary>
        /// True if warning (50% of threshold) audio/UI has fired.
        /// </summary>
        public bool WarningTriggered;

        /// <summary>
        /// True if critical (at threshold) audio/UI has fired.
        /// </summary>
        public bool CriticalTriggered;

        /// <summary>
        /// True if Geiger counter click audio should be playing.
        /// Rate of clicks increases with radiation level.
        /// </summary>
        public bool GeigerActive;
    }

    /// <summary>
    /// Tag component indicating this entity is susceptible to radiation.
    /// Remove to make an entity immune (e.g., robots, vehicles).
    /// </summary>
    public struct RadiationSusceptible : IComponentData { }
}
