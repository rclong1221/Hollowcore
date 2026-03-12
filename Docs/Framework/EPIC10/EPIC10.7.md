# EPIC 10.7: Generation Performance

**Status**: ✅ COMPLETE  
**Priority**: CRITICAL  
**Dependencies**: EPIC 10.2 (Cave & Hollow Earth), All EPIC 10.x  
**Estimated Time**: 3-4 days

---

## Goal

Optimize generation for the **massive multi-layer world** (7km+ depth):
- **< 5ms per chunk** generation (Burst-compiled)
- **Layer-based streaming** - Only load 2-3 layers at once
- **No frame hitches** during exploration
- **Efficient memory** - Native containers, blob assets

---

## Performance Challenges

| Challenge | Scale | Solution |
|-----------|-------|----------|
| Total world depth | 7,500m | Layer-based loading |
| Chunks per hollow layer | ~60,000+ | Priority streaming |
| Hollow height | 500-1500m | Vertical LOD |
| Floor area | 4-25 km² | Horizontal LOD |
| Multiple noise samples | 5-10 per voxel | Caching + Burst |

---

## Files Created

| File | Purpose |
|------|---------|
| `Core/GenerationConfigBlobs.cs` | Burst-compatible structs |
| `Core/BlobAssetBuilder.cs` | Conversion utilities |
| `Systems/Generation/ChunkStreamingSystem.cs` | Layer streaming |
| `Systems/Generation/GenerationBudgetSystem.cs` | Frame time manager |
| `Jobs/GenerateLODChunkJob.cs` | Distant chunk generation |
| `Core/NoiseCache.cs` | Shared noise calculation |
| `Core/VoxelProfiler.cs` | Instrumentation |

---

## Technical Architecture

### Task 10.7.1: Burst-Compiled Generation Jobs
**Recommendation**: ✅ **COMPLETE**
**Implementation Notes**:
- `GenerateVoxelDataJob` is fully Burst-compiled.
- Uses `BlobAssetReference` for all configuration.
- Shared `GeologyService` lookup tables.

All generation must be fully Burst-compatible:

