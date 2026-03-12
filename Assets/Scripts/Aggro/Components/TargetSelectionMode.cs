namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Determines how AggroTargetSelectorSystem picks a target
    /// from the ThreatEntry buffer. Default is HighestThreat (classic MMO).
    /// </summary>
    public enum TargetSelectionMode : byte
    {
        HighestThreat  = 0,
        WeightedScore  = 1,
        Nearest        = 2,
        LastAttacker   = 3,
        LowestHealth   = 4,
        Random         = 5,
        Defender       = 6,
    }
}
