using System;

namespace DIG.Weapons.Config
{
    /// <summary>
    /// Flags defining which actions can cancel an attack.
    /// </summary>
    [Flags]
    public enum ComboCancelPriority : byte
    {
        /// <summary>
        /// No actions can cancel attacks.
        /// </summary>
        None = 0,

        /// <summary>
        /// Dodge/roll can cancel attacks.
        /// </summary>
        Dodge = 1 << 0,

        /// <summary>
        /// Jump can cancel attacks.
        /// </summary>
        Jump = 1 << 1,

        /// <summary>
        /// Block/shield can cancel attacks.
        /// </summary>
        Block = 1 << 2,

        /// <summary>
        /// Special abilities can cancel attacks.
        /// </summary>
        Ability = 1 << 3,

        /// <summary>
        /// Any movement input can cancel attacks.
        /// </summary>
        Movement = 1 << 4,

        /// <summary>
        /// All cancel options enabled.
        /// </summary>
        All = Dodge | Jump | Block | Ability | Movement
    }
}
