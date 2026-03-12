namespace DIG.Combat.UI
{
    /// <summary>
    /// Controls which damage numbers a player sees in multiplayer.
    /// Configured per-game via DamageVisibilityConfig.DefaultVisibility,
    /// optionally overridden per-player via gameplay settings.
    /// </summary>
    public enum DamageNumberVisibility
    {
        /// <summary>See damage numbers from all players (Diablo 4 style).</summary>
        All = 0,
        /// <summary>Only see damage numbers you dealt (Destiny 2 style).</summary>
        SelfOnly = 1,
        /// <summary>Only see damage within NearbyDistance of the player.</summary>
        Nearby = 2,
        /// <summary>Only see damage from party members (including self).</summary>
        Party = 3,
        /// <summary>Disable all damage numbers.</summary>
        None = 4
    }
}
