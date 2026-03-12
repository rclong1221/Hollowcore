using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.7.3: Spatial hash grid for O(N) collision detection.
    /// 
    /// Partitions players into grid cells for efficient proximity queries.
    /// Instead of checking all N*(N-1)/2 player pairs (O(N²)), we only check
    /// players within the same cell and 8 adjacent cells (O(N*k) where k~10).
    /// 
    /// Grid cell size is 2x collision radius (3m) to ensure colliding players
    /// are always in the same or adjacent cells.
    /// </summary>
    public struct SpatialHashGrid : IComponentData
    {
        /// <summary>
        /// Cell size in world units. Should be >= 2x max collision radius.
        /// Default: 3m (collision radius is ~0.8m, diameter 1.6m, so 3m is safe)
        /// </summary>
        public float CellSize;
        
        /// <summary>
        /// Grid width in cells (X axis). Used for hash function.
        /// Default: 100 cells = 300m world width centered on origin.
        /// </summary>
        public int GridWidth;
        
        /// <summary>
        /// Grid height in cells (Z axis). Used for bounds checking.
        /// Default: 100 cells = 300m world height centered on origin.
        /// </summary>
        public int GridHeight;
        
        /// <summary>
        /// Offset to apply before hashing (to handle negative coordinates).
        /// Default: half of grid size to center grid on origin.
        /// </summary>
        public float2 WorldOffset;
        
        /// <summary>
        /// Whether the grid has been populated this frame.
        /// Reset to false at start of frame, set to true after population.
        /// </summary>
        public bool IsPopulated;
        
        /// <summary>
        /// Number of players inserted into grid this frame.
        /// Used for debugging and profiling.
        /// </summary>
        public int PlayerCount;
        
        /// <summary>
        /// Create grid with default settings for typical game scenarios.
        /// </summary>
        public static SpatialHashGrid CreateDefault()
        {
            return new SpatialHashGrid
            {
                CellSize = 3.0f,          // 3m cells (2x collision diameter)
                GridWidth = 100,          // 100 cells = 300m
                GridHeight = 100,         // 100 cells = 300m
                WorldOffset = new float2(150f, 150f), // Center on origin
                IsPopulated = false,
                PlayerCount = 0
            };
        }
        
        /// <summary>
        /// Create grid with custom settings.
        /// </summary>
        public static SpatialHashGrid Create(float cellSize, int gridWidth, int gridHeight)
        {
            return new SpatialHashGrid
            {
                CellSize = cellSize,
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                WorldOffset = new float2(gridWidth * cellSize * 0.5f, gridHeight * cellSize * 0.5f),
                IsPopulated = false,
                PlayerCount = 0
            };
        }
        
        /// <summary>
        /// Calculate cell index from world position.
        /// Returns -1 if position is outside grid bounds.
        /// </summary>
        public int GetCellIndex(float3 worldPosition)
        {
            // Apply offset to handle negative coordinates
            float offsetX = worldPosition.x + WorldOffset.x;
            float offsetZ = worldPosition.z + WorldOffset.y;
            
            // Calculate cell coordinates
            int cellX = (int)(offsetX / CellSize);
            int cellZ = (int)(offsetZ / CellSize);
            
            // Bounds check
            if (cellX < 0 || cellX >= GridWidth || cellZ < 0 || cellZ >= GridHeight)
                return -1;
            
            // Convert 2D cell coords to 1D index
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
        /// Get cell index from cell coordinates.
        /// </summary>
        public int GetCellIndexFromCoords(int2 coords)
        {
            if (coords.x < 0 || coords.x >= GridWidth || coords.y < 0 || coords.y >= GridHeight)
                return -1;
            return coords.x + coords.y * GridWidth;
        }
        
        /// <summary>
        /// Get indices of cell and its 8 neighbors (3x3 neighborhood).
        /// Returns actual count of valid neighbors (may be less than 9 at edges).
        /// </summary>
        public int GetNeighborCells(int cellIndex, ref NativeList<int> neighborCells)
        {
            int2 coords = GetCellCoords(cellIndex);
            int count = 0;
            
            // Check 3x3 neighborhood
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = coords.x + dx;
                    int nz = coords.y + dz;
                    
                    if (nx >= 0 && nx < GridWidth && nz >= 0 && nz < GridHeight)
                    {
                        neighborCells.Add(nx + nz * GridWidth);
                        count++;
                    }
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Get indices of cell and its 8 neighbors (fixed array version).
        /// </summary>
        public int GetNeighborCellsFixed(int cellIndex, ref NeighborCells neighbors)
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
    public struct NeighborCells
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
    /// Static holder for the spatial hash grid data.
    /// NativeContainers cannot be stored in ECS components, so we use a static class.
    /// The PlayerSpatialHashSystem manages the lifecycle of this data.
    /// </summary>
    public static class SpatialHashGridData
    {
        /// <summary>
        /// The NativeParallelMultiHashMap storing cell→entity mappings.
        /// Key = cell index, Value = entity in that cell.
        /// </summary>
        public static NativeParallelMultiHashMap<int, Entity> CellToEntities;
        
        /// <summary>
        /// Epic 7.7.7: Sorted list of occupied cell indices for scan-line iteration.
        /// Cells are stored in row-major order (0,0), (1,0), (2,0), ..., (0,1), (1,1), etc.
        /// This enables cache-friendly iteration across the spatial grid.
        /// </summary>
        public static NativeList<int> OccupiedCells;
        
        /// <summary>
        /// Whether the grid data has been initialized.
        /// </summary>
        public static bool IsInitialized;
        
        /// <summary>
        /// Initialize the grid with given capacity.
        /// </summary>
        public static void Initialize(int capacity)
        {
            if (IsInitialized) return;
            
            CellToEntities = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            OccupiedCells = new NativeList<int>(128, Allocator.Persistent);
            IsInitialized = true;
        }
        
        /// <summary>
        /// Dispose the grid data.
        /// </summary>
        public static void Dispose()
        {
            if (!IsInitialized) return;
            
            if (CellToEntities.IsCreated)
                CellToEntities.Dispose();
            
            if (OccupiedCells.IsCreated)
                OccupiedCells.Dispose();
            
            IsInitialized = false;
        }
        
        /// <summary>
        /// Clear the grid for a new frame.
        /// </summary>
        public static void Clear()
        {
            if (IsInitialized && CellToEntities.IsCreated)
                CellToEntities.Clear();
            
            if (IsInitialized && OccupiedCells.IsCreated)
                OccupiedCells.Clear();
        }
        
        /// <summary>
        /// Ensure the grid has enough capacity.
        /// </summary>
        public static void EnsureCapacity(int minCapacity)
        {
            if (IsInitialized && CellToEntities.IsCreated && CellToEntities.Capacity < minCapacity)
            {
                CellToEntities.Capacity = minCapacity;
            }
        }
        
        /// <summary>
        /// Epic 7.7.7: Build sorted list of occupied cells for scan-line iteration.
        /// Call this after spatial hash population is complete.
        /// Cells are sorted in row-major order for cache-friendly memory access patterns.
        /// </summary>
        public static void BuildOccupiedCellList()
        {
            if (!IsInitialized || !CellToEntities.IsCreated || !OccupiedCells.IsCreated)
                return;
            
            OccupiedCells.Clear();
            
            // Get unique cell keys from the hashmap
            var keys = CellToEntities.GetKeyArray(Allocator.Temp);
            
            // Add unique keys to occupied cells list
            var seenCells = new NativeHashSet<int>(keys.Length, Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (seenCells.Add(keys[i]))
                {
                    OccupiedCells.Add(keys[i]);
                }
            }
            
            // Sort in ascending order (scan-line/row-major order)
            // Cell index = cellX + cellZ * GridWidth, so sorting by index gives scan-line order
            OccupiedCells.Sort();
            
            keys.Dispose();
            seenCells.Dispose();
        }
        
        /// <summary>
        /// Epic 7.7.7: Get the number of occupied cells for iteration.
        /// </summary>
        public static int GetOccupiedCellCount()
        {
            return IsInitialized && OccupiedCells.IsCreated ? OccupiedCells.Length : 0;
        }
        
        /// <summary>
        /// Epic 7.7.7: Get occupied cell index at position for scan-line iteration.
        /// Returns the cell index (not the position in the list).
        /// </summary>
        public static int GetOccupiedCellAt(int position)
        {
            if (!IsInitialized || !OccupiedCells.IsCreated || position < 0 || position >= OccupiedCells.Length)
                return -1;
            
            return OccupiedCells[position];
        }
    }
}
