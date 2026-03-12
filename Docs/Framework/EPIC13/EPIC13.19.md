# EPIC 13.19: Server-Authoritative Ragdoll Sync

> **Status:** IMPLEMENTED
> **Priority:** HIGH
> **Dependencies:** EPIC 10.17 (Physics & Collision Bug Fixes - Ragdoll Presentation)
> **Reference:** `Assets/Scripts/Player/Systems/RagdollHipsSyncSystem.cs`, `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs`

## Overview

Death ragdolls now display correctly on all clients. Previously, other players saw the dead body frozen at the death position because:

1. **Server** runs ragdoll physics simulation on bone entities (via `RagdollTransitionSystem`)
2. **Bone entities** are children of the player ghost, but child `LocalTransform` is **not replicated by default**
3. **Clients** have kinematic ragdolls that follow entity position, but entity position is never updated
4. **No hips position was replicated** to other clients

This epic implements **server-authoritative ragdoll synchronization** by having the server copy the physics-simulated pelvis position to a replicated component on the root player entity.

---

## Implementation Summary

### Files Created
| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Components/RagdollHipsSync.cs` | Networked component for hips position/rotation sync |
| `Assets/Scripts/Player/Systems/RagdollHipsSyncSystem.cs` | Server writer + Client reader systems + Diagnostics |
| `Assets/Scripts/Player/Utilities/RagdollHipsSyncDebugger.cs` | Inspector-based debug controls |

### Files Modified
| File | Changes |
|------|---------|
| `Assets/Scripts/Player/Authoring/PlayerAuthoring.cs` | Added `RagdollHipsSync` component to baker |
| `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs` | Added `SetRemoteSyncData()` method, modified `SyncToEntityPosition()` |
| `Assets/Scripts/Player/Components/DeathState.cs` | Changed `GhostPrefabType.AllPredicted` to `GhostPrefabType.All` |

---

## Critical Implementation Details

### GhostPrefabType Fix

A critical discovery during implementation: **`GhostPrefabType.AllPredicted` only replicates to predicted ghosts (owned players)**, not to interpolated ghosts (other players you observe).

Both `RagdollHipsSync` and `DeathState` required this fix:

```csharp
// WRONG - only replicates to predicted ghosts (owned players)
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]

// CORRECT - replicates to ALL ghosts including interpolated (observers)
[GhostComponent(PrefabType = GhostPrefabType.All)]
```

This is required because:
- **Owned players** run local physics simulation (predicted ghost)
- **Observed players** need the sync data to display ragdolls (interpolated ghost)
- `AllPredicted` excludes interpolated ghosts from receiving the data

### Component Structure

**RagdollHipsSync Component:**
```csharp
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct RagdollHipsSync : IComponentData
{
    [GhostField(Quantization = 100)]  // 1cm precision
    public float3 Position;

    [GhostField(Quantization = 1000)] // ~0.1 degree precision
    public quaternion Rotation;

    [GhostField]
    public bool IsActive;
}
```

---

## Architecture

### Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ SERVER                                                          │
│                                                                 │
│  Player dies → DeathState.Phase = Dead                          │
│     ↓                                                           │
│  RagdollTransitionSystem activates bone physics                 │
│     ↓                                                           │
│  Unity Physics simulates Pelvis entity each tick                │
│     ↓                                                           │
│  RagdollHipsSyncServerSystem copies Pelvis LocalTransform       │
│     → RagdollHipsSync component (on ROOT entity)                │
│     ↓                                                           │
│  GhostField replicates to ALL clients (delta compressed)        │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ CLIENT (Observer)                                               │
│                                                                 │
│  Receives RagdollHipsSync via ghost snapshot                    │
│     ↓                                                           │
│  RagdollHipsSyncReaderSystem reads component                    │
│     ↓                                                           │
│  Passes position to RagdollPresentationBridge.SetRemoteSyncData │
│     ↓                                                           │
│  SyncToEntityPosition() applies to kinematic ragdoll root       │
│     ↓                                                           │
│  Ragdoll visually follows server physics simulation             │
└─────────────────────────────────────────────────────────────────┘
```

### System Attributes

| System | World Filter | Update Group | Update After |
|--------|--------------|--------------|--------------|
| `RagdollHipsSyncServerSystem` | `ServerSimulation` | `SimulationSystemGroup` | `PhysicsSystemGroup` |
| `RagdollHipsSyncReaderSystem` | `ClientSimulation` | `PresentationSystemGroup` | - |

---

## Diagnostics & Testing

### RagdollHipsSyncDebugger

Add `RagdollHipsSyncDebugger` component to any GameObject in the scene to enable diagnostics via Inspector.

**Controls:**
| Field | Purpose |
|-------|---------|
| `EnableServerLogging` | Log server-side sync updates (host/server only) |
| `EnableClientLogging` | Log client-side sync reads (all clients) |
| `EnablePositionComparison` | Log position comparison between server and visual |
| `LogIntervalFrames` | Frames between logs (30 = ~0.5 sec at 60fps) |
| `EnableAll` / `DisableAll` | Quick toggle buttons |

### Diagnostic Log Examples

**Server writing sync data:**
```
[RagdollHipsSync:Server] Entity 2 ragdoll STARTED, Pelvis=19, Pos=(1.5, 0.3, -6.5)
[RagdollHipsSync:Server] Entity 2 syncing Pos=(1.5, 0.28, -6.5)
```

