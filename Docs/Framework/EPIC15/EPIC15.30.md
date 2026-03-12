# EPIC 15.30: Damage Pipeline Visual Unification

**Status:** Complete
**Last Updated:** February 11, 2026
**Priority:** Critical (Combat Core)
**Dependencies:**
- EPIC 15.28 (Unified Combat Resolution Pipeline — complete)
- EPIC 15.29 (Weapon Damage Profile & Modifier System — complete)
- EPIC 15.22 (Floating Damage Text System — complete)
- EPIC 15.9 (Combat UI Infrastructure — complete)

**Feature:** Unified single-path damage visualization. Any damage source that creates a `DamageEvent` automatically gets correct damage numbers, correct elemental colors, correct health bar updates, and correct DOT visual treatment — with zero additional hookup per weapon/spell/modifier. Full-fidelity damage visuals on remote clients via RPC.

---

## Problem Statement

Weapon modifiers (Bleed, Burn, BonusDamage, Explosion, etc.) produced no visible damage numbers with correct elemental colors. The root cause was a split visual architecture:

**Previous (Broken) Architecture — Two Competing Visual Paths:**
```
PATH A: CRS → CRE → CombatUIBridgeSystem → ShowDamageNumber
         (ThemeDamageType, correct colors, but ONLY for primary hit)

PATH B: DamageEvent → DamageEventVisualBridgeSystem → DamageVisualQueue → CombatUIBridgeSystem → ShowDamageNumber
         (SurvivalDamageType, LOSSY conversion, wrong colors)

DEDUP:  CombatResolvedTargets (HashSet<int>) suppressed PATH B when PATH A fired
         → killed ALL DamageEvent visuals on that entity for that frame
         → BonusDamage, DOT ticks, explosion hits silently lost
```

**Additional Problem — Lossy DamageType Bottleneck:**
```
Theme (8 types) → ToSurvival() → Survival (6 types) → ToTheme() → Theme (degraded)

Ice → Physical → Physical       (was Ice, now white)
Lightning → Physical → Physical  (was Lightning, now white)
Holy → Physical → Physical       (was Holy, now white)
Shadow → Physical → Physical     (was Shadow, now white)
Arcane → Physical → Physical     (was Arcane, now white)
```

**Remote Client Problem — No Damage Visuals:**
Remote clients (joining a listen server) saw no damage numbers or health bar updates because:
- `DamageEventVisualBridgeSystem` runs `ServerSimulation|LocalSimulation` only
- `DamageEvent` has `[GhostComponent(PrefabType=AllPredicted)]` — not on interpolated enemy ghosts
- All damage modifies CHILD.Health but ROOT is the ghost entity — ROOT.Health never updated
- `HasHitboxes` is not ghost-replicated — health bar query couldn't find entities on remote clients

---

## Implemented Architecture

**Single Visual Path, No Suppression, Full Remote Client Support:**
```
ALL damage numbers → DamageVisualQueue → CombatUIBridgeSystem → ShowDamageNumber / ShowDOTTick
                  ↘ DamageVisualRpc → remote clients → DamageVisualRpcReceiveSystem → DamageVisualQueue

CRE → CombatUIBridgeSystem → hitmarkers, combo, killfeed, defensive text, miss text (NO damage numbers)

Status effects → StatusVisualQueue → CombatUIBridgeSystem → ShowStatusApplied ("BLEEDING!", "BURNING!")

Health sync → HealthRootSyncSystem → CHILD.Health → ROOT.Health → ghost replication → remote client health bars
```

**Lossless DamageType (11 values):**
```
Player.Components.DamageType : byte
  Physical=0, Heat=1, Radiation=2, Suffocation=3, Explosion=4, Toxic=5,
  Ice=6, Lightning=7, Holy=8, Shadow=9, Arcane=10

ToSurvival: Fire→Heat, Poison→Toxic, Ice→Ice, Lightning→Lightning, Holy→Holy, Shadow→Shadow, Arcane→Arcane
ToTheme:    Heat→Fire, Toxic→Poison, Ice→Ice, Lightning→Lightning, Holy→Holy, Shadow→Shadow, Arcane→Arcane
```

---

## Implementation Phases

### Phase 0: Damageable Component Infrastructure [COMPLETE]
*Split-entity bake pattern, hitscan entity resolution chain, and runtime fixup. Foundation that all visual unification depends on.*

#### Task 0.1: DamageableLink Component
- [x] **New file:** `Assets/Scripts/Combat/Components/DamageableLink.cs`
- [x] Links physics body entities back to damageable root
- [x] Unity Physics extracts dynamic bodies, removing Parent components — DamageableLink provides a stable back-reference
- [x] Baked by DamageableAuthoring onto all child entities

#### Task 0.2: HitboxOwnerLink Component
- [x] **New file:** `Assets/Scripts/Combat/Components/HitboxOwnerLink.cs`
- [x] Reverse link from DamageableAuthoring ROOT → HitboxOwnerMarker CHILD
- [x] Redirects damage from ROOT entity's compound collider → CHILD entity where DamageEvent buffer is tracked
- [x] Baked by HitboxOwnerMarker onto the parent DamageableAuthoring entity

