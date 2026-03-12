using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    public enum StatusEffectType : byte
    {
        None = 0,
        // Survival (environmental hazards)
        Hypoxia = 1,
        RadiationPoisoning = 2,
        Burn = 3,
        Frostbite = 4,
        Bleed = 5,
        Concussion = 6,
        // Combat (EPIC 15.29 — from weapon modifiers)
        Shock = 7,          // Lightning DOT/stun
        PoisonDOT = 8,      // Combat poison (distinct from survival Toxic)
        Stun = 9,           // Brief full stun (no actions)
        Slow = 10,          // Movement speed reduction
        Weaken = 11,        // Defense reduction
    }

    /// <summary>
    /// Durable status effect state on the player.
    /// </summary>
    [InternalBufferCapacity(8)]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct StatusEffect : IBufferElementData
    {
        [GhostField]
        public StatusEffectType Type;

        /// <summary>
        /// Severity (0.0 to 1.0).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Severity;

        /// <summary>
        /// Remaining duration in seconds.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeRemaining;

        /// <summary>
        /// Timer for damage ticks.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TickTimer;
    }

    /// <summary>
    /// Request to add/refresh a status effect.
    /// Consumed by StatusEffectSystem to enforce stacking rules.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatusEffectRequest : IBufferElementData
    {
        public StatusEffectType Type;
        public float Severity;
        public float Duration;
        public bool Additive; // If true, adds severity. If false, maxs severity.
    }
}
