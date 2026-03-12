namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Distance-based force falloff for area knockback (explosions, shockwaves).
    /// Single-target knockback (melee hit) uses None.
    /// </summary>
    public enum KnockbackFalloff : byte
    {
        /// <summary>No falloff. Full force at any distance. For single-target knockback.</summary>
        None = 0,

        /// <summary>Force = Base * (1 - distance/radius). Gentle falloff.</summary>
        Linear = 1,

        /// <summary>Force = Base * (1 - (distance/radius)^2). Sharp close, gentle far. DEFAULT for explosions.</summary>
        Quadratic = 2,

        /// <summary>Force = Base * (1 - (distance/radius)^3). Very sharp close, almost zero at edge.</summary>
        Cubic = 3
    }
}
