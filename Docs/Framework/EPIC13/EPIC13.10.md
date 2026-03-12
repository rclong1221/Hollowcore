# EPIC 13.10: Performance Optimization

> **Goal:** Address critical performance bottlenecks identified in Voxel Physics and Entity Lifecycle management.
> **Priority:** HIGH (Blocker for playable framerates)

## Problem Analysis
Profiler data reveals two major bottlenecks:
1.  **Server Lag (6ms+ spikes):** `ChunkColliderBuildSystem` on the Server World is taking excessively long (up to 30ms+ in spikes) to bake physics meshes. This is likely happening synchronously or choking the main thread.
2.  **Client Lag (19% CPU):** `Unity.Entities.StructuralChange` is consuming ~20% of CPU time on the Client. This suggests frequent entity creation/destruction, likely from Voxel Chunks being streamed or re-meshed inappropriately.

## Objectives
1.  **Refactor Collider Baking**:
    - Move `ChunkColliderBuildSystem` to a fully jobified, asynchronous pipeline.
    - Ensure `PhysicsShape.Bake` or equivalent mesh generation happens in Burst-compiled jobs.
    - Slicing: If baking is too heavy for one frame, slice the workload across multiple frames.

2.  **Optimize Chunk Lifecycle**:
    - Investigate usage of `EntityManager.CreateEntity` vs `Instantiate`.
    - Implement pooling or optimize the archetype creation to minimize structural changes.
    - Ensure `VoxelMeshingSystem` does not destroy/recreate entities for mesh updates, but rather updates existing components.

3.  **Validate Performance**:
    - Use the Profiler to confirm `ChunkColliderBuildSystem` drops to <1ms on main thread.
    - Reduce `StructuralChange` impact to <5%.

## Implementation Plan
- [ ] **13.10.1 profiling & Analysis**: Create deep profile trace to pinpoint exact lines (Completed via screenshots).
- [ ] **13.10.2 Async Collider Baking**: Rewrite `ChunkColliderBuildSystem` to use `BlobAssetReference` generation in jobs.
- [ ] **13.10.3 Structural Change Reduction**: Refactor `ChunkStreamingSystem` to use Entity Command Buffers (ECB) strictly and potentially pre-allocate chunks.
