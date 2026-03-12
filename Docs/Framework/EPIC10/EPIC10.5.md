# EPIC 10.5: Decorators & Structures

**Status**: ✅ COMPLETE  
**Priority**: LOW  
**Dependencies**: EPIC 10.4 (Biome System), EPIC 10.2 (Cave & Hollow Earth)  
**Estimated Time**: 3-4 days

---

---

## Performance

| Operation | Budget | Approach |
|-----------|--------|----------|
| Surface detection | <1ms | Burst parallel |
| Placement rules | <0.5ms | Single Burst job |
| Instantiation | Async | Main thread, pooling possible |

---

## Editor Tools

### Decorator Setup

**Menu**: `DIG → Quick Setup → Decorators`

Automated setup tools:
- Create Complete Setup: 8 samples + registry
- Validate Setup: Checks IDs and prefabs
- Create Giant Decorators: Hollow earth variants

---

## Files Created

| File | Purpose |
|------|---------|
| `Decorators/DecoratorDefinition.cs` | Decorator config SO |
| `Decorators/DecoratorRegistry.cs` | Central catalog |
| `Decorators/DecoratorService.cs` | Burst-compatible data |
| `Decorators/StructureDefinition.cs` | Structure config SO |
| `Jobs/SurfaceDetectionJob.cs` | Surface detection |
| `Jobs/DecoratorPlacementJob.cs` | Placement logic |
| `Systems/Generation/DecoratorSpawnSystem.cs` | Orchestration + spawning |
| `Editor/DecoratorQuickSetup.cs` | Editor tools |

---

## Designer Workflow

### Setting Up Decorators (2 minutes)

1. **Create Complete Setup**:
   - Go to **DIG → Quick Setup → Decorators → Create Complete Decorator Setup**
   - Creates 8 sample decorators + DecoratorRegistry in Resources

2. **Assign Prefabs** (Optional):
   - Open `Assets/Resources/Decorators/` in Project window
   - Select decorator assets and assign prefabs in Inspector
   - Without prefabs, decorator positions are calculated but nothing spawns

3. **Enter Play Mode**: Decorators spawn on cave surfaces!

### Creating Custom Decorators

1. Right-click in Project → **Create → DIG → World → Decorator Definition**
2. Configure:
   - **DecoratorID**: Unique 1-255
   - **Prefab**: GameObject to spawn
   - **RequiredSurface**: Floor, Ceiling, or Wall
   - **SpawnProbability**: 0-1 chance per surface point
   - **MinSpacing**: Distance between instances (prevents clustering)
3. Run **DIG → Quick Setup → Decorators → Create Decorator Registry** to rebuild registry

---

## Component Reference

### DecoratorDefinition (ScriptableObject)

Defines a single decorator type.

**Location**: `Assets/Scripts/Voxel/Decorators/DecoratorDefinition.cs`

| Field | Type | Description |
|-------|------|-------------|
| `DecoratorID` | `byte` | Unique ID (1-255) |
| `DecoratorName` | `string` | Display name |
| `Prefab` | `GameObject` | Main prefab to spawn |
| `Variations` | `GameObject[]` | Optional random variations |
| `RequiredSurface` | `SurfaceType` | Floor, Ceiling, or Wall |
| `MinSpacing` | `float` | Minimum distance between instances |
| `SpawnProbability` | `float` | Chance per valid surface (0-1) |
| `MinCaveRadius` | `float` | Minimum cave size to spawn |
| `ScaleWithCaveSize` | `bool` | Larger in bigger caves |
| `MinScale` / `MaxScale` | `float` | Scale range |
| `MinDepth` / `MaxDepth` | `float` | Depth constraints |
| `AllowedBiomeIDs` | `byte[]` | Biome restrictions (empty = any) |
| `RandomYRotation` | `bool` | Random rotation |
| `AlignToSurface` | `bool` | Match surface normal |
| `IsGiantDecorator` | `bool` | For hollow earth (large scale) |
| `MaxHeight` | `float` | For tall decorators |

---

### DecoratorRegistry (ScriptableObject)

Central catalog of all decorators.

