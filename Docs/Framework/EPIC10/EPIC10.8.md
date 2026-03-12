# EPIC 10.8: Performance Optimization & Bug Fixes

**Status**: ✅ IN PROGRESS  
**Priority**: CRITICAL  
**Dependencies**: EPIC 10.3 (Fluids), EPIC 10.4 (Biomes)  

---

## The Problem
Introduction of **Fluid Simulation (10.3)**, **Biome Noise (10.4)**, and **Cave Generation (10.2)** caused significant performance degradation.
- **10.3 Issue**: Fluid simulation runs on all chunks (even empty ones).
- **10.4 Issue**: Biome noise sampled 32,768 times with 32x redundancy.
- **10.2 Issue**: Cave meshes generate massive triangle counts, causing 241ms+ spikes in MeshSystem.

**Goal**: Restore performance to 60+ FPS by optimizing simulation loops, generation pipelines, and mesh generation.

---

## Files Created

| File | Purpose |
|------|---------|
| `Systems/Meshing/ChunkMeshingSystem.cs` | Logic updates (Culling/LOD) |
| `Systems/Meshing/FluidMeshSystem.cs` | Decoupled fluid renderer |
| `Jobs/GenerateMarchingCubesMeshJob.cs` | LOD support |
| `Jobs/GenerateFluidMeshJob.cs` | Fluid-only meshing |
| `Components/FluidComponents.cs` | Mesh tracking |

---

## ✅ COMPLETED Optimizations

### Task 10.8.4: Mesh System Optimization (Cave Performance Fix)
*Implemented: 2024-12-21*

#### 10.8.4.1: Frustum Culling ✅
- Added camera frustum plane calculation each frame
- Chunks outside the camera view are skipped from meshing
- Uses `GeometryUtility.TestPlanesAABB` for efficient AABB testing

#### 10.8.4.2: Buried Chunk Occlusion ✅
- Chunks more than 2 chunk-depths below camera are skipped
- Camera look direction is checked (don't skip if looking steeply down)
- Prevents meshing underground caves when player is on surface

#### 10.8.4.3: LOD-Aware Marching Cubes ✅
- Added `VoxelStep` parameter to `GenerateMarchingCubesMeshJob`
- Distant chunks use coarser sampling (step 2/4/8)
- LOD 3 (step=8) generates **64x fewer triangles**
- Integrates with existing `VoxelLODConfig` and `ChunkLODState`

#### 10.8.4.4: Priority Queue Streaming ✅
- Chunks sorted by distance before processing
- Nearest chunks meshed first (highest priority)
- Increased `MAX_CONCURRENT_MESHES` from 4 to 8
- Spreads mesh load across frames to prevent spikes

#### 10.8.4.5: Separate Fluid Mesh System ✅
- Created `FluidMeshSystem` for decoupled fluid rendering
- Created `GenerateFluidMeshJob` for fluid-specific Marching Cubes
- Fluid updates no longer trigger expensive terrain remesh
- Added `FluidMeshReference` component for mesh entity tracking

**Files Modified:**
- `ChunkMeshingSystem.cs` - Visibility culling, LOD integration, priority queue
- `GenerateMarchingCubesMeshJob.cs` - VoxelStep LOD support
- `FluidMeshSystem.cs` - New system for fluid rendering
- `GenerateFluidMeshJob.cs` - New job for fluid meshing
- `FluidComponents.cs` - Added FluidMeshReference

---

## Optimization Plan

### Task 10.8.1: Fluid Simulation Optimization (Priority #1)
**Recommendation**: ✅ **IMPLEMENT** - Priority #1 for performance
*Focus: Reduce overhead of the cellular automata simulation causing runtime lag.*

1. **Chunk Activation Tracking** ✅
   - Add `HasFluid` component to chunks
   - Maintain `NativeList<Entity> ActiveFluidChunks`
   - Skip simulation for dry chunks (90% of the world)

2. **Stride-Based Indexing** ✅
   - Pre-calculate neighbor offsets (Strides)
   - Replace math instructions with fast pointer additions

3. **Sleep System**
   - Identify "Stable" cells where `Level` = 255 and surrounded by `Level` = 255
   - Skip processing until neighbor changes

---

### Task 10.8.2: Generation Pre-Pass (Column Hoisting) ✅
*Focus: Reduce generation freezes/stutters.*

1. **2D Data Job (The "Column Job")** ✅
   - Created `GenerateColumnDataJob` for 32x32 grid (1024 iterations)
   - Calculates XZ-dependent values: BiomeID, HeightMap

2. **3D Generation Job Integration** ✅
   - Modified `GenerateVoxelDataJob` to read from pre-computed column data
   - Reduced 31,744 noise calls per chunk

---

### Task 10.8.3: Branchless Optimizations ✅
*Focus: Micro-optimizations for hot loops.*

1. **Optimized `IsCaveAir`** ✅
   - Converted to branchless using `math.select`
   - Boolean accumulation instead of branching

2. **Optimized `GetHollowDensity`** ✅
   - Branchless material selection
   - Early exit for solid regions

---

## Acceptance Criteria
- [x] MeshSystem spikes reduced from 241ms to <50ms max
- [x] Frustum culling prevents off-screen mesh generation
- [x] Underground caves don't mesh when player is on surface
- [x] LOD system reduces distant chunk triangle counts by 64x
- [x] Fluid rendering decoupled from terrain mesh
- [ ] Profiler shows Fluid Simulation taking < 1ms per frame average
- [ ] No visible stutter when moving between chunks
- [ ] Fluid behavior (flow/damage) remains identical to 10.3 implementation

---

## Optimization Summary & Recommendations

| Task | Name | Priority | Recommendation | Notes |
|------|------|----------|----------------|-------|
| 10.8.1 | Fluid Sim Optimization | CRITICAL | ✅ IMPLEMENT | Priority #1 for FPS |
| 10.8.2 | Generation Pre-Pass | CRITICAL | ✅ COMPLETED | Solved stutters |
| 10.8.3 | Branchless Opts | HIGH | ✅ COMPLETED | Micro-opts |
| 10.8.4 | Mesh System (Cave Fix) | CRITICAL | ✅ COMPLETED | 10x gain, essential |


