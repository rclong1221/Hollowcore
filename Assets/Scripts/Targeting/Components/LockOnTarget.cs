using Unity.Entities;

namespace DIG.Targeting.Components
{
    /// <summary>
    /// EPIC 15.16: Tag component marking an entity as a valid lock-on target.
    /// Add this to any entity that should be targetable (enemies, bosses, destructibles).
    /// 
    /// This is more efficient than physics layer queries because:
    /// 1. Direct ECS query instead of physics world search
    /// 2. Explicit opt-in rather than relying on collision layers
    /// 3. Can be enabled/disabled without structural changes
    /// </summary>
    public struct LockOnTarget : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Priority for targeting. Higher = preferred when multiple targets available.
        /// Use 0 for normal enemies, 10 for elites, 100 for bosses.
        /// </summary>
        public int Priority;
        
        /// <summary>
        /// Vertical offset for the lock-on indicator (relative to entity position).
        /// Use this to position the reticle at chest/head height.
        /// </summary>
        public float IndicatorHeightOffset;
    }
    
    /// <summary>
    /// Optional: Marks entity as currently being targeted by a player.
    /// Useful for AI reactions, visual effects, etc.
    /// </summary>
    public struct IsBeingTargeted : IComponentData, IEnableableComponent
    {
        /// <summary>The player entity that is targeting this entity.</summary>
        public Entity TargetingPlayer;
    }
}
