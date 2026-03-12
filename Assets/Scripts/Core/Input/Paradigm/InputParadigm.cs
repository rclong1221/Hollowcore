namespace DIG.Core.Input
{
    /// <summary>
    /// Defines the core input paradigm types.
    /// Each paradigm represents a different game genre's input model.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public enum InputParadigm : byte
    {
        /// <summary>
        /// TPS/FPS default. Mouse delta rotates camera. Cursor locked and hidden.
        /// Targeting via CameraRaycast (crosshair center).
        /// Games: Dark Souls, Elden Ring, Monster Hunter, TPS Shooters.
        /// </summary>
        Shooter = 0,

        /// <summary>
        /// Free cursor by default, RMB hold for camera orbit.
        /// A/D turn vs strafe toggle based on RMB state.
        /// Games: World of Warcraft, Final Fantasy XIV, Guild Wars 2.
        /// </summary>
        MMO = 1,

        /// <summary>
        /// Fixed isometric camera. Click-to-move with cursor always visible.
        /// Games: Diablo, Path of Exile, Grim Dawn, Last Epoch.
        /// </summary>
        ARPG = 2,

        /// <summary>
        /// Fixed top-down camera. RMB for move, LMB for select.
        /// Edge-pan camera, spacebar snap to champion.
        /// Games: League of Legends, Dota 2, Heroes of the Storm.
        /// </summary>
        MOBA = 3,

        /// <summary>
        /// WASD + cursor aim. Character always faces cursor.
        /// Games: Hades, Enter the Gungeon, Risk of Rain 2.
        /// </summary>
        TwinStick = 4,

        /// <summary>
        /// 2D side-scroller controls. A/D move, optional mouse aim.
        /// Games: Hollow Knight, Dead Cells, Celeste.
        /// </summary>
        SideScroller2D = 5,
    }

    /// <summary>
    /// Mode overlays that temporarily modify any base paradigm.
    /// </summary>
    public enum InputModeOverlay : byte
    {
        /// <summary>No overlay, standard paradigm behavior.</summary>
        None = 0,

        /// <summary>Vehicle/mount mode. Different physics, no strafe.</summary>
        VehicleMount = 1,

        /// <summary>Build/placement mode. Cursor controls ghost object.</summary>
        BuildPlacement = 2,
    }

    /// <summary>
    /// How the character faces during gameplay.
    /// </summary>
    public enum MovementFacingMode : byte
    {
        /// <summary>Always face camera direction (Shooter).</summary>
        CameraForward = 0,

        /// <summary>Face movement direction (MMO, ARPG, MOBA).</summary>
        MovementDirection = 1,

        /// <summary>Always face cursor position (Twin-Stick).</summary>
        CursorDirection = 2,

        /// <summary>Face locked target (Souls lock-on).</summary>
        TargetLocked = 3,

        /// <summary>Only turn via explicit input (MMO LMB turn).</summary>
        ManualTurn = 4,
    }

    /// <summary>
    /// How camera orbiting is controlled.
    /// </summary>
    public enum CameraOrbitMode : byte
    {
        /// <summary>Mouse always controls camera orbit (Shooter).</summary>
        AlwaysOrbit = 0,

        /// <summary>Camera orbit only when RMB held (MMO).</summary>
        ButtonHoldOrbit = 1,

        /// <summary>No mouse orbit, use Q/E keys (Isometric).</summary>
        KeyRotateOnly = 2,

        /// <summary>Camera follows character, no player control.</summary>
        FollowOnly = 3,
    }

    /// <summary>
    /// Which mouse button triggers click-to-move.
    /// </summary>
    public enum ClickToMoveButton : byte
    {
        /// <summary>Click-to-move disabled.</summary>
        None = 0,

        /// <summary>Left mouse button (ARPG style).</summary>
        LeftButton = 1,

        /// <summary>Right mouse button (MOBA style).</summary>
        RightButton = 2,
    }

    /// <summary>
    /// State of the paradigm state machine.
    /// </summary>
    public enum ParadigmState : byte
    {
        /// <summary>Stable state, ready for transitions.</summary>
        Stable = 0,

        /// <summary>Transition in progress, reject new transition requests.</summary>
        Transitioning = 1,
    }
}
