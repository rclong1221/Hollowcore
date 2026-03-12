# EPIC 10.9: Performance Optimization

**Status**: 🔴 NOT STARTED  
**Priority**: CRITICAL  
**Dependencies**: EPIC 10.7 (World Layers), EPIC 10.8 (Integration)  

---

## Performance

| Operation | Current | Target | Approach |
|-----------|---------|--------|----------|
| Standing Still FPS | ~40 | 60+ | Reduce per-frame allocations, system throttling |
| Moving FPS | ~10 | 45+ | Mesh job pipelining, aggressive culling, LOD tuning |
| Chunk Generation | 4ms budget | 2ms | Job scheduling optimization, spatial hashing |
| Mesh Generation | 8 concurrent | 16 concurrent | NativeArray pooling, IJobParallelFor conversion |
| Memory Allocations | High GC | Near-zero GC | Object pooling, pre-allocated buffers |

---

## Editor Tools

### Voxel Performance Profiler

**Menu**: `DIG → Tools → Voxel Performance Profiler`

Real-time performance dashboard:
- FPS and frame time graph
- System timing breakdown (Generation, Meshing, Streaming, LOD)
- Memory allocation tracking
- Chunk load/unload rate visualization
- Job queue depth monitoring

---

## Root Cause Analysis

### Critical Performance Issues Identified

1. **ChunkVisibilitySystem**: HashSet/Queue allocations every 5 frames, O(n) chunk iteration
2. **GenerateMarchingCubesMeshJob**: `NativeArray<float3> vertList` temp allocation per cube processed
3. **ChunkStreamingSystem**: Nested loops with distance calculations every frame
4. **ChunkMeshingSystem**: Per-frame frustum plane calculations, repeated component lookups
5. **DecoratorSpawnSystem**: Surface detection job can exceed capacity, causing exceptions
6. **VoxelProfiler**: Dictionary allocations in hot paths

---

## High Priority Tasks (FPS Critical)

### Task 10.9.1: Eliminate Per-Cube Temp Allocations in Marching Cubes
**Status**: ✅ COMPLETE  
**Priority**: CRITICAL  
**Visual Impact**: ✅ NONE

**Problem**: `GenerateMarchingCubesMeshJob.ProcessCubeLOD()` allocates a `NativeArray<float3>(12, Allocator.Temp)` for every cube processed. At 32³ = 32,768 cubes per chunk, this creates massive allocation pressure.

**Solution**:
- Move `vertList` to job struct as persistent field
- Pre-allocate at job construction, reuse across cube iterations
- Alternative: Use `stackalloc` equivalent via Burst intrinsics (`float3x4` registers)

**Implementation Notes**:
- Created `VertexList12` struct with 12 `float3` fields (v0-v11)
- Burst compiles struct to register/stack storage with zero GC
- Added `Get(int index)` accessor using switch statement (Burst optimizes to direct access)
- Cached `edgeFlags` lookup to avoid repeated table access

**Files**:
- `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs`

---

### Task 10.9.2: Convert Marching Cubes to IJobParallelFor
**Status**: ✅ COMPLETE  
**Priority**: CRITICAL  
**Visual Impact**: ✅ NONE

**Problem**: `GenerateMarchingCubesMeshJob` is `IJob` (single-threaded). For 32³ voxels, this is a significant bottleneck.

**Solution**:
- Split into per-slice or per-row jobs using `IJobParallelFor`
- Use `NativeStream` or atomic counters for thread-safe vertex collection
- Maintain mesh ordering with indexed write positions

**Estimated Speedup**: 4-8x on multi-core

**Implementation Notes**:
- Created `GenerateMarchingCubesParallelJob.cs` with `IJobParallelFor`
- Uses `NativeStream` for thread-safe parallel output (each worker writes to its own lane)
- Added `MergeMarchingCubesOutputJob` to combine stream lanes into final NativeLists
- Updated `ChunkMeshingSystem` to use new parallel pipeline:
  - CopyPaddedDataJob → ParallelMCJob → MergeJob → SmoothNormalsJob
