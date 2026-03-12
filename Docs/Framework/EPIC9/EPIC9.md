# EPIC 9: Advanced Voxel Features (Polish & Scalability)

**Status**: 🔄 IN PROGRESS (4/8 complete)  
**Goal**: Transform the Core Voxel Engine into a production-ready, feature-rich product suitable for high-end games.

**Overview**:
Epic 9 focuses on **Scalability** (LOD, Octrees), **Visuals** (Shaders, Texturing), and **Workflow** (Editor Tools). This is what differentiates a "demo" from a "professional tool".

**Features:**
- **LOD System**: Generic Octree-based LOD for massive view distances.
- **Network Optimization**: Delta compression for voxel edits (minimizing bandwidth).
- **Advanced Meshing**: Manifold-Dual Contouring or similar tech for sharp edges (optional).
- **Prefab System**: Copy/Paste functionality for saving/loading voxel structures (e.g., buildings, ruins).
- **Designer Tools**: Professional-grade editor windows with auto-detection.
**Estimated Duration**: 3-4 weeks

---

## Architecture Principles

### 1. Designer-Friendly Configuration
All parameters exposed via ScriptableObjects with:
- **Live preview** in editor
- **Validation** (no broken references)
- **Auto-detection** of file types (textures, models, etc.) - Drag & Drop support.

### 2. Smart Editor Tools
Tools should:
- **Auto-detect** asset types instead of requiring user to specify manually.
- **Validate** configurations before play mode.
- **Preview** results in editor without playing.
- **Bulk operations** for efficiency.

### 3. Performance First
Every system must:
- Profile before and after changes.
- Stay within frame budget.
- Use Burst where possible (almost everywhere).

---

## Sub-Epics

| Sub-Epic | Topic | Priority | Status | Depends On |
|----------|-------|----------|--------|------------|
| [9.1](EPIC9.1.md) | Visual Refinement (Shaders/Textures) | HIGH | ✅ COMPLETE | 8.12 |
| [9.2](EPIC9.2.md) | LOD System | HIGH | ✅ COMPLETE | 8.8 |
| [9.3](EPIC9.3.md) | Network Optimization | HIGH | ✅ COMPLETE | 8.9 |
| [9.4](EPIC9.4.md) | Gameplay Integration Samples | MEDIUM | ✅ COMPLETE | 8.7 |
| [9.5](EPIC9.5.md) | Debug & Validation Tools | HIGH | 🔲 | 8.13 |
| [9.6](EPIC9.6.md) | Material Definition Workflow | HIGH | ✅ COMPLETE | 8.10, 9.1 |
| [9.7](EPIC9.7.md) | Performance Profiling Suite | HIGH | 🔲 | 8.1, 9.2 |
| [9.8](EPIC9.8.md) | Chunk Loading Visualization | MEDIUM | ✅ COMPLETE | 8.8, 9.2 |
| [9.9](EPIC9.9.md) | Voxel Performance Optimization | CRITICAL | 🔲 | 9.4 |
| [9.10](EPIC9.10.md) | Quick Setup & Test Automation | MEDIUM | ✅ COMPLETE | 8.16, All 9.x |

---

## Implementation Notes (Based on Epic 8 Implementation)

### What's Already Implemented (Epic 8)
- **Neighbor Data Sampling**: `GetNeighborBlobs()` + `GetDensityWithNeighbors()` in `ChunkMeshingSystem`
- **ChunkLookup Singleton**: Fast O(1) chunk access via `NativeHashMap<int3, Entity>`
- **Burst-Compiled Meshing**: `GenerateMarchingCubesMeshJob` with vertex scaling in job
- **Hybrid Collision**: Both DOTS `PhysicsCollider` and Unity `MeshCollider`
- **Spawning**: Currently limited to Y=-1 (surface layer only) for testing

### What's Complete (Epic 9.1 + 9.2)

