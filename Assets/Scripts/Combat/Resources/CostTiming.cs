namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: When resource deduction occurs during ability execution.
    /// </summary>
    public enum CostTiming : byte
    {
        OnCast     = 0,
        PerTick    = 1,
        OnComplete = 2,
        OnHit      = 3
    }
}