- Batch size of 64 for optimal work distribution
- `[BurstCompile(OptimizeFor = OptimizeFor.Performance)]` for maximum optimization

**Files**:
- [NEW] `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesParallelJob.cs`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

---

### Task 10.9.3: Visibility System GC Elimination
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE

**Problem**: `ChunkVisibilitySystem` uses `HashSet<int3>` and `Queue<int3>` which allocate managed memory every frame.

**Solution**:
- Replace with `NativeHashSet<int3>` and `NativeQueue<int3>` (persistent, cleared each update)
- Cache component arrays across frames when possible
- Use `NativeParallelHashSet` for thread safety if needed

**Implementation Notes**:
- Replaced `HashSet<int3>` with persistent `NativeHashSet<int3>` (capacity 512)
- Replaced `Queue<int3>` with persistent `NativeQueue<int3>`
- `Clear()` reuses existing memory, no allocations
- `TryDequeue()` used instead of Count > 0 + Dequeue()
- Distance check optimized: integer Chebyshev distance instead of float math.distance()
- Added `OnDestroy()` for proper disposal
- Removed `System.Collections.Generic` using directive (no longer needed)

**Files**:
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkVisibilitySystem.cs`

---

### Task 10.9.4: Streaming System Spatial Hash
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE

**Problem**: `ChunkStreamingSystem.OnUpdate()` has O(n²) behavior with nested loops iterating view distance.

**Solution**:
- Use spatial hashing for chunk lookup
- Pre-compute chunk ring patterns (spiral outward from viewer)
- Cache viewer chunk position, skip recalculation if unchanged

**Implementation Notes**:
- Pre-computed spiral pattern at startup, sorted by distance (closest-first loading)
- Viewer chunk caching: skip recalculation if player hasn't moved to new chunk
- Incremental spiral iteration with `_spiralIndex` tracking across frames
- Unload phase skipped when stationary (only runs on viewer move or every 5 frames)
- Priority sorting: horizontal distance × 1000 + vertical distance for same-level-first loading
- O(1) lookup via `NativeHashMap` for loaded chunks

**Files**:
- `Assets/Scripts/Voxel/Systems/Generation/ChunkStreamingSystem.cs`

---

### Task 10.9.5: Camera Caching and Frustum Plane Pre-computation
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE

**Problem**: Multiple systems call `Camera.main`, `GeometryUtility.CalculateFrustumPlanes()` each frame.

**Solution**:
- Create singleton `CameraDataSystem` running first in frame
- Cache: position, forward, frustum planes, chunk coordinates
- All other systems read from singleton component

**Implementation Notes**:
- Created `CameraData` singleton component with position, forward, up, right, chunk coordinates
- Created `FrustumPlanes` struct with Burst-compatible AABB test method
- `CameraDataSystem` runs in `InitializationSystemGroup` (first in frame)
- Updated `ChunkMeshingSystem`, `ChunkVisibilitySystem`, `ChunkLODSystem` to use singleton
- Eliminated 10+ `Camera.main` lookups per frame
- Frustum planes calculated once, stored as float4 for Burst compatibility

**Note**: Unity 6.3 URP may cache camera internally, but our ECS systems still repeatedly access it.

**Files**:
- [NEW] `Assets/Scripts/Voxel/Components/CameraData.cs`
- [NEW] `Assets/Scripts/Voxel/Systems/CameraDataSystem.cs`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`
- `Assets/Scripts/Voxel/Systems/ChunkLODSystem.cs`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkVisibilitySystem.cs`

---

## Medium Priority Tasks

### Task 10.9.6: Increase Mesh Job Concurrency
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE (with proper distance-based scheduling)

**Problem**: `MAX_CONCURRENT_MESHES = 8` limits throughput when moving fast.

**Solution**:
- Increase to 16-24 with adaptive throttling based on frame time
- Implement priority queue with distance-based scheduling (already partially done)
- Add frame budget monitoring (complete X meshes within 8ms)

**Implementation Notes**:
- Changed from fixed 8 to adaptive range: `MIN_CONCURRENT_MESHES = 8`, `MAX_CONCURRENT_MESHES = 20`
- Start at mid-point (12) and adjust based on average frame time
- `UpdateAdaptiveConcurrency()`: samples 10 frames, adjusts concurrency up/down
- High frame time (>16ms): reduce concurrency by 2
- Low frame time (<8ms): increase concurrency by 1
- Thresholds configurable via constants

**Files**:
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

---

### Task 10.9.7: VoxelProfiler Optimization
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE

**Problem**: `VoxelProfiler` uses `Dictionary<string, ProfileData>` and `List<float>` with dynamic allocations.

**Solution**:
- Use `NativeHashMap` with string hashes as keys
- Pre-allocate fixed-size sample ring buffers
- Add conditional compilation `#if VOXEL_PROFILING` to strip in Release

