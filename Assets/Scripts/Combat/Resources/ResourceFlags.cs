namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: Per-slot behavioral modifiers controlling regen, decay, generation.
    /// </summary>
    [System.Flags]
    public enum ResourceFlags : byte
    {
        None           = 0,
        CanOverflow    = 1 << 0,
        DecaysWhenFull = 1 << 1,
        PausedRegen    = 1 << 2,
        GenerateOnHit  = 1 << 3,
        GenerateOnTake = 1 << 4,
        DecaysWhenIdle = 1 << 5,
        IsInteger      = 1 << 6
    }
}
