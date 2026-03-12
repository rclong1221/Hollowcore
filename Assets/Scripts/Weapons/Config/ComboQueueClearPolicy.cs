using System;

namespace DIG.Weapons.Config
{
    /// <summary>
    /// Flags defining when the attack queue should be cleared.
    /// </summary>
    [Flags]
    public enum ComboQueueClearPolicy : byte
    {
        /// <summary>
        /// Queue is never automatically cleared.
        /// </summary>
        Never = 0,

        /// <summary>
        /// Queue clears when a cancel action is performed.
        /// </summary>
        OnCancel = 1 << 0,

        /// <summary>
        /// Queue clears when the player takes damage.
        /// </summary>
        OnHit = 1 << 1,

        /// <summary>
        /// Queue clears when dodging.
        /// </summary>
        OnDodge = 1 << 2,

        /// <summary>
        /// Queue clears when blocking.
        /// </summary>
        OnBlock = 1 << 3,

        /// <summary>
        /// Standard setting: clear on cancel and hit.
        /// </summary>
        Standard = OnCancel | OnHit
    }
}
