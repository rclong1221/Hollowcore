using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Player health component. Server-authoritative with replication to clients.
    /// </summary>
    /// <remarks>
    /// Design goals (EPIC 4.1):
    /// - Server is the only writer: DamageApplySystem applies damage
    /// - Replicated: clients see health for UI/effects
    /// - Never set Health.Current directly - use DamageEvent buffer instead
    /// </remarks>
    [GhostComponent(PrefabType = GhostPrefabType.All)] // Replicate for ALL ghost types (including interpolated enemies)
    public struct Health : IComponentData
    {
        /// <summary>
        /// Current HP. Clamped to [0, Max] by DamageApplySystem.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Current;

        /// <summary>
        /// Maximum HP. Replicated so clients can calculate normalized health for UI.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Max;

        /// <summary>
        /// Normalized health (0-1) for UI display.
        /// </summary>
        public float Normalized => Max > 0 ? Current / Max : 0f;

        /// <summary>
        /// Check if health is depleted.
        /// </summary>
        public bool IsDepleted => Current <= 0f;

        /// <summary>
        /// Default values for player spawn.
        /// </summary>
        public static Health Default => new Health
        {
            Current = 100f,
            Max = 100f
        };
    }
}
