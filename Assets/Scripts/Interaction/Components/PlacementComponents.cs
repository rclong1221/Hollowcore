using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 6: Spatial Placement
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// How a placement position is validated before allowing confirmation.
    /// </summary>
    public enum PlacementValidation : byte
    {
        /// <summary>Always valid — no checks performed.</summary>
        None = 0,
        /// <summary>Physics overlap check — fails if geometry exists within OverlapCheckRadius.</summary>
        NoOverlap = 1,
        /// <summary>Surface must be within MaxSurfaceAngle from flat.</summary>
        FlatSurface = 2,
        /// <summary>Must be placed on a specific foundation surface (game-specific).</summary>
        Foundation = 3,
        /// <summary>Delegated to game-specific validation system.</summary>
        Custom = 4
    }

    /// <summary>
    /// EPIC 16.1 Phase 6: Configuration for a placeable item/tool.
    /// Placed on the ITEM/TOOL entity that can spawn objects in the world.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PlaceableConfig : IComponentData
    {
        /// <summary>Prefab entity to spawn when placement is confirmed.</summary>
        [GhostField]
        public Entity PlaceablePrefab;

        /// <summary>Maximum raycast distance from player eye.</summary>
        public float MaxPlacementRange;

        /// <summary>Snap-to-grid size. 0 = free placement (no snapping).</summary>
        public float GridSnap;

        /// <summary>Maximum surface angle from flat (in degrees). Default: 45.</summary>
        public float MaxSurfaceAngle;

        /// <summary>How to validate the placement position.</summary>
        public PlacementValidation Validation;

        /// <summary>Radius for physics overlap check (NoOverlap validation). Default: 0.5.</summary>
        public float OverlapCheckRadius;
    }

    /// <summary>
    /// EPIC 16.1 Phase 6: Runtime placement state on the PLAYER entity.
    /// Tracks whether the player is in placement mode and where the preview is.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlacementState : IComponentData
    {
        /// <summary>Whether the player is currently in placement mode.</summary>
        [GhostField]
        public bool IsPlacing;

        /// <summary>World position where the preview is shown.</summary>
        [GhostField(Quantization = 100)]
        public float3 PreviewPosition;

        /// <summary>Preview orientation (aligned to surface normal).</summary>
        [GhostField(Quantization = 1000)]
        public quaternion PreviewRotation;

        /// <summary>Whether the current placement position is valid for confirmation.</summary>
        [GhostField]
        public bool IsValid;

        /// <summary>Player pressed confirm this frame.</summary>
        [GhostField]
        public bool ConfirmPlacement;

        /// <summary>Player pressed cancel this frame.</summary>
        [GhostField]
        public bool CancelPlacement;

        /// <summary>The item/tool entity providing PlaceableConfig.</summary>
        public Entity PlaceableSource;

        /// <summary>Hit surface normal (for angle validation and preview orientation).</summary>
        public float3 SurfaceNormal;
    }
}
