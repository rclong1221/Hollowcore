using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Singleton component for game-mode level collision settings.
    /// Controls friendly fire, team collision, and other global collision behaviors.
    /// Epic 7.6.3: Friendly fire toggle for PvP game modes.
    /// </summary>
    public struct CollisionGameSettings : IComponentData
    {
        /// <summary>
        /// When true, player-player collisions cause stagger/knockdown.
        /// When false, players pass through each other without damage (soft collision only).
        /// Default: true (friendly fire enabled).
        /// </summary>
        public bool FriendlyFireEnabled;
        
        /// <summary>
        /// When true, same-team players can stagger/knockdown each other.
        /// When false, same-team collisions are skipped (different teams still collide).
        /// Only applies when FriendlyFireEnabled is true.
        /// Default: false (team members don't hurt each other).
        /// </summary>
        public bool TeamCollisionEnabled;
        
        /// <summary>
        /// When true, players still experience soft push forces even when friendly fire is disabled.
        /// When false, players completely ignore each other's collision.
        /// Default: true (soft collision for movement separation).
        /// </summary>
        public bool SoftCollisionWhenDisabled;
        
        /// <summary>
        /// Multiplier for push forces when friendly fire is disabled but soft collision is enabled.
        /// Range 0-1. Lower values = gentler pushes when FF is off.
        /// Default: 0.3 (30% push force when FF disabled).
        /// </summary>
        public float SoftCollisionForceMultiplier;
        
        /// <summary>
        /// Default settings: friendly fire on, team collision off, soft collision enabled.
        /// </summary>
        public static CollisionGameSettings Default => new CollisionGameSettings
        {
            FriendlyFireEnabled = true,
            TeamCollisionEnabled = false,
            SoftCollisionWhenDisabled = true,
            SoftCollisionForceMultiplier = 0.3f
        };
    }
    
    /// <summary>
    /// Team identifier for team-based collision filtering.
    /// Players with the same TeamId (and TeamCollisionEnabled=false) won't stagger each other.
    /// Epic 7.6.3: Team-based collision filtering.
    /// </summary>
    public struct TeamId : IComponentData
    {
        /// <summary>
        /// Team identifier.
        /// 0 = No team (always participates in collision)
        /// 1-255 = Team ID (same-team collision controlled by TeamCollisionEnabled)
        /// </summary>
        public byte Value;
        
        /// <summary>
        /// Returns true if this entity is on a team (non-zero team ID).
        /// </summary>
        public bool HasTeam => Value != 0;
        
        /// <summary>
        /// Returns true if two team IDs represent the same team.
        /// Note: Two entities with TeamId=0 are NOT considered same team (no team vs no team).
        /// </summary>
        public static bool IsSameTeam(TeamId a, TeamId b)
        {
            // Both must have a team AND same team ID
            return a.Value != 0 && a.Value == b.Value;
        }
    }
}
