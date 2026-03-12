namespace DIG.CameraSystem
{
    /// <summary>
    /// Available camera modes for the camera system.
    /// Used by CameraConfig to determine which implementation to use.
    /// </summary>
    public enum CameraMode : byte
    {
        /// <summary>
        /// Behind-character orbit camera with mouse look. Default for DIG.
        /// Camera follows behind and above the character, mouse controls orbit.
        /// Movement is relative to camera forward direction.
        /// </summary>
        ThirdPersonFollow = 0,

        /// <summary>
        /// Fixed angle isometric camera. Default for ARPG games.
        /// Camera is at a fixed 45-60° angle looking down at character.
        /// No mouse orbit, WASD movement transformed for isometric view.
        /// </summary>
        IsometricFixed = 1,

        /// <summary>
        /// Top-down camera looking straight down or at slight angle.
        /// Simplified isometric variant for certain game styles.
        /// </summary>
        TopDownFixed = 2,

        /// <summary>
        /// Isometric camera with Q/E rotation in 45° increments.
        /// Optional mode for games that want rotatable isometric view.
        /// </summary>
        IsometricRotatable = 3,
        
        /// <summary>
        /// First-person camera at eye level.
        /// Mouse controls look direction, no external camera orbit.
        /// </summary>
        FirstPerson = 4
    }

    /// <summary>
    /// Method for projecting cursor position to world space.
    /// Used by isometric/top-down cameras for aim/targeting.
    /// </summary>
    public enum CursorProjectionMethod : byte
    {
        /// <summary>
        /// Project cursor ray to Y=0 plane.
        /// Simple and fast, works for flat terrain.
        /// </summary>
        GroundPlane = 0,

        /// <summary>
        /// Raycast cursor to terrain collider.
        /// Accurate for uneven terrain.
        /// </summary>
        TerrainHit = 1,

        /// <summary>
        /// Project to character's Y height.
        /// Good for floating/flying characters.
        /// </summary>
        FixedHeight = 2,

        /// <summary>
        /// Use NavMesh or terrain height sampling.
        /// Most accurate but slightly more expensive.
        /// </summary>
        SmartHeight = 3
    }
}