#### Task 0.3: HitboxOwnerMarker Authoring
- [x] **New file:** `Assets/Scripts/Player/Authoring/HitboxOwnerMarker.cs`
- [x] MonoBehaviour marker identifying the hitbox owner on character entities
- [x] Baker adds HasHitboxes, HitboxElement buffer to CHILD entity
- [x] Bakes DamageableLink (CHILD→ROOT) when parent has DamageableAuthoring
- [x] Bakes HitboxOwnerLink (ROOT→CHILD) on the parent entity

#### Task 0.4: DamageableFixupSystem
- [x] **New file:** `Assets/Scripts/Combat/Systems/DamageableFixupSystem.cs`
- [x] Runtime fixup for split-entity bake pattern — adds missing components at spawn
- [x] Pass 1: Adds Health/DamageEvent/DeathState/StatusEffect to CHILD entities with HasHitboxes
- [x] Pass 2: Adds StatusEffect/StatusEffectRequest buffers to pre-existing entities
- [x] Pass 3: Adds missing HitboxOwnerLink on ROOT entities (runtime fixup for pre-baked subscenes)
- [x] Uses DamageableLink to read MaxHealth from ROOT DamageableAuthoring entity
- [x] Self-disables after 30 consecutive idle frames
- [x] Burst-compiled, `[UpdateInGroup(SimulationSystemGroup, OrderFirst = true)]`, ServerSimulation|LocalSimulation
- [x] **Critical fix:** Removed old Pass 3 that disabled ROOT PhysicsCollider (was breaking all physics-based damage)

#### Task 0.5: DamageableAuthoring — StatusEffect Buffer Baking
- [x] **Modified file:** `Assets/Scripts/Combat/Authoring/DamageableAuthoring.cs`
- [x] Added StatusEffect + StatusEffectRequest buffer baking support

#### Task 0.6: WeaponFireSystem — Hitscan Entity Resolution Chain
- [x] **Modified file:** `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs`
- [x] Added component lookups: DamageableLink, HitboxOwnerLink, HasHitboxes
- [x] Implemented 5-step resolution chain for hitscan hits:
  1. Hitbox→Owner: If hit entity has Hitbox component, resolve via `Hitbox.OwnerEntity`
  2. HitboxOwnerLink redirect: If target has HitboxOwnerLink, redirect to CHILD where DamageEvent is tracked
  3. DamageableLink fallback: If no DamageEvent buffer, use DamageableLink to find root
  4. Parent chain walk: Final fallback for entities without DamageableLink
  5. Accept if Health/HasHitboxes found
- [x] Reads damage element from weapon's DamageProfile, converts via `DamageTypeConverter.ToSurvival()`
- [x] Comprehensive self-hit filtering on owner and owner's hitbox child

#### Task 0.7: Diagnostic Trace Tools
- [x] **New file:** `Assets/Scripts/DemoTools/DamageTrace.cs`
- [x] Shared toggle (`DamageTrace.ENABLED`) for end-to-end damage pipeline tracing
- [x] Filter console output with `[DMG_TRACE]` tag
- [x] **WARNING:** When enabled, `[BurstCompile]` must be removed from instrumented systems
- [x] **New file:** `Assets/Scripts/DemoTools/DamageBufferTraceSystem.cs`
- [x] Observer system logging DamageEvent buffer contents before DamageApplySystem
- [x] Verifies entity has ALL required components for DamageApplySystem to match
- [x] Controlled by `DamageTrace.ENABLED` toggle

---

### Phase 1: Lossless DamageType Foundation [COMPLETE]
*Eliminates the color-loss bottleneck. No behavioral changes, pure data layer fix.*

#### Task 1.1: Expand DamageType Enum
- [x] **File:** `Assets/Scripts/Player/Components/DamageEvent.cs`
- [x] Added `Ice = 6`, `Lightning = 7`, `Holy = 8`, `Shadow = 9`, `Arcane = 10` to `DamageType : byte` enum

**Safety:** `[GhostField]` serializes DamageType as byte. Values 0-5 unchanged. Same-binary client+server deploy. `DamageEvent` struct unchanged. `DamageApplySystem` is NOT modified. `SimpleDamageApplySystem.GetResistanceMultiplier` has `_ => 1f` default — new types get full damage with zero code change.

#### Task 1.2: Update DamageTypeConverter — Lossless Mapping
- [x] **File:** `Assets/Scripts/Combat/Utility/DamageTypeConverter.cs`
- [x] `ToSurvival()`: Added `Ice→Ice`, `Lightning→Lightning`, `Holy→Holy`, `Shadow→Shadow`, `Arcane→Arcane`
- [x] `ToTheme()`: Added `Ice→Ice`, `Lightning→Lightning`, `Holy→Holy`, `Shadow→Shadow`, `Arcane→Arcane`. Radiation/Suffocation/Explosion → Physical (environmental-only, acceptable)

