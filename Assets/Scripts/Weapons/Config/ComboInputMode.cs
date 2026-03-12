namespace DIG.Weapons.Config
{
    /// <summary>
    /// Defines how combo input is processed.
    /// </summary>
    public enum ComboInputMode : byte
    {
        /// <summary>
        /// Each attack requires a new button press. (Dark Souls, Elden Ring, Monster Hunter)
        /// Holding the button does NOT continue the combo.
        /// </summary>
        InputPerSwing = 0,

        /// <summary>
        /// Holding the attack button auto-advances through combo chain. (Devil May Cry, Bayonetta)
        /// Releasing stops the combo after the current swing.
        /// </summary>
        HoldToCombo = 1,

        /// <summary>
        /// Timed inputs with penalty for mistiming. (Batman Arkham, Spider-Man)
        /// Too early = breaks combo, too late = misses window.
        /// </summary>
        RhythmBased = 2
    }
}
