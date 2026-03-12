using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Type of damage for differentiated effects and mitigation (Epic 4.2).
    /// </summary>
    public enum DamageType : byte
    {
        Physical = 0,    // Collisions, melee, fall damage
        Heat = 1,        // Fire, high temperature exposure
        Radiation = 2,   // Radiation zones
        Suffocation = 3, // Oxygen depletion
        Explosion = 4,   // Explosive blasts
        Toxic = 5,       // Poison, chemical hazards
        // Combat elemental types (EPIC 15.30)
        Ice = 6,         // Frost, cold damage
        Lightning = 7,   // Electrical, shock damage
        Holy = 8,        // Light, divine damage
        Shadow = 9,      // Dark, necrotic damage
        Arcane = 10      // Pure magical damage
    }

    /// <summary>
    /// Damage event buffer element. All damage sources enqueue DamageEvents rather than
    /// directly modifying Health.Current. Server-authoritative and consumed each tick.
    /// </summary>
    /// <remarks>
    /// Design goals (EPIC 4.1):
    /// - One pipeline: all harm becomes DamageEvent
    /// - Server is the only writer to Health.Current
    /// - Bounded buffers: InternalBufferCapacity limits memory usage
    /// - Ordering: damage events processed after hazard/tool systems, before death transition
    /// </remarks>
    [InternalBufferCapacity(8)]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DamageEvent : IBufferElementData
    {
        /// <summary>
        /// Amount of HP to subtract (post-mitigation optional, Epic 4.2).
        /// Negative or NaN values are clamped/rejected.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Amount;

        /// <summary>
        /// Type of damage for effects and mitigation.
        /// </summary>
        [GhostField]
        public DamageType Type;

        /// <summary>
        /// Entity that caused this damage (optional, for attribution/UI).
        /// </summary>
        [GhostField]
        public Entity SourceEntity;

        /// <summary>
        /// World-space position of the hit (for directional feedback, optional).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 HitPosition;

        /// <summary>
        /// World-space direction of the attack (for shield blocking, optional).
        /// Points FROM attacker TO victim. Zero if not applicable.
        /// EPIC 15.7: Shield Block System
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 Direction;

        /// <summary>
        /// Server tick when this damage event was created (for ordering/debugging).
        /// </summary>
        [GhostField]
        public uint ServerTick;
    }
}