#### Task 1.3: Fix StatusEffectSystem MapToDamageType
- [x] **File:** `Assets/Scripts/Player/Systems/StatusEffectSystem.cs`
- [x] Changed `Frostbite → DamageType.Ice` (was Heat — wrong element)
- [x] Changed `Shock → DamageType.Lightning` (was Heat — wrong element)

---

### Phase 2: Unify Visual Path — Remove Dual Enqueue [COMPLETE]
*Eliminates duplicate damage numbers and restores suppressed modifier visuals. Core architectural fix.*

#### Task 2.1: Update DamageVisualQueue Data Structure
- [x] **File:** `Assets/Scripts/Combat/UI/DamageVisualQueue.cs`
- [x] Added `public bool IsDOT;` field to `DamageVisualData` struct
- [x] Removed `CombatResolvedTargets` suppression
- [x] Added `CombatVisualHint` struct and `NativeHashMap<int, CombatVisualHint>` for Burst-compatible resolver severity passthrough from CRS to DamageEventVisualBridgeSystem
- [x] Added `RuntimeInitializeOnLoadMethod` for static state reset on domain reload

#### Task 2.2: Remove CRS Visual Enqueue + Suppression Registration
- [x] **File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`
- [x] Removed visual enqueue (DamageVisualQueue.Enqueue) — damage numbers now come from DamageEventVisualBridgeSystem
- [x] Removed CombatResolvedTargets suppression registration
- [x] Added CombatVisualHint write: CRS now sets `DamageVisualQueue.SetCombatHint(targetEntity.Index, hint)` with resolver HitType + ResultFlags for DamagePreApplied=true hits, so the DamageEvent pipeline can display critical/graze/execute severity

#### Task 2.3: Update DamageEventVisualBridgeSystem — Burst Job + Hints + IsDOT + RPC
- [x] **File:** `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs`
- [x] Removed suppression checks
- [x] Added IsDOT detection: `IsDOT = (evt.SourceEntity == Entity.Null)`
- [x] Refactored to Burst-compiled `IJobEntity` (VisualBridgeJob) for inner loop — reads DamageEvent buffers, CombatVisualHints, writes to NativeQueue
- [x] Managed drain loop moves NativeQueue → static DamageVisualQueue
- [x] Added `DamageVisualRpc` broadcasting in drain loop (ServerWorld only) — per-entity archetype for minimal structural changes
- [x] First non-DOT DamageEvent per entity consumes CombatVisualHint from CRS, preserving resolver severity (Critical/Graze/Execute) and flags (Headshot/Backstab)

#### Task 2.4: Add Visual Enqueue to DamageApplicationSystem
- [x] **File:** `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs`
- [x] For `DamagePreApplied == false` hits (where no DamageEvent exists), enqueues damage visual after health reduction
- [x] Added `DamageVisualRpc` broadcasting (ServerWorld only, archetype-based)

---

### Phase 3: Reroute CombatUIBridgeSystem [COMPLETE]
*CRE no longer shows damage numbers (DamageVisualQueue handles them). CRE retains hitmarkers, combo, killfeed, defensive text, miss.*

#### Task 3.1: Split CRE Responsibilities in CombatUIBridgeSystem
- [x] **File:** `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`
- [x] Removed `ShowDamageNumber` for `combat.DidHit` from CRE processing
- [x] Kept `ShowDamageNumber` for defensive results only (Blocked/Parried/Immune)
- [x] Kept `ShowMiss` for misses
- [x] Kept all hitmarker, combo, killfeed, camera shake, combat log logic unchanged

#### Task 3.2: Update DamageVisualQueue Dequeue — DOT Routing
- [x] **File:** `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`
- [x] Provider checks + adapter cast hoisted outside dequeue loop (avoids per-event overhead)
- [x] `IsDOT == true` → routes to `ShowDOTTick` via `DamageNumbersProAdapter` cast
- [x] `IsDOT == false` → routes to `ShowDamageNumber`

#### Task 3.3: Add Fallback Element Colors to DamageNumberAdapterBase
- [x] **File:** `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs`
- [x] Updated `GetElementColor()` with hardcoded fallback colors for Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane when DamageFeedbackProfile is missing or returns default

---

### Phase 4: Status Effect Visual Bridge [COMPLETE]
*Enables "BLEEDING!", "BURNING!", "POISONED!" floating text when combat status effects are applied.*

#### Task 4.1: Create StatusVisualQueue
- [x] **New file:** `Assets/Scripts/Combat/UI/StatusVisualQueue.cs`
- [x] Static queue bridging server-side status detection → client-side UI

#### Task 4.2: Create StatusEffectTypeConverter
- [x] **New file:** `Assets/Scripts/Combat/UI/StatusEffectTypeConverter.cs`
- [x] Maps `Player.Components.StatusEffectType` → `DIG.Combat.UI.StatusEffectType`
- [x] Environmental effects (Hypoxia, RadiationPoisoning, Concussion) → `None`

#### Task 4.3: Create StatusEffectVisualBridgeSystem
- [x] **New file:** `Assets/Scripts/Combat/Systems/StatusEffectVisualBridgeSystem.cs`
- [x] Burst-compiled `IJobEntity` (StatusVisualJob) computes bitmask deltas, writes to NativeQueue
- [x] Managed drain loop moves NativeQueue → static StatusVisualQueue
- [x] Periodic cleanup of destroyed entities every 60 frames
- [x] `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`

#### Task 4.4: Add StatusVisualQueue Dequeue to CombatUIBridgeSystem
- [x] **File:** `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`
- [x] Added StatusVisualQueue dequeue loop after DamageVisualQueue loop
- [x] Converts via `StatusEffectTypeConverter.ToUI()`, calls `ShowStatusApplied`

---

### Phase 5: Remote Client Visibility [COMPLETE]
*Enables health bars and damage numbers on remote clients (P2/P3 joining a listen server).*

#### Task 5.1: Health Root Sync System
- [x] **New file:** `Assets/Scripts/Combat/Systems/HealthRootSyncSystem.cs`
- [x] Burst-compiled `ISystem` syncing CHILD.Health → ROOT.Health each frame
- [x] `[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]` — captures all damage from both DamageSystemGroup and DamageApplicationSystem
- [x] `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- [x] Dirty check: only writes when values changed
- [x] Queries CHILD entities (HasHitboxes + Health + DamageableLink), writes to ROOT via ComponentLookup

