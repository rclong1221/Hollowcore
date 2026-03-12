using System;
using System.Collections.Generic;

namespace DIG.Core.Input
{
    /// <summary>
    /// Provider interface for input paradigm settings.
    /// Decouples UI and gameplay systems from concrete implementation.
    /// 
    /// Pattern: Same as IHealthBarSettingsProvider in Combat.UI.
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public interface IInputParadigmProvider
    {
        /// <summary>Current active paradigm profile.</summary>
        InputParadigmProfile ActiveProfile { get; }

        /// <summary>All paradigms available for selection.</summary>
        IReadOnlyList<InputParadigmProfile> AvailableProfiles { get; }

        /// <summary>Current mode overlay (Vehicle, Build, or None).</summary>
        InputModeOverlay ActiveModeOverlay { get; }

        /// <summary>Current state of the state machine.</summary>
        ParadigmState CurrentState { get; }

        /// <summary>Fired when paradigm changes successfully. Listeners self-configure.</summary>
        event Action<InputParadigmProfile> OnParadigmChanged;

        /// <summary>Fired when mode overlay changes.</summary>
        event Action<InputModeOverlay> OnModeOverlayChanged;

        /// <summary>Fired when transition starts. Args: (from, to).</summary>
        event Action<InputParadigmProfile, InputParadigmProfile> OnTransitionStarted;

        /// <summary>Fired when transition completes. Args: (profile, success).</summary>
        event Action<InputParadigmProfile, bool> OnTransitionCompleted;

        /// <summary>
        /// Attempt to switch paradigm. Returns false if incompatible or transition fails.
        /// </summary>
        bool TrySetParadigm(InputParadigmProfile profile);

        /// <summary>
        /// Attempt to switch by paradigm type (finds matching profile).
        /// </summary>
        bool TrySetParadigm(InputParadigm paradigm);

        /// <summary>
        /// Activate or deactivate a mode overlay.
        /// </summary>
        void SetModeOverlay(InputModeOverlay overlay);

        /// <summary>
        /// Check if a paradigm is compatible with current camera.
        /// </summary>
        bool IsParadigmCompatible(InputParadigmProfile profile);
    }
}
