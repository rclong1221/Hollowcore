# EPIC 18.17: Multiplayer Damage Number Visibility & Configurable Display

**Status:** IMPLEMENTED
**Priority:** High (multiplayer-blocking — remote clients see zero damage numbers without this)
**Dependencies:**
- `DamageVisualQueue` (existing — `Assets/Scripts/Combat/UI/DamageVisualQueue.cs`, EPIC 15.30, static NativeQueue bridge)
- `DamageEventVisualBridgeSystem` (existing — `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs`, EPIC 15.30, Burst jobs → DamageVisualQueue)
- `DamageVisualRpc` / `DamageVisualRpcReceiveSystem` (existing — `Assets/Scripts/Combat/Systems/`, server → client RPC broadcast)
- `CombatUIBridgeSystem` (existing — `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`, EPIC 15.9/15.22, dequeues DamageVisualQueue → UI)
- `DamageApplicationSystem` (existing — `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs`, CRE → health damage + visual RPC)
- `CombatResolutionSystem` (existing — `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`, creates CombatResultEvent entities)
- `DamageFeedbackProfile` (existing — `Assets/Scripts/Combat/UI/Config/DamageFeedbackProfile.cs`, EPIC 15.22, designer-configurable visual profiles)
- `DamageNumbersProAdapter` (existing — `Assets/Scripts/Combat/UI/Adapters/DamageNumbersProAdapter.cs`, third-party integration)
- `CombatUIRegistry` (existing — `Assets/Scripts/Combat/UI/CombatUIRegistry.cs`, provider interface registry)
- `GameplaySettingsPage` (existing — `Assets/Scripts/Settings/Pages/GameplaySettingsPage.cs`, EPIC 18.2, player settings)
- Unity NetCode (`Unity.NetCode` — `GhostOwner`, `SendRpcCommandRequest`, `ReceiveRpcCommandRequest`)
- DamageNumbersPro (third-party — `DamageNumbersPro` namespace)

**Feature:** Fixes multiplayer damage number visibility so all players see all damage numbers (not just their own), adds per-damage-event source attribution via `GhostOwner.NetworkId`, routes defensive/miss text through the RPC pipeline, and provides designer-configurable visibility modes with optional player override via gameplay settings.

---

## Problem

In multiplayer, only the listen server host sees damage numbers. Remote clients see **zero** damage numbers — hits, crits, DOTs, defensive text, misses — because the `DamageVisualQueue` static NativeQueue is never initialized on pure `ClientSimulation` worlds.

Additionally, defensive results (BLOCKED / PARRIED / IMMUNE) and MISS text only existed as `CombatResultEvent`-based display in `CombatUIBridgeSystem`. Since CRE entities are created in `ServerSimulation` / `LocalSimulation` worlds and `SystemAPI.Query` only queries its own world, these visuals were broken on:
- Remote clients (no CREs exist)
- Listen server ClientWorld (CREs exist in ServerWorld, not ClientWorld)

---

## Root Cause Analysis

### Issue 1: DamageVisualQueue Not Initialized on Remote Clients

```
DamageVisualQueue.Initialize() called by:
  DamageEventVisualBridgeSystem.OnCreate()  [ServerSimulation | LocalSimulation]

On a remote client (only ClientSimulation world):
  DamageEventVisualBridgeSystem is NEVER created
  DamageVisualQueue._queueInitialized remains false
  DamageVisualRpcReceiveSystem.Enqueue() → silently dropped (_queueInitialized guard)
  CombatUIBridgeSystem.TryDequeue() → always returns false
```

### Issue 2: Defensive/Miss Text Has No RPC Path

```
Server:
  CombatResolutionSystem → CombatResultEvent (Blocked/Parried/Immune/Miss)
  DamageApplicationSystem → skips !DidHit and DamagePreApplied CREs
  → NO DamageVisualRpc created for defensive or miss results

Remote client:
  CombatUIBridgeSystem queries CREs → none exist → no defensive/miss text
```

### Issue 3: Prediction Rollback Destroys RPC Entities