**Why needed:** All damage systems modify CHILD.Health. ROOT is the ghost entity with `[GhostField]` on Health — without sync, ROOT.Health stays at max and remote clients never see health changes.

#### Task 5.2: DamageVisualRpc — Server→Client Damage Broadcasts
- [x] **New file:** `Assets/Scripts/Combat/Systems/DamageVisualRpc.cs`
- [x] `IRpcCommand` struct with Damage, HitPosition, HitType, DamageType, Flags, IsDOT
- [x] Sent by DamageEventVisualBridgeSystem (DamageEvent pipeline) and DamageApplicationSystem (CRE pipeline, DamagePreApplied=false)
- [x] All fields packed as bytes where possible for minimal wire size

#### Task 5.3: DamageVisualRpcReceiveSystem
- [x] **New file:** `Assets/Scripts/Combat/Systems/DamageVisualRpcReceiveSystem.cs`
- [x] `[WorldSystemFilter(ClientSimulation)]` — only runs on remote clients
- [x] `[UpdateBefore(typeof(CombatUIBridgeSystem))]` in PresentationSystemGroup
- [x] Self-disables on listen servers (checks for ServerWorld existence)
- [x] Receives DamageVisualRpc, enqueues to DamageVisualQueue, destroys RPC entities

#### Task 5.4: EnemyHealthBarBridgeSystem — Client Query Fix
- [x] **File:** `Assets/Scripts/Combat/Bridges/EnemyHealthBarBridgeSystem.cs`
- [x] `GetServerWorld()` returns `out bool isClientWorld` — identifies when falling back to ClientWorld
- [x] When `isClientWorld=true` (remote client): drops `HasHitboxes` from query — queries ROOT entities directly (Health + ShowHealthBarTag + LocalTransform, all ghost PrefabType=All)
- [x] When `isClientWorld=false` (ServerWorld/LocalWorld): keeps `HasHitboxes` for phantom ghost filtering

---

### Phase 6: Optimization Pass [COMPLETE]
*Performance improvements across all implemented systems.*

#### Task 6.1: CombatReactionSystem — Burst Re-enable
- [x] **File:** `Assets/Scripts/Combat/Systems/CombatReactionSystem.cs`
- [x] Removed 8 `Debug.Log` calls (GC pressure from string interpolation on every combat event)
- [x] Re-enabled `[BurstCompile]` on `OnUpdate` (was disabled for debugging)

#### Task 6.2: DamageEventVisualBridgeSystem — Archetype RPC Creation
- [x] **File:** `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs`
- [x] Cached `EntityArchetype` (DamageVisualRpc + SendRpcCommandRequest) in OnCreate
- [x] RPC creation uses `CreateEntity(archetype)` + `SetComponentData` — 1 structural change instead of 3

#### Task 6.3: HealthRootSyncSystem — Burst Compilation
- [x] **File:** `Assets/Scripts/Combat/Systems/HealthRootSyncSystem.cs`
- [x] Added `[BurstCompile]` to struct, OnCreate, and OnUpdate (zero managed calls)

#### Task 6.4: StatusEffectVisualBridgeSystem — Burst Split
- [x] **File:** `Assets/Scripts/Combat/Systems/StatusEffectVisualBridgeSystem.cs`
- [x] Refactored from managed SystemBase foreach to Burst `IJobEntity` (StatusVisualJob) + managed drain
- [x] Same pattern as DamageEventVisualBridgeSystem.VisualBridgeJob

#### Task 6.5: DamageApplicationSystem — Archetype RPC Creation
- [x] **File:** `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs`
- [x] Cached `EntityArchetype` in OnCreate, uses `ecb.CreateEntity(archetype)` + `ecb.SetComponent`

#### Task 6.6: ClientDamageVisualBridgeSystem — Deleted
- [x] **Deleted:** `Assets/Scripts/Combat/Systems/ClientDamageVisualBridgeSystem.cs`
- [x] Was disabled (replaced by DamageVisualRpc approach). Removed dead code and wasted persistent NativeHashMap allocation.