**Location**: `Assets/Resources/DecoratorRegistry.asset`

| Field | Type | Description |
|-------|------|-------------|
| `Decorators` | `DecoratorDefinition[]` | All registered decorators |
| `MaxDecoratorsPerChunk` | `int` | Limit per chunk (default: 50) |
| `GlobalSpawnMultiplier` | `float` | Scales all probabilities |
| `EnableDecorators` | `bool` | Master toggle |

**Context Menu**:
- **Auto Populate**: Finds all DecoratorDefinition assets

---

### DecoratorService (Static Class)

Burst-compatible data for jobs.

**Location**: `Assets/Scripts/Voxel/Decorators/DecoratorService.cs`

```csharp
// Initialize from registry (done automatically by DecoratorSpawnSystem)
DecoratorService.Initialize(registry);

// Access in jobs
var decoratorParams = DecoratorService.DecoratorParamsArray;
var floorDecorators = DecoratorService.FloorDecorators;

// Get managed definition for instantiation
DecoratorDefinition def = DecoratorService.GetDefinition(decoratorID);
```

---

### StructureDefinition (ScriptableObject)

For procedural structures in hollow earth.

**Location**: `Assets/Scripts/Voxel/Decorators/StructureDefinition.cs`

| Field | Type | Description |
|-------|------|-------------|
| `Rarity` | `float` | Spawn chance (0-0.01) |
| `RequiresCaveSpace` | `bool` | Needs open space |
| `MinClearance` | `float` | Required clearance |
| `PlaceOnHollowFloor` | `bool` | Hollow earth only |
| `MinHollowHeight` | `float` | Required hollow height |
| `RotationSnap` | `float` | 0=continuous, 90=cardinal |

---

## DOTS Architecture

### Data Flow

```
DecoratorRegistry (ScriptableObject)
         │
         ▼ Initialize
DecoratorService (Static, NativeArrays)
         │
         ▼ Query
DecoratorSpawnSystem (SystemBase)
         │
         ├─ SurfaceDetectionJob (IJobParallelFor)
         │    └─ Detects floor/ceiling/walls
         │
         └─ DecoratorPlacementJob (IJob)
              └─ Applies rules, outputs placements
                      │
                      ▼
              Instantiate Prefabs (Main Thread)
```

### System Overview

| System | Type | Purpose |
|--------|------|---------|
| `DecoratorSpawnSystem` | `SystemBase` | Orchestrates detection → placement → spawning |
| `SurfaceDetectionJob` | `IJobParallelFor` | Finds floor/ceiling/wall surfaces in chunk |
| `DecoratorPlacementJob` | `IJob` | Determines decorator positions from rules |

### Key Components

| Component | Type | Purpose |
|-----------|------|---------|
| `ChunkDecorated` | Tag | Marks processed chunks |
| `SurfacePoint` | Struct | Detected surface (position, normal, biome) |
| `DecoratorPlacement` | Struct | Final spawn decision |

---

## Designer Tips & Tuning

### Adjusting Spawn Density

| Setting | Effect |
|---------|--------|
| `SpawnProbability` ↑ | More decorators |
| `MinSpacing` ↓ | Allow clustering |
| `GlobalSpawnMultiplier` | Scales all decorators |
| `MaxDecoratorsPerChunk` | Hard limit |

### Biome-Specific Decorators

1. Create decorator with `AllowedBiomeIDs = [5]` (e.g., Crystal Cavern)
2. Only spawns in that biome

### Hollow Earth Giant Decorators

1. Set `IsGiantDecorator = true`
2. Set `MinScale = 5f`, `MaxScale = 15f`
3. Set `MinDepth = 300f` (hollow earth layers)
4. Set `ScaleWithCaveSize = true`

---

## Developer Integration

### Adding Decorators Programmatically

```csharp
var decorator = ScriptableObject.CreateInstance<DecoratorDefinition>();
decorator.DecoratorID = 100;
decorator.DecoratorName = "Custom Crystal";
decorator.Prefab = myPrefab;
decorator.RequiredSurface = SurfaceType.Wall;
decorator.SpawnProbability = 0.1f;
decorator.MinSpacing = 3f;
```

