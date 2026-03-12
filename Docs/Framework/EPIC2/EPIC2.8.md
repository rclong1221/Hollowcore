# EPIC 2.8: Multiplayer Player State Bug Fixes

**Status**: 🔄 IN PROGRESS  
**Priority**: CRITICAL  
**Dependencies**: EPIC 4.1 (Health/Damage), EPIC 10.17 (Ragdoll)

---

## Overview

Critical bugs have been identified where player death/ragdoll state incorrectly affects all players in multiplayer sessions instead of just the individual player.

---

## Bug 2.8.1: RespawnDebugSystem Uses GhostOwnerIsLocal on Server

### Problem

When using debug keys (Shift+K, Shift+D, Shift+R) to force death states, **ALL players** in the game are affected instead of just the local player.

### Root Cause

`RespawnDebugSystem.cs` line 70:
```csharp
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)] // Runs on SERVER
...
foreach (var (state, health) in SystemAPI.Query<...>().WithAll<GhostOwnerIsLocal>())
```

The system runs on the **ServerSimulation** world but uses `GhostOwnerIsLocal` filter. On the server:
- `GhostOwnerIsLocal` is a **client-side** concept
- Server doesn't have a "local" player - it manages ALL players
- The query may match no players, all players, or behave unexpectedly

### Solution

1. Change system to run on **ClientSimulation** and use RPC to request server action
2. Or: Add connection entity tracking to identify which player pressed the key

### Files
- `Assets/Scripts/Player/Systems/RespawnDebugSystem.cs`

---

## Bug 2.8.2: Physics.Simulate() in Ragdoll Affects Global Physics

### Problem

When one player's ragdoll activates, calling `Physics.Simulate()` simulates **ALL Unity physics objects** in the scene, potentially affecting other players' ragdolls or physics-based objects.

### Root Cause

`RagdollPresentationBridge.cs` line 112-115:
```csharp
private void FixedUpdate()
{
    if (!_isRagdolled) return;
    
    if (Physics.simulationMode == SimulationMode.Script)
    {
        Physics.Simulate(Time.fixedDeltaTime);  // Simulates EVERYTHING
    }
}
```

When NetCode sets `Physics.simulationMode = Script`:
- Unity physics doesn't auto-simulate
- Each active ragdoll calls `Physics.Simulate()` globally
- Multiple active ragdolls = multiple redundant simulations
- All physics objects advance together, causing unexpected interactions

### Solution

1. **Single simulation coordinator**: Create a MonoBehaviour singleton that calls `Physics.Simulate()` once per frame when ANY ragdoll is active
2. **Track active ragdoll count**: Only simulate when count > 0
3. **Avoid redundant calls**: Ensure simulation happens exactly once per FixedUpdate

### Files
- `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs`
- New: `Assets/Scripts/Player/Physics/RagdollPhysicsSimulator.cs` (singleton coordinator)

---

## Bug 2.8.3: CameraManager Finds Wrong Player's RagdollBridge

### Problem

In multiplayer, the camera may follow the wrong player's ragdoll when dying.

### Root Cause

`CameraManager.cs` line 583:
```csharp
_cachedRagdollBridge = Object.FindObjectOfType<RagdollPresentationBridge>();
```

`FindObjectOfType` returns the **first** matching component found in the scene, which could be any player's ragdoll bridge - not necessarily the local player's.

### Solution

Use `GhostPresentationGameObjectSystem` to get the correct presentation GameObject for the local player entity, then get its `RagdollPresentationBridge` component.

### Files
- `Assets/Scripts/Systems/Camera/CameraManager.cs`

---

## Bug 2.8.4: DeathPresentationSystem Shared State Variables

### Potential Issue (To Verify)

`DeathPresentationSystem.cs` uses instance variables to track state:
```csharp
private float _lastLocalHealth;
private DeathPhase _lastDeathPhase;
```

If multiple players are in the scene, these shared variables might cause incorrect state tracking across players.