---

## File Summary

### Files Modified (12)

| # | File | Change | Phase |
|---|------|--------|-------|
| 1 | `Assets/Scripts/Combat/Authoring/DamageableAuthoring.cs` | StatusEffect + StatusEffectRequest buffer baking | 0 |
| 2 | `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs` | 5-step hitscan entity resolution chain, DamageTypeConverter usage | 0 |
| 3 | `Assets/Scripts/Player/Components/DamageEvent.cs` | Add 5 enum values (Ice, Lightning, Holy, Shadow, Arcane) | 1 |
| 4 | `Assets/Scripts/Combat/Utility/DamageTypeConverter.cs` | Lossless 1:1 mapping for new types | 1 |
| 5 | `Assets/Scripts/Player/Systems/StatusEffectSystem.cs` | Fix Frostbite→Ice, Shock→Lightning | 1 |
| 6 | `Assets/Scripts/Combat/UI/DamageVisualQueue.cs` | Add IsDOT, CombatVisualHints, remove CombatResolvedTargets | 2 |
| 7 | `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | Remove visual enqueue, add CombatVisualHint write | 2 |
| 8 | `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs` | Burst IJobEntity, hints, IsDOT, RPC broadcast, archetype | 2+6 |
| 9 | `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs` | Visual enqueue for PreApplied=false, RPC broadcast, archetype | 2+6 |
| 10 | `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` | Split CRE/queue roles, DOT routing, StatusVisualQueue dequeue | 3+4 |
| 11 | `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs` | Fallback element colors | 3 |
| 12 | `Assets/Scripts/Combat/Bridges/EnemyHealthBarBridgeSystem.cs` | Client query fix (drop HasHitboxes on ClientWorld) | 5 |

### Files Created (12)

| # | File | Purpose | Phase |
|---|------|---------|-------|
| 13 | `Assets/Scripts/Combat/Components/DamageableLink.cs` | Physics body → damageable root link | 0 |
| 14 | `Assets/Scripts/Combat/Components/HitboxOwnerLink.cs` | ROOT → CHILD reverse link for compound collider redirect | 0 |
| 15 | `Assets/Scripts/Player/Authoring/HitboxOwnerMarker.cs` | Baker for HasHitboxes, HitboxElement, DamageableLink, HitboxOwnerLink | 0 |
| 16 | `Assets/Scripts/Combat/Systems/DamageableFixupSystem.cs` | Runtime fixup for split-entity bake pattern (Burst, ServerSim) | 0 |
| 17 | `Assets/Scripts/DemoTools/DamageTrace.cs` | Shared toggle for end-to-end damage pipeline tracing | 0 |
| 18 | `Assets/Scripts/DemoTools/DamageBufferTraceSystem.cs` | Observer system logging DamageEvent buffers pre-apply | 0 |
| 19 | `Assets/Scripts/Combat/Systems/StatusEffectVisualBridgeSystem.cs` | Burst-split bitmask delta detection → StatusVisualQueue | 4+6 |
| 20 | `Assets/Scripts/Combat/UI/StatusVisualQueue.cs` | Static queue for status application events | 4 |
| 21 | `Assets/Scripts/Combat/UI/StatusEffectTypeConverter.cs` | Maps ECS StatusEffectType → UI StatusEffectType | 4 |
| 22 | `Assets/Scripts/Combat/Systems/HealthRootSyncSystem.cs` | Burst-compiled CHILD→ROOT health sync for ghost replication | 5 |
| 23 | `Assets/Scripts/Combat/Systems/DamageVisualRpc.cs` | IRpcCommand for server→client damage visual broadcasts | 5 |
| 24 | `Assets/Scripts/Combat/Systems/DamageVisualRpcReceiveSystem.cs` | ClientSimulation receiver → DamageVisualQueue | 5 |

### Files Deleted (1)

| # | File | Reason |
|---|------|--------|
| 25 | `Assets/Scripts/Combat/Systems/ClientDamageVisualBridgeSystem.cs` | Replaced by DamageVisualRpc approach. Was disabled dead code. | 6 |

### Files Modified (Optimization Only) (2)

| # | File | Change | Phase |
|---|------|--------|-------|
| 26 | `Assets/Scripts/Combat/Systems/CombatReactionSystem.cs` | Removed 8 Debug.Logs, re-enabled [BurstCompile] on OnUpdate | 6 |
| 27 | `Assets/Scripts/Combat/Systems/HealthRootSyncSystem.cs` | Added [BurstCompile] to struct + OnCreate + OnUpdate | 6 |

### Files NOT Modified (confirmed safe)

| File | Reason |
|------|--------|
| `DamageApplySystem.cs` | **NEVER MODIFY.** Burst-compiled, server-only, ghost-aware. Unknown DamageTypes → 1.0f resistance default. |
| `SimpleDamageApplySystem.cs` | `_ => 1f` default in GetResistanceMultiplier handles new types automatically |
| `WeaponFireSystem.cs` | Already calls `DamageTypeConverter.ToSurvival()` — now lossless, no change needed |
| `SweptMeleeHitboxSystem.cs` | Same as above |
| `ProjectileSystem.cs` | Same as above |
| `ModifierExplosionSystem.cs` | Same as above — `ToSurvival()` now preserves Ice/Lightning/etc. |
| `DamageableAuthoring.cs` | Already bakes StatusEffect + StatusEffectRequest buffers |
| `StatusEffectConfig.cs` | No change needed — default config handles all types |
| `FloatingTextManager.cs` | Already implements `ShowStatusApplied()` — just needs to be called |
| `DamageNumbersProAdapter.cs` | Already implements `ShowDOTTick()` — just needs to be called |

---

## Verification Traces

### Trace 1: Hitscan (Fire weapon, DamagePreApplied=true) + Bleed modifier
```
1. WeaponFireSystem → DamageEvent(Heat) + PendingCombatHit
2. CRS → CRE(Fire, PreApplied=true) + StatusEffectRequest(Bleed)
     + SetCombatHint(target.Index, {HitType=Hit/Critical/etc, Flags})
