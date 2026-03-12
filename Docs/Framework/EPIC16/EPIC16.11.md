# EPIC 16.11: Combat UI Pipeline Wiring & Diagnostic Hardening

**Status:** IMPLEMENTED
**Priority:** High (Core Combat Feedback)
**Dependencies:**
- `CombatUIBridgeSystem` (existing — `DIG.Combat.UI`, EPIC 15.9/15.22)
- `CombatUIRegistry` static singleton (existing — `DIG.Combat.UI`)
- `DamageVisualQueue` / `DamageVisualRpc` pipeline (existing — EPIC 15.30)
- `DamageEventVisualBridgeSystem` (existing — `DIG.Combat.Systems`)
- `EnemyHealthBarBridgeSystem` (existing — `DIG.Combat.Bridges`, EPIC 15.9)
- `HealthHUD` MonoBehaviour (existing — `Player.UI`)
- `DamageNumbersProAdapter` (existing — `DIG.Combat.UI.Adapters`)
- `CombatUIBootstrap` (existing — `DIG.Combat.UI`)
- `SurfaceDamageSystem` (existing — `DIG.Surface`, EPIC 16.10)

**Feature:** Wire up the existing combat UI pipeline so that player-specific feedback (hitmarkers, directional damage, combo, kill feed, camera shake) actually works, ensure damage numbers appear for all damage sources including surface DOT, and add diagnostic logging so missing scene setup is immediately obvious.

---

## Overview

### Problem

The full combat UI pipeline was architected across EPICs 15.9–15.30 but several wiring gaps prevent it from functioning end-to-end:

| What Exists (Functional) | What's Broken/Missing |
|--------------------------|----------------------|
| `CombatUIBridgeSystem` reads CombatResultEvent + DamageVisualQueue | `_playerEntity` is **never set** → all player-specific feedback (hitmarkers, directional damage, combo, kill feed, camera shake) is dead |
| `CombatUIBridgeSystem.SetPlayerEntity()` API defined | **No caller** — no system or MonoBehaviour ever invokes it |
| `DamageNumbersProAdapter` registers with `CombatUIRegistry` on `OnEnable` | If adapter MonoBehaviour is **not in scene**, `HasDamageNumbers == false` → all damage numbers silently dropped with zero diagnostic output |
| `CombatUIBootstrap.Instance` singleton for hitmarkers/directional/combo | If bootstrap is **not in scene**, `Instance == null` → all tactical feedback silently skipped |
| `HealthHUD` reads Health from local player entity | If **not in scene**, no player health bar (but fails silently) |
| `DamageEventVisualBridgeSystem` sends `DamageVisualRpc` to remote clients | Works correctly for DamageEvent pipeline hits |
| `DamageVisualRpcReceiveSystem` receives RPCs and enqueues to `DamageVisualQueue` | Works correctly |
| `SurfaceDamageSystem` writes `health.Current -= damage` directly | **Bypasses DamageEvent pipeline entirely** → no visual feedback for surface DOT damage |
| `EnemyHealthBarBridgeSystem` queries ServerWorld/ClientWorld | Works correctly (EPIC 15.9) |

**The gap:** A player fighting enemies gets zero feedback beyond the health bar going down. No floating damage numbers, no hitmarkers, no camera shake on crits, no combo counter, no kill feed. The entire feedback layer is architecturally complete but electrically disconnected.

### Solution

1. **Wire `CombatUIBridgeSystem._playerEntity`** by creating a lightweight `CombatUIPlayerBindingSystem` that detects the local player entity and calls `SetPlayerEntity()`.
2. **Add diagnostic warnings** to `CombatUIBridgeSystem` so missing scene components (adapters, bootstrap, HUD) are immediately logged instead of silently failing.
3. **Bridge SurfaceDamageSystem to DamageVisualQueue** so surface DOT damage shows floating numbers.
4. **Verify scene setup** by creating an editor tool that checks for required MonoBehaviours.

### Principles

1. **Wire, don't rewrite** — all UI systems exist and work. We only connect them.
2. **Fail loud** — missing scene components must log warnings, not silently swallow events.
3. **Zero new components on player entity** — no archetype changes. Uses managed code to call existing API.
4. **Minimal surface area** — touch as few existing systems as possible.

---

## Architecture: What Exists vs What's Missing

### Player Pipeline (Attacker/Target Feedback)

