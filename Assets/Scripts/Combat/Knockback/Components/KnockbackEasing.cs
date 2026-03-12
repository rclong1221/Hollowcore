namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Easing curve for knockback velocity decay over duration.
    /// </summary>
    public enum KnockbackEasing : byte
    {
        /// <summary>Constant deceleration. Functional but feels mechanical.</summary>
        Linear = 0,

        /// <summary>Fast start, gradual stop. DEFAULT. Feels responsive and natural.</summary>
        EaseOut = 1,

        /// <summary>Slides past target, bounces back slightly. Cartoonish, fun.</summary>
        Bounce = 2,

        /// <summary>Near-instant burst, very fast decay. Snappy hit reactions.</summary>
        Sharp = 3
    }
}
