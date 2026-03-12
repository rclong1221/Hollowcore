namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Budget category for VFX throttling.
    /// Each category has an independent per-frame cap in VFXBudgetConfig.
    /// </summary>
    public enum VFXCategory : byte
    {
        Combat = 0,
        Environment = 1,
        Ability = 2,
        Death = 3,
        UI = 4,
        Ambient = 5,
        Interaction = 6
    }
}
