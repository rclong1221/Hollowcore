# EPIC 10.14: Memory Optimization (Allocations)

**Status**: � IN PROGRESS  
**Priority**: CRITICAL  
**Dependencies**: None

---

## Problem Statement
Performance captures indicate a **Critical Memory Leak**:
- **Allocation Rate**: ~71 MB/s (Extremely High)
- **Managed Memory**: Growing ~6MB/sec (+200MB in 30s)
- **Native Memory**: Growing ~4MB/sec (+138MB in 30s)
- **GC Pressure**: 1.5MB allocated *per frame*

This will lead to:
1. Frequent GC spikes (stuttering).
2. Out Of Memory crashes on consoles/mobile.
3. Degraded performance over long sessions.

**Likely Causes**:
1. `new` array allocations in `OnUpdate`.
2. `NativeArray` not being disposed (Temp/TempJob leak).
3. String concatenation in debug logging or UI.
4. Improper use of `Allocator.Temp` vs `Allocator.TempJob`.

---

## Objectives
1. **Identify the Source**: Use Memory Profiler / Deep Profiling to find the 71MB/s source.
2. **Eliminate Per-Frame Allocations**: Convert to `NativeArray` (persistent) or re-use buffers.
3. **Fix Leaks**: Ensure all NativeCollections are `Dispose()`d.

---

## Tasks

### Task 10.14.1: Allocation Profiling
- [x] Run Unity Memory Profiler.
- [x] Identify top allocators (Bytes per frame) -> `VoxelProfiler`, `PerformanceDashboard`, `string.Format`.
- [x] Check `ChunkMeshingSystem` and `ChunkColliderBuildSystem` for allocation loops.

### Task 10.14.2: Code Audit & Fixes
- [x] Replace `new T[]` with `NativeArray<T>` or pre-allocated buffers.
- [x] Check `ToEntityArray(Allocator.Temp)` usage in tight loops.
- [x] Check string usage (`Debug.Log`, UI).

### Task 10.14.3: Native Memory Leak Fix
- [x] Check for `NativeList` or `BlobAsset` leaks.
- [x] Verify `Dispose()` in `OnDestroy` for all systems (Added Reference Counting to Services).

---

## Acceptance Criteria
- [ ] **Allocation Rate < 100 KB/s** (Idle).
- [ ] **Allocation Rate < 1 MB/s** (Active moving).
- [ ] Zero "Growing" trend in managed/native memory over 60s.