```
Player attacks enemy:
  WeaponFireSystem → PendingCombatHit → CombatResolutionSystem → CombatResultEvent
    ↓
  CombatUIBridgeSystem.ProcessCombatResult()
    ↓
  isPlayerAttacker = (combat.AttackerEntity == _playerEntity)  ← ALWAYS FALSE (_playerEntity==Null)
    ↓
  Hitmarker, ComboHit, KillConfirm, HitStop → ALL SKIPPED
```

**Fix:** Set `_playerEntity` correctly so `isPlayerAttacker`/`isPlayerTarget` resolve true.

### Damage Number Pipeline

```
DamageEvent (grenades/AOE/hazards):
  DamageEventVisualBridgeSystem → NativeQueue → DamageVisualQueue → [RPC to clients]
    ↓
  CombatUIBridgeSystem.OnUpdate() dequeues → CombatUIRegistry.DamageNumbers.ShowDamageNumber()
    ↓
  CombatUIRegistry.HasDamageNumbers == false ← NO ADAPTER IN SCENE? → SILENTLY DROPPED

CombatResultEvent (hitscan/melee resolved hits):
  CombatResolutionSystem → DamageVisualQueue.Enqueue() directly
    ↓
  Same consumer path ↑

SurfaceDamageSystem (DOT from surface zones):
  health.Current -= damage  ← BYPASSES EVERYTHING → NO VISUALS AT ALL
```

### Enemy Pipeline (Working)

```
Enemy health bars:
  EnemyHealthBarBridgeSystem → queries Health+ShowHealthBarTag → EnemyHealthBarPool → UI ✓

Enemy damage numbers (listen server):
  DamageEventVisualBridgeSystem → DamageVisualQueue → CombatUIBridgeSystem → adapter ✓ (IF adapter in scene)

Enemy damage numbers (remote client):
  DamageVisualRpc → DamageVisualRpcReceiveSystem → DamageVisualQueue → same path ✓
```

---

## Phase 1: Wire Player Entity to CombatUIBridgeSystem

### 1.1 Create CombatUIPlayerBindingSystem

Create a new managed system that detects the local player entity and calls `SetPlayerEntity()` on `CombatUIBridgeSystem`. Also updates player position for directional damage.

```csharp
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(CombatUIBridgeSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class CombatUIPlayerBindingSystem : SystemBase
{
    private EntityQuery _localPlayerQuery;
    private Entity _cachedPlayerEntity;

    protected override void OnCreate()
    {
        _localPlayerQuery = GetEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<GhostOwnerIsLocal>(),
            ComponentType.ReadOnly<LocalToWorld>()
        );
    }

    protected override void OnUpdate()
    {
        if (_localPlayerQuery.IsEmpty)
        {
            // Fallback for non-NetCode (LocalWorld) — any PlayerTag entity
            var fallbackQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalToWorld>());
            if (fallbackQuery.IsEmpty) return;
            BindPlayer(fallbackQuery);
            return;
        }
        BindPlayer(_localPlayerQuery);
    }

    private void BindPlayer(EntityQuery query)
    {
        using var entities = query.ToEntityArray(Allocator.Temp);
        if (entities.Length == 0) return;
        var entity = entities[0];

        if (entity != _cachedPlayerEntity)
        {
            _cachedPlayerEntity = entity;
            var bridgeSystem = World.GetExistingSystemManaged<CombatUIBridgeSystem>();
            if (bridgeSystem != null)
                bridgeSystem.SetPlayerEntity(entity);
        }

        // Update position each frame for directional damage
        var ltw = EntityManager.GetComponentData<LocalToWorld>(entity);
        var bridge = World.GetExistingSystemManaged<CombatUIBridgeSystem>();
        if (bridge != null)
            bridge.SetPlayerPosition(ltw.Position);
    }
}
```

**File:** `Assets/Scripts/Combat/UI/Systems/CombatUIPlayerBindingSystem.cs` (NEW)

**Why a new system instead of self-discover in CombatUIBridgeSystem:**
- SRP — the bridge system routes events, it shouldn't also discover players
- Follows the `EquipmentProviderBindingSystem` pattern already in the codebase
- Testable — can be disabled independently

---

## Phase 2: Diagnostic Logging for Missing Scene Components

### 2.1 CombatUIBridgeSystem Startup Diagnostics

Add a one-time diagnostic check in `OnUpdate` that logs warnings for missing providers.

**File:** `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` (MODIFY)

Add `_diagnosticsDone` bool field and `RunStartupDiagnostics()`:

