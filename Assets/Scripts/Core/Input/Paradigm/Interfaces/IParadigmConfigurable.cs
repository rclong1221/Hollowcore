namespace DIG.Core.Input
{
    /// <summary>
    /// Interface for subsystems that configure themselves during paradigm transitions.
    /// Supports pre-validation, snapshot capture, configuration, and rollback.
    /// 
    /// Order of operations during transition:
    /// 1. CanConfigure() - Check if this subsystem can handle the new paradigm
    /// 2. CaptureSnapshot() - Save current state for potential rollback
    /// 3. Configure() - Apply the new paradigm configuration
    /// 4. Rollback() - Restore from snapshot if later step fails
    /// 
    /// EPIC 15.20 - State Machine Coordinator Architecture
    /// </summary>
    public interface IParadigmConfigurable
    {
        /// <summary>
        /// Configuration priority. Lower values = configured first.
        /// Recommended ranges:
        /// - 0-99: Core systems (cursor, camera)
        /// - 100-199: Movement systems
        /// - 200-299: Facing/rotation systems
        /// - 300+: UI/visual systems
        /// </summary>
        int ConfigurationOrder { get; }

        /// <summary>Human-readable name for logging.</summary>
        string SubsystemName { get; }

        /// <summary>
        /// Pre-validate: Can this subsystem configure for the given profile?
        /// Called BEFORE any configuration starts.
        /// Return false with error message to abort entire transition.
        /// </summary>
        bool CanConfigure(InputParadigmProfile profile, out string errorReason);

        /// <summary>
        /// Capture current state for potential rollback.
        /// Called BEFORE Configure() is invoked.
        /// </summary>
        IConfigSnapshot CaptureSnapshot();

        /// <summary>
        /// Apply configuration for the new paradigm.
        /// May throw on error (coordinator will rollback).
        /// </summary>
        void Configure(InputParadigmProfile profile);

        /// <summary>
        /// Rollback to the provided snapshot.
        /// Called if a later subsystem fails during Configure().
        /// </summary>
        void Rollback(IConfigSnapshot snapshot);
    }

    /// <summary>
    /// Marker interface for configuration snapshots.
    /// Each subsystem defines its own snapshot class.
    /// </summary>
    public interface IConfigSnapshot { }
}
