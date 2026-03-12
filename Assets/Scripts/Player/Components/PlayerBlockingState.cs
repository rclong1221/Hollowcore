using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tracks player's current blocking state for damage reduction.
    /// Synced from equipped shield's ShieldState component.
    /// EPIC 15.7: Shield Block System
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerBlockingState : IComponentData
    {
        /// <summary>
        /// Whether the player is currently blocking.
        /// </summary>
        [GhostField]
        public bool IsBlocking;

        /// <summary>
        /// Whether the parry window is active.
        /// </summary>
        [GhostField]
        public bool IsParrying;

        /// <summary>
        /// Damage reduction percentage (0.0 - 1.0).
        /// 0.7 means 70% damage reduction.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float DamageReduction;

        /// <summary>
        /// Block coverage angle in degrees (frontal arc).
        /// </summary>
        [GhostField(Quantization = 10)]
        public float BlockAngle;

        /// <summary>
        /// Entity of the shield currently providing the block.
        /// Used for stamina deduction.
        /// </summary>
        public Entity ShieldEntity;

        /// <summary>
        /// Forward direction when blocking started (for angle check).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 BlockDirection;

        public static PlayerBlockingState Default => new PlayerBlockingState
        {
            IsBlocking = false,
            IsParrying = false,
            DamageReduction = 0f,
            BlockAngle = 0f,
            ShieldEntity = Entity.Null,
            BlockDirection = float3.zero
        };
    }
}
