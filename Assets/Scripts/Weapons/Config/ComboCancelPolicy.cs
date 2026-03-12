namespace DIG.Weapons.Config
{
    /// <summary>
    /// Defines when attacks can be canceled by other actions.
    /// </summary>
    public enum ComboCancelPolicy : byte
    {
        /// <summary>
        /// Cannot cancel attacks. Must commit until combo ends or gets interrupted by damage.
        /// </summary>
        None = 0,

        /// <summary>
        /// Can only cancel during recovery phase (after hitbox deactivates).
        /// </summary>
        RecoveryOnly = 1,

        /// <summary>
        /// Can cancel at any time during the attack. Most responsive.
        /// </summary>
        Anytime = 2
    }
}
