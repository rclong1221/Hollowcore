using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting
{
    /// <summary>
    /// Result of cursor hover raycasting. Written by CursorHoverSystem,
    /// read by UI (tooltip, cursor icon) and highlighting systems.
    ///
    /// Only populated when cursor is free (HybridToggle+modifier or TacticalCursor).
    /// When cursor is locked (ShooterDirect), IsValid is always false.
    /// </summary>
    public struct CursorHoverResult : IComponentData
    {
        /// <summary>Entity currently under the cursor (Entity.Null if none).</summary>
        public Entity HoveredEntity;

        /// <summary>World position of the hover hit point.</summary>
        public float3 HitPoint;

        /// <summary>What category the hovered object falls into.</summary>
        public HoverCategory Category;

        /// <summary>True if the raycast hit anything valid this frame.</summary>
        public bool IsValid;
    }

    /// <summary>
    /// Classification of what the cursor is hovering over.
    /// Drives cursor icon selection and available interactions.
    /// </summary>
    public enum HoverCategory : byte
    {
        None = 0,
        Enemy = 1,
        Friendly = 2,
        Interactable = 3,
        Lootable = 4,
        Ground = 5,
    }
}
