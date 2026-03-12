namespace DIG.Targeting
{
    /// <summary>
    /// Available targeting modes for the targeting system.
    /// Used by TargetingConfig to determine which implementation to use.
    /// </summary>
    public enum TargetingMode : byte
    {
        /// <summary>
        /// Fire toward screen center / crosshair. Default for TPS games.
        /// </summary>
        CameraRaycast = 0,
        
        /// <summary>
        /// Fire toward mouse cursor position in world space. For ARPG games.
        /// </summary>
        CursorAim = 1,
        
        /// <summary>
        /// Auto-lock to nearest enemy in range. For fast-paced action.
        /// </summary>
        AutoTarget = 2,
        
        /// <summary>
        /// Manual lock-on with tab cycling. Souls-like.
        /// </summary>
        LockOn = 3,
        
        /// <summary>
        /// Click enemy to select, then use ability. Diablo-style.
        /// </summary>
        ClickSelect = 4
    }
    
    /// <summary>
    /// Priority for auto-target selection.
    /// </summary>
    public enum TargetPriority : byte
    {
        Nearest = 0,
        LowestHealth = 1,
        HighestThreat = 2,
        CursorProximity = 3
    }
}
