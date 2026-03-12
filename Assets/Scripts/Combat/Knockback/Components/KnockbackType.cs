namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Determines knockback direction calculation and vertical behavior.
    /// </summary>
    public enum KnockbackType : byte
    {
        /// <summary>Horizontal away from source. Standard explosion/hit knockback.</summary>
        Push = 0,

        /// <summary>Horizontal + upward arc. Boss slams, uppercuts, geysers.</summary>
        Launch = 1,

        /// <summary>Toward source. Vortex grenades, grapple pulls, gravity wells.</summary>
        Pull = 2,

        /// <summary>Brief freeze + small push from hit direction. Heavy hit stagger.</summary>
        Stagger = 3
    }
}