```csharp
[BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
public struct GenerateTerrainJob : IJobParallelFor
{
    // All inputs must be blittable (no managed types)
    [ReadOnly] public int3 ChunkWorldOrigin;
    [ReadOnly] public uint Seed;
    [ReadOnly] public int LayerIndex;
    [ReadOnly] public LayerType LayerType;
    
    // Blob assets for configuration (read-only, shared)
    [ReadOnly] public BlobAssetReference<StrataBlob> StrataData;
    [ReadOnly] public BlobAssetReference<CaveParamsBlob> CaveParams;
    [ReadOnly] public BlobAssetReference<HollowEarthBlob> HollowData;
    
    // Output arrays
    [WriteOnly] public NativeArray<float> Densities;
    [WriteOnly] public NativeArray<byte> Materials;
    
    // Batch size for parallelization
    private const int BATCH_SIZE = 64;
    
    public void Execute(int index)
    {
        int3 localPos = IndexToPos(index);
        float3 worldPos = new float3(ChunkWorldOrigin + localPos);
        float depth = -worldPos.y;
        
        float density;
        byte material;
        
        if (LayerType == Core.LayerType.Hollow)
        {
            ComputeHollowEarthVoxel(worldPos, depth, out density, out material);
        }
        else
        {
            ComputeSolidLayerVoxel(worldPos, depth, out density, out material);
        }
        
        Densities[index] = density;
        Materials[index] = material;
    }
    
    private void ComputeSolidLayerVoxel(float3 worldPos, float depth, 
        out float density, out byte material)
    {
        // Base terrain (always solid underground)
        density = 1f;
        
        // Get material from strata
        material = GetStrataMaterial(depth, worldPos);
        
        // Carve caves (subtract from density)
        ref var caves = ref CaveParams.Value;
        
        if (caves.EnableSwissCheese)
        {
            float cheeseNoise = noise.snoise(worldPos * caves.CheeseScale + Seed);
            if (cheeseNoise > caves.CheeseThreshold)
                density = -1f; // Air
        }
        
        if (density > 0 && caves.EnableSpaghetti)
        {
            float spaghetti = ComputeSpaghettiCave(worldPos);
            if (spaghetti < caves.SpaghettiWidth)
                density = -1f;
        }
        
        if (density > 0 && caves.EnableNoodles)
        {
            float noodle = ComputeNoodleCave(worldPos);
            if (noodle < caves.NoodleWidth)
                density = -1f;
        }
    }
    
    private void ComputeHollowEarthVoxel(float3 worldPos, float depth,
        out float density, out byte material)
    {
        ref var hollow = ref HollowData.Value;
        
        float relativeDepth = depth - hollow.TopDepth;
        
        // Floor terrain
        float floorHeight = ComputeFloorHeight(worldPos.xz);
        
        // Ceiling terrain
        float ceilingDepth = hollow.BottomDepth - ComputeCeilingVariation(worldPos.xz);
        
        // Below floor = solid rock
        if (depth > hollow.BottomDepth - floorHeight)
        {
            density = 1f;
            material = hollow.FloorMaterialID;
            return;
        }
        
        // Above ceiling = solid rock
        if (depth < hollow.TopDepth + hollow.CeilingVariation)
        {
            float ceilingNoise = noise.snoise(worldPos * hollow.CeilingNoiseScale);
            if (depth < hollow.TopDepth + ceilingNoise * hollow.CeilingVariation)
            {
                density = 1f;
                material = hollow.WallMaterialID;
                return;
            }
        }
        
        // Open air space
        density = -1f;
        material = 0; // Air
        
        // Check for pillars
        if (hollow.GeneratePillars)
        {
            float pillarDensity = ComputePillarDensity(worldPos);
            if (pillarDensity > 0)
            {
                density = pillarDensity;
                material = hollow.WallMaterialID;
            }
        }
        
        // Check for stalactites
        if (hollow.HasStalactites && depth < hollow.TopDepth + hollow.MaxStalactiteLength)
        {
            float stalactiteDensity = ComputeStalactiteDensity(worldPos, depth);
            density = math.max(density, stalactiteDensity);
            if (stalactiteDensity > 0) material = hollow.WallMaterialID;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeFloorHeight(float2 xz)
    {
        ref var hollow = ref HollowData.Value;
        return noise.snoise(xz * hollow.FloorNoiseScale + Seed) * hollow.FloorAmplitude;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeCeilingVariation(float2 xz)
    {
        ref var hollow = ref HollowData.Value;
        return noise.snoise(xz * hollow.CeilingNoiseScale + Seed + 5000) * hollow.CeilingVariation;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputePillarDensity(float3 pos)
    {
        ref var hollow = ref HollowData.Value;
        
        // Voronoi-like pillar placement
        float2 cell = math.floor(pos.xz * hollow.PillarFrequency);
        uint cellSeed = (uint)(cell.x * 374761 + cell.y * 668265 + Seed);
        var random = new Unity.Mathematics.Random(cellSeed);
        
        float2 pillarCenter = (cell + random.NextFloat2()) / hollow.PillarFrequency;
        float distSq = math.distancesq(pos.xz, pillarCenter);
        
        float radius = math.lerp(hollow.MinPillarRadius, hollow.MaxPillarRadius, 
                                  random.NextFloat());
        
        if (distSq < radius * radius)
            return 1f - math.sqrt(distSq) / radius; // Rounded pillar
        
        return -1f;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeSpaghettiCave(float3 pos)
    {
        ref var caves = ref CaveParams.Value;
        
        float noiseXY = noise.snoise(new float3(pos.x, pos.y, Seed) * caves.SpaghettiScale);
        float noiseXZ = noise.snoise(new float3(pos.x, Seed, pos.z) * caves.SpaghettiScale);
        
        return math.abs(noiseXY) + math.abs(noiseXZ);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeNoodleCave(float3 pos)
    {
        ref var caves = ref CaveParams.Value;
        
        float noiseXY = noise.snoise(new float3(pos.x, pos.y, Seed + 10000) * caves.NoodleScale);
        float noiseXZ = noise.snoise(new float3(pos.x, Seed + 10000, pos.z) * caves.NoodleScale);
        
        return math.abs(noiseXY) + math.abs(noiseXZ);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int3 IndexToPos(int i)
    {
        const int SIZE = 32;
        return new int3(i % SIZE, (i / SIZE) % SIZE, i / (SIZE * SIZE));
    }
}
```

### Task 10.7.2: Blob Assets for Configuration
**Recommendation**: ✅ **COMPLETE**
**Implementation Notes**:
- Created `GenerationConfigBlobs.cs` with `StrataBlob`, `CaveParamsBlob`, `HollowEarthBlob`.
- Created `BlobAssetBuilder` for conversion.
- `ChunkGenerationSystem` manages blob lifecycle.

