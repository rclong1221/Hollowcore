namespace DIG.Core.Input
{
    /// <summary>
    /// Routes movement input based on active paradigm.
    /// Decides whether WASD, click-to-move, or both are active.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public interface IMovementRouter
    {
        /// <summary>Whether WASD direct movement is enabled.</summary>
        bool IsWASDEnabled { get; }

        /// <summary>Whether click-to-move is enabled.</summary>
        bool IsClickToMoveEnabled { get; }

        /// <summary>Which mouse button triggers click-to-move.</summary>
        ClickToMoveButton ClickToMoveButton { get; }

        /// <summary>Whether pathfinding should be used for movement.</summary>
        bool UsePathfinding { get; }

        /// <summary>Whether A/D keys turn the character (MMO default) vs strafe (Shooter).</summary>
        bool ADTurnsCharacter { get; }
    }
}