3. DamageApplicationSystem: PreApplied=true → SKIP (no health write, no visual enqueue)
4. DamageEventVisualBridgeSystem:
   - VisualBridgeJob [Burst]: DamageEvent(Heat) → consumes CombatHint → enqueue to NativeQueue
   - Drain loop: enqueue to DamageVisualQueue + send DamageVisualRpc to remote clients
5. StatusEffectVisualBridgeSystem:
   - StatusVisualJob [Burst]: new Bleed bit detected → enqueue to NativeQueue
   - Drain loop: enqueue to StatusVisualQueue
6. CombatUIBridgeSystem:
   - CRE → hitmarker + combo + killfeed (NO damage number from CRE)
   - DamageVisualQueue → ShowDamageNumber(Fire, CriticalHitType) → orange-red damage number
   - StatusVisualQueue → ShowStatusApplied(Bleed) → "BLEEDING!" floating text
7. Remote clients: DamageVisualRpcReceiveSystem → DamageVisualQueue → same visual
8. Next frames: StatusEffect ticks → DamageEvent(Physical, SourceEntity=Null)
   → VisualBridgeJob → Visual(Physical, IsDOT=true) → ShowDOTTick + DamageVisualRpc
```
**Result:** Orange-red fire hit number (with correct severity) + "BLEEDING!" text + periodic bleed tick numbers (DOT style). Same on all clients.

### Trace 2: Hitscan (Ice weapon) + BonusDamage(Lightning)
```
1. WeaponFireSystem → DamageEvent(Ice) + PendingCombatHit
2. CRS → CRE(Ice, PreApplied=true) + BonusDamage modifier fires
   → ecb.AppendToBuffer: DamageEvent(Lightning, Amount=BonusDamage)
   + SetCombatHint(target.Index, {HitType, Flags})
3. DamageEventVisualBridgeSystem:
   - VisualBridgeJob [Burst]: buffer has [Ice, Lightning]
     → Ice (non-DOT) consumes CombatHint → enqueue Visual(Ice, CritHitType)
     → Lightning (non-DOT, hint consumed) → enqueue Visual(Lightning, Hit)
   - Drain: enqueue both + send 2 RPCs
4. CombatUIBridgeSystem:
   - CRE → hitmarker only
   - Queue → ShowDamageNumber(Ice) → blue number + ShowDamageNumber(Lightning) → yellow number
```
**Result:** Blue ice number + yellow lightning bonus number — both visible on all clients

### Trace 3: Bow (DamagePreApplied=false) + Poison modifier
```
1. BowSystem → PendingCombatHit only (no DamageEvent buffer on target)
2. CRS → CRE(Arcane, PreApplied=false) + StatusEffectRequest(PoisonDOT)
3. DamageApplicationSystem: PreApplied=false → applies health → enqueues Visual(Arcane, IsDOT=false) + sends DamageVisualRpc
4. StatusEffectVisualBridgeSystem: new PoisonDOT → enqueue StatusApplied(PoisonDOT)
5. CombatUIBridgeSystem:
   - CRE → hitmarker only (no damage number from CRE)
   - DamageVisualQueue → ShowDamageNumber(Arcane) → magenta damage number
   - StatusVisualQueue → ShowStatusApplied(Poison) → "POISONED!" floating text
6. Next frames: DOT ticks → DamageEvent(Toxic, SourceEntity=Null)
   → Visual(Poison, IsDOT=true) → ShowDOTTick → small green tick number