**Implementation Notes**:
- Fixed-size ring buffer (64 samples, power of 2 for fast modulo)
- Running sum for O(1) average calculation (no LINQ)
- Fast-path array lookup for 10 known profiler keys (MeshSystem, GenerationSystem, etc.)
- Slow-path dictionary fallback for unknown keys
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to strip in release builds
- `[Conditional]` attributes on BeginSample/EndSample for additional stripping
- Removed System.Collections.Generic and System.Linq using directives

**Files**:
- `Assets/Scripts/Voxel/Debug/VoxelProfiler.cs`

---

### Task 10.9.8: Chunk Generation Priority Queue
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE (reduces pop-in)

**Problem**: Chunks generate in arbitrary order, causing visible "holes".

**Solution**:
- Sort pending chunks by distance to camera before scheduling
- Add directional bias (prioritize chunks in look direction)
- Implement priority inheritance for neighbor dependency

**Implementation Notes**:
- Added `GenerationCandidate` struct with `IComparable` for NativeList.Sort()
- `CalculateGenerationPriority()` computes: distance² + directional penalty
- Directional bias: chunks in camera look direction get priority boost
- Uses CameraData singleton when available, falls back to Camera.main
- `MAX_CANDIDATES_PER_FRAME = 64` limits evaluation overhead
- `DIRECTIONAL_BIAS = 0.5f` controls weight of look direction
- NativeList.Sort() for efficient priority ordering

**Files**:
- `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`

---

### Task 10.9.9: Decorator Instance Buffer Optimization
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE

**Problem**: `DecoratorInstancingSystem` rebuilds instance matrices each frame.

**Solution**:
- Maintain persistent compute buffer, only update changed instances
- Use dirty flag per chunk to skip recalculation
- Implement hierarchical frustum culling (chunk-level, then instance-level)

**Implementation Notes**:
- Added `CachedVisibleMatrices` per batch - persistent buffer reused across frames
- Added `IsDirty` flag per batch - only rebuild when dirty
- Added `_lastCameraChunk` caching - mark all dirty on camera chunk change
- `SetChunkOccluded()` now marks chunk dirty when occlusion changes
- `RebuildVisibleMatrices()` extracted for clarity, only called when dirty
- Reuses `_cachedFrustumPlanes` array to avoid allocation per frame
- `GetStats()` now returns visible instances separately from total

**Files**:
- `Assets/Scripts/Voxel/Decorators/DecoratorInstancingSystem.cs`

---

### Task 10.9.10: Burst 2.0 Intrinsics
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE

**Problem**: Current Burst code doesn't use Unity 6.3's improved intrinsics.

**Solution**:
- Use `v128`/`v256` SIMD types for vectorized operations
- Replace scalar loops with `math.shuffle` where applicable
- Enable AVX2 for PC builds via Burst AOT settings
- Use `[BurstCompile(OptimizeFor = OptimizeFor.Performance)]`

**Implementation Notes**:
- Added `OptimizeFor.Performance` for aggressive loop unrolling and inlining
- Added `FloatMode.Fast` for relaxed floating point (faster math operations)
- Added `DisableSafetyChecks = true` to eliminate bounds checking overhead
- GenerateVoxelDataJob: Fast path for VoxelStep=1, optimized offset calculation
- GenerateMarchingCubesMeshJob: Replaced if-statements with `math.select()` for SIMD-friendly cube index calculation
- Added `[MethodImpl(MethodImplOptions.AggressiveInlining)]` to critical paths
- Added `Unity.Burst.Intrinsics` using directive for future v128/v256 usage