Convert ScriptableObjects to Burst-compatible blob assets:

```csharp
// Strata blob
public struct StrataBlob
{
    public BlobArray<StrataLayerData> Layers;
    public uint NoiseSeed;
    public float NoiseScale;
}

public struct StrataLayerData
{
    public byte MaterialID;
    public float MinDepth;
    public float MaxDepth;
    public float BlendWidth;
    public float NoiseInfluence;
}

// Cave parameters blob
public struct CaveParamsBlob
{
    // Swiss Cheese
    public bool EnableSwissCheese;
    public float CheeseScale;
    public float CheeseThreshold;
    
    // Spaghetti
    public bool EnableSpaghetti;
    public float SpaghettiScale;
    public float SpaghettiWidth;
    
    // Noodles
    public bool EnableNoodles;
    public float NoodleScale;
    public float NoodleWidth;
    
    // Caverns
    public bool EnableCaverns;
    public float CavernScale;
    public float CavernThreshold;
}

// Hollow Earth blob
public struct HollowEarthBlob
{
    public float TopDepth;
    public float BottomDepth;
    public float AverageHeight;
    
    // Floor
    public float FloorNoiseScale;
    public float FloorAmplitude;
    public byte FloorMaterialID;
    
    // Ceiling
    public float CeilingNoiseScale;
    public float CeilingVariation;
    public bool HasStalactites;
    public float MaxStalactiteLength;
    
    // Pillars
    public bool GeneratePillars;
    public float PillarFrequency;
    public float MinPillarRadius;
    public float MaxPillarRadius;
    
    // Wall material
    public byte WallMaterialID;
}

// Blob asset builder
public static class BlobAssetBuilder
{
    public static BlobAssetReference<CaveParamsBlob> CreateCaveBlob(CaveProfile profile)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<CaveParamsBlob>();
        
        root.EnableSwissCheese = profile.EnableSwissCheese;
        root.CheeseScale = profile.CheeseScale;
        root.CheeseThreshold = profile.CheeseThreshold;
        
        root.EnableSpaghetti = profile.EnableSpaghetti;
        root.SpaghettiScale = profile.SpaghettiScale;
        root.SpaghettiWidth = profile.SpaghettiWidth;
        
        root.EnableNoodles = profile.EnableNoodles;
        root.NoodleScale = profile.NoodleScale;
        root.NoodleWidth = profile.NoodleWidth;
        
        root.EnableCaverns = profile.EnableCaverns;
        root.CavernScale = profile.CavernScale;
        root.CavernThreshold = profile.CavernThreshold;
        
        var result = builder.CreateBlobAssetReference<CaveParamsBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }
    
    public static BlobAssetReference<HollowEarthBlob> CreateHollowBlob(
        HollowEarthProfile profile, float topDepth, float bottomDepth)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<HollowEarthBlob>();
        
        root.TopDepth = topDepth;
        root.BottomDepth = bottomDepth;
        root.AverageHeight = profile.AverageHeight;
        
        root.FloorNoiseScale = profile.FloorNoiseScale;
        root.FloorAmplitude = profile.FloorAmplitude;
        root.FloorMaterialID = profile.FloorMaterialID;
        
        root.CeilingNoiseScale = profile.CeilingNoiseScale;
        root.CeilingVariation = profile.HeightVariation;
        root.HasStalactites = profile.HasStalactites;
        root.MaxStalactiteLength = profile.MaxStalactiteLength;
        
        root.GeneratePillars = profile.GeneratePillars;
        root.PillarFrequency = profile.PillarFrequency;
        root.MinPillarRadius = profile.MinPillarRadius;
        root.MaxPillarRadius = profile.MaxPillarRadius;
        
        root.WallMaterialID = profile.WallMaterialID;
        
        var result = builder.CreateBlobAssetReference<HollowEarthBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }
}
```

### Task 10.7.3: Layer-Based Streaming System
**Recommendation**: ✅ **COMPLETE**
**Implementation Notes**:
- Integrated into `ChunkStreamingSystem`.
- Uses `GeologyService.GetMin/MaxLayerY` to determine load range.
- Dynamic buffering based on player vertical velocity.

Only load chunks in nearby layers:

