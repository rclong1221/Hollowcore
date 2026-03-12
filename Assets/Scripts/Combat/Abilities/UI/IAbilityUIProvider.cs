namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Interface for UI systems that display ability state.
    /// Implemented by MonoBehaviour adapters (e.g., AbilityBarAdapter).
    ///
    /// EPIC 18.19 - Phase 7
    /// </summary>
    public interface IAbilityUIProvider
    {
        /// <summary>
        /// Update a single ability slot's display.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-5).</param>
        /// <param name="abilityId">Ability ID (-1 = empty).</param>
        /// <param name="cooldownRemaining">Seconds until ready.</param>
        /// <param name="cooldownTotal">Total cooldown duration for fill calculation.</param>
        /// <param name="chargesRemaining">Current charges (0 = no charge system).</param>
        /// <param name="maxCharges">Max charges (0 = no charge system).</param>
        void UpdateSlot(int slotIndex, int abilityId, float cooldownRemaining, float cooldownTotal,
            int chargesRemaining, int maxCharges);

        /// <summary>
        /// Update cast bar display.
        /// </summary>
        /// <param name="visible">Whether the cast bar should be shown.</param>
        /// <param name="progress">Cast progress 0-1.</param>
        /// <param name="phaseName">Current phase name for display.</param>
        void UpdateCastBar(bool visible, float progress, string phaseName);

        /// <summary>
        /// Update GCD overlay on all slots.
        /// </summary>
        /// <param name="gcdRemaining">Seconds of GCD remaining.</param>
        /// <param name="gcdTotal">Total GCD duration for fill calculation.</param>
        void UpdateGCD(float gcdRemaining, float gcdTotal);

        /// <summary>
        /// Show an error message (not enough mana, on cooldown, out of range, etc.).
        /// </summary>
        /// <param name="message">Error message to display.</param>
        void ShowError(string message);
    }
}