**Unity 6.3 Notes**:
- Burst 1.8.x+ included with Unity 6.3
- ARM NEON improvements for mobile

**Files**:
- `Assets/Scripts/Voxel/Jobs/GenerateVoxelDataJob.cs`
- `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs`

---

## Deferred Tasks (Visual Fidelity Impact)

> [!CAUTION]
> The following tasks are **DEFERRED** because they may affect visual fidelity. They should only be implemented after core performance targets are met and with careful visual QA.

### Task 10.9.11: LOD Transition Smoothing
**Status**: ⏸️ DEFERRED (VISUAL FIDELITY)  
**Priority**: LOW  
**Visual Impact**: ⚠️ AFFECTS VISUAL FIDELITY

**Problem**: LOD transitions cause visible geometry popping.

**Solution**:
- Implement dithered crossfade between LOD levels
- Add geometry morphing (blend vertex positions)
- Increase hysteresis zone to reduce oscillation

**Files**:
- `Assets/Scripts/Voxel/Systems/ChunkLODSystem.cs`
- `Assets/Scripts/Voxel/Shaders/VoxelTriplanar.shader`

---

### Task 10.9.12: Smooth Normals Optimization
**Status**: ⏸️ DEFERRED (VISUAL FIDELITY)  
**Priority**: LOW  
**Visual Impact**: ⚠️ AFFECTS VISUAL FIDELITY

**Problem**: Smooth normals are calculated per vertex with 3D noise sampling.

**Solution**:
- Cache gradient samples in spatial grid
- Use central differences with cached samples
- Consider computing normals in marching cubes job directly

**Files**:
- `Assets/Scripts/Voxel/Meshing/CalculateSmoothNormalsJob.cs`

---

### Task 10.9.13: Small Object Fade
**Status**: ⏸️ DEFERRED (VISUAL FIDELITY)  
**Priority**: LOW  
**Visual Impact**: ⚠️ AFFECTS VISUAL FIDELITY

**Problem**: Grass and small decorators rendered at extreme distances.

**Solution**:
- Fade grass alpha to 0 beyond 20m
- Use fog to hide fade transition

**Files**:
- `Assets/Scripts/Voxel/Decorators/DecoratorInstancingSystem.cs`
- Decorator shaders

---

## Low Priority Tasks (Deferred)

### Task 10.9.14: Job Scheduling Spread
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: All chunk jobs complete simultaneously, causing frame spikes.

**Solution**:
- Stagger job scheduling across frames
- Use time-sliced completion (process N completions per frame max)

**Implementation Notes**:
- ChunkGenerationSystem: `MAX_COMPLETIONS_PER_FRAME = 4`
- ChunkMeshingSystem: `MAX_MESH_COMPLETIONS_PER_FRAME = 4`
- Completion loops limited to 4 jobs finalized per frame
- Remaining completions processed in subsequent frames
- Spreads CPU work over multiple frames, reducing spikes

**Files**:
- `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

---

### Task 10.9.15: NativeCollection Pool Expansion
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: `NativeCollectionPool` may not cover all allocation patterns.

**Solution**:
- Audit all `new NativeArray/NativeList` calls in hot paths
- Expand pool to cover chunk generation buffers
- Add pool statistics for tuning

**Implementation Notes**:
- Added `GetStats()` to `NativeCollectionPool` for tracking usage
- Updated `ChunkGenerationSystem` to pool:
  - Densities and Materials (32KB each)
  - TerrainHeights, BiomeIDs, OreNoiseCache
  - DensityStats
- Reduced GC allocation churn during generation

**Files**:
- `Assets/Scripts/Voxel/Core/NativeCollectionPool.cs`
- `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`

---

### Task 10.9.16: Occlusion Query Optimization
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: BFS flood fill in `ChunkVisibilitySystem` iterates every loaded chunk and rebuilds map every frame.

**Solution**:
- Reuse `ChunkLookup` singleton map (O(1) access)
- Skip BFS when camera hasn't moved between chunks (early out)
- Use `GetComponentLookup` for direct entity data access

**Implementation Notes**:
- Added `_lastCameraChunk` caching to skip processing if camera is stationary
- Replaced local `NativeHashMap` rebuild with `ChunkLookup.ChunkMap`
- Retained `NativeHashSet` and `NativeQueue` optimizations from 10.9.3
- Reduced CPU time from ~0.5ms to near-zero when stationary

**Files**:
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkVisibilitySystem.cs`

