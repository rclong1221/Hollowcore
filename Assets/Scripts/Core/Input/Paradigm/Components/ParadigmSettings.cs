using Unity.Entities;
using DIG.Targeting;

namespace DIG.Core.Input
{
    /// <summary>
    /// ECS singleton that holds the current paradigm settings for Burst-compiled systems.
    /// This is synced from the managed ParadigmStateMachine by ParadigmSettingsSyncSystem.
    /// 
    /// Unlike InputParadigmState (per-entity), this is a singleton that Burst systems
    /// can safely read without accessing managed code.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public struct ParadigmSettings : IComponentData
    {
        /// <summary>Active input paradigm.</summary>
        public InputParadigm ActiveParadigm;

        /// <summary>Current facing mode.</summary>
        public MovementFacingMode FacingMode;

        /// <summary>
        /// Whether movement is screen-relative (isometric/ARPG) or camera-relative (TPS/FPS).
        /// Screen-relative: W=up on screen, A=left on screen
        /// Camera-relative: W=camera forward, A=camera left
        /// </summary>
        public bool UseScreenRelativeMovement;

        /// <summary>Whether WASD direct movement is enabled.</summary>
        public bool IsWASDEnabled;

        /// <summary>Whether A/D keys turn the character vs strafe.</summary>
        public bool ADTurnsCharacter;

        /// <summary>Whether cursor should be visible in this paradigm.</summary>
        public bool CursorVisible;

        /// <summary>Whether this is an isometric camera mode.</summary>
        public bool IsIsometric;

        /// <summary>Whether click-to-move is enabled in this paradigm.</summary>
        public bool IsClickToMoveEnabled;

        /// <summary>Which mouse button triggers click-to-move.</summary>
        public ClickToMoveButton ClickToMoveButton;

        /// <summary>Whether pathfinding is used for click-to-move.</summary>
        public bool UsePathfinding;

        /// <summary>
        /// Camera yaw in degrees for screen-relative movement calculation.
        /// Set by CinemachineCameraController when in isometric mode.
        /// </summary>
        public float CameraYaw;

        /// <summary>Active targeting mode for the current paradigm.</summary>
        public TargetingMode ActiveTargetingMode;

        /// <summary>Whether settings have been initialized (valid data).</summary>
        public bool IsValid;
    }
}