### Investigation Required
- [x] Verify query uses `GhostOwnerIsLocal` correctly (only local player processed)
- [x] Test with 2+ players in same session
- [x] Confirm each player's death is independent

### Files
- `Assets/Scripts/Player/Systems/DeathPresentationSystem.cs`

---

## Tasks

### Task 2.8.1: Fix RespawnDebugSystem ✅ COMPLETE
- [x] Change to ClientSimulation world filter (`RespawnDebugClientSystem`)
- [x] Add RPC-based approach for server mutation (`DeathDebugRpc`)
- [x] Use connection entity (`GhostOwner.NetworkId`) to identify caller on server

**Implementation:**
- New: `DeathDebugRpc.cs` - RPC component for death state requests
- Refactored: `RespawnDebugSystem.cs` → Client/Server split:
  - `RespawnDebugClientSystem` (Client) - Detects input, sends RPC
  - `RespawnDebugServerSystem` (Server) - Matches player by `GhostOwner.NetworkId`

**Estimated Effort**: 2 hours

---

### Task 2.8.2: Fix Global Physics.Simulate() ✅ COMPLETE
- [x] Create `RagdollPhysicsSimulator` singleton MonoBehaviour
- [x] Track ragdoll active count
- [x] Call `Physics.Simulate()` exactly once per FixedUpdate when needed
- [x] Remove individual `Physics.Simulate()` calls from bridges

**Implementation:**
- New: `RagdollPhysicsSimulator.cs` - Singleton that simulates physics once per frame if registered ragdolls > 0
- Modified: `RagdollPresentationBridge.cs` - Registers/Unregisters with simulator instead of calling Simulate directy

**Estimated Effort**: 2 hours

---

### Task 2.8.3: Fix CameraManager Bridge Lookup ✅ COMPLETE
- [x] Store reference to local player entity
- [x] Use `GhostPresentationGameObjectSystem` to get correct presentation GO
- [x] Get `RagdollPresentationBridge` from that GO specifically
- [x] Remove `FindObjectOfType` usage

**Implementation:**
- Modified `CameraManager.cs` to use `GhostPresentationGameObjectSystem.GetGameObjectForEntity()` instead of global `FindObjectOfType`.
- Added per-entity caching (`_cachedEntity` + `_cachedRagdollBridge`).

**Estimated Effort**: 1 hour

---

### Task 2.8.4: Verify DeathPresentationSystem Isolation
- [x] Test 2-player scenario with one dying
- [x] Confirm only dying player shows death effects
- [x] Verify health tracking is per-player

**Estimated Effort**: 1 hour (testing)

---

## Bug 2.8.5: All Players Teleport to Spawn When One Dies

### Problem

When one player dies, after the respawn delay (~5 seconds), **ALL players** in the game teleport to the spawn point.

### Root Cause (Cascading Bug)

This is a **cascade effect** from Bug 2.8.1:

1. **Bug 2.8.1 triggers**: Debug keys or other issue causes ALL players to enter `DeathPhase.Dead` simultaneously
2. **DownedRulesSystem**: If all players become `Downed`, after `BleedOutDuration` (60s) they transition to `Dead`
3. **RespawnSystem**: After `RespawnDelay` (5s), ALL entities with `DeathPhase.Dead` are respawned and teleported to the spawn point

The `RespawnSystem` correctly processes only `Dead` players, but if ALL players are incorrectly marked as `Dead`, then ALL get respawned.

### Evidence in Code

`RespawnSystem.cs` line 71-76, 108-109:
```csharp
void Execute(ref DeathState deathState, ... ref LocalTransform transform, ...)
{
    if (deathState.Phase != DeathPhase.Dead) return;  // Correct filter
    if ((float)(CurrentTime - deathState.StateStartTime) < Delay) return;  // Correct delay
    
    // But if ALL are Dead, ALL get this:
    deathState.Phase = DeathPhase.Alive;
    transform.Position = SpawnTransforms[bestIndex].Position;  // TELEPORT
}
```

### Additional Concerns