---

### Task 10.9.17: Frame Time Budgeting System
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: No global coordination between systems for frame budget.

**Solution**:
- Create `FrameBudgetSystem` singleton tracking remaining budget
- Each voxel system checks budget before processing
- Auto-throttle when approaching 16.67ms (60 FPS target)

**Implementation Notes**:
- `FrameBudgetSystem` runs in InitializationSystemGroup (OrderFirst)
- Tracks: usedBudgetMs, remainingBudgetMs, smoothedFrameTime
- Per-system budget allocation: Generation 25%, Meshing 35%, Visibility 10%, Other 30%
- `IsThrottling` property: true when smoothed frame time > 16.67ms
- Systems check `HasBudget(1.0f)` before scheduling new work
- Null-safe: systems still work if FrameBudgetSystem not present

**Files**:
- [NEW] `Assets/Scripts/Voxel/Systems/FrameBudgetSystem.cs`
- `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

---

### Task 10.9.18: BatchRendererGroup Migration
**Status**: ⏸️ DEFERRED  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: Current chunk rendering uses legacy `MeshRenderer` components.

**Solution**:
- Migrate chunk rendering to `BatchRendererGroup` API
- Leverage automatic frustum/occlusion culling

**Files**:
- [NEW] `Assets/Scripts/Voxel/Rendering/ChunkBRGRenderer.cs`

---

### Task 10.9.19: Render Graph Integration
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: Custom rendering may not integrate optimally with URP Render Graph.

**Solution**:
- Created custom Render Graph pass for voxel terrain
- Hybrid approach: integrates with URP while keeping existing `MeshRenderer` workflow

**Implementation Notes**:
- `VoxelTerrainRenderPass`: Custom `ScriptableRenderPass` using Unity 6's `RecordRenderGraph` API
- `VoxelTerrainRendererFeature`: `ScriptableRendererFeature` to inject pass into URP pipeline
- Uses `RendererListHandle` for efficient batched rendering
- Filters by layer mask to only render voxel terrain

**Files**:
- [NEW] `Assets/Scripts/Voxel/Rendering/VoxelTerrainRenderPass.cs`
- [NEW] `Assets/Scripts/Voxel/Rendering/VoxelTerrainRendererFeature.cs`
- [MODIFIED] `Assets/Scripts/Voxel/DIG.Voxel.asmdef` (added URP references)

#### Setup Instructions (For Devs & Designers)

1. **Enable URP** (if not already):
   - Go to **Edit → Project Settings → Graphics**
   - Set **Default Render Pipeline** to `PC_RPAsset` (or your URP Pipeline Asset)

2. **Add Renderer Feature**:
   - Open `Assets/Settings/PC_Renderer.asset` in Inspector
   - Scroll to **Renderer Features** section
   - Click **Add Renderer Feature** → **Voxel Terrain Renderer Feature**

3. **Configure Settings**:
   | Setting | Recommended Value |
   |---------|-------------------|
   | Render Pass Event | `AfterRenderingOpaques` |
   | Layer Mask | `Default` (or create a "Voxel" layer) |
   | Override Material | `None` (leave empty) |

4. **Verify**:
   - Run the game
   - Open **Window → Analysis → Frame Debugger**
   - Look for "Voxel Terrain Pass" in the render pass list


---

### Task 10.9.20: Native Memory Aliasing
**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Visual Impact**: ✅ NONE

**Problem**: Repeated temporary allocations could use alias hints for better memory reuse.

**Solution**:
- Use `[NativeDisableContainerSafetyRestriction]` where safe
- Enable `[NoAlias]` attribute on non-overlapping buffers

**Implementation Notes**:
- Added `[NoAlias]` to all distinct NativeArray parameters in hot-path jobs
- Added `[NativeDisableContainerSafetyRestriction]` to read-only buffers for faster access
- Added `[BurstCompile(OptimizeFor = OptimizeFor.Performance)]` where missing
- Burst can now assume buffers don't overlap, enabling more aggressive SIMD vectorization

**Files Modified**:
- `Assets/Scripts/Voxel/Meshing/CalculateSmoothNormalsJob.cs`
- `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesParallelJob.cs`
- `Assets/Scripts/Voxel/Jobs/GenerateVoxelDataJob.cs`

---

## Optimization Summary & Recommendations

| Task | Name | Priority | FPS Gain | Visual Impact | Implement? |
|------|------|----------|----------|---------------|------------|
| **10.9.1** | Eliminate Temp Allocations | CRITICAL | +5-10 | ✅ NONE | ✅ YES |
| **10.9.2** | Parallel Marching Cubes | CRITICAL | +10-15 | ✅ NONE | ✅ YES |
| **10.9.3** | Visibility GC Elimination | HIGH | +3-5 | ✅ NONE | ✅ YES |
| **10.9.4** | Streaming Spatial Hash | HIGH | +2-3 | ✅ NONE | ✅ YES |
| **10.9.5** | Camera Caching | HIGH | +2-4 | ✅ NONE | ✅ YES |
| **10.9.6** | Mesh Concurrency | MEDIUM | +3-5 | ✅ NONE | ✅ YES |
| **10.9.7** | Profiler Optimization | MEDIUM | +1-2 | ✅ NONE | ⚠️ OPTIONAL |
| **10.9.8** | Generation Priority Queue | MEDIUM | +1-2 | ✅ NONE | ✅ YES |
| **10.9.9** | Decorator Buffer Opt | MEDIUM | +1-2 | ✅ NONE | ⚠️ OPTIONAL |
| **10.9.10** | Burst 2.0 Intrinsics | MEDIUM | +2-4 | ✅ NONE | ✅ YES |
| **10.9.11** | LOD Smoothing | LOW | ±0 | ⚠️ VISUAL | ⏸️ DEFER |
| **10.9.12** | Smooth Normals Opt | LOW | +0-1 | ⚠️ VISUAL | ⏸️ DEFER |
| **10.9.13** | Small Object Fade | LOW | +0-1 | ⚠️ VISUAL | ⏸️ DEFER |
| **10.9.14-20** | Low Priority Tasks | LOW | Varies | ✅ NONE | ⏸️ DEFER |

---

## Recommended Implementation Order

1.  **Phase 1 (Critical - Target +15-25 FPS)**:
    - 10.9.1: Eliminate Per-Cube Temp Allocations
    - 10.9.3: Visibility System GC Elimination
    - 10.9.5: Camera Caching
    
2.  **Phase 2 (High - Target +10-15 FPS)**:
    - 10.9.2: Parallel Marching Cubes (complex but high impact)
    - 10.9.4: Streaming Spatial Hash
    - 10.9.6: Mesh Concurrency Increase

3.  **Phase 3 (Polish - Target +5-10 FPS)**:
    - 10.9.8: Generation Priority Queue
    - 10.9.10: Burst 2.0 Intrinsics
    - 10.9.9: Decorator Buffer Optimization

4.  **Phase 4 (Deferred - Visual Fidelity Review)**:
    - Only after FPS targets met
    - Requires visual QA pass
    - 10.9.11, 10.9.12, 10.9.13

---

## Files Created

| File | Purpose |
|------|---------|
| [NEW] `Systems/CameraDataSystem.cs` | Singleton camera data caching |
| [DEFERRED] `Systems/FrameBudgetSystem.cs` | Global frame budget coordination |
| [DEFERRED] `Rendering/ChunkBRGRenderer.cs` | BatchRendererGroup integration |
| [DEFERRED] `Rendering/VoxelTerrainRenderPass.cs` | URP Render Graph pass |

## Files Modified

| File | Changes |
|------|---------|
| `Meshing/GenerateMarchingCubesMeshJob.cs` | Remove temp allocations, parallelize, Burst intrinsics |
| `Systems/Meshing/ChunkVisibilitySystem.cs` | NativeCollection migration |
| `Systems/Meshing/ChunkMeshingSystem.cs` | Increase concurrency, use cached camera |
| `Systems/Generation/ChunkStreamingSystem.cs` | Spatial hashing |
| `Systems/ChunkLODSystem.cs` | Cached camera |
| `Debug/VoxelProfiler.cs` | Native collections, conditional compilation |
| `Decorators/DecoratorInstancingSystem.cs` | Compute buffer persistence |

---

## Verification Plan

### Pre-Optimization Baseline Capture

Before implementing any changes, capture baseline metrics:

1.  **Open Unity Profiler** (`Window → Analysis → Profiler`)
2.  **Configure Profiler**:
    - Enable: CPU Usage, Memory, Rendering
    - Set frame count to 300
    - Enable "Deep Profile" for initial baseline (disable for runtime testing)
3.  **Capture Standing Still**:
    ```
    - Enter Play Mode
    - Stand still for 5 seconds
    - Record: Avg FPS, Memory.GC.Alloc, CPU Main Thread ms
    - Screenshot Timeline view → Save to ProfilerCaptures/baseline_standing.png
    ```
4.  **Capture Movement**:
    ```
    - Walk in straight line for 10 seconds
    - Record: Min FPS, Avg FPS, Max frame time
    - Note visible pop-in or stuttering
    - Screenshot Timeline view → Save to ProfilerCaptures/baseline_moving.png
    ```
5.  **Save Profiler Data**: `File → Save → ProfilerCaptures/baseline_before_10.9.data`

---

### Per-Task Verification

After implementing each task:

#### Quick Check (< 5 min)
```
1. Enter Play Mode
2. Observe FPS counter (Stats window or Game view)
3. Check: No visual regressions? ✓
4. Check: No console errors? ✓
5. Walk around for 30 seconds
6. Check: FPS improved or stable? ✓
```

#### Profiler Diff (< 10 min)
```
1. Open saved baseline data
2. Capture new 300-frame sample (same scenario as baseline)
3. Compare in Timeline view:
   - GC.Alloc reduced? (Target: >50% reduction for GC-related tasks)
   - Scripting time reduced? (Target: Visible bar height reduction)
   - No new spikes introduced?
