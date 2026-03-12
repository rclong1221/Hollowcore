# EPIC 8.14: Voxel Performance Optimization

**Status**: 🟡 IN PROGRESS (7/11 Tasks Complete)  
**Priority**: HIGH  
**Dependencies**: EPIC 8.2-8.6 (Core Voxel Systems)  
**Estimated Time**: 3-5 days

---

## Goal

Optimize all voxel systems for maximum performance using async job processing, Burst compilation, native collection pooling, and DOTS best practices. Target: **60+ FPS** with 1000+ active chunks on mid-range hardware.

---

## Current Performance Issues

The voxel system currently uses **synchronous job completion** (`Schedule().Complete()`) which blocks the main thread and causes frame stuttering. This epic converts all critical paths to async processing.

---

## Task Breakdown

### Phase 1: Critical Path Async Conversion (Highest Impact)

#### Task 8.14.1: Async ChunkGenerationSystem
**Status**: ✅ COMPLETED  
**Impact**: HIGH  
**Current Issue**: `handle.Complete()` blocks main thread during chunk generation

**Implementation**:
```csharp
// Before (blocking):
var handle = job.Schedule(VoxelConstants.VOXELS_PER_CHUNK, 256);
handle.Complete(); // BLOCKS!

// After (async):
// Track pending jobs, check completion next frame
private NativeHashMap<int3, JobHandle> _pendingGenerations;
```

**Changes Required**:
- Track pending chunk generation jobs by chunk position
- Check `IsCompleted` each frame instead of blocking
- Process completed chunks and create VoxelBlob
- Handle chunk unloading while generation pending

---

#### Task 8.14.2: Async ChunkMeshingSystem
**Status**: ✅ COMPLETED  
**Impact**: HIGH  
**Current Issue**: `job.Schedule().Complete()` and `smoothNormalsJob.Complete()` block main thread

**Implementation**:
```csharp
// Track multiple in-flight mesh jobs
private struct PendingMeshJob
{
    public Entity Entity;
    public int3 ChunkPos;
    public JobHandle MainHandle;
    public JobHandle NormalsHandle;
    public NativeList<float3> Vertices;
    // ... other outputs
}
private NativeList<PendingMeshJob> _pendingMeshJobs;
```

**Changed Workflow**:
1. Schedule marching cubes job → store handle
2. Next frame: check `IsCompleted`
3. If complete: create Mesh + Collider, schedule smooth normals
4. Next frame: check normals complete
5. Apply final mesh with normals

---

#### Task 8.14.3: Burst CopyPaddedDataJob
**Status**: ✅ COMPLETED  
**Impact**: MEDIUM-HIGH  
**Current Issue**: 34³ = 39,304 iterations on main thread to copy voxel data to padded arrays

**Implementation**:
```csharp
[BurstCompile]
public struct CopyPaddedDataJob : IJobParallelFor
{
    [ReadOnly] public BlobAssetReference<VoxelBlob> SourceBlob;
    [WriteOnly] public NativeArray<byte> PaddedDensities;
    [WriteOnly] public NativeArray<byte> PaddedMaterials;
    
    public void Execute(int index)
    {
        // Convert linear index to 3D coords
        // Map to source blob with clamping
        // Write to padded array
    }
}
```

---

### Phase 2: Memory Optimization (Medium Impact)

#### Task 8.14.4: Native Collection Pooling
**Status**: ✅ COMPLETED
**Impact**: MEDIUM
**Current Issue**: Each chunk allocates new NativeList/NativeArray, causing GC pressure

**Implementation**:
```csharp
public class NativeCollectionPool<T> where T : struct
{
    private Stack<NativeList<T>> _available;
    
    public NativeList<T> Rent(int initialCapacity);
    public void Return(NativeList<T> list);
}
```

**Collections to Pool**:
- `NativeList<float3>` for vertices/normals
- `NativeList<int>` for indices
- `NativeList<int3>` for triangles
- `NativeArray<byte>` for padded densities (34³)
- `ChunkMeshingSystem` now uses `NativeCollectionPool.GetList/GetArray` and returns them in `DisposeJobData`.

---

#### Task 8.14.5: Cached Neighbor Lookups
**Status**: ✅ COMPLETED
**Impact**: MEDIUM
**Current Issue**: 6 entity lookups per chunk on main thread (GetNeighborBlobs)

**Implementation**:
- Cache `ChunkLookup` singleton in `OnUpdate`
- Use cached lookup for all chunk neighbors in loop
- Avoid repeated `SystemAPI.GetSingleton` calls
- Configured `ChunkLookupSystem` to rebuild map in background job.