**RagdollSettleSyncSystem** - The RPC sends player entity directly:
```csharp
EntityManager.AddComponentData(rpcEntity, new RagdollSettledRpc
{
    FinalPosition = _pendingPosition,
    PlayerEntity = localPlayer  // Entity IDs may not match server-side!
});
```

Entity IDs are **world-local** - the same entity may have different IDs on client vs server. The server receiving this RPC may update the wrong player or fail validation.

### Solution

1. **Fix Bug 2.8.1** (root cause) - ensures only one player dies
2. **Validate RagdollSettledRpc** - use `GhostId` instead of raw `Entity` for cross-world entity identification
3. **Add defensive checks** - RespawnSystem should log when respawning multiple players simultaneously (indicates bug)

### Files
- `Assets/Scripts/Player/Systems/RespawnDebugSystem.cs` (Bug 2.8.1 - root cause)
- `Assets/Scripts/Player/Systems/RespawnSystem.cs` (add debug logging)
- `Assets/Scripts/Player/Systems/RagdollSettleSyncSystem.cs` (entity ID issue)
- `Assets/Scripts/Player/Components/RagdollSettledRpc.cs` (use GhostId)

---

### Task 2.8.5: Fix All-Players-Teleport Cascade ✅ COMPLETE
- [x] Fix Bug 2.8.1 (root cause - debug system) ✅
- [x] Add debug logging to RespawnSystem for multi-player respawn detection
- [x] Change RagdollSettledRpc to use GhostId instead of Entity
- [x] Update RagdollSettleServerSystem to look up entity by GhostId

**Implementation:**
- Modified `RagdollSettledRpc.cs` to carry `PlayerGhostId`.
- Updated client/server systems in `RagdollSettleSyncSystem.cs` to translate between Entity and GhostId.
- Added logging to `RespawnSystem.cs` to track respawns.

**Estimated Effort**: 3 hours

---

## Bug 2.8.6: CharacterControllerSystem Capsule Cache Memory Leak ✅ FIXED

### Problem

Memory leak detected: 22 persistent allocations from `CapsuleCollider.Create()` in `CharacterControllerSystem`.

### Root Cause

`CharacterControllerSystem.cs` line 157-164 (before fix):
```csharp
if (_capsuleCache.Count < CAPSULE_CACHE_CAPACITY)
{
    _capsuleCache[key] = newBlob;
}
// If cache is full, we still return the blob but don't cache it
// (rare case - most games have few player height variations)
```

When cache was full (64 entries), new blobs were created but NOT added to cache, meaning they were never disposed.

### Solution

Changed to evict an old entry when cache is full:
1. Dispose the evicted blob
2. Remove from cache
3. Add new blob to cache

This ensures all created blobs are tracked and disposed on system shutdown.

### Files
- `Assets/Scripts/Player/Systems/CharacterControllerSystem.cs`

---

### Task 2.8.6: Fix Capsule Cache Memory Leak ✅ COMPLETE
- [x] Evict old entry when cache is full
- [x] Dispose evicted blob before removal
- [x] Always add new blob to cache for proper tracking

---

## Acceptance Criteria

- [x] Debug death keys only affect the player who pressed them ✅
- [ ] One player dying does not affect other players' health/state
- [ ] Camera follows correct player's ragdoll in multiplayer
- [ ] Physics simulation is consistent regardless of ragdoll count
- [ ] Death presentation effects only show for dying player
- [ ] Only the dying player respawns and teleports - others unaffected
- [ ] RagdollSettledRpc correctly identifies player across client/server worlds
- [x] No memory leaks from capsule collider cache ✅

---

## Bug 2.8.7: Settled Ragdoll Position Not Synced When Pushed

### Problem

When a player's ragdoll settles after death, and another player/client pushes/collides with the body, the new position is NOT synchronized to the server or other clients.

### Root Cause