### Extending Surface Detection

The `SurfaceDetectionJob` can detect additional surface types:
1. Add to `SurfaceType` enum
2. Add detection logic in `SurfaceDetectionJob.Execute()`
3. Add decorator list in `DecoratorService`

### Client-Side Only

Decorators are **not networked**. Each client generates its own decorators deterministically from chunk seed. This saves bandwidth and is fine for visual-only elements.

---



## Sample Decorators by Surface

### Floor Decorators
| Name | Probability | Spacing | Notes |
|------|-------------|---------|-------|
| Small Crystal | 15% | 2m | Caves |
| Mushroom Cluster | 12% | 3m | Shallow caves |
| Stalagmite | 8% | 4m | Deep caves |
| Ore Cluster | 5% | 5m | Deep only |

### Ceiling Decorators
| Name | Probability | Spacing | Notes |
|------|-------------|---------|-------|
| Stalactite | 10% | 3m | Deep caves |
| Glowing Spores | 15% | 2m | Shallow-mid |

### Wall Decorators
| Name | Probability | Spacing | Notes |
|------|-------------|---------|-------|
| Wall Crystal | 8% | 3m | All caves |
| Moss Patch | 12% | 2m | Shallow only |

---



## Acceptance Criteria

- [x] DecoratorDefinition ScriptableObject created
- [x] DecoratorRegistry ScriptableObject created
- [x] DecoratorService static class with NativeArrays
- [x] SurfaceDetectionJob detects floor/ceiling/walls
- [x] DecoratorPlacementJob applies spacing/depth/biome rules
- [x] DecoratorSpawnSystem orchestrates jobs + instantiation
- [x] StructureDefinition for hollow earth structures
- [x] Quick Setup editor tools
- [x] Decorators spawn on floor/ceiling/walls
- [x] Biome-specific filtering
- [x] Spacing rules prevent clustering
- [x] All placement Burst-compiled
- [x] Client-side only (not networked)

---

## Performance Optimization Tasks

### Task 10.5.6: Eliminate Blob Data Copy
**Status**: ✅ COMPLETE  
**Priority**: HIGH

**Problem**: Currently copies all 32,768 voxels per chunk (160KB allocation).

**Solution**:
- Pass blob reference directly to job (use `[ReadOnly]` BlobAssetReference)
- Or use `UnsafeUtility.MemCpy` for bulk copy
- Avoid per-voxel byte→float conversion

**Implementation**: `SurfaceDetectionJob` now takes `BlobAssetReference<VoxelBlob>` directly. Eliminated 160KB allocation + copy per chunk.

| Pros | Cons |
|------|------|
| ✅ Eliminates 160KB allocation per chunk | ⚠️ Job must handle blob directly |
| ✅ No memory copy overhead | ⚠️ Blob must stay valid during job |
| ✅ Zero GC pressure | |

---

### Task 10.5.7: Early-Out for Solid/Empty Chunks
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM

**Problem**: SurfaceDetectionJob runs on all chunks, even fully solid ones.

**Solution**:
- Add `ChunkHasCaves` component during generation
- Skip decorator processing for chunks without caves
- Potentially skip 80%+ of chunks

**Implementation**:
- `HasCaveSurfaces()` method in `DecoratorSpawnSystem`
- Samples 9 key points (corners + center) first for fast early-out
- Falls back to sparse scan (1/64 of voxels) if needed
- Skips scheduling job entirely for homogeneous chunks

| Pros | Cons |
|------|------|
| ✅ Skips 80%+ of chunks (solid underground) | ⚠️ Extra pre-check cost per chunk |
| ✅ No job scheduling overhead for empty chunks | ⚠️ Rare false negatives possible (sparse scan) |
| ✅ Scales well with world depth | |

---

### Task 10.5.8: Spatial Hashing for Spacing Checks
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM

**Problem**: Current spacing check is O(n²) - each placement checks all previous.

**Solution**:
- Implement grid-based spatial hash
- Only check neighboring grid cells
- Reduce to O(n) average case