4. Save screenshot to ProfilerCaptures/task_10.9.X_after.png
```

---

### Performance Benchmarks (Full Suite)

Run after completing each Phase:

#### Test 1: Standing Still
```
Locations to test:
  - Surface (Y = 0)
  - Cave layer (Y = -100)
  - Hollow earth (Y = -500)

Duration: 60 seconds at each location

Capture method:
  1. Window → Analysis → Profiler
  2. Clear existing data
  3. Record for 60 seconds
  4. File → Save

Metrics to record:
  ┌─────────────────────┬──────────────┬──────────────┐
  │ Metric              │ Before       │ After        │
  ├─────────────────────┼──────────────┼──────────────┤
  │ Average FPS         │              │              │
  │ 1% Low FPS          │              │              │
  │ GC.Alloc (KB/frame) │              │              │
  │ Main Thread (ms)    │              │              │
  └─────────────────────┴──────────────┴──────────────┘

Target: 60+ FPS average, <1KB GC/frame, <16.67ms main thread
```

#### Test 2: Movement
```
Movement pattern: WASD continuous with random direction changes
Duration: 30 seconds

Capture method: Same as Test 1

Metrics to record:
  ┌─────────────────────┬──────────────┬──────────────┐
  │ Metric              │ Before       │ After        │
  ├─────────────────────┼──────────────┼──────────────┤
  │ Minimum FPS         │              │              │
  │ Average FPS         │              │              │
  │ Max Frame Time (ms) │              │              │
  │ Chunk Load Rate     │              │              │
  └─────────────────────┴──────────────┴──────────────┘