```
**Result:** Magenta arcane hit number + "POISONED!" text + periodic green poison ticks

### Trace 4: Explosion modifier on nearby entity
```
1. ModifierExplosionSystem → DamageEvent(Heat/Ice/etc., SourceEntity=attacker) on nearby entities
2. DamageEventVisualBridgeSystem: VisualBridgeJob reads DamageEvent → correct element → enqueue Visual(element, IsDOT=false) + RPC
3. SimpleDamageApplySystem → health reduced (new types → 1.0f resistance default)
4. HealthRootSyncSystem → CHILD.Health → ROOT.Health → ghost replication
```
**Result:** Correct elemental damage number on each hit entity, health bars update on all clients

### Trace 5: Environmental radiation
```
1. Hazard system → DamageEvent(Radiation, SourceEntity=Null)
2. DamageEventVisualBridgeSystem → ToTheme(Radiation) → Physical → Visual(Physical, IsDOT=true)
3. CombatUIBridgeSystem → ShowDOTTick → small white tick number
```
**Result:** White DOT-style tick (acceptable for environmental, no combat element)

### Trace 6: Blocked / Parried / Immune
```
1. CRS → CRE(Blocked/Parried/Immune)
2. CombatUIBridgeSystem:
   - isDefensiveResult = true
   - ShowDamageNumber with defensive HitType → dedicated defensive prefab/color
```
**Result:** Defensive text preserved (BLOCKED, PARRIED, IMMUNE) — NOT from DamageVisualQueue

### Trace 7: Miss
```
1. CRS → CRE(DidHit=false, not defensive)
2. CombatUIBridgeSystem → ShowMiss at hit point
```
**Result:** "MISS" text preserved — NOT from DamageVisualQueue

### Trace 8: Future magic spell (Shadow element) — zero hookup test
```
1. Spell system → DamageEvent(Shadow) on target
2. DamageEventVisualBridgeSystem → ToTheme(Shadow) → Shadow → Visual(Shadow, IsDOT=false) + RPC
3. CombatUIBridgeSystem → ShowDamageNumber(Shadow) → purple damage number (fallback color)
4. SimpleDamageApplySystem → health reduced (1.0f resistance default)
5. HealthRootSyncSystem → CHILD.Health → ROOT.Health → ghost replication
6. Health bar bridge reads updated Health → health bar updates on all clients
```
**Result:** Purple damage number + health bar change with zero additional system hookup, works on all clients

### Trace 9: Remote client observing combat (not attacking)
```
1. Server processes all damage normally (Traces 1-8)
2. HealthRootSyncSystem: CHILD.Health → ROOT.Health on server
3. Ghost replication: ROOT.Health sent to all clients
4. EnemyHealthBarBridgeSystem on remote client:
   - GetServerWorld → falls back to ClientWorld (isClientWorld=true)
   - Query drops HasHitboxes → finds ROOT entities with Health + ShowHealthBarTag
   - Health bar updates in real-time
5. DamageVisualRpc received by DamageVisualRpcReceiveSystem:
   - Enqueues full-fidelity DamageVisualData to DamageVisualQueue
   - CombatUIBridgeSystem shows correct elemental damage numbers with DOT routing
```
**Result:** Remote clients see same health bars and damage numbers as listen server host

---

## Execution Order Reference

```
PredictedFixedStepSimulationSystemGroup
  └─ DamageSystemGroup
       ├─ DamageEventVisualBridgeSystem     [Burst job reads DamageEvent+CombatHints → NativeQueue → DamageVisualQueue + RPC]
       ├─ DamageApplySystem                 [players: reads+clears DamageEvent → writes CHILD.Health]
       └─ SimpleDamageApplySystem           [NPCs: reads+clears DamageEvent → writes CHILD.Health]

SimulationSystemGroup
  ├─ CombatResolutionSystem                [PendingCombatHit → CRE + DamageEvents + StatusEffectRequests + CombatVisualHint]
  ├─ DamageApplicationSystem               [CRE (PreApplied=false) → Health + DamageVisualQueue + RPC]
  ├─ CombatReactionSystem [Burst]          [CRE → enter combat state]
  ├─ StatusEffectSystem                    [StatusEffectRequest → StatusEffect buffer, DOT ticks → DamageEvent]
  ├─ StatusEffectVisualBridgeSystem        [Burst job detects new StatusEffects → NativeQueue → StatusVisualQueue]
  └─ HealthRootSyncSystem [Burst, Last]    [CHILD.Health → ROOT.Health for ghost replication]

PresentationSystemGroup
  ├─ DamageVisualRpcReceiveSystem          [ClientSimulation: DamageVisualRpc → DamageVisualQueue]
  ├─ CombatUIBridgeSystem                  [CRE → hitmarkers/combo/killfeed/defensive/miss]
  │                                        [DamageVisualQueue → ShowDamageNumber/ShowDOTTick]
  │                                        [StatusVisualQueue → ShowStatusApplied]
  └─ EnemyHealthBarBridgeSystem            [Cross-world Health query → world-space health bars]

CombatEventCleanupSystem                   [destroys CRE entities — after PresentationSystemGroup]
```

---

## Split-Entity Pattern Reference

Understanding the ROOT/CHILD entity split is critical for this EPIC:

```
DamageableAuthoring (ROOT entity — ghost)
  ├─ Health [GhostComponent(PrefabType=All), GhostField]     ← replicated to ALL clients
  ├─ DamageableTag
  ├─ DamageEvent [GhostComponent(PrefabType=AllPredicted)]    ← NOT on interpolated client ghosts
  ├─ DeathState
  ├─ PhysicsShape (compound: includes Head/Torso children)
  ├─ ShowHealthBarTag [GhostComponent(PrefabType=All)]        ← replicated
  └─ HitboxOwnerLink → CHILD entity