```
DamageEventVisualBridgeSystem runs in DamageSystemGroup (PredictedFixedStepSimulationSystemGroup).
NetCode's prediction loop replays multiple ticks per frame.
RPC entities created via ECB.Playback during a prediction tick are DESTROYED
by prediction rollback before the generated RPC send system can serialize them.

Result: RPC entities exist momentarily but never reach the network layer.
Listen server host is unaffected because it reads the shared static DamageVisualQueue
directly (bypasses RPCs entirely).

Fix: Relay pattern — DamageEventVisualBridgeSystem queues RPC data to
DamageVisualQueue.PendingServerRpcs (NativeList). DamageVisualRpcSendSystem
drains that list in SimulationSystemGroup [UpdateAfter PredictedFixedStepSimulationSystemGroup],
creating actual RPC entities OUTSIDE the prediction loop.
```

### Issue 4: SystemAPI.Query Unreliable for Transient RPC Entities

```
DamageVisualRpcReceiveSystem originally used SystemAPI.Query<DamageVisualRpc, ReceiveRpcCommandRequest>().
Source-gen query matching is unreliable for transient entities (RPCs, CREs, PendingCombatHit).
Manual EntityQuery with ToComponentDataArray reliably finds RPC entities on remote clients.

Fix: Rewritten from ISystem with SystemAPI.Query to SystemBase with manual
GetEntityQuery + ToComponentDataArray. Matches pattern used by all other
working RPC receive systems in the project (e.g., CinematicRpcReceiveSystem).
```

### Issue 5: CRE-Based Display Broken on Listen Server ClientWorld

`CombatUIBridgeSystem` runs in ClientWorld but CREs are created in ServerWorld. `SystemAPI.Query<CombatResultEvent>()` only queries the system's own world. So even on the listen server host, CRE-based defensive/miss damage numbers were broken — only hit damage numbers worked (via shared static `DamageVisualQueue` populated by ServerWorld).

---

## Architecture Overview

```
                        SERVER SIMULATION
  DamageVisibilityServerFilter [UpdateBefore DamageApplicationSystem]
  (rebuilds NetworkIdToConnection + player position maps from config)

  CombatResolutionSystem → CombatResultEvent entities
                ↓
  DamageApplicationSystem
  (reads CREs, applies health damage, enqueues ALL result types
   to DamageVisualQueue + creates FILTERED RPCs via DamageVisibilityServerFilter)
                ↓                              ↓
  DamageVisualQueue (shared static)    DamageVisualRpc entities (TARGETED)
  (listen server → direct path)        (SendRpcCommandRequest → NetCode)

  DamageSystemGroup (PredictedFixedStep):
  DamageEventVisualBridgeSystem
  (reads DamageEvent buffers via Burst jobs,
   enqueues to DamageVisualQueue + QUEUES pending RPCs to NativeList,
   SourceNetworkId = -1 for environment/AOE damage)
   NOTE: RPCs NOT created here — prediction rollback would destroy them

  SimulationSystemGroup [UpdateAfter PredictedFixedStepSimulationSystemGroup]:
  DamageVisualRpcSendSystem (relay)
  (drains DamageVisualQueue.PendingServerRpcs → creates FILTERED RPCs
   via DamageVisibilityServerFilter, OUTSIDE the prediction loop)

  Server-Side Filter Logic:
    All     → broadcast (TargetConnection = Entity.Null)
    SelfOnly → targeted RPC to attacker's connection only
    Nearby  → targeted RPC to connections within NearbyDistance of hit
    Party   → targeted RPC to attacker's party members
    None    → no RPCs sent
    Environment (SourceNetworkId == -1) → always broadcast

                        LOCAL SIMULATION (single player)
  No RPCs. DamageVisualQueue shared static path only.
  Client-side filter handles all modes.

                        CLIENT SIMULATION
  DamageVisualRpcReceiveSystem [UpdateBefore CombatUIBridgeSystem]
  (OnCreate: DamageVisualQueue.Initialize() — the critical fix)
  (OnUpdate: RPC → DamageVisualQueue, skips enqueue on listen server)
                ↓
  CombatUIBridgeSystem [PresentationSystemGroup]
  (dequeues DamageVisualQueue, applies defense-in-depth visibility filter,
   dispatches to IDamageNumberProvider via CombatUIRegistry)
                ↓
  DamageNumberVisibilitySettings.EffectiveVisibility
  (resolves: designer default from DamageVisibilityConfig
   → player override from PlayerPrefs, if allowed; cached per-frame)
                ↓
  DamageNumbersProAdapter → spawns DamageNumbersPro instances
```

