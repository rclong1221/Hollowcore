# SETUP GUIDE 17.11: Anti-Cheat & Server Validation

**Status:** Implemented
**Last Updated:** February 25, 2026
**Requires:** Player prefab with GhostOwner (NetCode), CurrencyInventory (EPIC 16.8 Economy)

This guide covers Unity Editor setup for the server-side anti-cheat validation system. After setup, the server validates RPC rate limits, movement speed, economy transactions, and automatically escalates penalties from warnings through kicks to bans.

**Key design**: All validation state lives on a **child entity** (via `ValidationLink`). Only 8 bytes added to the player archetype.

---

## What Changed

Previously, RPC handlers processed all incoming requests without rate limiting, there was no server-side movement validation, and economy transactions had no audit trail. A cheating client could spam RPCs, speed-hack, or memory-edit currency with no consequence.

Now:

- **RPC rate limiting** via per-type token buckets on a child entity
- **Movement validation** (Burst-compiled) checks position delta vs max speed every predicted tick
- **Economy audit trail** with balance reconciliation detects memory-edit exploits
- **Weighted violation scoring** with configurable decay
- **Automated penalty escalation** — Warn > Kick > Temp Ban > Perma Ban
- **Persistent ban list** saved to JSON with async background writes (no main-thread hitching)
- **Connection gate** rejects banned players before spawning
- **Teleport immunity API** prevents false positives from respawns, portals, admin TP
- **Editor workstation** with 6 live-inspection tabs

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Token refill | RateLimitRefillSystem refills all token buckets every tick (Burst) |
| Movement checks | MovementValidationSystem validates position deltas every predicted tick (Burst) |
| Economy reconciliation | EconomyAuditSystem compares balance vs audit trail each frame |
| Violation decay | ViolationAccumulatorSystem decays scores at ViolationDecayRate per second |
| Penalty execution | PenaltyExecutionSystem issues warnings, kicks, or bans when thresholds are crossed |
| Ban persistence | Ban list auto-saves to disk on a background thread, auto-loads on startup |
| Connection blocking | ConnectionValidationSystem rejects banned players before GoInGame |
| Telemetry | ValidationTelemetryBridgeSystem enqueues state changes for external analytics |

---

## 1. Create ScriptableObject Assets

### 1.1 Validation Profile

1. **Project window** > right-click > **Create > DIG > Validation > Validation Profile**
2. Save as `Assets/Resources/ValidationProfile.asset`
3. The asset comes pre-populated with all 9 RPC types

#### Inspector Fields — RPC Rate Limits Array

Each entry in the `Rpc Rate Limits` array configures one RPC type:

| Field | Type | Description |
|-------|------|-------------|
| **Rpc Type Id** | ushort | Stable identifier (matches RpcTypeIds constants) |
| **Display Name** | string | Human-readable label for the editor workstation |
| **Tokens Per Second** | float (min 0.01) | Refill rate — how many RPCs per second are allowed sustained |
| **Max Burst** | float (min 1) | Bucket capacity — how many rapid RPCs are tolerated before throttling |
| **Violation Severity** | float (0-1) | How harshly a rate-limit breach scores against the player |

#### Default RPC Entries

| RPC Type | ID | Tokens/s | Max Burst | Severity |
|----------|----|----------|-----------|----------|
| Dialogue Choice | 1 | 2.0 | 3 | 0.3 |
| Dialogue Skip | 2 | 2.0 | 3 | 0.3 |
| Craft Request | 3 | 3.0 | 5 | 0.5 |
| Stat Allocation | 4 | 1.0 | 5 | 0.7 |
| Talent Allocation | 5 | 1.0 | 5 | 0.7 |
| Talent Respec | 6 | 0.1 | 1 | 0.9 |
| Voxel Damage | 7 | 10.0 | 20 | 0.3 |
| Trade Request | 8 | 1.0 | 3 | 0.8 |
| Respawn | 9 | 0.5 | 2 | 0.5 |

#### Fallback Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Default Tokens Per Second** | float | 2.0 | Refill rate for any RPC type not in the array |
| **Default Max Burst** | float | 5.0 | Bucket capacity for any RPC type not in the array |

### 1.2 Penalty Config

1. **Project window** > right-click > **Create > DIG > Validation > Penalty Config**
2. Save as `Assets/Resources/PenaltyConfig.asset`

#### Inspector Fields

**Violation Thresholds**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Warn Threshold** | float (min 0.1) | 5.0 | Score to issue a warning to the player |
| **Kick Threshold** | float (min 1) | 20.0 | Score to disconnect the player |

**Ban Escalation**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Consecutive Kicks For Temp Ban** | int (min 1) | 3 | How many kicks within a session before temp ban |
| **Temp Bans For Perma Ban** | int (min 1) | 3 | How many temp bans before permanent ban |
| **Temp Ban Duration Minutes** | int (min 1) | 30 | Length of each temp ban |

**Decay**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Violation Decay Rate** | float (min 0.01) | 0.5 | Score points decayed per second. Higher = more forgiving |

