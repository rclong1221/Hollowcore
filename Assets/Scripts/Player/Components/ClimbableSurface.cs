using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// EPIC 13.20.1: Marks an entity/surface as climbable for FreeClimb system.
    /// 
    /// Attach to any object that should be climbable. The FreeClimbDetectionSystem
    /// will validate surfaces have this component (in addition to layer mask checks).
    /// </summary>
    public struct ClimbableSurface : IComponentData
    {
        /// <summary>
        /// Can the player hang without footholds on this surface?
        /// If false, player will dismount when feet lose contact.
        /// </summary>
        public bool AllowFreeHang;

        /// <summary>
        /// Can the player gap-jump to nearby climbable surfaces?
        /// Enables chain climbing between pillars, ledges, etc.
        /// </summary>
        public bool AllowGapCrossing;

        /// <summary>
        /// Multiplier for stamina drain while climbing this surface.
        /// 1.0 = normal, 2.0 = double drain (harder surface), 0.5 = easier grip.
        /// </summary>
        public float GripStrength;

        /// <summary>
        /// Optional surface type for audio/VFX selection.
        /// 0 = default, map to SurfaceMaterialId if needed.
        /// </summary>
        public int SurfaceType;

        /// <summary>
        /// Create default climbable surface with standard settings.
        /// </summary>
        public static ClimbableSurface Default => new ClimbableSurface
        {
            AllowFreeHang = true,
            AllowGapCrossing = true,
            GripStrength = 1.0f,
            SurfaceType = 0
        };
    }
}