```csharp
[BurstCompile]
public partial struct LayerStreamingSystem : ISystem
{
    private int _currentPlayerLayer;
    private NativeList<int3> _chunksToLoad;
    private NativeList<int3> _chunksToUnload;
    
    public void OnCreate(ref SystemState state)
    {
        _chunksToLoad = new NativeList<int3>(1000, Allocator.Persistent);
        _chunksToUnload = new NativeList<int3>(1000, Allocator.Persistent);
    }
    
    public void OnDestroy(ref SystemState state)
    {
        _chunksToLoad.Dispose();
        _chunksToUnload.Dispose();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Get player position
        float3 playerPos = float3.zero;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>()
            .WithAll<PlayerTag>())
        {
            playerPos = transform.ValueRO.Position;
            break;
        }
        
        float playerDepth = -playerPos.y;
        
        // Determine current layer
        int newLayer = GetLayerIndex(playerDepth);
        
        if (newLayer != _currentPlayerLayer)
        {
            // Player changed layers - update streaming
            UpdateLayerStreaming(newLayer);
            _currentPlayerLayer = newLayer;
        }
        
        // Process chunk load/unload queues with budget
        ProcessLoadQueue(ref state, 4);  // Max 4 loads per frame
        ProcessUnloadQueue(ref state, 8); // Max 8 unloads per frame
    }
    
    private void UpdateLayerStreaming(int newLayer)
    {
        _chunksToLoad.Clear();
        _chunksToUnload.Clear();
        
        // Layers to keep loaded
        int minLayer = math.max(0, newLayer - 1);
        int maxLayer = math.min(_totalLayers - 1, newLayer + 1);
        
        // Mark chunks outside range for unload
        // Mark chunks in range for load
        // ... implementation
    }
    
    private int GetLayerIndex(float depth)
    {
        // Use WorldStructureConfig to find layer
        // ... implementation
        return 0;
    }
}

### Task 10.7.4: Frame Budget Manager
**Recommendation**: ✅ **COMPLETE**
**Implementation Notes**:
- Implemented in `ChunkGenerationSystem`.
- Uses `Stopwatch` and `FRAME_BUDGET_MS` (4ms).
- Caps concurrent generation jobs (Max 8).

Limit generation work per frame:

```csharp
[BurstCompile]
public partial struct GenerationBudgetSystem : ISystem
{
    private double _lastFrameTime;
    private float _rollingAverage;
    
    // Configuration
    private const float BUDGET_MS = 4f;          // Max generation time per frame
    private const int MAX_CHUNKS_PER_FRAME = 8;   // Hard limit
    
    public void OnUpdate(ref SystemState state)
    {
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        int chunksGenerated = 0;
        
        // Process generation queue
        foreach (var (request, entity) in 
            SystemAPI.Query<RefRO<ChunkGenerationRequest>>()
                     .WithEntityAccess())
        {
            // Check budget
            if (stopwatch.Elapsed.TotalMilliseconds > BUDGET_MS)
                break;
            
            if (chunksGenerated >= MAX_CHUNKS_PER_FRAME)
                break;
            
            // Generate chunk
            GenerateChunk(request.ValueRO.ChunkPos, ref state);
            chunksGenerated++;
            
            // Remove request
            state.EntityManager.DestroyEntity(entity);
        }
        
        stopwatch.Stop();
        
        // Update rolling average for profiler
        _rollingAverage = math.lerp(_rollingAverage, 
            (float)stopwatch.Elapsed.TotalMilliseconds, 0.1f);
    }
    
    private void GenerateChunk(int3 chunkPos, ref SystemState state)
    {
        // Get layer for this chunk
        float depth = -chunkPos.y * 32f;  // Assuming 32 voxel chunks
        
        // Schedule appropriate generation job based on layer type
        // ... implementation
    }
}
```

### Task 10.7.5: LOD Generation for Distant Chunks
**Recommendation**: ✅ **COMPLETE**

Lower detail for chunks far from player:

```csharp
[BurstCompile]
public struct GenerateLODChunkJob : IJobParallelFor
{
    [ReadOnly] public int3 ChunkOrigin;
    [ReadOnly] public int LODLevel;  // 1, 2, 4, 8
    [ReadOnly] public BlobAssetReference<StrataBlob> Strata;
    
    [WriteOnly] public NativeArray<float> Densities;
    [WriteOnly] public NativeArray<byte> Materials;
    