### Data Flow Per Result Type

| Result Type | Source System | Queue Path | RPC Path | Notes |
|-------------|-------------|------------|----------|-------|
| Direct hit (non-pre-applied) | DamageApplicationSystem | DamageVisualQueue.Enqueue | DamageVisualRpc | SourceNetworkId from GhostOwner |
| Direct hit (pre-applied) | DamageEventVisualBridgeSystem | Burst job → NativeQueue | DamageVisualRpc | SourceNetworkId = -1 |
| DOT tick | DamageEventVisualBridgeSystem | Burst job → NativeQueue | DamageVisualRpc | IsDOT = true, SourceNetworkId = -1 |
| Blocked / Parried / Immune | DamageApplicationSystem | DamageVisualQueue.Enqueue | DamageVisualRpc | **NEW** — was CRE-only before |
| Miss | DamageApplicationSystem | DamageVisualQueue.Enqueue | DamageVisualRpc | **NEW** — Damage = 0, was CRE-only before |
| Environmental / AOE | DamageEventVisualBridgeSystem | Burst job → NativeQueue | DamageVisualRpc | SourceNetworkId = -1 |

---

## Core Types

### DamageNumberVisibility Enum

```csharp
// Assets/Scripts/Combat/UI/DamageNumberVisibility.cs
public enum DamageNumberVisibility
{
    All = 0,       // See damage from all players (Diablo 4 style)
    SelfOnly = 1,  // Only see damage you dealt (Destiny 2 style)
    Nearby = 2,    // Only see damage within NearbyDistance of the player
    Party = 3,     // Only see damage from party members (including self)
    None = 4       // Disable all damage numbers
}
```

### DamageVisibilityConfig (Phase 2)

```csharp
// Assets/Scripts/Combat/UI/DamageVisibilityConfig.cs
// ScriptableObject in Resources/ — single source of truth for visibility policy
[CreateAssetMenu(menuName = "DIG/Combat/Damage Visibility Config")]
public class DamageVisibilityConfig : ScriptableObject
{
    public DamageNumberVisibility DefaultVisibility = DamageNumberVisibility.All;
    public bool AllowPlayerVisibilityOverride = true;
    public float NearbyDistance = 50f;
    public static DamageVisibilityConfig Instance { get; } // cached Resources.Load
}
```

### DamageVisualData (Modified)

```csharp
// Assets/Scripts/Combat/UI/DamageVisualQueue.cs
public struct DamageVisualData
{
    public float Damage;
    public float3 HitPosition;
    public HitType HitType;
    public DamageType DamageType;
    public ResultFlags Flags;
    public bool IsDOT;
    public int SourceNetworkId;  // NEW: attacker's GhostOwner.NetworkId (-1 = environment/unknown)
}
```

### DamageVisualRpc (Modified)

```csharp
// Assets/Scripts/Combat/Systems/DamageVisualRpc.cs
public struct DamageVisualRpc : IRpcCommand
{
    public float Damage;
    public float3 HitPosition;
    public byte HitType;
    public byte DamageType;
    public byte Flags;
    public byte IsDOT;
    public int SourceNetworkId;  // NEW: -1 = environment/unknown
}
```

### DamageNumberVisibilitySettings

```csharp
// Assets/Scripts/Combat/UI/DamageNumberVisibilitySettings.cs
public static class DamageNumberVisibilitySettings
{
    public static DamageNumberVisibility EffectiveVisibility { get; }
    public static bool IsPlayerOverrideAllowed { get; }
    public static void SetPlayerOverride(DamageNumberVisibility vis);
    public static void ClearPlayerOverride();
}
```

Resolution priority (per-frame cached):
1. Player override (PlayerPrefs `Settings_DmgNumVisibility`) — if config allows
2. Designer default (`DamageVisibilityConfig.DefaultVisibility`)
3. Fallback: `DamageNumberVisibility.All`