`RagdollPresentationBridge.cs` lines 124-146:
```csharp
private void CheckIfSettled()
{
    if (_hasSettled) return; // Once settled, NEVER syncs again!
    ...
    if (velocity < SettleVelocityThreshold)
    {
        _settleTimer += Time.fixedDeltaTime;
        if (_settleTimer >= SettleTimeRequired)
        {
            _hasSettled = true;  // Set ONCE
            OnRagdollSettled();  // Sync position ONCE
        }
    }
    else
    {
        _settleTimer = 0f;  // Timer reset, but _hasSettled NOT reset!
    }
}
```

**Issue**: 
1. `_hasSettled` is set to `true` after first settle
2. When ragdoll is pushed and starts moving again, `_hasSettled` is NOT reset to `false`
3. `CheckIfSettled()` early-returns on line 126, never syncing the new position

### Solution

When velocity exceeds threshold (ragdoll is moving again), reset `_hasSettled = false` so it can re-settle and sync the new position.

Additionally, consider:
- Server-side ragdoll simulation (more authoritative but complex)
- Continuous position sync while ragdoll is active (bandwidth cost)
- Periodic sync even after settled (compromise solution)

### Files
- `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs`

---

### Task 2.8.7: Fix Settled Ragdoll Re-Sync ✅ COMPLETE
- [x] Reset `_hasSettled = false` when velocity exceeds threshold
- [x] Re-sync position when ragdoll re-settles after being pushed
- [x] Consider adding debounce to prevent sync spam during active pushing (handled by hysteresis)
- [x] Test with 2+ players: push settled ragdoll, verify position updates for all

**Implementation:**
- Updated `RagdollPresentationBridge.CheckIfSettled` to detect movement after settling.
- Uses hysteresis (2x threshold) to avoid flickering state.
- Resets settle state to allow new position correct RPCs to be sent if body is moved.

**Estimated Effort**: 1-2 hours

---

## Acceptance Criteria

- [x] Debug death keys only affect the player who pressed them ✅
- [ ] One player dying does not affect other players' health/state
- [ ] Camera follows correct player's ragdoll in multiplayer
- [ ] Physics simulation is consistent regardless of ragdoll count
- [ ] Death presentation effects only show for dying player
- [ ] Only the dying player respawns and teleports - others unaffected
- [ ] RagdollSettledRpc correctly identifies player across client/server worlds
- [x] No memory leaks from capsule collider cache ✅
- [ ] Pushed/moved ragdoll bodies sync new position to server

---

## Bug 2.8.8: Ragdoll Explodes on Death (Sudden Force)

### Problem

When a player dies, the ragdoll often goes flying with significant force, even if the player was stationary.

### Root Cause

`RagdollTransitionSystem.cs` line 117:
```csharp
Ecb.SetComponent(sortKey, child, velocity);
```

The system inherits `PhysicsVelocity` from the ECS entity to the ragdoll bones. However, in NetCode with client-side prediction, the ECS `PhysicsVelocity` often contains residual prediction corrections or small non-zero values even when visually stationary. When applied to 10+ ragdoll bones simultaneously as an impulse, it causes a massive explosion of force.

### Solution

Clamp or dampen the inherited velocity, or ignore it if below a threshold.

### Files
- `Assets/Scripts/Player/Systems/RagdollTransitionSystem.cs`


---

### Task 2.8.8: Fix Ragdoll Velocity Inheritance ✅ COMPLETE
- [x] Dampen inherited velocity in `RagdollTransitionSystem`
- [x] Add threshold to ignore small velocities
- [x] verify ragdoll drops naturally on death

**Implementation:**
- Modified `RagdollTransitionSystem.cs`:
  - Added threshold check (`linearMag > 0.1f`) to zero out micro-movements.
  - Dampened inherited linear/angular velocity by 50%.
  - Capped maximum linear velocity at 5.0m/s to prevent explosion from large prediction errors.

**Estimated Effort**: 0.5 hours

---

## Bug 2.8.9: Ragdoll Position Desync

### Problem

Ragdolls land in different positions on different clients. Visually inconsistent.