---

#### Task 8.14.6: MeshDataArray Async Upload
**Status**: ✅ COMPLETED  
**Impact**: MEDIUM  
**Current Issue**: `mesh.SetVertices()` etc. run on main thread

**Implementation**:
```csharp
// Use Unity 2021+ MeshDataArray API for async mesh upload
var meshDataArray = Mesh.AllocateWritableMeshData(1);
var meshData = meshDataArray[0];

// Configure in job (off main thread)
meshData.SetVertexBufferParams(vertexCount, ...);
meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

// Apply on main thread (minimal work)
Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
```

---

### Phase 3: System Optimization (Lower Impact)

#### Task 8.14.6: ISystem + BurstCompile Conversion
**Status**: 🔲 NOT STARTED  
**Impact**: MEDIUM  
**Current Issue**: Systems use `SystemBase` which has managed overhead

**Candidates for Conversion**:
| System | Complexity | Notes |
|--------|------------|-------|
| `ChunkLookupSystem` | Low | Simple query, easy convert |
| `ChunkMemoryCleanupSystem` | Low | Just cleanup logic |
| `ChunkGenerationSystem` | Medium | After async conversion |
| `ChunkStreamingSystem` | High | Complex logic, defer |
| `ChunkMeshingSystem` | High | Hybrid rendering needs managed |

---

#### Task 8.14.7: Batch Entity Operations
**Status**: ✅ COMPLETED  
**Impact**: LOW-MEDIUM  
**Current Issue**: `ChunkStreamingSystem` spawns entities one at a time

**Implementation**:
```csharp
// Before:
foreach (var chunkPos in chunksToSpawn)
{
    ecb.CreateEntity(ChunkArchetype);
    ecb.SetComponent(...);
}

// After:
var entities = ecb.CreateEntity(ChunkArchetype, chunksToSpawn.Length, Allocator.Temp);
for (int i = 0; i < chunksToSpawn.Length; i++)
{
    ecb.SetComponent(entities[i], ...);
}
```

---

#### Task 8.14.8: Query Optimization
**Status**: ✅ COMPLETED  
**Impact**: LOW  
**Current Issue**: Multiple `foreach` loops with SystemAPI.Query in same Update

**Implementation**:
- Cache `EntityQuery` references
- Use `CalculateChunkCount()` for early-out
- Combine related queries where possible
- Add `[WithNone]` / `[WithAll]` for precise filtering

---

#### Task 8.14.9: ChunkLookupSystem Async
**Status**: ✅ COMPLETED  
**Impact**: LOW  
**Current Issue**: `Dependency.Complete()` called every frame

**Implementation**:
- Remove forced completion
- Chain dependencies properly
- Only force completion when lookup is actually needed

---

### Phase 4: Additional Optimizations

#### Task 8.14.10: Dirty Flag for Cleanup
**Status**: ✅ COMPLETED  
**Impact**: LOW  
**Current Issue**: `ChunkMemoryCleanupSystem` iterates all entities every frame

**Implementation**:
- Add `ChunkNeedsCleanup` tag component
- Only process entities with this tag
- Remove tag after cleanup

---

#### Task 8.14.11: Parallel Chunk Processing
**Status**: 🔲 NOT STARTED  
**Impact**: MEDIUM  
**Current Issue**: Only one chunk processed per frame in some systems

**Implementation**:
- Process multiple chunks in parallel
- Use `IJobParallelForDefer` where applicable
- Balance parallelism vs memory pressure

---

## Implementation Order (Recommended)

1. **Task 8.14.2** - Async ChunkMeshingSystem (biggest frame rate impact)
2. **Task 8.14.1** - Async ChunkGenerationSystem (biggest loading impact)
3. **Task 8.14.3** - Burst CopyPaddedDataJob (removes main thread work)
4. **Task 8.14.4** - Native Collection Pooling (reduces GC)
5. **Task 8.14.5** - MeshDataArray Async Upload (further main thread reduction)
6. Remaining tasks as time permits

---

## Performance Metrics (Targets)

| Metric | Current (Estimated) | Target |
|--------|---------------------|--------|
| Frame Time (new chunk) | 16-50ms spike | <5ms |
| Frame Time (steady state) | 8-12ms | <4ms |
| GC Allocations | High (per-chunk allocs) | Near-zero |
| Chunk Load Time | Variable stutter | Smooth streaming |
| Max Chunks | ~200 before issues | 1000+ |

