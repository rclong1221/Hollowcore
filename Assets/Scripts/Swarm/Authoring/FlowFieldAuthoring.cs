using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Swarm.Components;

namespace DIG.Swarm.Authoring
{
    /// <summary>
    /// EPIC 16.2 Phase 1: Authoring component for the flow field grid.
    /// Place on a single GameObject in your subscene. The grid is centered on this transform.
    /// </summary>
    public class FlowFieldAuthoring : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        [Tooltip("Grid width in cells (100 @ 2m = 200m coverage)")]
        public int GridWidth = 100;
        [Tooltip("Grid height in cells (100 @ 2m = 200m coverage)")]
        public int GridHeight = 100;
        [Tooltip("World units per cell (smaller = more precise, more expensive)")]
        public float CellSize = 2f;

        [Header("Update")]
        [Tooltip("Seconds between flow field rebuilds (0.5 = 2 rebuilds/sec)")]
        public float UpdateInterval = 0.5f;

        [Header("Debug")]
        [Tooltip("Show flow field gizmos in Scene view")]
        public bool ShowGizmos = false;
        [Tooltip("Gizmo arrow length")]
        public float GizmoArrowLength = 0.8f;

        public class Baker : Baker<FlowFieldAuthoring>
        {
            public override void Bake(FlowFieldAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.WorldSpace);
                var transform = authoring.transform;

                // Center grid on authoring transform
                float3 origin = (float3)transform.position - new float3(
                    authoring.GridWidth * authoring.CellSize * 0.5f,
                    0f,
                    authoring.GridHeight * authoring.CellSize * 0.5f
                );

                AddComponent(entity, new FlowFieldGrid
                {
                    GridWidth = authoring.GridWidth,
                    GridHeight = authoring.GridHeight,
                    CellSize = authoring.CellSize,
                    WorldOrigin = origin,
                    UpdateInterval = authoring.UpdateInterval,
                    TimeSinceLastUpdate = 999f, // Force immediate build
                    IsBuilt = false
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!ShowGizmos) return;

            float3 origin = (float3)transform.position - new float3(
                GridWidth * CellSize * 0.5f,
                0f,
                GridHeight * CellSize * 0.5f
            );

            // Draw grid bounds
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            Vector3 size = new Vector3(GridWidth * CellSize, 0.1f, GridHeight * CellSize);
            Vector3 center = (Vector3)origin + size * 0.5f;
            Gizmos.DrawWireCube(center, size);

            // Draw cell grid (limited to avoid performance issues)
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.1f);
            int maxCells = Mathf.Min(GridWidth, 50);
            int maxRows = Mathf.Min(GridHeight, 50);
            float stepX = (float)GridWidth / maxCells;
            float stepZ = (float)GridHeight / maxRows;

            for (int x = 0; x <= maxCells; x++)
            {
                int cellX = Mathf.Min((int)(x * stepX), GridWidth);
                float worldX = origin.x + cellX * CellSize;
                Gizmos.DrawLine(
                    new Vector3(worldX, origin.y, origin.z),
                    new Vector3(worldX, origin.y, origin.z + GridHeight * CellSize)
                );
            }
            for (int z = 0; z <= maxRows; z++)
            {
                int cellZ = Mathf.Min((int)(z * stepZ), GridHeight);
                float worldZ = origin.z + cellZ * CellSize;
                Gizmos.DrawLine(
                    new Vector3(origin.x, origin.y, worldZ),
                    new Vector3(origin.x + GridWidth * CellSize, origin.y, worldZ)
                );
            }

            // Draw flow field arrows if data exists at runtime
            if (Application.isPlaying && SwarmFlowFieldData.IsInitialized && SwarmFlowFieldData.Cells.IsCreated)
            {
                var grid = new FlowFieldGrid
                {
                    GridWidth = GridWidth, GridHeight = GridHeight,
                    CellSize = CellSize, WorldOrigin = origin
                };

                int skip = Mathf.Max(1, GridWidth / 40);
                for (int z = 0; z < GridHeight; z += skip)
                {
                    for (int x = 0; x < GridWidth; x += skip)
                    {
                        int idx = z * GridWidth + x;
                        if (idx >= SwarmFlowFieldData.Cells.Length) continue;

                        var cell = SwarmFlowFieldData.Cells[idx];
                        if (math.lengthsq(cell.Direction) < 0.001f) continue;

                        float3 cellCenter = SwarmFlowFieldData.GetCellCenter(idx, grid);
                        float3 dir = new float3(cell.Direction.x, 0f, cell.Direction.y);

                        // Color by distance (red=close, blue=far)
                        float t = Mathf.Clamp01(cell.Distance / 50f);
                        Gizmos.color = Color.Lerp(Color.red, Color.blue, t);
                        Gizmos.DrawRay((Vector3)cellCenter, (Vector3)(dir * GizmoArrowLength));
                    }
                }
            }
        }
#endif
    }
}