**Violation Weights**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Rate Limit Weight** | float (min 0.1) | 1.0 | Multiplier for RPC rate-limit violations |
| **Movement Weight** | float (min 0.1) | 2.0 | Multiplier for speed/teleport violations |
| **Economy Weight** | float (min 0.1) | 3.0 | Multiplier for economy audit violations |
| **Cooldown Weight** | float (min 0.1) | 1.5 | Multiplier for ability cooldown violations |

**Warning**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Warn Cooldown Seconds** | float (min 1) | 10.0 | Minimum gap between warnings to the same player |

### 1.3 Movement Limits

1. **Project window** > right-click > **Create > DIG > Validation > Movement Limits**
2. Save as `Assets/Resources/MovementLimits.asset`
3. Set max speeds to match your game's actual movement speeds (check PlayerMovementSystem values)

#### Inspector Fields

**Max Speed Per State (m/s)**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Max Speed Standing** | float | 5.0 | Walk speed |
| **Max Speed Sprinting** | float | 10.0 | Sprint speed |
| **Max Speed Crouching** | float | 2.5 | Crouch speed |
| **Max Speed Prone** | float | 1.0 | Prone speed |
| **Max Speed Swimming** | float | 4.0 | Swimming speed |
| **Max Speed Falling** | float | 50.0 | Terminal velocity (generous for physics) |

**Tolerance**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Speed Tolerance Multiplier** | float (1.0-2.0) | 1.3 | Multiplier above max speed before flagging. 1.3 = 30% tolerance for network jitter |
| **Teleport Threshold** | float (min 5) | 20.0 | Position delta (meters) above which movement is flagged as a teleport |

**Error Accumulation**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Error Accumulation Rate** | float | 1.0 | How fast position error accumulates per frame |
| **Error Decay Rate** | float | 2.0 | How fast accumulated error decays per second during normal movement |
| **Max Accumulated Error** | float (min 1) | 10.0 | Error cap — violation fires when exceeded, then resets |
| **Teleport Grace Ticks** | uint (min 1) | 10 | Ticks of immunity after a server-granted teleport |

---

## 2. Add ValidationAuthoring to Player Prefab

1. Open the player prefab (`Warrok_Server`)
2. Click **Add Component** > search for **Player Validation** (under DIG > Validation)
3. Optionally drag the `ValidationProfile` SO into the **Profile** field (if left empty, it loads from `Resources/ValidationProfile` at bake time)
4. **Reimport the subscene** containing the player prefab

#### Inspector Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| **Profile** | ValidationProfileSO | No | Reference to validation profile. Falls back to `Resources/ValidationProfile` if null |

The baker creates:
- `ValidationLink` (8 bytes) on the player entity pointing to a child entity
- Child entity with: `ValidationChildTag`, `ValidationOwner`, `PlayerValidationState`, `RateLimitEntry` buffer (pre-populated from profile), `MovementValidationState`, `EconomyAuditEntry` buffer

---

## 3. System Execution Order

All systems auto-register via `[WorldSystemFilter]` attributes. No manual registration needed.

```
InitializationSystemGroup:
  ValidationBootstrapSystem (Server|Local)  — loads SOs, creates config singleton (runs once, self-disables)
  ConnectionValidationSystem (Server only)  — checks ban list, flushes deferred ban saves

PredictedFixedStepSimulationSystemGroup:
  MovementValidationSystem (Server|Local)   — Burst ISystem, position delta checks (after PlayerMovementSystem)

SimulationSystemGroup:
  RateLimitRefillSystem (Server|Local)      — Burst ISystem, refills token buckets (OrderFirst)
  EconomyAuditSystem (Server|Local)         — balance reconciliation
  CooldownValidationSystem (Server|Local)   — placeholder (disabled, future EPIC)
  ViolationAccumulatorSystem (Server|Local)  — aggregates violations, decay, threshold checks

LateSimulationSystemGroup:
  PenaltyExecutionSystem (Server|Local)     — executes warn/kick/ban penalties

PresentationSystemGroup:
  ValidationTelemetryBridgeSystem (Server|Local) — bridges state changes to telemetry queue
```

---

## 4. Editor Workstation

Open via **DIG > Validation Workstation** in the Unity menu bar.

| Tab | Purpose | When to Use |
|-----|---------|-------------|
| **Player Monitor** | Live violation scores per player, color-coded green/yellow/red | Quick overview of who's flagged |
| **Rate Limits** | Per-player per-RPC token bucket bar graphs | Tune rate limit values to avoid false positives |
| **Movement Trail** | SceneView overlay showing player movement trails colored by error | Diagnose false-positive movement flags |
| **Economy Audit** | Per-player transaction log with balance reconciliation | Investigate suspected economy exploits |
| **Ban Manager** | View/add/remove bans, manual ban controls | Manual moderation, unban players |
| **Violation Timeline** | Chronological violation events with type, severity, detail code | Root-cause investigation of penalty events |

### Ban Manager Usage

- **Network ID** field accepts the player's integer `NetworkId` value (visible in the Player Monitor tab or via NetCode's NetworkId component in the Entity Inspector)
- **Permanent** toggle switches between temp ban (with configurable duration) and permanent ban
- Click **Initialize Ban List** if the manager hasn't been initialized (normally done automatically by `ValidationBootstrapSystem` on play)
- **Unban** button on each row removes the ban immediately

