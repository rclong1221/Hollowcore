namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: Typed identifiers for all combat resource pools.
    /// None = ability is free (no resource check). Used by ResourcePool, AbilityDefinition, ChannelAction.
    /// </summary>
    public enum ResourceType : byte
    {
        None     = 0,
        Stamina  = 1,
        Mana     = 2,
        Energy   = 3,
        Rage     = 4,
        Combo    = 5,
        Custom0  = 6,
        Custom1  = 7
    }
}
