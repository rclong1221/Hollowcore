using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Heal event buffer element (13.16.9).
    /// All healing sources enqueue HealEvents rather than directly modifying Health.
    /// Mirrors the DamageEvent pattern for consistency.
    /// </summary>
    [InternalBufferCapacity(4)]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct HealEvent : IBufferElementData
    {
        /// <summary>
        /// Amount of HP to restore.
        /// Negative or NaN values are rejected.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float Amount;

        /// <summary>
        /// Entity that caused this healing (optional, for attribution/UI).
        /// </summary>
        [GhostField]
        public Entity SourceEntity;

        /// <summary>
        /// World-space position of the heal source (for effects, optional).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 Position;

        /// <summary>
        /// Server tick when this heal event was created.
        /// </summary>
        [GhostField]
        public uint ServerTick;

        /// <summary>
        /// Type of heal for effects differentiation.
        /// </summary>
        [GhostField]
        public HealType Type;
    }

    /// <summary>
    /// Type of healing for effects and UI.
    /// </summary>
    public enum HealType : byte
    {
        Generic = 0,      // Default heal
        Medkit = 1,       // Medical item
        Regeneration = 2, // Passive health regen
        Ability = 3,      // Active ability
        Environmental = 4, // Heal zone
        Lifesteal = 5     // On-hit lifesteal modifier
    }
}
