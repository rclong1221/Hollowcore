namespace DIG.Core.Input
{
    /// <summary>
    /// Controls cursor visibility and lock state.
    /// Listens to paradigm changes and self-configures.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public interface ICursorController
    {
        /// <summary>Whether the cursor is currently visible.</summary>
        bool IsCursorVisible { get; }

        /// <summary>Whether the cursor is currently locked.</summary>
        bool IsCursorLocked { get; }

        /// <summary>Whether cursor is free by default in current paradigm.</summary>
        bool IsCursorFreeByDefault { get; }

        /// <summary>Whether cursor is temporarily free (e.g., Alt held in Shooter).</summary>
        bool IsTemporaryCursorFree { get; }

        /// <summary>
        /// Combined check: cursor is free for interaction.
        /// True when: paradigm default is free, OR temporary override is active.
        /// </summary>
        bool IsCursorFree { get; }

        /// <summary>
        /// Temporarily override cursor state (e.g., Alt-to-free in Shooter).
        /// </summary>
        void SetTemporaryFree(bool free);
    }
}
