using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Jetpack state for EVA movement. Provides vertical thrust capability
    /// with fuel management, consumption, and regeneration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct JetpackState : IComponentData
    {
        /// <summary>
        /// Current fuel amount (0 to MaxFuel).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Fuel;

        /// <summary>
        /// Maximum fuel capacity (default: 100).
        /// </summary>
        public float MaxFuel;

        /// <summary>
        /// True while thrust input is held and fuel > 0.
        /// Used for VFX/audio triggers and regen delay.
        /// </summary>
        [GhostField]
        public bool IsThrusting;

        /// <summary>
        /// Vertical thrust acceleration (default: 8 m/s²).
        /// Applied to velocity when thrusting.
        /// </summary>
        public float ThrustForce;

        /// <summary>
        /// Fuel consumption rate per second while thrusting (default: 10).
        /// At default rate, 100 fuel = 10 seconds of thrust.
        /// </summary>
        public float FuelConsumptionRate;

        /// <summary>
        /// Fuel regeneration rate per second when not thrusting (default: 2).
        /// At default rate, 50 seconds to fully recharge.
        /// </summary>
        public float FuelRegenRate;

        /// <summary>
        /// Delay in seconds after thrust stops before regen begins (default: 1.0).
        /// Creates a tactical cooldown period.
        /// </summary>
        public float RegenDelay;

        /// <summary>
        /// Tracks time since last thrust ended for regen delay calculation.
        /// Not replicated - calculated locally.
        /// </summary>
        public float TimeSinceThrust;

        /// <summary>
        /// Fuel percentage (0-1).
        /// </summary>
        public readonly float FuelPercent => MaxFuel > 0 ? Fuel / MaxFuel : 0f;

        /// <summary>
        /// True if fuel is depleted.
        /// </summary>
        public readonly bool IsDepleted => Fuel <= 0f;

        /// <summary>
        /// True if fuel is at maximum capacity.
        /// </summary>
        public readonly bool IsFull => Fuel >= MaxFuel;

        public static JetpackState Default => new JetpackState
        {
            Fuel = 100f,
            MaxFuel = 100f,
            IsThrusting = false,
            ThrustForce = 8f,
            FuelConsumptionRate = 10f,
            FuelRegenRate = 2f,
            RegenDelay = 1.0f,
            TimeSinceThrust = 0f
        };
    }
}