---

## System Changes

### 1. DamageVisualRpcReceiveSystem — Queue Initialization + SystemBase Rewrite

**File:** `Assets/Scripts/Combat/Systems/DamageVisualRpcReceiveSystem.cs`

Rewritten from `ISystem` with `SystemAPI.Query` to `SystemBase` with manual `EntityQuery`. `SystemAPI.Query` source-gen matching is unreliable for transient RPC entities on remote clients (same issue as `CombatResultEvent` and `PendingCombatHit` documented in MEMORY.md).

Key changes:
- **`OnCreate`:** Calls `DamageVisualQueue.Initialize()` — the critical fix for remote clients. Idempotent (checks `_queueInitialized`).
- **Manual EntityQuery:** `GetEntityQuery(ComponentType.ReadOnly<DamageVisualRpc>(), ComponentType.ReadOnly<ReceiveRpcCommandRequest>())` + `ToComponentDataArray` for reliable matching.
- **Listen server detection:** One-time `World.All` iteration to detect `ServerWorld` presence. On listen servers, skips enqueue (shared static queue already populated by ServerWorld) but still destroys RPC entities.
- **Batch entity destruction:** `EntityManager.DestroyEntity(_rpcQuery)` — single structural change instead of per-entity destruction.
- **SourceNetworkId passthrough:** Converts RPC bytes back to enum types when enqueuing to `DamageVisualQueue`.

### 2. DamageApplicationSystem — Defensive/Miss RPC + Source Attribution

**File:** `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs`

Three changes:
- **GhostOwner lookup**: Added `ComponentLookup<GhostOwner>` to resolve attacker's NetworkId
- **Defensive/miss enqueue + RPC**: Before the existing `!DidHit` / `DamagePreApplied` early-continues, enqueues defensive results and misses to `DamageVisualQueue` AND creates `DamageVisualRpc` entities (on server). Then `continue` — no health damage for these.
- **SourceNetworkId on all RPCs**: Both the new defensive/miss path and the existing hit path populate `SourceNetworkId` from `GhostOwner`

```csharp
private int GetAttackerNetworkId(Entity attacker)
{
    if (attacker != Entity.Null && _ghostOwnerLookup.HasComponent(attacker))
        return _ghostOwnerLookup[attacker].NetworkId;
    return -1;
}
```

### 3. DamageEventVisualBridgeSystem — Source Attribution + RPC Relay

**File:** `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs`

Both Burst jobs (`VisualBridgeJob`, `VisualBridgeJobBatched`) now set `SourceNetworkId = -1` on all `DamageVisualData`. The DamageEvent pipeline doesn't carry player attribution (DamageEvent.SourceEntity is Entity.Null for DOTs, and the buffer is ghost-replicated so we don't modify it).

**Critical change:** The server path no longer creates RPC entities directly. Instead, it queues `DamageVisualRpc` data to `DamageVisualQueue.EnqueueServerRpc()`. The actual RPC entity creation is deferred to `DamageVisualRpcSendSystem` (relay), which runs in `SimulationSystemGroup` after the prediction loop completes. This avoids NetCode prediction rollback destroying RPC entities before they can be serialized.

### 3a. DamageVisualRpcSendSystem — RPC Relay (Prediction Rollback Fix)

**File:** `Assets/Scripts/Combat/Systems/DamageVisualRpcSendSystem.cs` — **NEW**

Relay system that drains `DamageVisualQueue.PendingServerRpcs` and creates actual `DamageVisualRpc` entities via `DamageVisibilityServerFilter.CreateFilteredRpcs()`. Runs in `SimulationSystemGroup` with `[UpdateAfter(typeof(PredictedFixedStepSimulationSystemGroup))]` on `ServerSimulation` only.

This exists because RPC entities created during `PredictedFixedStepSimulationSystemGroup` are destroyed by NetCode's prediction rollback before the generated send system serializes them. The relay pattern queues data during prediction, then creates entities after prediction completes.

Lifecycle: `OnCreate` → `DamageVisualQueue.InitializePendingRpcs()`, `OnDestroy` → `DamageVisualQueue.DisposePendingRpcs()`.

