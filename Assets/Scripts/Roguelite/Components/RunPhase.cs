namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Run lifecycle phases. Drives the core rogue-lite game loop.
    /// </summary>
    public enum RunPhase : byte
    {
        None = 0,             // No active run
        Lobby = 1,            // Pre-run: config selection
        Preparation = 2,      // Loadout, meta-upgrades applied
        ZoneLoading = 3,      // IZoneProvider loading zone
        Active = 4,           // Normal gameplay in zone
        BossEncounter = 5,    // Boss zone (bridges EncounterState)
        ZoneTransition = 6,   // Between zones: rewards, shop, path choice
        RunEnd = 7,           // Death or final boss killed
        MetaScreen = 8        // Post-run: stats, meta-currency, unlocks
    }

    /// <summary>
    /// EPIC 23.1: Why the run ended.
    /// </summary>
    public enum RunEndReason : byte
    {
        None = 0,
        PlayerDeath = 1,
        BossDefeated = 2,
        Abandoned = 3,
        TimedOut = 4
    }
}