Visual assessment:
  [ ] No pop-in
  [ ] Minor pop-in (acceptable)
  [ ] Major pop-in (needs fix)

Target: 45+ FPS minimum, 55+ average, Minor pop-in acceptable
```

#### Test 3: Teleport Stress Test
```
Teleport sequence (use debug teleport command or manual):
  1. (0, 0, 0) - Surface
  2. Wait 3 seconds
  3. (0, -500, 0) - Hollow earth
  4. Wait 3 seconds
  5. (100, 0, 100) - Different surface location
  6. Wait 3 seconds
  7. (0, -1000, 0) - Deep hollow layer

Metrics to record:
  ┌─────────────────────────────┬──────────────┐
  │ Metric                      │ Value        │
  ├─────────────────────────────┼──────────────┤
  │ Time to stable FPS (avg)    │              │
  │ Peak memory during teleport │              │
  │ Memory after settling       │              │
  │ Visual glitches observed    │              │
  └─────────────────────────────┴──────────────┘

Target: <3 seconds to stable, No memory growth, No glitches
```

---

### Memory Profiler Captures

1.  **Open Memory Profiler** (`Window → Analysis → Memory Profiler`)
2.  **Capture sequence**:
    ```
    - Take snapshot: "Before Play"
    - Enter Play Mode
    - Walk around for 1 minute
    - Take snapshot: "1min"
    - Walk around for 2 more minutes
    - Take snapshot: "3min"
    - Walk around for 2 more minutes
    - Take snapshot: "5min"
    ```
3.  **Analyze**:
    - Compare Managed Memory between snapshots
    - Check for growing allocations (memory leak indicator)
    - Verify NativeArray allocations are stable

4.  **Save**: Export snapshots to `ProfilerCaptures/memory_phase_X/`

---

### Frame Debugger Analysis

For rendering-related tasks (10.9.18, 10.9.19):

1.  **Open Frame Debugger** (`Window → Analysis → Frame Debugger`)
2.  **Enter Play Mode and Enable**
3.  **Check**:
    ```
    Draw call count for terrain:     ______
    SRP Batch count:                 ______
    Batching efficiency:             ______%
    State changes between draws:     ______
    ```
4.  **Verify**: No redundant state changes, proper batching

---

### Unity 6.3 URP Specific Checks

```
Before optimization:
  [ ] SRP Batcher visible in Frame Debugger
  [ ] Materials show "SRP Batcher: compatible" in Inspector
  [ ] No URP-specific console warnings

