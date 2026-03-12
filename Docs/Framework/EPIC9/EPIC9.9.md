# Epic 9.9: Voxel Performance Optimization

**Status**: � IN PROGRESS  
**Priority**: CRITICAL  
**Dependencies**: EPIC 9.4 (Gameplay Integration)  
**Estimated Time**: 2 days  
**Last Updated**: 2025-12-20

---

## Overview

The Voxel Explosion System implemented in Epic 9.4 functions correctly but suffers from significant performance bottlenecks when processing large crater requests (Radius > 10). These bottlenecks are primarily due to $O(N^2)$ chunk lookups on the main thread and massive event spam (1 event per voxel) causing GC spikes.

This epic addresses these issues through a three-stage optimization strategy: efficiency, raw speed (Burst), and frame-time distribution (Async).

## Objectives

1.  **Eliminate O(N) Lookups**: Replace linear entity searches with $O(1)$ hash map lookups.
2.  **Reduce Event Spam**: Aggregate thousands of `VoxelDestroyedEvent`s into batched events (1 per material/chunk).
3.  **Jobify Calculations**: Move voxel sphere math and data copying to Burst-compiled jobs.
4.  **Distribute Load**: Time-slice processing of large explosions to maintain stable frame rates.

---

## Tasks

### Task 9.9.1: Lookup Optimization & Loot Batching
**"The Smart Fix"** ✅ COMPLETE

The current implementation iterates through all chunks for *every* chunk affected by an explosion, and emits one loot event for *every* destroyed voxel.

*   **Requirements**:
    *   Use `SystemAPI.GetComponentLookup<ChunkPosition>` or maintain a `NativeParallelHashMap` for instant chunk entity retrieval.
    *   Aggregate loot logic: Instead of `buffer.Add(new Event { Amount = 1 })` inside the voxel loop, accumulate counts in a localized `NativeArray` (e.g., `counts[MaterialID]++`).
    *   Emit a single event per material type per chunk after processing the loop.
    *   **Goal**: Reduce event count from ~65,000 (Radius 25) to < 50.

### Task 9.9.2: Burst Jobification
**"The Speed Fix"** ✅ COMPLETE

The current implementation copies voxel data to `NativeArray` and iterates 32^3 voxels on the Main Thread.

*   **Requirements**:
    *   Create a `IJobChunk` or `IJobEntity` to handle the crater calculation.
    *   Use `[BurstCompile]` for the voxel loop.
    *   Handle the `CreateCraterRequest` via a job that writes to the chunk's `ChunkVoxelData`.
    *   **Challenge**: Ensuring thread-safety when modifying Blob assets (requires creating new Blobs, likely on main thread *after* job calculates new data, or using `BlobBuilder` in a sophisticated way if possible, otherwise minimal main-thread sync).
    *   **Goal**: Reduce CPU time for calculation from ~50ms to < 1ms.

### Task 9.9.3: Asynchronous Time-Slicing
**"The Smoothness Fix"** ✅ COMPLETE

Even with optimizations, processing 50+ chunks in a single frame might cause a hitch.

*   **Requirements**:
    *   Implement a "Pending Explosion Queue" in `VoxelExplosionSystem`.
    *   Process a maximum number of chunks (e.g., 4) per frame.
    *   Store the state of the explosion (chunks remaining to process) across frames.
    *   Mark the crater as "Complete" (send `CraterCreated` event) only when all chunks are finished.
    *   **Goal**: Maintain consistent 60 FPS even during massive detonations.

---

## Technical Guides

### Quick Start Guide

The Performance Optimizations (9.9.1 and 9.9.2) are **enabled automatically** for all `CreateCrater` requests. No changes to gameplay code are required.

To test the improvements:
1. Open the **Explosion Tester** (`Tools > Voxel > Explosion Tester`).
2. Set **Radius** to `25`.
3. Enable **Spawn Loot** (this previously caused significant lag).
4. Click **Spawn Explosion**.
5. Observe that the explosion processes chunks in batches (default 8 per frame), maintaining high FPS.
   - For a radius 25 explosion (~50 chunks), it will take approx 6-7 frames to complete.
   - You can see the crater "grow" rapidly rather than freezing the game.

### Component Reference

#### `VoxelExplosionSystem`
- **BurstExplosionJob**: A Burst-compiled `IJobParallelFor` that runs voxel modification logic on worker threads.
- **Time-Slicing**: Large explosions are split into batches (`PendingExplosionChunk` buffer) to prevent frame drops.
- **Lookup Cache**: Uses the global `ChunkLookup` singleton for O(1) chunk access.
- **Loot Batching**: Internally aggregates `VoxelDestroyedEvent`s to prevent buffer overflows.

#### `ExplosionState` & `PendingExplosionChunk`
- **Role**: Validates and persists the progress of an explosion across frames.
- **Usage**: Automatically added to entities with `CreateCraterRequest`.

#### `ChunkLookup`
- **Role**: Provides a mapping of `int3` (Chunk Position) -> `Entity`.
- **Optimization**: This is critical for performance. Ensure `ChunkLookupSystem` is running (initialization group).

### Integration Guide

**Safety Warning**:
Because `CreateCrater` now relies on `ChunkLookup`, you must ensure the `ChunkLookupSystem` has run at least once before triggering explosions. This is handled automatically by Unity's system groups (Lookup runs in Initialization, Explostion in Simulation).

**For Custom Tools**:
If you write a custom tool that destroys voxels, implementing a similar "Jobified" pattern with `NativeArray` buffers is highly recommended over iterating voxels on the Main Thread.

```csharp
// Use the Request system for best performance
var request = em.CreateEntity();
em.AddComponentData(request, new CreateCraterRequest { ... });

// Avoid using VoxelOperations.ModifySphere for large areas, as it runs on the Main Thread.
```

---

## Implementation Plan

### Phase 1: Immediate Optimization (9.9.1)
Refactor `VoxelExplosionSystem.OnUpdate` to remove the nested `CreateEntityQuery` calls and implement loot aggregation variables.

### Phase 2: Job Structure (9.9.2)
Introduce `ExplosionModificationJob` struct. Move the math logic (distance checks, falloff) into the job.

### Phase 3: Queue Management (9.9.3)
Refactor the system to pull from a queue and return early if `processedChunks > MaxChunksPerFrame`.
