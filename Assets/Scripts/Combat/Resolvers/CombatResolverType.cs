namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Types of combat resolvers available.
    /// Used for configuration and resolver lookup.
    /// </summary>
    public enum CombatResolverType : byte
    {
        /// <summary>
        /// Physics collision determines hit, no stat scaling.
        /// Best for: Pure action games where player skill (aiming) determines hit.
        /// </summary>
        PhysicsHitbox = 0,
        
        /// <summary>
        /// Stat-based combat where attacks in range always hit.
        /// Damage scales with stats. No accuracy roll.
        /// Best for: Fast-paced ARPGs where "game feel" matters more than dice rolls.
        /// </summary>
        StatBasedDirect = 1,
        
        /// <summary>
        /// Full stat-based combat with accuracy vs evasion rolls.
        /// More tactical feel with potential misses.
        /// Best for: Tactical ARPGs, turn-based games.
        /// </summary>
        StatBasedRoll = 2,
        
        /// <summary>
        /// Hybrid approach: Requires physics hit, then applies stat damage.
        /// Combines skillful aiming with RPG damage depth.
        /// Best for: Games wanting both aiming skill and stat progression.
        /// </summary>
        Hybrid = 3
    }
}
