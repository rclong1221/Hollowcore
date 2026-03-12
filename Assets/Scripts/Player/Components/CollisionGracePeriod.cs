using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.6.4: Collision grace period to prevent collision spam during spawn/teleport.
    /// Add this component to temporarily disable collision processing for an entity.
    /// The CollisionGracePeriodSystem will tick down RemainingTime and remove the component when expired.
    /// </summary>
    public struct CollisionGracePeriod : IComponentData
    {
        /// <summary>
        /// Time remaining until collisions re-enable (in seconds).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RemainingTime;
        
        /// <summary>
        /// When true, skip player-player collision during grace period.
        /// Use for spawn protection.
        /// </summary>
        [GhostField]
        public bool IgnorePlayerCollision;
        
        /// <summary>
        /// When true, skip ALL collision during grace period.
        /// Use for teleport effects where player should be intangible.
        /// </summary>
        [GhostField]
        public bool IgnoreAllCollision;
        
        /// <summary>
        /// Default spawn grace period (1 second, player collision only).
        /// </summary>
        public static CollisionGracePeriod SpawnDefault => new CollisionGracePeriod
        {
            RemainingTime = 1.0f,
            IgnorePlayerCollision = true,
            IgnoreAllCollision = false
        };
        
        /// <summary>
        /// Default teleport grace period (0.5 seconds, all collision).
        /// </summary>
        public static CollisionGracePeriod TeleportDefault => new CollisionGracePeriod
        {
            RemainingTime = 0.5f,
            IgnorePlayerCollision = true,
            IgnoreAllCollision = true
        };
        
        /// <summary>
        /// Create a custom grace period.
        /// </summary>
        public static CollisionGracePeriod Create(float duration, bool ignorePlayerCollision = true, bool ignoreAllCollision = false)
        {
            return new CollisionGracePeriod
            {
                RemainingTime = duration,
                IgnorePlayerCollision = ignorePlayerCollision,
                IgnoreAllCollision = ignoreAllCollision
            };
        }
    }
}
