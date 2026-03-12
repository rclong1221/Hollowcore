namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Match lifecycle phase enumeration.
    /// </summary>
    public enum PvPMatchPhase : byte
    {
        WaitingForPlayers = 0,
        Warmup            = 1,
        Active            = 2,
        Overtime          = 3,
        Results           = 4,
        Ended             = 5
    }

    /// <summary>
    /// EPIC 17.10: Supported PvP game modes.
    /// </summary>
    public enum PvPGameMode : byte
    {
        FreeForAll       = 0,
        TeamDeathmatch   = 1,
        CapturePoint     = 2,
        Duel             = 3
    }

    /// <summary>
    /// EPIC 17.10: Competitive tier derived from Elo rating.
    /// </summary>
    public enum PvPTier : byte
    {
        Bronze   = 0,
        Silver   = 1,
        Gold     = 2,
        Platinum = 3,
        Diamond  = 4,
        Master   = 5
    }
}