```csharp
private bool _diagnosticsDone;

// In OnUpdate(), before existing code:
if (!_diagnosticsDone)
{
    _diagnosticsDone = true;
    RunStartupDiagnostics();
}

private void RunStartupDiagnostics()
{
    if (!CombatUIRegistry.HasDamageNumbers)
        Debug.LogWarning("[CombatUI] No IDamageNumberProvider registered. " +
            "Add DamageNumbersProAdapter to scene Canvas.");
    if (CombatUIBootstrap.Instance == null)
        Debug.LogWarning("[CombatUI] CombatUIBootstrap.Instance is null. " +
            "Add CombatUIBootstrap for hitmarkers, combo, kill feed.");
    if (!CombatUIRegistry.HasFeedback)
        Debug.LogWarning("[CombatUI] No ICombatFeedbackProvider registered. " +
            "Hit stop and camera shake will not function.");
    if (_playerEntity == Entity.Null)
        Debug.LogWarning("[CombatUI] _playerEntity is null. " +
            "CombatUIPlayerBindingSystem should set this when local player spawns.");
}
```

---

## Phase 3: Surface Damage Visual Bridge

### 3.1 SurfaceDamageSystem → DamageVisualQueue + RPC

`SurfaceDamageSystem` directly writes `health.Current -= damage` bypassing the visual pipeline. Add enqueue to `DamageVisualQueue` and RPC broadcast when damage is applied.

**File:** `Assets/Scripts/Surface/Systems/SurfaceDamageSystem.cs` (MODIFY)

Add to `OnCreate`:
```csharp
_isServer = World.Name == "ServerWorld";
if (_isServer)
    _rpcArchetype = EntityManager.CreateArchetype(typeof(DamageVisualRpc), typeof(SendRpcCommandRequest));
```

Inside the damage application block (after `health.ValueRW.Current -= damage`):
```csharp
var visualData = new DamageVisualData
{
    Damage = damage,
    HitPosition = entityPos + new float3(0, 1.5f, 0),
    HitType = HitType.Normal,
    DamageType = DamageTypeConverter.ToTheme(zone.DamageType),
    Flags = ResultFlags.None,
    IsDOT = true
};
DamageVisualQueue.Enqueue(visualData);

if (_isServer)
{
    var rpcEntity = EntityManager.CreateEntity(_rpcArchetype);
    EntityManager.SetComponentData(rpcEntity, new DamageVisualRpc
    {
        Damage = visualData.Damage,
        HitPosition = visualData.HitPosition,
        HitType = (byte)visualData.HitType,
        DamageType = (byte)visualData.DamageType,
        Flags = (byte)visualData.Flags,
        IsDOT = 1
    });
}
```

### 3.2 SurfaceDamageZone.DamageType Field

Verified `SurfaceDamageZone` already has `DamageType` field — no modification needed.

---

## Phase 4: Scene Setup Verification Tool

### 4.1 Editor Menu Validator

Create editor tool under `DIG > Diagnostics > Verify Combat UI Setup` that checks:

| Component | Required For | Check |
|-----------|-------------|-------|
| `DamageNumberAdapterBase` subclass | Damage numbers | `FindObjectOfType<DamageNumberAdapterBase>()` |
| `CombatUIBootstrap` | Hitmarkers, combo, kill feed | `FindObjectOfType<CombatUIBootstrap>()` |
| `HealthHUD` | Player health bar | `FindObjectOfType<HealthHUD>()` |
| `EnemyHealthBarPool` | Enemy floating bars | `FindObjectOfType<EnemyHealthBarPool>()` |
| `DamageFeedbackProfile` on adapter | Colors/scales | Serialized field check |

**File:** `Assets/Scripts/Editor/Setup/CombatUIVerificationTool.cs` (NEW)

---

## Files Summary

### New Files

| # | File | Type | Phase |
|---|------|------|-------|
| 1 | `Assets/Scripts/Combat/UI/Systems/CombatUIPlayerBindingSystem.cs` | SystemBase, Managed | 1 |
| 2 | `Assets/Scripts/Editor/Setup/CombatUIVerificationTool.cs` | Editor Tool | 4 |

### Modified Files

| # | File | Changes | Phase |
|---|------|---------|-------|
| 1 | `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` | +startup diagnostics (one-time warnings) | 2 |
| 2 | `Assets/Scripts/Surface/Systems/SurfaceDamageSystem.cs` | +DamageVisualQueue enqueue + RPC send | 3 |

### Unchanged (Verified Working)