    public void Execute(int index)
    {
        // For LOD chunks, sample every Nth voxel
        int step = LODLevel;
        int sampleCount = 32 / step;  // e.g., LOD4 = 8 samples per axis
        
        int3 lodPos = new int3(
            (index % sampleCount) * step,
            ((index / sampleCount) % sampleCount) * step,
            (index / (sampleCount * sampleCount)) * step
        );
        
        float3 worldPos = new float3(ChunkOrigin + lodPos);
        
        // Sample once, apply to entire LOD block
        float density = ComputeDensity(worldPos);
        byte material = ComputeMaterial(worldPos);
        
        // Fill the LOD block with same values
        for (int dz = 0; dz < step; dz++)
        {
            for (int dy = 0; dy < step; dy++)
            {
                for (int dx = 0; dx < step; dx++)
                {
                    int3 fillPos = lodPos + new int3(dx, dy, dz);
                    int fillIndex = fillPos.x + fillPos.y * 32 + fillPos.z * 32 * 32;
                    Densities[fillIndex] = density;
                    Materials[fillIndex] = material;
                }
            }
        }
    }
}
```

### Task 10.7.6: Noise Caching System
**Recommendation**: ✅ **COMPLETE**

Cache expensive noise calculations:

```csharp
public struct NoiseCache : IDisposable
{
    private NativeHashMap<int3, float> _cache;
    private int _maxSize;
    
    public NoiseCache(int maxSize)
    {
        _cache = new NativeHashMap<int3, float>(maxSize, Allocator.Persistent);
        _maxSize = maxSize;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetOrCompute(int3 pos, float scale, uint seed)
    {
        if (_cache.TryGetValue(pos, out float cached))
            return cached;
        
        // Evict if full
        if (_cache.Count >= _maxSize)
        {
            _cache.Clear();  // Simple eviction - clear all
        }
        
        float value = noise.snoise(new float3(pos) * scale + seed);
        _cache[pos] = value;
        return value;
    }
    
    public void Dispose()
    {
        _cache.Dispose();
    }
}

// Per-chunk column cache for biome/height
public struct ColumnCache : IDisposable
{
    private NativeHashMap<int2, ColumnData> _cache;
    
    public struct ColumnData
    {
        public byte BiomeID;
        public float SurfaceHeight;
        public float Temperature;
        public float Humidity;
    }
    
    public ColumnCache(int capacity)
    {
        _cache = new NativeHashMap<int2, ColumnData>(capacity, Allocator.Persistent);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColumnData GetOrCompute(int2 column, uint seed)
    {
        if (_cache.TryGetValue(column, out ColumnData data))
            return data;
        
        // Compute column data (once per column, not per voxel)
        data = ComputeColumnData(column, seed);
        _cache[column] = data;
        return data;
    }
    
    private ColumnData ComputeColumnData(int2 column, uint seed)
    {
        float2 worldXZ = new float2(column.x * 32 + 16, column.y * 32 + 16);
        
        return new ColumnData
        {
            Temperature = noise.snoise(worldXZ * 0.001f + seed),
            Humidity = noise.snoise(worldXZ * 0.0013f + seed + 50000),
            SurfaceHeight = noise.snoise(worldXZ * 0.02f + seed) * 10f,
            BiomeID = 1  // Computed from temp/humidity
        };
    }
    
    public void Dispose()
    {
        _cache.Dispose();
    }
}
```

---

## Performance Targets

| Operation | Budget | LOD0 | LOD1 | LOD2 | LOD4 |
|-----------|--------|------|------|------|------|
| Solid chunk gen | 5ms | ✓ | - | - | - |
| Hollow chunk gen | 3ms | ✓ | - | - | - |
| LOD1 chunk | 2ms | - | ✓ | - | - |
| LOD2 chunk | 0.5ms | - | - | ✓ | - |
| LOD4 chunk | 0.2ms | - | - | - | ✓ |

| System | Frame Budget |
|--------|--------------|
| Total generation | 4ms |
| Streaming decisions | 0.5ms |
| Mesh generation | 6ms |
| Total voxel systems | 12ms |

---

## Profiling Integration (Task 10.7.7)
**Recommendation**: ✅ **COMPLETE**
**Implementation Notes**:
- Added `VoxelProfiler` static class.
- Instrumented `ChunkGenerationSystem` and jobs.
- Added `GenerationBenchmark` editor tool.

```csharp
// Add profiling markers for all generation stages
[BurstCompile]
public partial struct ProfiledGenerationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        VoxelProfiler.BeginSample("Generation.Total");
        
        VoxelProfiler.BeginSample("Generation.Terrain");
        // Terrain generation
        VoxelProfiler.EndSample("Generation.Terrain");
        
        VoxelProfiler.BeginSample("Generation.Caves");
        // Cave carving
        VoxelProfiler.EndSample("Generation.Caves");
        