### 4. CombatUIBridgeSystem — Defense-in-Depth Visibility Filter

**File:** `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`

Three changes:
- **Removed CRE defensive/miss path**: These now flow through `DamageVisualQueue` from DamageApplicationSystem.
- **`ShouldShowDamageNumber()` method**: Unified filter supporting all 5 visibility modes with `switch` expression. Environment damage (`SourceNetworkId == -1`) always shown.
- **Party member cache**: `HashSet<int>` of party member NetworkIds, rebuilt per-frame via `PartyLink` → `PartyMemberElement` → `GhostOwner.NetworkId` when Party mode is active.
- **Nearby distance**: Reads `DamageVisibilityConfig.Instance.NearbyDistance` and computes squared distance.

Client-side filter remains as defense-in-depth because:
- Listen server host reads shared static queue (not RPC-filtered)
- Player override may be MORE strict than server default
- Protects against modified clients

### 5. DamageVisibilityConfig — Separated Visibility Policy (Phase 2)

**File:** `Assets/Scripts/Combat/UI/DamageVisibilityConfig.cs` — **NEW**

ScriptableObject in `Resources/` for server-accessible visibility policy. Separated from `DamageFeedbackProfile` (visual config only) so server ECS systems can read it via `Resources.Load` without needing the MonoBehaviour adapter chain.

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `DefaultVisibility` | `DamageNumberVisibility` | `All` | Designer-configurable default |
| `AllowPlayerVisibilityOverride` | `bool` | `true` | Whether the player settings dropdown appears |
| `NearbyDistance` | `float` | `50` | Distance threshold for Nearby mode |

### 6. DamageVisibilityServerFilter — Server-Side RPC Filtering (Phase 2)

**File:** `Assets/Scripts/Combat/Systems/DamageVisibilityServerFilter.cs` — **NEW**

Managed system running on `ServerSimulation` only. Per-frame rebuilds:
- `NativeHashMap<int, Entity>` — NetworkId → connection entity
- `NativeHashMap<int, float3>` — NetworkId → player world position

Provides `CreateFilteredRpcs()` called by both `DamageApplicationSystem` and `DamageEventVisualBridgeSystem` to create targeted RPCs based on the server visibility policy. Party-mode uses `PartyLink` → `PartyMemberElement` buffer for same-party membership checks.

### 7. DamageFeedbackProfile — Visibility Fields Removed (Phase 2)

**File:** `Assets/Scripts/Combat/UI/Config/DamageFeedbackProfile.cs`

`DefaultVisibility` and `AllowPlayerVisibilityOverride` fields moved to `DamageVisibilityConfig`. Profile now only contains visual config (hit profiles, damage type colors, prefabs, culling).

### 8. DamageNumberAdapterBase — Public Profile Accessor

**File:** `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs`

Exposed `feedbackProfile` as a public read-only property (`FeedbackProfile`) for editor tools.

### 9. GameplaySettingsPage — Visibility Dropdown

**File:** `Assets/Scripts/Settings/Pages/GameplaySettingsPage.cs`

"Damage Number Visibility" dropdown (choices: "All Players", "Self Only", "Nearby", "Party Only", "None") below the existing "Show Damage Numbers" toggle. Only rendered when `DamageVisibilityConfig.AllowPlayerVisibilityOverride = true`. Follows existing snapshot/apply/revert/reset pattern.

---

## Execution Order

