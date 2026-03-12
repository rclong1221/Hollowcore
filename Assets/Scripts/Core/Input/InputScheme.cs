namespace DIG.Core.Input
{
    /// <summary>
    /// Defines how mouse input is routed during Gameplay context.
    /// Orthogonal to InputContext (Gameplay/UI) — schemes only apply during Gameplay.
    ///
    /// CONSTRAINT: Schemes are paired with camera modes.
    /// ShooterDirect/HybridToggle require SupportsOrbitRotation cameras (TPS/FPS).
    /// TacticalCursor requires UsesCursorAiming cameras (Isometric/TopDown).
    /// </summary>
    public enum InputScheme : byte
    {
        /// <summary>
        /// TPS/FPS default. Mouse delta rotates camera. Cursor locked and hidden.
        /// Targeting via CameraRaycast (crosshair center).
        /// Compatible cameras: ThirdPersonFollow, FirstPerson.
        /// </summary>
        ShooterDirect = 0,

        /// <summary>
        /// Hybrid. ShooterDirect by default; holding a modifier key (e.g., Alt)
        /// temporarily frees the cursor and pauses camera orbit.
        /// Compatible cameras: ThirdPersonFollow, FirstPerson.
        /// </summary>
        HybridToggle = 1,

        /// <summary>
        /// ARPG/Tactical. Mouse moves a visible cursor permanently.
        /// Camera rotation via Q/E keys or edge-scroll — NOT mouse.
        /// Compatible cameras: IsometricFixed, IsometricRotatable, TopDownFixed.
        /// </summary>
        TacticalCursor = 2,
    }
}