---

## 5. Ban List

Bans persist at `Application.persistentDataPath/ban_list.json`.

- **Temp bans** expire after the configured duration (checked at connection time)
- **Permanent bans** persist until manually removed via the Ban Manager tab
- **Writes are async** — ban list changes are saved on a background thread to avoid main-thread hitching
- **Shutdown flush** — pending writes are flushed synchronously when the server shuts down to prevent data loss
- Expired temp bans are cleaned up lazily during connection checks

---

## 6. Teleport Immunity

When teleporting a player legitimately (respawn, portal, admin TP), call the helper to prevent false-positive movement violations:

```csharp
TeleportImmunityHelper.GrantImmunity(EntityManager, playerEntity, currentTick, graceTicks);
```

- `graceTicks` should match the `TeleportGraceTicks` value from your MovementLimits SO (default 10)
- The helper resets `LastValidatedPosition` to the current position to prevent delta spikes
- Already integrated in the respawn system; add to any new teleport/portal systems

---

## 7. Adding Rate Limiting to a New RPC System

When creating a new RPC type that should be rate-limited:

1. Add a new constant to `RpcTypeIds` in `Assets/Scripts/Validation/Components/ValidationEnums.cs`
2. Add a new entry to the `ValidationProfile` SO in the editor with matching ID, tune tokens/burst/severity
3. In your RPC receive system, add the check after resolving the player entity:

```csharp
using DIG.Validation;

// Inside processing loop, after resolving playerEntity:
if (EntityManager.HasComponent<ValidationLink>(playerEntity))
{
    var valChild = EntityManager.GetComponentData<ValidationLink>(playerEntity).ValidationChild;
    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.YOUR_NEW_RPC))
    {
        RateLimitHelper.CreateViolation(EntityManager, playerEntity,
            ViolationType.RateLimit, 0.5f, RpcTypeIds.YOUR_NEW_RPC, 0);
        EntityManager.DestroyEntity(rpcEntity);
        continue;
    }
}
```

---

## 8. Tuning Guide

### Rate Limits

- **Tokens Per Second** = sustained rate. Set to the maximum legitimate action rate for that RPC
- **Max Burst** = spike tolerance. Set to how many rapid-fire actions are reasonable (e.g., bulk crafting)
- If players hit rate limits during normal gameplay, increase tokens/burst in the Validation Profile SO
- Watch the Rate Limits tab in the workstation to see real-time token levels

### Movement

- **Speed Tolerance Multiplier** at 1.3 gives 30% headroom for network jitter. Lower it for stricter detection, raise it if legitimate players get flagged
- **Max Accumulated Error** controls how much "drift budget" a player gets. Higher = more lenient
- **Error Decay Rate** controls how fast error forgives. Higher = quicker forgiveness
- Use the Movement Trail tab to visualize which positions trigger errors

### Penalties

- **Violation Weights** control how heavily each category counts. Economy (3x) is weighted highest because currency exploits are most damaging to the game
- **Violation Decay Rate** at 0.5 means a score of 10 decays to 0 in 20 seconds. Higher = more forgiving
- **Consecutive Kicks For Temp Ban** at 3 means a player must be kicked 3 times before escalating to a temp ban
- For playtesting, consider raising thresholds significantly to avoid false-positive disruption

---

## 9. Architecture Notes

- **Child entity pattern** — identical to SaveStateLink, TalentLink, PvPRankingLink. Zero risk to the player's 16KB archetype budget
- **Ephemeral state** — no ISaveModule integration. All validation state resets on player disconnect. Ban list is the only persistent artifact
- **Burst-compiled hot paths** — RateLimitRefillSystem and MovementValidationSystem are `ISystem` with `[BurstCompile]`
- **Async ban persistence** — ban list writes go to a background thread via `ThreadPool`. Dirty flag prevents redundant writes
- **Backward compatible** — if `ValidationAuthoring` is not on the player prefab, all systems gracefully skip (RequireForUpdate guards)
- **Namespace**: `DIG.Validation`

---

## Verification Checklist

- [ ] `Assets/Resources/ValidationProfile.asset` exists
- [ ] `Assets/Resources/PenaltyConfig.asset` exists
- [ ] `Assets/Resources/MovementLimits.asset` exists
- [ ] `ValidationAuthoring` (Player Validation) on player prefab
- [ ] Subscene reimported after adding authoring
- [ ] Enter Play Mode > **DIG > Validation Workstation** shows player data
- [ ] Send rapid RPCs > Rate Limits tab shows token drain, violations appear in timeline
- [ ] Move faster than max speed > Movement Trail shows red, violations accumulate
- [ ] Violations accumulate past thresholds > warnings issued, then kick, then temp ban
- [ ] Banned player reconnects > ConnectionValidationSystem blocks them (check console in dev builds)
- [ ] Ban Manager tab shows active bans, Unban button works
- [ ] Verify `ban_list.json` exists in `Application.persistentDataPath` after a ban is issued
