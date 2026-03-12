using Unity.Physics;
using DIG.Player.Components;

namespace DIG.Vision.Core
{
    /// <summary>
    /// Centralized collision filter factories for the vision system.
    /// Mirrors the CollisionLayers pattern — single source of truth for what
    /// sensors can detect and what blocks line-of-sight.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    public static class VisionLayers
    {
        /// <summary>
        /// Filter for the broad-phase overlap query.
        /// Finds entities that sensors should attempt to detect (players).
        /// </summary>
        public static CollisionFilter DetectableFilter => new CollisionFilter
        {
            BelongsTo = CollisionLayers.Everything,
            CollidesWith = CollisionLayers.Player,
            GroupIndex = 0
        };

        /// <summary>
        /// Filter for occlusion raycasts (AI vision system).
        /// Tests what geometry blocks line-of-sight between sensor and target.
        /// Includes static geometry, environment, and ship hull.
        /// </summary>
        public static CollisionFilter OcclusionFilter => new CollisionFilter
        {
            BelongsTo = CollisionLayers.Everything,
            CollidesWith = CollisionLayers.Default | CollisionLayers.Environment | CollisionLayers.Ship,
            GroupIndex = 0
        };

    }
}