**Implementation**:
- `NativeParallelMultiHashMap<int, int>` for spatial hash grid
- 4m cell size, 16³ grid (fits 64m chunks)
- `GetSpatialHash()` converts position → cell hash
- `CheckSpacingSpatialHash()` only checks cells within `minSpacing` radius
- Complexity: O(k) where k = decorators in nearby cells (typically <10)

| Pros | Cons |
|------|------|
| ✅ O(n) instead of O(n²) | ⚠️ Extra memory for hash map |
| ✅ Scales to 100s of decorators | ⚠️ Hash collisions possible |
| ✅ Cell size tunable | ⚠️ Slightly more complex code |

---

### Task 10.5.9: Object Pooling for Decorators
**Status**: ✅ COMPLETE  
**Priority**: HIGH

**Problem**: `Object.Instantiate()` called synchronously for every decorator.

**Solution**:
- Create `DecoratorPool` class with pre-instantiated objects
- Pool per decorator type
- Return to pool when chunk unloads

**Implementation**:
- `DecoratorPool.cs` - Singleton pool manager with per-decorator-type stacking
- `DecoratorCleanupSystem.cs` - Returns decorators to pool when chunks unload
- `PooledDecorator` component tracks chunk ownership for cleanup
- Prefabs registered on system initialization

| Pros | Cons |
|------|------|
| ✅ No Instantiate() calls after warmup | ⚠️ Upfront memory for pooled objects |
| ✅ Zero GC from spawning | ⚠️ Pool management complexity |
| ✅ Smooth framerate | ⚠️ Must reset object state on reuse |

---

### Task 10.5.10: Batched Instantiation with Frame Budget
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM

**Problem**: All decorators for a chunk spawn in one frame.

**Solution**:
- Queue decorator placements
- Instantiate N per frame (e.g., 10)
- Spread load across frames
- Similar to mesh streaming priority queue

**Implementation**:
- `QueuedPlacement` struct for deferred spawning
- `List<QueuedPlacement>` for pending decorators (with priority)
- `ProcessInstantiationQueue()` spawns max 15 per frame
- Combined with object pooling for smooth framerate

| Pros | Cons |
|------|------|
| ✅ Consistent frame times | ⚠️ Decorators appear gradually |
| ✅ No spawn spikes | ⚠️ Slight visual delay |
| ✅ Configurable budget | ⚠️ Queue can grow large if overwhelmed |

---

### Task 10.5.11: LOD for Decorators
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM

**Problem**: All decorators spawned regardless of distance from player.

**Solution**:
- Skip small decorators for chunks beyond distance threshold
- Only spawn large/important decorators at distance
- Reduce draw calls significantly

**Implementation**:
- `DecoratorLODImportance` enum: Low(4 chunks), Medium(8), High(16), Critical(255)
- `LODImportance` field on `DecoratorDefinition`
- `MaxChunkDistance` field in `DecoratorParams`
- `ShouldSpawnAtDistance()` check in `ProcessInstantiationQueue()`
- Uses Manhattan distance for fast chunk distance calculation

| Pros | Cons |
|------|------|
| ✅ Major draw call reduction | ⚠️ Distant areas look sparse |
| ✅ Configurable per decorator type | ⚠️ Player may notice pop-in |
| ✅ Critical decorators always visible | ⚠️ Requires designer tuning |

---

### Task 10.5.12: GPU Instancing for Decorators
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM

**Problem**: Each decorator is a separate draw call.

**Solution**:
- Use `Graphics.DrawMeshInstanced` for same prefab types
- Batch 100+ decorators into single draw call
- Requires instanced materials

**Implementation**:
- `DecoratorInstancingSystem.cs` MonoBehaviour singleton
- Batches matrices per decorator type (max 1023 per draw call)
- Renders in `LateUpdate()` via `Graphics.DrawMeshInstanced`
- `UseGPUInstancing`, `InstancedMesh`, `InstancedMaterial` fields on `DecoratorDefinition`
- Auto-extracts mesh/material from prefab if not specified

**GPU Instancing vs GameObject Pool Comparison**:

| Aspect | GPU Instancing | GameObjects (Pool) |
|--------|---------------|-------------------|
| Draw calls | ✅ 1 per type | ❌ 1 per instance |
| Physics colliders | ❌ None | ✅ Full physics |
| LOD meshes | ❌ None (single mesh) | ✅ Unity handles LOD |
| Culling | ❌ Manual frustum culling | ✅ Unity handles |
| Animation | ❌ None | ✅ Animator support |
| Interactivity | ❌ Can't click/select | ✅ Full interaction |
| Shadows | ⚠️ Limited precision | ✅ Per-object shadows |
| Memory | ✅ Lower | ❌ Higher (per GO) |

**When to Use Each**:
- **GPU Instancing**: Grass, small rocks, debris, non-interactive props
- **GameObjects**: Mineable/destructible decorators, animated plants, interactive objects

---

### Task 10.5.13: Decorator Streaming Priority
**Status**: ✅ COMPLETE  
**Priority**: LOW

**Problem**: Decorator spawning doesn't prioritize player proximity.

**Solution**:
- Priority queue based on distance to player
- Near chunks get decorators first
- Defer distant chunk decorators

**Implementation**:
- Added `DistanceSq` field to `QueuedPlacement` struct
- Changed from `Queue` to `List` for sort capability
- Lazy sort on `_queueDirty` flag (sort only when new items added)
- `RemoveAt(0)` processes closest decorators first

| Pros | Cons |
|------|------|
| ✅ Near-player decorators appear first | ⚠️ Sort cost O(n log n) when dirty |
| ✅ Better perceived performance | ⚠️ RemoveAt(0) is O(n) |
| ✅ Lazy sort minimizes overhead | ⚠️ Distant areas populate last |

---

### Task 10.5.14: Parallel Surface Detection Batching
**Status**: ⬜ SKIPPED  
**Priority**: LOW

**Problem**: Job scheduling overhead for surface detection.

**Solution**:
- Batch multiple chunks into single job
- Reduce `Schedule()` call overhead
- Better CPU utilization

**Skipped Reason**: Marginal gains, high complexity. Modern Unity job scheduling is efficient. Other optimizations handle the real bottlenecks.

---

## Optimization Summary & Recommendations

### Complete Decision Matrix

| Task | Name | Status | Key Benefit | Key Risk | Keep? | Notes |
|------|------|--------|-------------|----------|-------|-------|
| **10.5.6** | Blob Direct Access | ✅ | 160KB/chunk saved | Blob validity | ✅ YES | Pure win, zero downside |
| **10.5.7** | Early-Out Chunks | ✅ | Skip 80%+ chunks | Rare false negatives | ✅ YES | Trivial check, huge gains |
| **10.5.8** | Spatial Hashing | ✅ | O(n) vs O(n²) | Memory for hash | ✅ YES | Scales to 100s of decorators |
| **10.5.9** | Object Pooling | ✅ | Zero GC | Upfront memory | ✅ YES | Industry standard |
| **10.5.10** | Frame Budget | ✅ | Consistent FPS | Visual delay | ✅ YES | Prevents frame spikes |
| **10.5.11** | LOD Distance | ✅ | Draw call reduction | Pop-in visible | 🔧 TUNE | Increase distances to minimize pop-in |
| **10.5.12** | GPU Instancing | ✅ | 1 draw call/type | No physics | ✅ YES | Opt-in, designer choice |
| **10.5.13** | Priority Queue | ✅ | Near-first spawning | O(n) RemoveAt | ⚠️ REVIEW | May revert to simple FIFO |
| **10.5.14** | Parallel Batching | ⬜ | Reduce scheduling | High complexity | ❌ SKIP | Marginal gains not worth it |

### Performance vs Fidelity Analysis

| Optimization | FPS Impact | Visual Fidelity Impact | Recommendation |
|--------------|------------|------------------------|----------------|
| 10.5.6-10.5.8 | ✅ Major improvement | ✅ None | **Must have** |
| 10.5.9-10.5.10 | ✅ Smooth frames | ⚠️ Slight delay | **Must have** |
| 10.5.11 | ✅ Draw call reduction | ⚠️ Pop-in at distance | **Tune distances** |
| 10.5.12 | ✅ Massive for small props | ⚠️ No physics/LOD | **Use selectively** |
| 10.5.13 | ⚠️ Minor improvement | ✅ Better perception | **Consider removing** |
| 10.5.14 | ⚠️ Minor improvement | ✅ None | **Skip** |

