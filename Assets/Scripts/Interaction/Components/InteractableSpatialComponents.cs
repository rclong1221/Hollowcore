using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Interaction
{
    /// <summary>
    /// EPIC 16.1 Phase 1: Spatial hash grid for O(1) interactable detection.
    ///
    /// Partitions interactables into grid cells for efficient proximity queries.
    /// Instead of checking all N interactables per player (O(N*M)), we only check
    /// interactables within the player's cell and 8 adjacent cells (~50 candidates max).
    ///
    /// Cell size is 4m to cover typical interaction radii (2-5m).
    /// Follows the same pattern as DIG.Player.Components.SpatialHashGrid.
    /// </summary>
    public struct InteractableSpatialGrid : IComponentData
    {
        /// <summary>
        /// Cell size in world units. Should be >= max interaction radius.
        /// Default: 4m (covers typical 2-5m interaction radii).
        /// </summary>
        public float CellSize;

        /// <summary>
        /// Grid width in cells (X axis). Used for hash function.
        /// Default: 200 cells = 800m world width centered on origin.
        /// </summary>
        public int GridWidth;

        /// <summary>
        /// Grid height in cells (Z axis). Used for bounds checking.
        /// Default: 200 cells = 800m world height centered on origin.
        /// </summary>
        public int GridHeight;

        /// <summary>
        /// Offset to apply before hashing (to handle negative coordinates).
        /// Default: half of grid size to center grid on origin.
        /// </summary>
        public float2 WorldOffset;

        /// <summary>
        /// Whether the grid has been populated this frame.
        /// </summary>
        public bool IsPopulated;

        /// <summary>
        /// Number of interactables inserted into grid this frame.
        /// </summary>
        public int EntityCount;

        public static InteractableSpatialGrid CreateDefault()
        {
            return new InteractableSpatialGrid
            {
                CellSize = 4.0f,
                GridWidth = 200,
                GridHeight = 200,
                WorldOffset = new float2(400f, 400f), // Center on origin (200 * 4 * 0.5)
                IsPopulated = false,
                EntityCount = 0
            };
        }

        /// <summary>
        /// Calculate cell index from world position.
        /// Returns -1 if position is outside grid bounds.
        /// </summary>
        public int GetCellIndex(float3 worldPosition)
        {
            float offsetX = worldPosition.x + WorldOffset.x;
            float offsetZ = worldPosition.z + WorldOffset.y;

            int cellX = (int)(offsetX / CellSize);
            int cellZ = (int)(offsetZ / CellSize);

            if (cellX < 0 || cellX >= GridWidth || cellZ < 0 || cellZ >= GridHeight)
                return -1;

            return cellX + cellZ * GridWidth;
        }

        /// <summary>
        /// Calculate cell index from world position (static version for jobs).
        /// </summary>
        public static int GetCellIndexStatic(float3 worldPosition, float cellSize, int gridWidth, int gridHeight, float2 worldOffset)
        {
            float offsetX = worldPosition.x + worldOffset.x;
            float offsetZ = worldPosition.z + worldOffset.y;

            int cellX = (int)(offsetX / cellSize);
            int cellZ = (int)(offsetZ / cellSize);

            if (cellX < 0 || cellX >= gridWidth || cellZ < 0 || cellZ >= gridHeight)
                return -1;

            return cellX + cellZ * gridWidth;
        }

        /// <summary>
        /// Get cell coordinates from cell index.
        /// </summary>
        public int2 GetCellCoords(int cellIndex)
        {
            return new int2(cellIndex % GridWidth, cellIndex / GridWidth);
        }

        /// <summary>
        /// Get indices of cell and its 8 neighbors (3x3 neighborhood).
        /// Uses fixed-size struct to avoid allocation.
        /// </summary>
        public int GetNeighborCellsFixed(int cellIndex, ref InteractableNeighborCells neighbors)
        {
            int2 coords = GetCellCoords(cellIndex);
            int count = 0;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = coords.x + dx;
                    int nz = coords.y + dz;

                    if (nx >= 0 && nx < GridWidth && nz >= 0 && nz < GridHeight)
                    {
                        neighbors[count] = nx + nz * GridWidth;
                        count++;
                    }
                }
            }

            neighbors.Count = count;
            return count;
        }
    }

    /// <summary>
    /// Fixed-size array for neighbor cell indices (max 9 = 3x3).
    /// Avoids allocation for neighbor queries.
    /// </summary>
    public struct InteractableNeighborCells
    {
        public int Cell0, Cell1, Cell2, Cell3, Cell4, Cell5, Cell6, Cell7, Cell8;
        public int Count;

        public int this[int index]
        {
            get
            {
                return index switch
                {
                    0 => Cell0,
                    1 => Cell1,
                    2 => Cell2,
                    3 => Cell3,
                    4 => Cell4,
                    5 => Cell5,
                    6 => Cell6,
                    7 => Cell7,
                    8 => Cell8,
                    _ => -1
                };
            }
            set
            {
                switch (index)
                {
                    case 0: Cell0 = value; break;
                    case 1: Cell1 = value; break;
                    case 2: Cell2 = value; break;
                    case 3: Cell3 = value; break;
                    case 4: Cell4 = value; break;
                    case 5: Cell5 = value; break;
                    case 6: Cell6 = value; break;
                    case 7: Cell7 = value; break;
                    case 8: Cell8 = value; break;
                }
            }
        }
    }

    /// <summary>
    /// EPIC 15.23: Tracks the last known grid cell index for an interactable entity.
    /// Used by InteractableSpatialMapSystem to skip hash map rebuilds when no entity has moved.
    /// Entities without this component trigger a full rebuild (backward compatible).
    /// </summary>
    public struct InteractableSpatialIdx : IComponentData
    {
        /// <summary>
        /// Grid cell index from last rebuild. -1 = uninitialized.
        /// </summary>
        public int LastCellIndex;
    }

    /// <summary>
    /// Static holder for the interactable spatial hash grid data.
    /// NativeContainers cannot be stored in ECS components, so we use a static class.
    /// The InteractableSpatialMapSystem manages the lifecycle of this data.
    /// </summary>
    public static class InteractableSpatialGridData
    {
        /// <summary>
        /// The NativeParallelMultiHashMap storing cell -> entity mappings.
        /// Key = cell index, Value = interactable entity in that cell.
        /// </summary>
        public static NativeParallelMultiHashMap<int, Entity> CellToEntities;

        /// <summary>
        /// Whether the grid data has been initialized.
        /// </summary>
        public static bool IsInitialized;

        public static void Initialize(int capacity)
        {
            if (IsInitialized) return;

            CellToEntities = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            IsInitialized = true;
        }

        public static void Dispose()
        {
            if (!IsInitialized) return;

            if (CellToEntities.IsCreated)
                CellToEntities.Dispose();

            IsInitialized = false;
        }

        public static void Clear()
        {
            if (IsInitialized && CellToEntities.IsCreated)
                CellToEntities.Clear();
        }

        public static void EnsureCapacity(int minCapacity)
        {
            if (IsInitialized && CellToEntities.IsCreated && CellToEntities.Capacity < minCapacity)
            {
                CellToEntities.Capacity = minCapacity;
            }
        }
    }
}