**Client reading sync data:**
```
[RagdollHipsSync:Client] Entity 3 applied sync Pos=(1.5, 0.28, -6.5) to 'Warrok_Client(Clone)'
[RagdollHipsSync:Client] Summary: Processed=1, SkippedOwned=0, SkippedAlive=0, SkippedNoSync=0
```

**Position comparison (for testing same-spot landing):**
```
[RagdollHipsSync:Compare] Entity 3 ServerPos=(1.5, 0.3, -6.5) VisualPos=(1.5, 0.31, -6.5) Diff=0.012m
```

**Movement detection (body being pushed while dead):**
```
[RagdollHipsSync:Movement] Entity 2 MOVED 1.25m: (1.5, 0.3, -6.5) -> (2.75, 0.5, -6.3)
```

### Testing Same-Spot Landing

To verify bodies land in the same spot on all clients:

1. Enable `EnablePositionComparison` in the debugger
2. Kill a player while another player observes
3. Check the `[RagdollHipsSync:Compare]` logs
4. `Diff` value should be small (< 0.1m) when ragdoll settles

### Moving Bodies While Dead

The system automatically supports moving dead bodies:

1. Server physics continues running on the ragdoll
2. `RagdollHipsSyncServerSystem` reads Pelvis position **every frame**
3. Position changes replicate via GhostField
4. Client kinematic ragdolls follow the synced position

If something moves the body (explosion, physics interaction), observers see the movement.

---

## Bandwidth Analysis

### Server-Authoritative GhostField (This Design)

| Component | Raw Size | Delta Compressed |
|-----------|----------|------------------|
| Position (float3, Q=100) | 12 bytes | ~3-6 bytes |
| Rotation (quaternion, Q=1000) | 16 bytes | ~2-4 bytes |
| IsActive (bool) | 1 byte | ~0 bytes (unchanged) |
| **Per snapshot** | 29 bytes | **~5-10 bytes** |

At server tick rate (60 Hz):
- **~300-600 bytes/sec** per ragdolling player (with delta compression)
- Ragdoll duration: ~3-5 seconds
- **Total per death: ~1-3 KB**

### Comparison to Alternatives

| Approach | Bytes/sec | Total/Death | Notes |
|----------|-----------|-------------|-------|
| **Server GhostField (this)** | ~300-600 | ~1-3 KB | Delta compressed, optimal |
| Client RPC stream | ~800-900 | ~3-4 KB | No compression, 4x overhead |
| All bones (15-20) | ~3-6 KB | ~10-20 KB | Child entity overhead |
| Final position only | N/A | ~50 bytes | No motion visible |

---

## Unity Editor Setup

### Required: None (Fully Automatic)

This implementation requires **no manual Unity Editor setup**. Everything is configured in code:

| Setup Step | How It's Done |
|------------|---------------|
| Add RagdollHipsSync component | Automatic via `PlayerAuthoring` baker |
| Configure GhostFields | Automatic via attributes in component |
| Server system registration | Automatic via `[WorldSystemFilter]` |
| Client system registration | Automatic via `[WorldSystemFilter]` |

### Optional: Debug Helper

To enable diagnostics:
1. Add empty GameObject to scene
2. Attach `RagdollHipsSyncDebugger` component
3. Enable desired logging options in Inspector

---

## Verification Checklist

- [x] Server writes sync data when player ragdolls
- [x] `IsActive` flag correctly set/cleared
- [x] Position replicates to interpolated ghosts (observers)
- [x] `DeathState.Phase` replicates to interpolated ghosts
- [x] Client reader finds presentation GameObject
- [x] `SetRemoteSyncData()` called on bridge
- [x] Kinematic ragdoll moves to server position
- [x] Smooth interpolation for small desyncs
- [x] Snap for large desyncs (> threshold)
- [x] Sync state cleared on ragdoll exit
- [x] Movement while dead syncs correctly
- [x] Diagnostic logging available

---

## Design Rationale

### Why Server-Authoritative?

| Approach | Problem |
|----------|---------|
| Client writes GhostField | GhostFields only replicate Server → Client |
| Client sends RPC stream | ~4-7x more bandwidth than delta-compressed GhostFields |
| **Server writes GhostField** | Optimal - uses delta compression, server authoritative |

### Why Not Replicate Bone Entities Directly?

Per Unity NetCode docs, child entities default to `DontSerializeVariant`. Enabling child replication for 15-20 bones would:
- Require prefab configuration for each bone
- Use random-access serialization (slower)
- Increase bandwidth significantly

**Syncing only the root pelvis position is simpler and more efficient.**

### Why Hips Only?

1. **Hips are the ragdoll root** - Other bones follow via joints
2. **Limb positions are secondary** - Viewers focus on body location
3. **Physics is chaotic** - Exact limb positions never match even with same inputs
4. **Bandwidth scales** - 1 bone = minimal overhead, 15-20 bones = significant

---

## Related Systems

| System | Relationship |
|--------|--------------|
| `RagdollTransitionSystem` | Activates server physics simulation |
| `RagdollRecoverySystem` | Deactivates ragdoll on revival |
| `DeathState` | Triggers ragdoll via phase change |
| `RagdollPresentationBridge` | Applies sync data to visual ragdoll |
| `RagdollSettleSyncSystem` | Signals when ragdoll settles (still used for settle detection) |

---

## Future Improvements

1. **Distance culling** - Skip sync for ragdolls far from any player
2. **LOD sync rate** - Reduce update frequency for distant ragdolls
3. **Velocity extrapolation** - Predict position between snapshots for smoother visuals