### Recommended Actions

1. **Keep 10.5.6 through 10.5.10** - These are all wins with minimal downside
2. **Tune 10.5.11** - Increase LOD distances (Low→8, Medium→16, High→32) for less pop-in
3. **Keep 10.5.12** - It's opt-in, zero cost if not used
4. **Review 10.5.13** - Consider reverting to simple queue; frame budget already handles hitches
5. **Skip 10.5.14** - Not worth the complexity for marginal gains

---

## Culling Optimization Tasks

### Task 10.5.15: Frustum Culling for Decorators
**Status**: ✅ COMPLETE  
**Priority**: **CRITICAL**  
**Visual Impact**: ✅ NONE (safe)

**Problem**: Decorator GameObjects render even when offscreen.

**Solution**:
- Unity handles automatically for MeshRenderers
- For GPU Instanced decorators: Need manual frustum check before DrawMeshInstanced
- Ensure pooled objects have proper bounds

**Implementation**:
- `GeometryUtility.CalculateFrustumPlanes()` extracts camera frustum each frame
- `TransformBounds()` helper transforms mesh AABB to world space per-instance
- `GeometryUtility.TestPlanesAABB()` tests each instance against frustum
- Only visible instances added to `_visibleMatrices` list (reusable, no GC)
- Pooled decorators: Unity handles frustum automatically via MeshRenderer

---

### Task 10.5.16: Occlusion Culling for Cave Decorators
**Status**: ✅ COMPLETE  
**Priority**: **CRITICAL**  
**Visual Impact**: ✅ NONE (safe)

**Problem**: Decorators behind cave walls still render. **Major savings for caves.**

**Solution**:
- Enable Unity Occlusion Culling for decorator GameObjects
- For GPU Instanced: Check if chunk is occluded before adding instances
- Integrates with cave occlusion system (10.2.16)

**Implementation**:
- Added `_occludedChunks` HashSet for tracking occluded chunk hashes
- `SetChunkOccluded(int3, bool)` - call from chunk visibility system
- `IsChunkOccluded(int3)` - query occlusion state
- `ChunkPositions` list tracks which chunk each instance belongs to
- `AddInstance()` now accepts optional chunkPos parameter
- Render loop skips instances from occluded chunks before frustum test
- Pooled decorators: Unity Occlusion Culling handles automatically

---

### Task 10.5.17: Hierarchical Chunk-Based Culling
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Problem**: Per-decorator culling checks are expensive.

**Solution**:
- Already partially done: decorators grouped by chunk
- Add: Skip decorator processing if chunk is culled
- Add: Early-out in ProcessInstantiationQueue for culled chunks

**Implementation**:
- `ProcessInstantiationQueue()` now checks `IsChunkOccluded()` first
- Skips all decorators for occluded chunks before any LOD/definition checks
- Order: Chunk occlusion → LOD distance → Definition lookup → Spawn
- Eliminates per-decorator work for entire chunks at once

---

### Task 10.5.18: Small Object Fade for Debris/Props
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Small rocks, debris visible at extreme distances.

**Solution**:
- Fade small decorator alpha beyond their LOD distance
- Use 10.5.11 LODImportance to control fade distance
- Smooth fade over 5-10m, never instant pop

**Mitigation**: Large/Critical decorators stay visible, only small props fade

---

### Culling Summary

| Task | Type | Visual Impact | Implement? |
|------|------|---------------|------------|
| 10.5.15 | Frustum | ✅ None | ✅ YES |
| 10.5.16 | Occlusion | ✅ None | ✅ **CRITICAL** |
| 10.5.17 | Hierarchical | ✅ None | ✅ Partial done |
| 10.5.18 | Small Object Fade | ⚠️ Careful | ⚠️ With LOD fade |