        VoxelProfiler.BeginSample("Generation.Hollow");
        // Hollow earth
        VoxelProfiler.EndSample("Generation.Hollow");
        
        VoxelProfiler.BeginSample("Generation.Ores");
        // Ore placement
        VoxelProfiler.EndSample("Generation.Ores");
        
        VoxelProfiler.EndSample("Generation.Total");
    }
}
```

---

## Acceptance Criteria

- [x] All generation jobs Burst-compiled
- [x] No managed allocations in hot paths
- [x] Blob assets for all configuration
- [x] Layer streaming working (2-3 layers loaded)
- [x] Frame budget system limits generation
- [x] LOD generation for distant chunks
- [x] Noise caching reduces redundant calculations
- [x] < 5ms per full-detail chunk
- [x] < 12ms total voxel budget per frame
- [x] No frame hitches during layer transitions

---

## Optimization Summary & Recommendations

### Complete Decision Matrix

| Task | Name | Priority | Key Benefit | Key Risk | Keep? | Notes |
|------|------|----------|-------------|----------|-------|-------|
| **10.7.1** | Burst-Compiled Jobs | CRITICAL | 10-100x faster | Must be blittable | ✅ YES | Non-negotiable for voxels |
| **10.7.2** | Blob Assets | HIGH | Zero GC, shared config | One-time setup cost | ✅ YES | Required for Burst |
| **10.7.3** | Layer-Based Streaming | CRITICAL | Only load 2-3 layers | Transition complexity | ✅ YES | Essential for 7km depth |
| **10.7.4** | Frame Budget System | HIGH | Smooth 60fps | May delay generation | ✅ YES | Prevents hitches |
| **10.7.5** | Generation LOD | HIGH | Faster distant chunks | Less detail at distance | ✅ YES | 5x faster for LOD2 |
| **10.7.6** | Noise Caching | MEDIUM | Reduce noise calls | Memory for cache | ✅ YES | Significant for multi-noise |
| **10.7.7** | Profiling Integration | LOW | Identify bottlenecks | Slight overhead | ✅ YES | Essential for tuning |

### Pros/Cons Summary

| Task | Pros | Cons |
|------|------|------|
| **10.7.1** | ✅ 10-100x faster, ✅ SIMD, ✅ Multi-threaded | ⚠️ No managed types, ⚠️ Debug harder |
| **10.7.2** | ✅ Zero GC, ✅ Shared across jobs | ⚠️ Must rebuild on config change |
| **10.7.3** | ✅ Memory efficient, ✅ Scales to infinite depth | ⚠️ Layer transition stutter possible |
| **10.7.4** | ✅ Consistent framerate, ✅ Configurable budget | ⚠️ Generation may lag exploration |
| **10.7.5** | ✅ Much faster at distance, ✅ Less memory | ⚠️ Visible detail reduction |
| **10.7.6** | ✅ Major savings for stacked noise | ⚠️ Cache invalidation complexity |
| **10.7.7** | ✅ Pinpoints bottlenecks, ✅ Timeline view | ⚠️ Slight CPU overhead |

### Performance vs Complexity Analysis

| Optimization | Performance Gain | Implementation Complexity | Priority |
|--------------|-----------------|---------------------------|----------|
| 10.7.1 Burst Jobs | ✅✅✅ Massive | ⚠️ Medium | **Must have** |
| 10.7.2 Blob Assets | ✅✅ High | ⚠️ Medium | **Must have** |
| 10.7.3 Layer Streaming | ✅✅✅ Massive | ⚠️⚠️ High | **Must have** |
| 10.7.4 Frame Budget | ✅✅ High | ✅ Low | **Must have** |
| 10.7.5 Generation LOD | ✅✅ High | ⚠️ Medium | **Should have** |
| 10.7.6 Noise Caching | ✅ Medium | ✅ Low | **Should have** |
| 10.7.7 Profiling | ✅ Low (enables others) | ✅ Low | **Should have** |

### Recommended Implementation Order

1. **10.7.7 Profiling** - Establish baseline measurements first
2. **10.7.1 Burst Jobs** - Foundation for all other optimizations
3. **10.7.2 Blob Assets** - Required for Burst compatibility
4. **10.7.4 Frame Budget** - Prevent hitches during development
5. **10.7.3 Layer Streaming** - Enable deep world exploration
6. **10.7.5 Generation LOD** - Reduce distant chunk cost
7. **10.7.6 Noise Caching** - Final optimization pass

