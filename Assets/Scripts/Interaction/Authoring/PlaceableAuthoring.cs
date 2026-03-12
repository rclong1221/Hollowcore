using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 6: Authoring component for placeable items/tools.
    ///
    /// Designer workflow:
    /// 1. Add PlaceableAuthoring to the item/tool prefab
    /// 2. Assign the PlaceablePrefab (what gets spawned on confirm)
    /// 3. Configure range, grid snap, surface angle, validation
    /// 4. Game systems set PlacementState.IsPlacing on the player to enter placement mode
    /// 5. PlacementSystem handles raycast, validation, confirm/cancel
    /// </summary>
    public class PlaceableAuthoring : MonoBehaviour
    {
        [Header("Placement Configuration")]
        [Tooltip("Prefab to spawn when placement is confirmed")]
        public GameObject PlaceablePrefab;

        [Tooltip("Maximum raycast distance from player eye")]
        public float MaxPlacementRange = 10f;

        [Tooltip("Snap-to-grid size. 0 = free placement (no snapping)")]
        public float GridSnap = 0f;

        [Tooltip("Maximum surface angle from flat (degrees)")]
        [Range(0f, 90f)]
        public float MaxSurfaceAngle = 45f;

        [Header("Validation")]
        [Tooltip("How to validate the placement position")]
        public PlacementValidation Validation = PlacementValidation.FlatSurface;

        [Tooltip("Radius for physics overlap check (NoOverlap validation)")]
        public float OverlapCheckRadius = 0.5f;

        public class Baker : Baker<PlaceableAuthoring>
        {
            public override void Bake(PlaceableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var prefabEntity = authoring.PlaceablePrefab != null
                    ? GetEntity(authoring.PlaceablePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(entity, new PlaceableConfig
                {
                    PlaceablePrefab = prefabEntity,
                    MaxPlacementRange = authoring.MaxPlacementRange,
                    GridSnap = authoring.GridSnap,
                    MaxSurfaceAngle = authoring.MaxSurfaceAngle,
                    Validation = authoring.Validation,
                    OverlapCheckRadius = authoring.OverlapCheckRadius
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Placement range (cyan wire sphere)
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, MaxPlacementRange);

            // Grid visualization (if snapping enabled)
            if (GridSnap > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0.2f, 0.2f);
                Vector3 center = transform.position;
                int gridLines = 5;
                for (int x = -gridLines; x <= gridLines; x++)
                {
                    for (int z = -gridLines; z <= gridLines; z++)
                    {
                        Vector3 gridPos = new Vector3(
                            Mathf.Floor(center.x / GridSnap + x) * GridSnap,
                            center.y,
                            Mathf.Floor(center.z / GridSnap + z) * GridSnap);
                        Gizmos.DrawWireCube(gridPos, new Vector3(GridSnap, 0.02f, GridSnap));
                    }
                }
            }
        }
#endif
    }
}
