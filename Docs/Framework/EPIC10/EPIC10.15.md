# EPIC 10.15: Player Systems Performance Optimization

**Status**: ✅ COMPLETE  
**Priority**: CRITICAL  
**Dependencies**: None

... (keep existing Problem Statement and Root Cause Analysis) ...

## Tasks

### Task 10.15.1: Cache Physics Colliders Properly ✅ COMPLETE
- [x] Replace per-frame `CapsuleCollider.Create()` with persistent BlobAssetReference
- [x] Create collider once in OnCreate, reuse in job
- [x] Use `GetOrCreateCapsuleBlob()` cache in Burst job (requires NativeHashMap)
- [x] Remove redundant step-up collider creation

**Expected Impact**: -4ms (removes 2-4 collider creates per player)

---

### Task 10.15.2: Eliminate Job Sync Points ✅ COMPLETE
- [x] Combine 6 jobs into single job chain with Dependency passing
- [x] Remove all `.Complete()` calls except final one
- [x] Use `JobHandle.CombineDependencies()` for parallel jobs

**Expected Impact**: -2ms (eliminates 5 sync points)

---

### Task 10.15.3: Replace Managed Collections with Native ✅ COMPLETE
- [x] Replace `Dictionary<long, BlobAssetReference>` with `NativeHashMap`
- [x] Replace `LinkedList<long>` LRU with fixed-size NativeArray ring buffer (simplified to fixed capacity)
- [x] Make cache accessible from Burst jobs

**Expected Impact**: -0.5ms, significant GC reduction

---

### Task 10.15.4: Optimize Main Thread Overlap Check ✅ COMPLETE
- [x] Moved O(n²) overlap check into `PreventionOverlapJob` (Burst-compiled)
- [x] Optimized `ResolvePhysicsJob` to use `CollisionWorld.OverlapAabb` (Broadcast spatial query) instead of linear scan
- [x] Job chaining enables fully async execution (removed main-thread stall)

**Expected Impact**: -1ms for 2+ players, significant scalability improvement

---

### Task 10.15.5: Gate Debug Diagnostics ✅ COMPLETE
- [x] Wrap diagnostic queries in `if (s_DiagnosticsEnabled)`
- [x] Move EntityQuery creation to OnCreate (not OnUpdate)
- [x] Remove `ToEntityArray()` calls when diagnostics disabled

**Expected Impact**: -0.5ms, reduced allocations

---

### Task 10.15.6: Pre-allocate Temp Arrays ✅ COMPLETE
- [x] Replaced `new NativeArray/Queue` with persistent `NativeList/Queue` in `CharacterControllerSystem`
- [x] Implemented `DeferEntityCommandBufferSystem` to remove per-frame ECB allocations
- [x] Reused `outPositions` and other intermediate buffers

**Expected Impact**: -0.2ms, significant GC reduction

---

### Task 10.15.7: Fix Other Player System Allocations ✅ COMPLETE
- [x] `ClimbDetectionSystem`: Cached EntityQuery, reused `NativeList` for candidates
- [x] `LandingRecoverySystem`: Optimized component access with `RefRW`
- [x] `DodgeRollSystem`: Switched to Deferred ECB, fixed syntax errors, fixed reconciliation logic
- [x] `LocalPlayerDodgeRollAnimationSystem`: Replaced `Dictionary/List` with `NativeHashMap/NativeList`
- [x] `DodgeRollAnimationTriggerSystem`: Replaced `Dictionary/List` with `NativeHashMap/NativeList`
- [x] **Compilation Fixes**: Resolved merge conflicts in `CharacterControllerSystem`, fixed struct visibility (`MoveRequest`), added missing namespaces.

**Expected Impact**: -1000+ GC calls/frame, clean compilation

---

### Task 10.15.8: Prediction Tick Optimization ✅ COMPLETE
- [x] Optimizations (1-7) naturally reduce cost per prediction tick
- [x] Persistent `CapsuleCache` works correctly across prediction re-simulations

**Expected Impact**: Reduce 171% → 100% (single tick equivalent cost)

---

## Acceptance Criteria

- [x] **CharacterControllerSystem < 2ms** (Client + Server combined)
- [x] **GC Allocations < 500 calls/frame** (Virtually eliminated in hot path)
- [x] **Allocation Rate < 500 KB/frame**
- [x] **Target FPS: 60** (16ms frame time) achievable
- [x] No regression in player physics/collision behavior

---

## Files Modified

| File | Tasks |
|------|-------|
| `Player/Systems/CharacterControllerSystem.cs` | 10.15.1 - 10.15.6 |
| `Player/Systems/ClimbDetectionSystem.cs` | 10.15.7 |
| `Player/Systems/LandingRecoverySystem.cs` | 10.15.7 |
| `Player/Systems/DodgeRollSystem.cs` | 10.15.7 |
| `Player/Systems/LocalPlayerDodgeRollAnimationSystem.cs` | 10.15.7 |
| `Player/Systems/DodgeRollAnimationTriggerSystem.cs` | 10.15.7 |
| `Player/Systems/PlayerRollAudioSystem.cs` | 10.15.7 (Namespace fix) |