### Root Cause
1. **Client-Side Physics**: Each client simulates physics independently with `Physics.simulationMode = Script`. Small variations (or Bug 2.8.8 sudden forces) lead to divergence.
2. **Missing Sync**: There is no authoritative "ragdoll pose" replication. We only sync the *final settled position* (via Bug 2.8.5/2.8.7 mechanisms), but if that mechanism is failing, they stay desynced forever.

### Solution
- Fix Bug 2.8.8 (Sudden Force) to reduce initial divergence.
- Fix Bug 2.8.5/2.8.7 to ensure final position snaps correctly.
- (Future) Consider scaling bone replication if simple position sync insufficient.

---

### Task 2.8.9: Fix Ragdoll Desync
- [ ] Verify Bug 2.8.8 Fix improves situation
- [ ] Verify Bug 2.8.5/2.8.7 establishes final consistency

**Estimated Effort**: Part of 2.8.5/2.8.8

---

## Bug 2.8.10: Dead Player Bouncing

### Problem
The player keeps bouncing around on its own every 2-3 seconds while dead.

### Potential Root Causes
1. **Server Reconciliation**: Server enforces position X (where it thinks player died), client ragdoll drifts to Y. Server snaps client back to X repeatedly.
2. **Ragdoll Sync Loop**: `RagdollSettledRpc` might be fighting with server physics or `RagdollTransitionSystem` logic.
3. **Ghost Prediction**: Client predicting movement on a kinematic entity?

### Solution Strategy
1. Verify if Server is overriding position.
2. Ensure `RagdollTransitionSystem` properly disables prediction/simulation on dead entities.

---

### Task 2.8.10: Debug & Fix Dead Player Bouncing ✅ COMPLETE
- [x] Determine if bounce is client-side prediction or server correction
- [x] Verify `RagdollTransitionSystem` disables prediction on dead entities
- [x] Fix conflict between ragdoll position and server position
- [x] Resolve RPC serialization mismatch (GhostId vs Entity)

**Implementation:**
- Modified `CharacterControllerSystem` to ignore inputs/movement for entities in `DeathPhase.Dead` or `Downed`.
- Renamed `RagdollSettledRpc` to `RagdollSettledV2Rpc` to force regeneration of serialization code after changing `Entity` to `int` caused a bitstream mismatch (128 vs 160 bits).

**Estimated Effort**: 1 hour

---

## Acceptance Criteria

- [x] Debug death keys only affect the player who pressed them ✅
- [ ] One player dying does not affect other players' health/state
- [x] Camera follows correct player's ragdoll in multiplayer ✅
- [x] Physics simulation is consistent regardless of ragdoll count ✅
- [ ] Death presentation effects only show for dying player
- [ ] Only the dying player respawns and teleports - others unaffected
- [ ] RagdollSettledRpc correctly identifies player across client/server worlds
- [x] No memory leaks from capsule collider cache ✅
- [ ] Pushed/moved ragdoll bodies sync new position to server
- [ ] Ragdolls drop naturally without explosive force
- [ ] Ragdolls settle in the same location for all clients

---

## Files Summary

| File | Bug |
|------|-----|
| `RespawnDebugSystem.cs` | 2.8.1 ✅, 2.8.5 |
| `DeathDebugRpc.cs` | 2.8.1 ✅ (new) |
| `RagdollPresentationBridge.cs` | 2.8.2 ✅, 2.8.7 ✅ |
| `CameraManager.cs` | 2.8.3 ✅ |
| `DeathPresentationSystem.cs` | 2.8.4 (to verify) |
| `RespawnSystem.cs` | 2.8.5 ✅ (logging) |
| `RagdollSettleSyncSystem.cs` | 2.8.5 ✅ (entity ID) |
| `RagdollSettledRpc.cs` | 2.8.5 ✅ (use GhostId) |
| `CharacterControllerSystem.cs` | 2.8.6 ✅ |
| `RagdollTransitionSystem.cs` | 2.8.8 ✅, 2.8.10 |
