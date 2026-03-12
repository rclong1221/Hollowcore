namespace DIG.Core.Input
{
    /// <summary>
    /// Controls character rotation/facing.
    /// Listens to paradigm changes and applies correct facing mode.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public interface IFacingController
    {
        /// <summary>Current facing mode.</summary>
        MovementFacingMode CurrentFacingMode { get; }
    }
}