```
SimulationSystemGroup (ServerSimulation):
  DamageVisibilityServerFilter [UpdateBefore DamageApplicationSystem]
    → rebuilds NetworkIdToConnection + positions + loads config
  CombatResolutionSystem → creates CREs
  DamageApplicationSystem → reads CREs → DamageVisualQueue + FILTERED RPCs (direct, safe here)

PredictedFixedStepSimulationSystemGroup (Server|Local):
  DamageSystemGroup:
    DamageEventVisualBridgeSystem → DamageEvent buffers → DamageVisualQueue
      + queues RPCs to DamageVisualQueue.PendingServerRpcs (NOT created here)

SimulationSystemGroup [UpdateAfter PredictedFixedStepSimulationSystemGroup] (ServerSimulation):
  DamageVisualRpcSendSystem (relay)
    → drains PendingServerRpcs → FILTERED RPCs via DamageVisibilityServerFilter
    (entities created OUTSIDE prediction loop — safe from rollback)

PresentationSystemGroup (ClientSimulation):
  DamageVisualRpcReceiveSystem [UpdateBefore CombatUIBridgeSystem]
    → manual EntityQuery → RPCs → DamageVisualQueue (batch destroy)
  CombatUIBridgeSystem [Client|Local]
    → DamageVisualQueue → defense-in-depth filter → UI
```

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Combat/UI/DamageNumberVisibility.cs` | Enum | **NEW** (Phase 1), Extended (Phase 2) |
| `Assets/Scripts/Combat/UI/DamageNumberVisibilitySettings.cs` | Static helper | **NEW** (Phase 1), Rewritten (Phase 2) |
| `Assets/Scripts/Combat/UI/DamageVisibilityConfig.cs` | ScriptableObject | **NEW** (Phase 2) |
| `Assets/Scripts/Combat/Systems/DamageVisibilityServerFilter.cs` | SystemBase | **NEW** (Phase 2) |
| `Assets/Scripts/Combat/Systems/DamageVisualRpc.cs` | IRpcCommand struct | Modified (+4 bytes) |
| `Assets/Scripts/Combat/UI/DamageVisualQueue.cs` | Static queue + data struct + pending RPC relay | Modified (+4 bytes on DamageVisualData, +PendingServerRpcs NativeList) |
| `Assets/Scripts/Combat/Systems/DamageVisualRpcSendSystem.cs` | SystemBase | **NEW** (Phase 2) — RPC relay out of prediction loop |
| `Assets/Scripts/Combat/Systems/DamageVisualRpcReceiveSystem.cs` | SystemBase | Rewritten (ISystem→SystemBase, manual EntityQuery, batch destroy) |
| `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs` | SystemBase | Modified (+filtered RPCs via server filter) |
| `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs` | SystemBase | Modified (queues RPCs to relay instead of creating directly) |
| `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` | SystemBase | Modified (+5-mode defense-in-depth filter) |
| `Assets/Scripts/Combat/UI/Config/DamageFeedbackProfile.cs` | ScriptableObject | Modified (visibility fields removed) |
| `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs` | Abstract MonoBehaviour | Modified (+FeedbackProfile property) |
| `Assets/Scripts/Settings/Pages/GameplaySettingsPage.cs` | ISettingsPage | Modified (+5-option dropdown) |

---

## Design Decisions

| Decision | Chosen | Alternative | Rationale |
|----------|--------|-------------|-----------|
| Visibility enforcement | Dual: server-side filter + client defense-in-depth | Client-only or server-only | Server prevents bandwidth waste + client handles listen server + player overrides |
| Config separation | `DamageVisibilityConfig` (Resources/) vs `DamageFeedbackProfile` | Single SO for both | Server systems need config via `Resources.Load`; adapter chain not accessible from ECS |
| Server filter location | `SimulationSystemGroup`, `ServerSimulation` only | Also in `LocalSimulation` | LocalSimulation doesn't need RPCs — shared static queue handles single player |
| DamageEvent SourceNetworkId | `-1` (no attribution) | Resolve via `SourceEntity` → `GhostOwner` in Burst job | DamageEvent buffer is ghost-replicated (`AllPredicted`); SourceEntity is Entity.Null for DOTs |
| Party filter | Per-frame rebuild HashSet on client, `PartyMemberElement` buffer on server | Cached with dirty flag | Max 6 members — per-frame cost is negligible vs cache invalidation complexity |
| Settings persistence | PlayerPrefs (`Settings_DmgNumVisibility`) | Save file integration | Consistent with existing settings pattern (EPIC 18.2) |
| Per-frame caching | `DamageNumberVisibilitySettings` caches with `Time.frameCount` | Per-event PlayerPrefs read | Eliminates per-dequeue PlayerPrefs lookups in the damage number loop |
| RPC creation timing | Relay system in SimulationSystemGroup | Direct creation in PredictedFixedStep | Prediction rollback destroys entities created during prediction ticks before the send system serializes them |
| RPC receive pattern | Manual EntityQuery + SystemBase | SystemAPI.Query + ISystem | Source-gen query matching unreliable for transient RPC entities on remote clients |
| RPC entity destruction | `EntityManager.DestroyEntity(_rpcQuery)` batch | Per-entity destruction in loop | Single structural change vs N structural changes per frame |

---

## Duplication Prevention

| Scenario | Why No Duplication |
|----------|-------------------|
| LocalSimulation (single player) | CRE defensive/miss path removed from CombatUIBridgeSystem. All visuals flow through DamageVisualQueue only |
| Listen server host | DamageVisualRpcReceiveSystem skips enqueue when `_isListenServer = true`. Visuals come from shared static queue (ServerWorld populates) |
| Remote client | Only receives via RPC → DamageVisualQueue. No CREs exist, no shared static queue |

---

## What This Does NOT Fix (Future Work)

### Requires Separate RPC Work
- **Kill feed** — should be visible to all players (currently CRE-based, broken on remote clients)
- **Combat log** — should be visible to all players (currently CRE-based, broken on remote clients)
- **Contextual floating text** (BROKEN/HEADSHOT) — currently CRE-based, broken on remote clients

### Player-Specific (Acceptable as-is)
- **Hitmarkers** — only relevant for the local attacker
- **Combo tracking** — only relevant for the local attacker
- **Directional damage indicators** — only relevant for the local target
- **Camera shake / hit stop** — only relevant for the local player

### Quality Improvements
- **DamageEvent pipeline source attribution** — resolve `SourceEntity` → `GhostOwner.NetworkId` in Burst jobs for grenade/AOE attribution (currently all environment damage `-1`)
- **Per-player visibility sync RPC** — allow different server-side filter per player (currently global from DamageVisibilityConfig)

### Phase 2 Items Completed
- ~~Server-side visibility enforcement~~ — `DamageVisibilityServerFilter` with targeted RPCs
- ~~Additional visibility modes~~ — `Nearby`, `Party`, `None` added
- ~~Per-frame PlayerPrefs caching~~ — `DamageNumberVisibilitySettings` caches with `Time.frameCount`
- ~~Visibility config separation~~ — `DamageVisibilityConfig` (Resources/) vs `DamageFeedbackProfile`
- ~~Prediction rollback fix~~ — `DamageVisualRpcSendSystem` relay pattern for DamageEvent pipeline RPCs
- ~~RPC receive reliability~~ — `DamageVisualRpcReceiveSystem` rewritten to `SystemBase` + manual `EntityQuery` + batch destroy

---

## Verification

### Phase 1 (Core)
1. **Local simulation (editor play mode):** Damage numbers, defensive text, miss text all still work — no duplication
2. **Listen server host:** All damage numbers show (own + other players') via shared static queue
3. **Remote client:** Damage numbers show from all players via RPC
4. **No duplication:** Listen server RPC receive skips enqueue; CRE defensive/miss path removed
5. **Hitmarkers/combo/directional still work:** LocalSimulation unaffected — these remain CRE-based and player-specific

### Phase 2 (Server-Side + Extended Modes)
6. **All mode (default):** Server broadcasts all RPCs. All clients see everything
7. **SelfOnly mode:** Server sends RPCs only to the attacker's connection. Client filter is redundant but harmless
8. **Nearby mode:** Server sends RPCs only to connections within 50m. Client filter matches
9. **Party mode:** Server sends RPCs only to party members. Client filter uses cached HashSet
10. **None mode:** Server sends no RPCs. Listen server host sees via shared queue + client filter blocks
11. **Environment damage (SourceNetworkId == -1):** Always broadcast server-side, always shown client-side
12. **Settings dropdown:** Shows all 5 options when `DamageVisibilityConfig.AllowPlayerVisibilityOverride = true`
13. **DamageVisibilityConfig inspector:** DefaultVisibility enum, AllowPlayerOverride toggle, NearbyDistance float
14. **Single player (LocalSimulation):** No RPCs created. All modes work via client-side filter only
