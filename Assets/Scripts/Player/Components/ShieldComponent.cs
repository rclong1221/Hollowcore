using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Shield component for damage absorption (13.16.3).
    /// Shield absorbs damage before health and regenerates after a delay.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ShieldComponent : IComponentData
    {
        /// <summary>
        /// Current shield value.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum shield capacity.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Max;

        /// <summary>
        /// Shield regeneration rate (per second).
        /// </summary>
        public float RegenRate;

        /// <summary>
        /// Delay before regeneration starts after taking damage (seconds).
        /// </summary>
        public float RegenDelay;

        /// <summary>
        /// Time when shield was last damaged.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float LastDamageTime;

        /// <summary>
        /// Normalized shield (0-1) for UI display.
        /// </summary>
        public float Normalized => Max > 0 ? Current / Max : 0f;

        /// <summary>
        /// Check if shield is depleted.
        /// </summary>
        public bool IsDepleted => Current <= 0f;

        /// <summary>
        /// Check if shield is full.
        /// </summary>
        public bool IsFull => Current >= Max;

        /// <summary>
        /// Default values for player spawn.
        /// </summary>
        public static ShieldComponent Default => new ShieldComponent
        {
            Current = 50f,
            Max = 50f,
            RegenRate = 10f,    // 10 shield per second
            RegenDelay = 3f,    // 3 seconds after damage
            LastDamageTime = 0f
        };

        /// <summary>
        /// No shield configuration.
        /// </summary>
        public static ShieldComponent None => new ShieldComponent
        {
            Current = 0f,
            Max = 0f,
            RegenRate = 0f,
            RegenDelay = 0f,
            LastDamageTime = 0f
        };
    }
}