---

## Acceptance Criteria

- [ ] No `Schedule().Complete()` in hot paths
- [ ] All voxel jobs are Burst-compiled
- [ ] Native collections are pooled
- [ ] Mesh upload uses MeshDataArray
- [ ] 60+ FPS with 500 visible chunks
- [ ] No GC allocations during steady-state gameplay
- [ ] Profiler shows <2ms total voxel system time per frame

---

## Dependencies

- Unity 2021.3+ (MeshDataArray API)
- Burst 1.7+ (for latest optimizations)
- Collections 1.4+ (NativeHashMap improvements)

---

## Files to Modify

| File | Changes |
|------|---------|
| `ChunkGenerationSystem.cs` | Async job tracking |
| `ChunkMeshingSystem.cs` | Async mesh generation |
| `ChunkStreamingSystem.cs` | Batch entity creation |
| `ChunkLookupSystem.cs` | Remove forced completion |
| `ChunkMemoryCleanupSystem.cs` | Dirty flag pattern |
| `VoxelModificationSystems.cs` | Query optimization |
| *NEW* `NativeCollectionPool.cs` | Pooling infrastructure |
| *NEW* `CopyPaddedDataJob.cs` | Burst padded copy |

---

## Notes

This epic is designed to be implemented incrementally. Each task provides standalone performance improvement and can be shipped independently. The recommended order prioritizes maximum impact tasks first.

**Reference**: ChunkPhysicsColliderSystem (EPIC 8.6) already demonstrates the async pattern for server-side physics collider creation.

---

🚀 Voxel System Performance Optimization Opportunities

## CRITICAL (High Impact)
| No. | System | Current Issue | Optimization | Status |
|:---:|:---|:---|:---|:---|
| 1 | `ChunkMeshingSystem.ProcessChunk` | SYNC - `job.Schedule().Complete()` blocks main thread | Make async: track job handles, process results next frame | ✅ DONE |
| 2 | `ChunkMeshingSystem.ProcessChunk` | SYNC - `smoothNormalsJob.Complete()` blocks | Chain with main job, complete together async | ✅ DONE |
| 3 | `ChunkGenerationSystem.GenerateChunk` | SYNC - `handle.Complete()` blocks main thread | Make async: track pending chunks, complete later | ✅ DONE |
| 4 | `ChunkMeshingSystem.ProcessChunk` | Padded array copy (34³ = 39K iterations) on main thread | Create `CopyPaddedDataJob` with Burst | ✅ DONE |
| 5 | `ChunkMeshingSystem.GetNeighborBlobs()` | 6 entity lookups per chunk on main thread | Cache neighbor references, use batch queries | ✅ DONE |

## MEDIUM (Moderate Impact)
| No. | System | Current Issue | Optimization | Status |
|:---:|:---|:---|:---|:---|
| 6 | `ChunkLookupSystem` | `Dependency.Complete()` blocks every frame | Use async completion pattern | ✅ DONE |
| 7 | `ChunkStreamingSystem` | `foreach` loops with entity spawning | Batch entity creation with archetype | ✅ DONE |
| 8 | `ChunkMeshingSystem` | Creates new `NativeList` every chunk | Pool/reuse native collections | ✅ DONE |
| N/A | `ChunkMeshingSystem` | Mesh upload on main thread | Use `MeshDataArray` for async upload | ✅ DONE |
| 9 | All systems | Missing `[BurstCompile]` on systems | Add `ISystem` with `[BurstCompile]` where possible | PENDING |
| 10 | `VoxelModificationSystems` | Multiple `foreach` per update | Combine queries, use `EntityQuery.CalculateChunkCount()` | ✅ DONE |

## LOW (Minor Impact)
| No. | System | Current Issue | Optimization | Status |
|:---:|:---|:---|:---|:---|
| 11 | `ChunkMemoryCleanupSystem` | Iterates all chunks every frame | Add dirty flag, only process marked entities | ✅ DONE |
| 12 | `TerrainPhysicsSyncSystem` | DISABLED but still compiled | Delete or keep disabled | PENDING |
| 13 | `VoxelLootSystem` | Main thread event processing | Move to job if many events | ✅ DONE |

## Recommended Implementation Order:
1. Async ChunkGenerationSystem (biggest gain for loading)
2. Async ChunkMeshingSystem (biggest gain for frame rate)
3. Burst CopyPaddedDataJob (remove 39K iteration overhead)
4. Pool NativeCollections (reduce GC pressure)
5. Convert to ISystem + BurstCompile (where possible)