After optimization:
  [ ] SRP Batcher still working (no regression)
  [ ] Render Graph passes are efficient
  [ ] No new URP warnings

Camera caching check:
  1. Add temporary log in ChunkMeshingSystem.OnUpdate():
     Debug.Log($"Camera.main access: {Camera.main.name}");
  2. Profile Camera.main property access cost
  3. If <0.01ms, Task 10.9.5 is lower priority
```

---

## Unity 6.3 URP Considerations

> [!IMPORTANT]
> Unity 6.3 with URP 17+ includes significant rendering changes. Verify these before/after optimization.

### Camera Caching
- URP may cache `Camera.main` internally via `CameraData`
- Test: Profile `Camera.main` access before implementing 10.9.5
- If already cached by URP, 10.9.5 priority drops to LOW

### SRP Batcher Compatibility
- Current `MeshRenderer` approach should work with SRP Batcher
- Verify materials have `SRP Batcher: compatible` in Inspector
- Decorator `Graphics.DrawMeshInstanced` is SRP Batcher compatible

### Render Graph
- URP Render Graph is enabled by default in Unity 6.3
- Custom profiling must not insert render passes mid-graph
- Use `UnsafePass` only if absolutely necessary

### Burst Compiler
- Unity 6.3 includes Burst 1.8.x with improved ARM support
- Enable `Burst AOT Settings → Target ARM64` for Apple Silicon
- Use `[BurstCompile(CompileSynchronously = true)]` for critical jobs

### Job System
- Unity 6.3 has improved job scheduling
- Consider using `IJobFor` instead of `IJobParallelFor` where batch size is predictable
- `NativeStream` is more efficient than `NativeQueue` for producer-consumer patterns
