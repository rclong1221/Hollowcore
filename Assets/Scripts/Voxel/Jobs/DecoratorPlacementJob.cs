using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// Burst-compiled job to determine decorator placements from surface points.
    /// Applies spacing, depth, probability, and biome constraints.
    /// 
    /// OPTIMIZATION 10.5.8: Uses spatial hashing for O(n) spacing checks instead of O(n²).
    /// </summary>
    [BurstCompile]
    public struct DecoratorPlacementJob : IJob
    {
        [ReadOnly] public NativeArray<DecoratorService.SurfacePoint> Surfaces;
        [ReadOnly] public NativeArray<DecoratorService.DecoratorParams> DecoratorParams;
        [ReadOnly] public NativeList<byte> FloorDecorators;
        [ReadOnly] public NativeList<byte> CeilingDecorators;
        [ReadOnly] public NativeList<byte> WallDecorators;
        [ReadOnly] public uint Seed;
        [ReadOnly] public float ChunkDepth;
        [ReadOnly] public int MaxDecorators;
        
        public NativeList<DecoratorService.DecoratorPlacement> Placements;
        
        // Spatial hash cell size - should be >= largest min spacing
        private const float CELL_SIZE = 4f;
        private const int GRID_SIZE = 16; // 64m / 4m = 16 cells per axis
        
        public void Execute()
        {
            if (Surfaces.Length == 0) return;
            
            var random = new Unity.Mathematics.Random(Seed);
            
            // OPTIMIZATION 10.5.8: Spatial hash grid for O(n) spacing checks
            // Each cell stores indices into usedPositions
            var spatialHash = new NativeParallelMultiHashMap<int, int>(256, Allocator.Temp);
            var usedPositions = new NativeList<float3>(64, Allocator.Temp);
            
            // Process surfaces
            for (int s = 0; s < Surfaces.Length && Placements.Length < MaxDecorators; s++)
            {
                var surface = Surfaces[s];
                
                // Get valid decorators for this surface type
                var validDecorators = GetDecoratorsForSurface(surface.Type);
                if (validDecorators.Length == 0) continue;
                
                // Try each decorator type
                for (int d = 0; d < validDecorators.Length && Placements.Length < MaxDecorators; d++)
                {
                    byte decoratorID = validDecorators[d];
                    if (decoratorID == 0 || decoratorID >= DecoratorParams.Length) continue;
                    
                    var param = DecoratorParams[decoratorID];
                    
                    // Check depth constraints
                    float absDepth = math.abs(ChunkDepth);
                    if (absDepth < param.MinDepth || absDepth > param.MaxDepth)
                        continue;
                    
                    // Check cave radius
                    if (surface.CaveRadius < param.MinCaveRadius)
                        continue;
                    
                    // Check spacing with spatial hash
                    if (!CheckSpacingSpatialHash(surface.Position, param.MinSpacing, spatialHash, usedPositions))
                        continue;
                    
                    // Random spawn chance
                    if (random.NextFloat() > param.SpawnProbability)
                        continue;
                    
                    // Calculate scale
                    float scale;
                    if (param.ScaleWithCaveSize)
                    {
                        float t = math.saturate(surface.CaveRadius / 50f);
                        scale = math.lerp(param.MinScale, param.MaxScale, t);
                    }
                    else
                    {
                        scale = random.NextFloat(param.MinScale, param.MaxScale);
                    }
                    
                    // Calculate rotation
                    float yRotation = param.RandomYRotation 
                        ? random.NextFloat() * math.PI * 2f 
                        : 0f;
                    
                    // Add placement
                    Placements.Add(new DecoratorService.DecoratorPlacement
                    {
                        DecoratorID = decoratorID,
                        Position = surface.Position,
                        Normal = surface.Normal,
                        Scale = scale,
                        YRotation = yRotation,
                        RandomSeed = random.NextUInt()
                    });
                    
                    // Add to spatial hash
                    int posIndex = usedPositions.Length;
                    usedPositions.Add(surface.Position);
                    int hash = GetSpatialHash(surface.Position);
                    spatialHash.Add(hash, posIndex);
                    
                    // Only one decorator per surface point
                    break;
                }
            }
            
            spatialHash.Dispose();
            usedPositions.Dispose();
        }
        
        private NativeList<byte> GetDecoratorsForSurface(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.Floor:
                    return FloorDecorators;
                case SurfaceType.Ceiling:
                    return CeilingDecorators;
                default:
                    return WallDecorators;
            }
        }
        
        /// <summary>
        /// Get spatial hash key for a position.
        /// </summary>
        private int GetSpatialHash(float3 pos)
        {
            // Convert to grid coordinates
            int cellX = (int)math.floor(pos.x / CELL_SIZE) & (GRID_SIZE - 1);
            int cellY = (int)math.floor(pos.y / CELL_SIZE) & (GRID_SIZE - 1);
            int cellZ = (int)math.floor(pos.z / CELL_SIZE) & (GRID_SIZE - 1);
            
            // Combine into single hash
            return cellX + cellY * GRID_SIZE + cellZ * GRID_SIZE * GRID_SIZE;
        }
        
        /// <summary>
        /// Check spacing using spatial hash - only checks nearby cells.
        /// </summary>
        private bool CheckSpacingSpatialHash(float3 pos, float minSpacing, 
            NativeParallelMultiHashMap<int, int> spatialHash, NativeList<float3> usedPositions)
        {
            float minSpacingSq = minSpacing * minSpacing;
            
            // How many cells to check based on min spacing
            int cellRadius = (int)math.ceil(minSpacing / CELL_SIZE);
            
            int baseCellX = (int)math.floor(pos.x / CELL_SIZE);
            int baseCellY = (int)math.floor(pos.y / CELL_SIZE);
            int baseCellZ = (int)math.floor(pos.z / CELL_SIZE);
            
            // Check neighboring cells
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        int cellX = (baseCellX + dx) & (GRID_SIZE - 1);
                        int cellY = (baseCellY + dy) & (GRID_SIZE - 1);
                        int cellZ = (baseCellZ + dz) & (GRID_SIZE - 1);
                        int hash = cellX + cellY * GRID_SIZE + cellZ * GRID_SIZE * GRID_SIZE;
                        
                        // Check all positions in this cell
                        if (spatialHash.TryGetFirstValue(hash, out int posIndex, out var iterator))
                        {
                            do
                            {
                                if (math.distancesq(pos, usedPositions[posIndex]) < minSpacingSq)
                                    return false;
                            }
                            while (spatialHash.TryGetNextValue(out posIndex, ref iterator));
                        }
                    }
                }
            }
            
            return true;
        }
    }
}