| System | File | Status |
|--------|------|--------|
| `DamageVisualRpc` | `Combat/Systems/DamageVisualRpc.cs` | Working |
| `DamageVisualRpcReceiveSystem` | `Combat/Systems/DamageVisualRpcReceiveSystem.cs` | Working |
| `DamageEventVisualBridgeSystem` | `Combat/Systems/DamageEventVisualBridgeSystem.cs` | Working |
| `EnemyHealthBarBridgeSystem` | `Combat/Bridges/EnemyHealthBarBridgeSystem.cs` | Working |
| `HealthHUD` | `Player/UI/HealthHUD.cs` | Working (if in scene) |
| `DamageNumbersProAdapter` | `Combat/UI/Adapters/DamageNumbersProAdapter.cs` | Working (if in scene) |
| `CombatUIBootstrap` | `Combat/UI/CombatUIBootstrap.cs` | Working (if in scene) |
| `CombatUIRegistry` | `Combat/UI/CombatUIRegistry.cs` | Working |
| `DamageVisualQueue` | `Combat/UI/DamageVisualQueue.cs` | Working |

---

## System Execution Order

```
PresentationSystemGroup:
  CombatUIPlayerBindingSystem    [NEW — finds local player, calls SetPlayerEntity]
  DamageVisualRpcReceiveSystem   [EXISTING — receives RPCs → DamageVisualQueue]
  CombatUIBridgeSystem           [EXISTING+MODIFIED — dequeues DamageVisualQueue, routes to providers]
  EnemyHealthBarBridgeSystem     [EXISTING — floating health bars]

DamageSystemGroup (PredictedFixedStep, Server|Local):
  DamageEventVisualBridgeSystem  [EXISTING — DamageEvent buffers → DamageVisualQueue + RPCs]
  DamageApplySystem              [EXISTING — clears DamageEvent buffers]

SimulationSystemGroup (Server|Local):
  SurfaceDamageSystem            [MODIFIED — +DamageVisualQueue enqueue + RPC send]
```

---

## Verification Checklist

### Player Entity Binding
- [ ] `CombatUIPlayerBindingSystem` finds local player entity on first available frame
- [ ] `CombatUIBridgeSystem._playerEntity` is non-null after player spawns
- [ ] Player position updates each frame for directional damage

### Player-Specific Feedback (Was Dead, Now Alive)
- [ ] Player shoots enemy → hitmarker shows on crosshair
- [ ] Player crits enemy → hit stop triggers (0.05s)
- [ ] Player kills enemy → kill marker + kill feed entry
- [ ] Player takes damage → directional damage indicator appears
- [ ] Player takes damage → combo breaks
- [ ] Player takes crit → camera shake

### Damage Numbers
- [ ] Enemy takes hitscan damage → floating damage number
- [ ] Enemy takes grenade/AOE damage → floating damage number (via DamageEvent pipeline)
- [ ] Player stands in SurfaceDamageZone → DOT damage numbers appear
- [ ] Remote client sees damage numbers on enemies (via RPC)
- [ ] Defensive results (block, parry, immune) show correct text

### Diagnostics
- [ ] Missing `DamageNumbersProAdapter` → warning in console on first frame
- [ ] Missing `CombatUIBootstrap` → warning in console
- [ ] `_playerEntity` still null → warning in console
- [ ] Editor: `DIG > Diagnostics > Verify Combat UI Setup` reports all missing components

### No Regressions
- [ ] Enemy floating health bars still work
- [ ] Player `HealthHUD` still updates
- [ ] `DamageVisualRpcReceiveSystem` self-disables on listen server (no double visuals)
- [ ] No new components on player entity (16KB archetype safe)
- [ ] No changes to `DamageApplySystem`, `DamageEvent`, or any ghost-replicated component

---

## Design Considerations

### Why Not Route Surface Damage Through DamageEvent?

Routing surface DOT through `DamageEvent` buffer would be cleaner architecturally. However:
- `DamageApplySystem` is Burst-compiled, server-only, ghost-aware — **NEVER modify** (per MEMORY.md)
- `DamageEvent` is ghost-replicated `IBufferElementData` — adding from `SurfaceDamageSystem` creates scheduling conflicts
- `SurfaceDamageSystem` runs in `SimulationSystemGroup`, not `DamageSystemGroup` — ordering issues
- Direct `DamageVisualQueue.Enqueue()` is zero-risk: static queue, same pattern used by CRS

### 16KB Archetype Impact

**Zero.** No new components on any entity. All changes are in managed/presentation code or static queue operations.
