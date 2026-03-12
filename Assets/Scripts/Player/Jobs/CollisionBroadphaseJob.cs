using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Player.Components;

namespace DIG.Player.Jobs
{
    /// <summary>
    /// Epic 7.7.5: Broadphase job - queries spatial hash to find candidate collision pairs.
    /// 
    /// For each player with Simulate tag, queries their cell and optionally
    /// neighboring cells (based on quality settings) to find nearby players.
    /// Outputs candidate pairs for narrowphase validation.
    /// 
    /// Performance: O(N*k) where k = average players per cell neighborhood.
    /// Runs parallel across players using IJobParallelFor.
    /// 
    /// Epic 7.7.7: FloatMode.Fast enables fused multiply-add and approximate sqrt for SIMD.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CollisionBroadphaseJob : IJobParallelFor
    {
        // Player position data (SoA layout for cache efficiency)
        // Epic 7.7.5: [NoAlias] enables Burst auto-vectorization
        [ReadOnly, NoAlias] public NativeArray<PlayerPositionData> PlayerPositions;
        
        // Spatial hash grid data
        [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, Entity> CellToEntities;
        
        // Entity to index lookup
        [ReadOnly, NoAlias] public NativeHashMap<Entity, int> EntityToIndex;
        
        // Grid configuration
        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public float2 WorldOffset;
        
        // Quality settings
        public bool QueryNeighborCells;  // True = 3x3 neighborhood, False = same cell only
        
        // Output: thread-safe collision pair writer
        [WriteOnly, NoAlias]
        public NativeQueue<CollisionPair>.ParallelWriter CollisionPairsWriter;
        
        // Shared set to track checked pairs (read-only after initialization)
        // Note: We can't use NativeHashSet.ParallelWriter with Contains, 
        // so we use a different approach - each pair is added with minIdx/maxIdx ordering
        // and duplicates are filtered in narrowphase
        
        public void Execute(int playerIndex)
        {
            var playerA = PlayerPositions[playerIndex];
            
            // Only process players with Simulate tag
            if (!playerA.HasSimulate)
                return;
            
            // Skip if on cooldown
            if (playerA.IsOnCooldown)
                return;
            
            // Skip if staggered/knocked down
            if (playerA.IsStaggeredOrKnockedDown)
                return;
            
            // Skip if has grace period
            if (playerA.HasGracePeriod)
                return;
            
            // Calculate cell index for player A
            int cellIndex = GetCellIndex(playerA.Position);
            if (cellIndex < 0)
                return;
            
            // Get cells to query (same cell or 3x3 neighborhood)
            var cellsToQuery = new NativeList<int>(9, Allocator.Temp);
            
            if (QueryNeighborCells)
            {
                GetNeighborCells(cellIndex, ref cellsToQuery);
            }
            else
            {
                cellsToQuery.Add(cellIndex);
            }
            
            // Query each cell
            for (int c = 0; c < cellsToQuery.Length; c++)
            {
                int queryCellIndex = cellsToQuery[c];
                
                if (CellToEntities.TryGetFirstValue(queryCellIndex, out Entity otherEntity, out var iterator))
                {
                    do
                    {
                        // Skip self
                        if (otherEntity == playerA.Entity)
                            continue;
                        
                        // Get other player's index
                        if (!EntityToIndex.TryGetValue(otherEntity, out int otherIndex))
                            continue;
                        
                        // Create ordered pair (lower index first) to avoid duplicates
                        int minIdx = math.min(playerIndex, otherIndex);
                        int maxIdx = math.max(playerIndex, otherIndex);
                        
                        // Only add if we are the "owner" of this pair (playerIndex is minIdx)
                        // This ensures each pair is only added once
                        if (playerIndex != minIdx)
                            continue;
                        
                        // Output candidate pair
                        CollisionPairsWriter.Enqueue(new CollisionPair
                        {
                            EntityA = playerA.Entity,
                            EntityB = otherEntity,
                            IndexA = minIdx,
                            IndexB = maxIdx,
                            CellIndex = queryCellIndex
                        });
                        
                    } while (CellToEntities.TryGetNextValue(out otherEntity, ref iterator));
                }
            }
            
            cellsToQuery.Dispose();
        }
        
        private int GetCellIndex(float3 worldPosition)
        {
            float offsetX = worldPosition.x + WorldOffset.x;
            float offsetZ = worldPosition.z + WorldOffset.y;
            
            int cellX = (int)(offsetX / CellSize);
            int cellZ = (int)(offsetZ / CellSize);
            
            if (cellX < 0 || cellX >= GridWidth || cellZ < 0 || cellZ >= GridHeight)
                return -1;
            
            return cellX + cellZ * GridWidth;
        }
        
        private void GetNeighborCells(int cellIndex, ref NativeList<int> neighbors)
        {
            int cellX = cellIndex % GridWidth;
            int cellZ = cellIndex / GridWidth;
            
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cellX + dx;
                    int nz = cellZ + dz;
                    
                    if (nx >= 0 && nx < GridWidth && nz >= 0 && nz < GridHeight)
                    {
                        neighbors.Add(nx + nz * GridWidth);
                    }
                }
            }
        }
    }
}
