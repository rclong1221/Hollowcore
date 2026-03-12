using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace DIG.Swarm.Components
{
    // ──────────────────────────────────────────────
    // EPIC 16.2 Phase 1: Flow Field Infrastructure
    // ──────────────────────────────────────────────

    /// <summary>
    /// Single cell in the flow field grid.
    /// Direction points toward nearest player. Cost encodes terrain passability.
    /// </summary>
    public struct FlowFieldCell
    {
        /// <summary>Normalized XZ direction toward nearest player.</summary>
        public float2 Direction;
        /// <summary>Distance to nearest player (for intensity falloff).</summary>
        public float Distance;
        /// <summary>Terrain cost (0=passable, 255=impassable).</summary>
        public byte Cost;
        /// <summary>Flags: 1=wall, 2=cliff, 4=water.</summary>
        public byte Flags;
    }

    /// <summary>
    /// Singleton component for flow field grid metadata.
    /// Actual cell data stored in SwarmFlowFieldData static class.
    /// </summary>
    public struct FlowFieldGrid : IComponentData
    {
        public int GridWidth;
        public int GridHeight;
        public float CellSize;
        public float3 WorldOrigin;
        public float UpdateInterval;
        public float TimeSinceLastUpdate;
        public bool IsBuilt;
    }

    /// <summary>
    /// Static holder for flow field NativeArray data.
    /// NativeContainers cannot be stored in ECS components.
    /// Follows the InteractableSpatialGridData pattern.
    /// </summary>
    public static class SwarmFlowFieldData
    {
        public static NativeArray<FlowFieldCell> Cells;
        public static bool IsInitialized;

        public static void Initialize(int cellCount)
        {
            if (IsInitialized && Cells.IsCreated)
                Cells.Dispose();

            Cells = new NativeArray<FlowFieldCell>(cellCount, Allocator.Persistent);
            IsInitialized = true;
        }

        public static void Dispose()
        {
            if (IsInitialized && Cells.IsCreated)
            {
                Cells.Dispose();
                IsInitialized = false;
            }
        }

        /// <summary>
        /// Get cell index from world position.
        /// Returns -1 if outside grid bounds.
        /// </summary>
        public static int GetCellIndex(float3 worldPos, in FlowFieldGrid grid)
        {
            int x = (int)math.floor((worldPos.x - grid.WorldOrigin.x) / grid.CellSize);
            int z = (int)math.floor((worldPos.z - grid.WorldOrigin.z) / grid.CellSize);

            if (x < 0 || x >= grid.GridWidth || z < 0 || z >= grid.GridHeight)
                return -1;

            return z * grid.GridWidth + x;
        }

        /// <summary>
        /// Get world position of cell center from cell index.
        /// </summary>
        public static float3 GetCellCenter(int cellIndex, in FlowFieldGrid grid)
        {
            int x = cellIndex % grid.GridWidth;
            int z = cellIndex / grid.GridWidth;
            return new float3(
                grid.WorldOrigin.x + (x + 0.5f) * grid.CellSize,
                grid.WorldOrigin.y,
                grid.WorldOrigin.z + (z + 0.5f) * grid.CellSize
            );
        }

        /// <summary>
        /// Sample flow direction at a world position with bilinear interpolation.
        /// Returns zero if outside grid or uninitialized.
        /// </summary>
        public static float2 SampleDirection(float3 worldPos, in FlowFieldGrid grid)
        {
            if (!IsInitialized || !Cells.IsCreated)
                return float2.zero;

            float fx = (worldPos.x - grid.WorldOrigin.x) / grid.CellSize - 0.5f;
            float fz = (worldPos.z - grid.WorldOrigin.z) / grid.CellSize - 0.5f;

            int x0 = (int)math.floor(fx);
            int z0 = (int)math.floor(fz);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            x0 = math.clamp(x0, 0, grid.GridWidth - 1);
            x1 = math.clamp(x1, 0, grid.GridWidth - 1);
            z0 = math.clamp(z0, 0, grid.GridHeight - 1);
            z1 = math.clamp(z1, 0, grid.GridHeight - 1);

            float tx = math.frac(fx);
            float tz = math.frac(fz);

            var d00 = Cells[z0 * grid.GridWidth + x0].Direction;
            var d10 = Cells[z0 * grid.GridWidth + x1].Direction;
            var d01 = Cells[z1 * grid.GridWidth + x0].Direction;
            var d11 = Cells[z1 * grid.GridWidth + x1].Direction;

            var d0 = math.lerp(d00, d10, tx);
            var d1 = math.lerp(d01, d11, tx);
            var dir = math.lerp(d0, d1, tz);

            float len = math.length(dir);
            return len > 0.001f ? dir / len : float2.zero;
        }

        /// <summary>
        /// Check if a cell is passable (cost below threshold).
        /// </summary>
        public static bool IsPassable(int cellIndex)
        {
            if (!IsInitialized || !Cells.IsCreated || cellIndex < 0 || cellIndex >= Cells.Length)
                return false;
            return Cells[cellIndex].Cost < 200;
        }
    }

    /// <summary>
    /// Static holder for swarm particle spatial grid (used by separation system).
    /// </summary>
    public static class SwarmSpatialData
    {
        public static NativeParallelMultiHashMap<int, int> CellToParticleIndex;
        public static bool IsInitialized;

        public static void Initialize(int capacity)
        {
            if (IsInitialized && CellToParticleIndex.IsCreated)
                CellToParticleIndex.Dispose();

            CellToParticleIndex = new NativeParallelMultiHashMap<int, int>(capacity, Allocator.Persistent);
            IsInitialized = true;
        }

        public static void Dispose()
        {
            if (IsInitialized && CellToParticleIndex.IsCreated)
            {
                CellToParticleIndex.Dispose();
                IsInitialized = false;
            }
        }

        public static int GetCellIndex(float3 position, float cellSize)
        {
            int x = (int)math.floor(position.x / cellSize);
            int z = (int)math.floor(position.z / cellSize);
            return x + z * 10000; // Simple hash, large grid
        }
    }
}
