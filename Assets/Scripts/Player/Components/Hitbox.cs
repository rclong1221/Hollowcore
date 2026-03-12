using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Hitbox component for damage multipliers (13.16.1).
    /// Attach to child collider GameObjects to define damage regions.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct Hitbox : IComponentData
    {
        /// <summary>
        /// The parent entity that owns this hitbox (receives damage).
        /// </summary>
        public Entity OwnerEntity;

        /// <summary>
        /// Damage multiplier for this hitbox region.
        /// 2.0 = headshot (2x damage)
        /// 1.0 = torso (normal damage)
        /// 0.5 = legs (half damage)
        /// </summary>
        [GhostField(Quantization = 100)]
        public float DamageMultiplier;

        /// <summary>
        /// Hitbox region type for effects and feedback.
        /// </summary>
        [GhostField]
        public HitboxRegion Region;

        public static Hitbox Default => new Hitbox
        {
            OwnerEntity = Entity.Null,
            DamageMultiplier = 1.0f,
            Region = HitboxRegion.Torso
        };
    }

    /// <summary>
    /// Hitbox region type for categorization.
    /// </summary>
    public enum HitboxRegion : byte
    {
        Head = 0,
        Torso = 1,
        Arms = 2,
        Legs = 3,
        Hands = 4,
        Feet = 5
    }

    /// <summary>
    /// Buffer element for tracking multiple hitboxes on a character.
    /// Stored on the owner entity for quick lookup.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HitboxElement : IBufferElementData
    {
        public Entity ColliderEntity;
        public float DamageMultiplier;
        public HitboxRegion Region;
    }

    /// <summary>
    /// Tag for entities that have hitboxes registered.
    /// </summary>
    public struct HasHitboxes : IComponentData { }
}
