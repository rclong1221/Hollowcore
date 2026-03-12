using Unity.Entities;
using Unity.Physics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.6.5: Helper utilities for managing Unity Physics GroupIndex for advanced collision filtering.
    /// GroupIndex allows entities to selectively ignore collisions beyond layer masks.
    /// </summary>
    public static class CollisionGroupIndex
    {
        /// <summary>
        /// Creates a unique negative GroupIndex for projectile owner filtering.
        /// Use the same negative index for both the projectile and its owner to make them ignore each other.
        /// </summary>
        /// <param name="ownerEntity">The entity that owns the projectile (e.g., player who fired)</param>
        /// <returns>Negative GroupIndex that makes entities ignore each other</returns>
        public static int ForProjectileOwner(Entity ownerEntity)
        {
            // Use negative entity index to create unique identifier
            // Negative values mean "never collide with same value"
            return -(int)ownerEntity.Index;
        }
        
        /// <summary>
        /// Creates a shared negative GroupIndex for team-based projectile filtering.
        /// All members of a team can ignore projectiles fired by teammates.
        /// </summary>
        /// <param name="teamId">The team identifier (1-255)</param>
        /// <returns>Negative GroupIndex for the team (-1000 to -1255)</returns>
        public static int ForTeam(byte teamId)
        {
            // Reserve -1000 to -1255 range for team-based filtering
            // This prevents team members' projectiles from hitting each other
            return -1000 - teamId;
        }
        
        /// <summary>
        /// Default GroupIndex for normal collision filtering.
        /// Use this to reset entities back to layer-based filtering.
        /// </summary>
        public const int Default = 0;
        
        /// <summary>
        /// Temporary GroupIndex duration for projectile owner filtering (in seconds).
        /// After firing, the owner's GroupIndex should be reset to Default after this duration.
        /// </summary>
        public const float OwnerFilterDuration = 0.1f;
        
        /// <summary>
        /// Sets a collision filter to ignore its owner temporarily.
        /// Typically called when spawning a projectile.
        /// </summary>
        /// <param name="filter">The collision filter to modify (projectile or owner)</param>
        /// <param name="ownerEntity">The owner entity</param>
        public static void SetOwnerIgnore(ref CollisionFilter filter, Entity ownerEntity)
        {
            filter.GroupIndex = ForProjectileOwner(ownerEntity);
        }
        
        /// <summary>
        /// Sets a collision filter to use team-based filtering.
        /// All team members will share the same negative GroupIndex.
        /// </summary>
        /// <param name="filter">The collision filter to modify</param>
        /// <param name="teamId">The team ID component</param>
        public static void SetTeamIgnore(ref CollisionFilter filter, TeamId teamId)
        {
            if (teamId.HasTeam)
            {
                filter.GroupIndex = ForTeam(teamId.Value);
            }
            else
            {
                filter.GroupIndex = Default;
            }
        }
        
        /// <summary>
        /// Resets a collision filter back to default layer-based filtering.
        /// Call this to clear temporary owner/team ignore states.
        /// </summary>
        /// <param name="filter">The collision filter to reset</param>
        public static void ResetToDefault(ref CollisionFilter filter)
        {
            filter.GroupIndex = Default;
        }
    }
    
    /// <summary>
    /// Epic 7.6.5: Component to track temporary GroupIndex override for projectile owner filtering.
    /// Add this to an entity when it fires a projectile to temporarily ignore that projectile.
    /// The system will reset GroupIndex to 0 after the duration expires.
    /// </summary>
    public struct GroupIndexOverride : IComponentData
    {
        /// <summary>
        /// The temporary GroupIndex value (negative for owner ignore)
        /// </summary>
        public int TemporaryGroupIndex;
        
        /// <summary>
        /// Time remaining until GroupIndex resets to 0 (in seconds)
        /// </summary>
        public float RemainingTime;
        
        /// <summary>
        /// Original GroupIndex value to restore after override expires
        /// </summary>
        public int OriginalGroupIndex;
        
        /// <summary>
        /// Creates a temporary owner ignore override for projectile firing.
        /// </summary>
        public static GroupIndexOverride CreateOwnerIgnore(Entity ownerEntity, int originalGroupIndex = 0)
        {
            return new GroupIndexOverride
            {
                TemporaryGroupIndex = CollisionGroupIndex.ForProjectileOwner(ownerEntity),
                RemainingTime = CollisionGroupIndex.OwnerFilterDuration,
                OriginalGroupIndex = originalGroupIndex
            };
        }
    }
}