**9.1 Visual Refinement:** ✅ COMPLETE
- `VoxelTriplanarEnhanced.shader` - Texture2DArray, detail textures, normal mapping, AO
- `VoxelVisualMaterial` - Per-material visual configuration
- `MaterialVisualEditor` - Drag-drop texture auto-assignment
- `TextureArrayBuilder` - Batch texture array creation

**9.2 LOD System:** ✅ COMPLETE
- `VoxelLODConfig` - Per-level distance, step, collider settings
- `ChunkLODSystem` - Distance-squared optimization, hysteresis
- `ChunkLODState` / `ChunkNeedsLODMesh` components
- `LODVisualizerWindow` - Scene gizmos, runtime stats, presets

**9.3 Network Optimization:** ✅ COMPLETE
- `VoxelBatchingSystem` - Server-side RPC batching with rolling stats
- `VoxelModificationHistory` - Persistent modification tracking
- `LateJoinSyncSystem` - Late-join synchronization with deferred retry
- `VoxelNetworkStatsWindow` - Real-time network monitoring editor tool

**9.4 Gameplay Integration:** ✅ COMPLETE
- `VoxelExplosionSystem` - Crater creation with loot spawning
- `VoxelHazardSystem` - Environmental hazards (Fire, Toxic, Radiation, etc.)
- `IVoxelTool` interface - Standard tool integration pattern
- `DrillTool` / `ExplosiveTool` - Reference implementations
- `VoxelExplosionTesterWindow` - Scene View explosion testing

### What Epic 9 Still Needs to Address

**9.5 Debug & Validation Tools:**
- World Slice Viewer for density/material visualization
- Collision Tester for automated validation
- Should integrate with LOD system from 9.2

**9.6 Material Definition Workflow:**
- Should integrate with `VoxelVisualMaterial` from 9.1
- Material Creator Wizard
- Batch Material Importer

**9.7 Performance Profiling:**
- Should include LOD system metrics from 9.2
- `Schedule().Complete()` is synchronous - need double-buffering
- `NativeArray` allocations per-chunk - need pooling

**9.8 Chunk Loading Visualization:**
- Should display LOD levels from 9.2
- Streaming state visualization

**9.9 Voxel Performance Optimization:**
- Burst-compiled explosion math
- Loot event aggregation
- Asynchronous time-slicing

---

## Tool Requirements for Asset Store

### All Tools Must:
1. **Auto-detect file types** - If a user drags a bunch of textures, the tool should classify them (Diffuse, Normal, etc.) automatically.
2. **Validate before apply** - Show errors clearly.
3. **Preview in editor** - See terrain changes instantly.
4. **Undo support** - Critical for editor tools.
5. **Bulk operations** - Apply settings to 100 biomes at once.

### Existing Tools (from 9.1 - 9.4):
- `MaterialVisualEditor` - Auto-detects texture types ✅
- `TextureArrayBuilder` - Batch operations ✅
- `LODVisualizerWindow` - Runtime statistics ✅
- `VoxelLODConfigEditor` - Quick presets ✅
- `VoxelNetworkStatsWindow` - Network monitoring ✅ *(9.3)*
- `VoxelExplosionTesterWindow` - Scene View explosion testing ✅ *(9.4)*

---

## Definition of Done

- [x] Terrain looks polished (smooth normals, triplanar texturing). *(9.1)*
- [x] LOD system maintains 60fps at 200m+ view distance. *(9.2)*
- [ ] Networking optimized (< 50 KB/s with intense modification). *(9.3)*
- [x] Visual configurations via designer-friendly tools. *(9.1)*
- [x] LOD configurations via designer-friendly tools. *(9.2)*
- [ ] Debug tools help identify issues quickly. *(9.5)*
- [ ] Performance profiling suite catches regressions. *(9.7)*

---

## Dependencies

| Depends On | Provides To |
|------------|-------------|
| Epic 8 (Core Voxel) | Base systems |
| — | Epic 10 (Advanced Generation) |
| — | Epic 11 (Resource Systems) |