HitboxOwnerMarker (CHILD entity — NOT a ghost)
  ├─ HasHitboxes                          ← NOT ghost-replicated
  ├─ HitboxElement buffer
  ├─ DamageableLink → ROOT entity
  ├─ Health (copied from ROOT by DamageableFixupSystem)
  ├─ DamageEvent (copied from ROOT by DamageableFixupSystem)
  └─ StatusEffect + StatusEffectRequest buffers

Key insight: All damage modifies CHILD.Health. ROOT.Health is what ghost-replicates.
HealthRootSyncSystem bridges this gap by copying CHILD → ROOT each frame.
```

---

## Known Limitations (documented, not blocking)

1. **DamageResistance** struct has no fields for Ice/Lightning/Holy/Shadow/Arcane. All default to 1.0f (full damage) via `_ => 1f` in `SimpleDamageApplySystem.GetResistanceMultiplier`. Add per-element resistance fields when designers need elemental mitigation.

2. **Combat log** only captures CRE-sourced events. DOT tick damage isn't logged. Can add later by logging in CombatUIBridgeSystem's DamageVisualQueue dequeue loop.

3. **Player screen shake for DOTs** — `OnPlayerTookDamage` only fires for CRE hits (via ProcessCombatResult). DOT ticks on the player don't trigger screen shake. Minor polish item; add player entity check in DamageVisualQueue dequeue if needed.

4. **Two DamageType enums** still exist (`Player.Components.DamageType` 11-value and `DIG.Targeting.Theming.DamageType` 8-value). The converter is now lossless for all combat types. Full unification (single enum) would require changing DamageEvent's field type — ghost serialization risk — not worth it.

5. **DamageFeedbackProfile ScriptableObject** should be configured with entries for Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane for best visual quality. The hardcoded fallback colors in `DamageNumberAdapterBase.GetElementColor()` provide acceptable defaults without profile configuration.

6. **Status effect text on remote clients** — `StatusEffectVisualBridgeSystem` runs `ServerSimulation|LocalSimulation` only. "BLEEDING!" / "BURNING!" floating text does not appear on remote clients. Would need a StatusVisualRpc (same pattern as DamageVisualRpc) to broadcast. Low priority — damage numbers are the primary feedback.

7. **EnemyHealthBarBridgeSystem per-frame query creation** — Cross-world EntityQuery objects are created each frame via `serverEM.CreateEntityQuery(...)`. Caching was attempted but reverted due to managed reference lifecycle issues in ISystem structs. Performance is acceptable for the typical entity count (<50 enemies).

---

## Compile-Time Safety Checks

- [x] `DamageTypeConverter.ToSurvival()` handles all 8 ThemeDamageType values → 11 SurvivalDamageType values
- [x] `DamageTypeConverter.ToTheme()` handles all 11 SurvivalDamageType values → 8 ThemeDamageType values
- [x] `DamageVisualData.IsDOT` field is set by all enqueue sites (DamageEventVisualBridgeSystem + DamageApplicationSystem)
- [x] `CombatResolvedTargets` has no remaining references
- [x] `StatusVisualQueue` is accessible from both `StatusEffectVisualBridgeSystem` (server) and `CombatUIBridgeSystem` (presentation)
- [x] No Burst compilation errors — StatusEffectVisualBridgeSystem inner job is Burst, drain is managed
- [x] `DamageVisualRpc` sent only from ServerWorld (guarded by `_isServer` check)
- [x] `DamageVisualRpcReceiveSystem` self-disables on listen servers (prevents double-enqueue)
- [x] `HealthRootSyncSystem` runs OrderLast to capture all damage writes

---

## Runtime Verification Checklist

- [x] Fire hitscan weapon at BoxingJoe → orange-red damage number appears
- [x] Ice weapon at BoxingJoe → blue damage number appears
- [x] Weapon with Bleed modifier → "BLEEDING!" floating text + periodic white DOT ticks
- [x] Weapon with Burn modifier → "BURNING!" floating text + periodic fire DOT ticks
- [x] Weapon with BonusDamage(Lightning) → yellow bonus damage number alongside primary
- [x] Weapon with Explosion modifier → damage numbers on all entities in radius
- [x] Bow/projectile hit (DamagePreApplied=false) → damage number appears
- [x] Blocked hit → "BLOCKED" text (from CRE, not DamageVisualQueue)
- [x] Missed hit → "MISS" text (from CRE, not DamageVisualQueue)
- [x] Health bar decreases on each hit type
- [x] No duplicate damage numbers
- [x] Environmental radiation → small white DOT tick (not full-size damage number)
- [x] Remote client (P2/P3) sees health bars on enemies
- [x] Remote client health bars update when enemy takes damage
- [x] Remote client sees per-event damage numbers with correct element colors
- [x] Remote client sees DOT ticks as separate styled numbers (not lumped)
- [x] Listen server host sees same visuals as before (no regression)
- [x] No double damage numbers on listen server (RPC receive system self-disables)
