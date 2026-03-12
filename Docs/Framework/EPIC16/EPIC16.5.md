# EPIC 16.5: Codebase Hygiene Audit & Warning Resolution

**Status:** **IMPLEMENTED**
**Priority:** High (Code Quality & Maintainability)
**Dependencies:** None (self-contained cleanup)

**Feature:** Systematic resolution of all compiler warnings, stub implementations, obsolete code, debug residue, and incomplete systems across the codebase. Categorized into deletions, manual fixes, and full implementations to bring the project to AAA production standards.

---

## Problem

The codebase has accumulated ~200+ compiler warnings and deferred work items across 1,496 C# files. These fall into distinct categories:

1. **Dead/obsolete code** generating warnings but providing no value
2. **Debug toggles left enabled** causing CS0162 unreachable code warnings
3. **Stub systems** that iterate entities but do nothing (wasted CPU)
4. **TODO-marked gaps** in critical gameplay systems (firearms, explosions, channeled abilities)
5. **Temporary diagnostic systems** that should have been removed after their bug was fixed
6. **Inconsistent debug logging** in production systems without conditional compilation

A clean warning-free build is a prerequisite for CI/CD, helps identify real regressions immediately, and is the baseline expectation for any AAA-quality engine.

---

## Audit Summary

| Category | Count | Severity | Phase |
|----------|-------|----------|-------|
| Obsolete code to delete | 5 files/classes | Low | 0 |
| Temporary/diagnostic code to delete | 4 files | Low | 0 |
| Debug toggles left enabled (CS0162) | 4 files | Medium | 1 |
| Debug toggles to convert to conditional compilation | 23 files | Medium | 1 |
| Empty/stub systems wasting CPU | 3 systems | Medium | 1 |
| ShootableActionSystem - firearms broken | 1 system | **CRITICAL** | 2 |
| ProjectileSystem - impact explosions broken | 1 system | High | 3 |
| ChannelActionSystem - channeled abilities stubbed | 1 system | High | 4 |
| WeaponAnimationEventSystem - throw spawning | 1 system | Medium | 4 |
| Combat resolution polish TODOs | 2 items | Low | 5 |
| Stealth/movement polish TODOs | 4 items | Low | 5 |
| EnemyHealthBar UI stubs | 1 file | Low | 5 |
| Debug.Log in 69 production systems | 69 files | Low | 6 |

---

## Phase 0: Deletions (Safe Removes)

Code that is explicitly marked obsolete, temporary, or provides no value. All deletions are backward-compatible — no systems reference this code (verified).

### Task 0.1: Delete Obsolete EquipmentSlotConfig ✅

- [x] Delete `Assets/Scripts/Items/Interfaces/EquipmentSlotConfig.cs`
- [x] Delete `Assets/Prefabs/EquipmentSlot.asset`, `MainHand.asset`, `OffHand.asset` (ScriptableObject instances of the deprecated type)
- [x] Verify: `EquipmentSlotDefinition` is the replacement (already in use)
- [x] Update docs referencing it: `Docs/EPIC14/EPIC14.5.md`, `EPIC14.29.md`, `SETUP_GUIDE_14.2.md`, `EPIC14.2.md` (change mentions to `EquipmentSlotDefinition`)

**Why:** Class is `[System.Obsolete]` with explicit message "Use EquipmentSlotDefinition instead. This class will be removed in a future version." Zero code references remain — only docs and dead assets.

### Task 0.2: Delete Legacy VoxelDamageRequest ✅

- [x] Delete the `VoxelDamageRequest_Legacy` struct from `Assets/Scripts/Runtime/Survival/Explosives/Components/ExplosiveComponents.cs` (lines 328-340)
- [x] Verify: `DIG.Voxel.VoxelDamageRequest` is the replacement (already in use by all systems)
- [x] Migration guide in `EPIC15_10_MigrationGuide.cs` can remain for documentation

**Why:** Struct is `[System.Obsolete("Use DIG.Voxel.VoxelDamageRequest from EPIC 15.10 instead.")]`. Zero code references outside its own definition and the migration guide doc.

### Task 0.3: Delete Obsolete FlashlightHUDView.RegisterWithECS ✅

- [x] Delete the `RegisterWithECS` method from `Assets/Scripts/Player/UI/Views/FlashlightHUDView.cs` (lines 107-111)

**Why:** Method is `[System.Obsolete("Use TryRegisterWithECS instead")]`. Zero callers in the codebase — all code already uses `TryRegisterWithECS`.

### Task 0.4: Delete Temporary FlashlightHUD MonoBehaviour ✅

- [x] Delete `Assets/Scripts/Visuals/UI/FlashlightHUD.cs`

**Why:** File header says "Temporary HUD to display Flashlight Battery and State." The production MVVM version exists at `Assets/Scripts/Player/UI/Views/FlashlightHUDView.cs` using the proper `UIView<FlashlightViewModel>` architecture (EPIC 15.8). This file is superseded.

### Task 0.5: Delete Temporary Diagnostic Systems ✅

- [x] Delete `Assets/Scripts/DemoTools/WeaponPositionDiagnosticSystem.cs`
- [x] Delete `Assets/Scripts/DemoTools/PlayerTelemetry.cs`
- [x] Delete `Assets/Scripts/DemoTools/BoneNameList.cs`

**Why:**
- `WeaponPositionDiagnosticSystem`: Header says "TEMPORARY: Remove after the bug is fixed." It's a diagnostic for the "assault rifle teleports player to 0,0,0" bug. Has `DEBUG_ENABLED = true` generating warnings.
- `PlayerTelemetry`: Contains only empty placeholder methods with comment "slide telemetry removed."
- `BoneNameList`: Empty file (1 line).

### Task 0.6: Delete Stale ProneSystem TODO Comment ✅

- [x] Remove the stale TODO comment and permissive fallback `SafeStandCheck` stub at line ~225 in `Assets/Scripts/Player/Systems/ProneSystem.cs`

**Why:** The full capsule-sweep implementation exists at lines 259-349. The stub at 225 is dead code — the proper method is already called.

---

## Phase 1: Warning Elimination (Manual Fixes)

Fixes that require small, targeted edits to eliminate compiler warnings and unnecessary CPU waste.

### Task 1.1: Disable Debug Toggles Left Enabled

Four files have `const bool DebugEnabled = true` in production systems, generating verbose logging and CS0162 warnings:

- [x] `Assets/Scripts/Items/Systems/ItemSetSwitchSystem.cs:29` — Change `const bool DebugEnabled = true` to `false`
- [x] `Assets/Scripts/Items/Systems/ItemSpawnSystem.cs:33` — Change `const bool DebugEnabled = true` to `false`
- [x] `Assets/Scripts/Items/Systems/ItemSwitchInputSystem.cs:28` — Change `const bool DebugEnabled = true` to `false`
- [x] `Assets/Scripts/Items/Systems/ItemEquipSystem.cs` — Verify debug state (may also be `true`)

**Impact:** Eliminates active debug spam in Item systems. These generate CS0162 warnings that are currently suppressed with `#pragma warning disable`.

### Task 1.2: Convert Debug Toggle Pattern to Conditional Compilation

23 files use `const bool DebugEnabled = false` + `#pragma warning disable CS0162`. This pattern works but is noisy. The AAA-standard approach is `[Conditional("DEBUG")]` or `#if UNITY_EDITOR` guards:

**Current pattern (generates warnings):**
```csharp
#pragma warning disable CS0162
private const bool DebugEnabled = false;
// ...
if (DebugEnabled) Debug.Log("...");
```

**Target pattern (zero warnings):**
```csharp
[System.Diagnostics.Conditional("DIG_DEBUG_ITEMS")]
static void DebugLog(string msg) => UnityEngine.Debug.Log(msg);
// ...
DebugLog("...");  // Compiled out entirely in release
```

Files to convert (prioritized by system importance):

**Item Systems (7 files):**
- [x] `ItemSetSwitchSystem.cs`
- [x] `ItemSpawnSystem.cs`
- [x] `ItemSwitchInputSystem.cs` (has TWO toggles at lines 28 and 128)
- [x] `InventoryBindingSystem.cs`
- [x] `ItemStateSystem.cs`
- [x] `ItemPickupSystem.cs`
- [x] `ItemEquipSystem.cs`
- [x] `StartingInventoryAuthoring.cs`

**Player Systems (4 files):**
- [x] `FootIKSystem.cs`
- [x] `AimDirectionSystem.cs`
- [x] `CollisionRelevancySystem.cs`
- [x] `RideMountDetectionSystem.cs`

**Weapon Systems (3 files):**
- [x] `WeaponDebugSystem.cs`
- [x] `SweptMeleeHitboxSystem.cs`
- [x] `ProjectileSystem.cs`

**Other (5 files):**
- [x] `EnvironmentZoneDetectionSystem.cs`
- [x] `ShipMovementSystem.cs`
- [x] `CursorClickTargetSystem.cs`
- [x] `CursorHoverSystem.cs`
- [x] `ItemVisualSystem.cs`
- [x] `EquipmentProviderBindingSystem.cs`

**How:** For each file:
1. Remove `#pragma warning disable CS0162`
2. Remove `const bool DebugEnabled` declaration
3. Add `[System.Diagnostics.Conditional("DIG_DEBUG")]` static method
4. Replace `if (DebugEnabled) Debug.Log(...)` with `DebugLog(...)`
5. Remove now-unnecessary `#pragma warning restore` if present

**Impact:** Eliminates all CS0162 warnings (~184 instances across packages too, but our 23 files are the controllable ones). Debug logging still works when `DIG_DEBUG` scripting define is set in Player Settings.

### Task 1.3: Gut Empty Stub Systems

Three systems iterate entities but do nothing meaningful — pure CPU waste:

**AddSpawnSystem** (`Assets/Scripts/AI/Systems/AddSpawnSystem.cs`):
- [x] Convert `OnUpdate` to early-return with comment explaining it's deferred to NetCode ghost spawning infrastructure
- [x] Or: Remove `RequireForUpdate<EncounterState>()` so it never runs (since OnUpdate is empty)

**TelegraphVisualBridge** (`Assets/Scripts/Combat/Bridges/TelegraphVisualBridge.cs`):
- [x] Add `Enabled = false` in OnCreate, or remove the foreach loop that does nothing
- [x] The system queries TelegraphZone entities, reads them, calculates progress, then drops the result

**DamageDebugLogSystem** (`Assets/Scripts/Player/Systems/TestSystems.cs:163-187`):
- [x] Gate with `#if UNITY_EDITOR` or `[Conditional("DEBUG")]`
- [x] Currently runs on ServerSimulation every frame, logging all health changes to console
- [x] Also review `KillFeedSystem` in same file (lines 138-161) — same pattern

**Impact:** Eliminates wasted query iteration and Debug.Log calls in production builds.

### Task 1.4: Suppress Intentional Inspector-Only Field Warnings

Two debug tester MonoBehaviours use `[SerializeField]` fields for Inspector display only (assigned in code, read only via Inspector):

- [x] Add `#pragma warning disable CS0414` to `Assets/Scripts/Vision/Debug/VisionDebugTester.cs`
- [x] Add `#pragma warning disable CS0414` to `Assets/Scripts/Aggro/Debug/AggroDebugTester.cs`

**Why:** These fields (`_isAggroed`, `_currentTargetName`, etc.) are assigned at runtime and displayed in the Inspector for debugging. The compiler correctly flags them as "assigned but never read" because no C# code reads them — Unity's Inspector does via reflection. This is a legitimate Unity pattern.

---

## Phase 2: ShootableActionSystem — Firearm Combat (CRITICAL)

**File:** `Assets/Scripts/Weapons/Systems/ShootableActionSystem.cs`
**Status:** Non-functional. Hitscan raycast detects hits but never applies damage. Projectile mode never spawns entities.

This is the **most critical stub in the codebase**. Firearms are core gameplay.

### Current State

```
Hitscan path: Raycast → hit detected → // TODO: Apply damage to hit.Entity → nothing happens
Projectile path: // TODO: Spawn projectile entity → nothing happens
```

### Required Implementation

**Task 2.1: Hitscan Damage Application** ✅
- [x] On hit, create `PendingCombatHit` entity (same pattern as `ProjectileSystem` lines 254-276)
- [x] Resolve hit entity through HitboxOwnerLink redirect (ROOT→CHILD pattern)
- [x] Set damage from `ShootableAction.BaseDamage` field
- [x] Set HitType based on hitbox zone (Head/Torso/Limb)
- [x] Set DamageType from weapon config
- [x] Apply spread deviation from `WeaponSpreadSystem` output

**Task 2.2: Projectile Spawning** ✅
- [x] On fire, instantiate projectile prefab entity via ECB
- [x] Set initial position to muzzle point
- [x] Set initial velocity from aim direction * `ShootableAction.ProjectileSpeed`
- [x] `ProjectileSystem` already handles flight, collision, and impact — just needs the spawn

**Task 2.3: Integration Verification** ✅
- [x] Verify PendingCombatHit flows through CombatResolutionSystem → CombatResultEvent → damage
- [x] Verify projectile spawning connects to existing ProjectileSystem lifecycle
- [x] Test with existing BoxingJoe enemy (known-good damage target)

**Impact:** Without this, no firearm in the game deals damage through this system. Other weapon systems (WeaponFireSystem) may handle some weapons, but ShootableActionSystem is the EPIC 15.7 weapon action pipeline.

---

## Phase 3: ProjectileSystem — Impact Explosions

**File:** `Assets/Scripts/Weapons/Systems/ProjectileSystem.cs:171`
**Status:** Timer-based detonation works. Impact-triggered detonation is stubbed.

### Current State

```
Grenade hits wall → ExplodeOnImpact = true → // TODO: Create explosion event → nothing happens
Grenade timer expires → DetonateOnTimer works → ProjectileExplosionSystem handles it correctly
```

### Required Implementation

**Task 3.1: Impact Explosion Event** ✅
- [x] When `impactRef.ExplodeOnImpact && impactRef.ImpactRadius > 0` and projectile impacts:
  - Create `ModifierExplosionRequest` entity (same as timer detonation path)
  - Set `Position` to impact point
  - Set `Radius` from `ImpactRadius`
  - Set `SourceEntity` to projectile owner
  - Destroy the projectile entity
- [x] Mirror the existing timer detonation code path in `ProjectileExplosionSystem`

**Task 3.2: Verify Integration** ✅
- [x] Confirm `ModifierExplosionRequest` is consumed by explosion damage system
- [x] Confirm `ExplosionSoundEmitterSystem` (EPIC 15.33) picks up the request for AI hearing
- [x] Test with a grenade prefab that has `ExplodeOnImpact = true`

**Impact:** Impact grenades, rockets, and any projectile with `ExplodeOnImpact` don't explode on contact. They only work if they also have a timer fallback.

---

## Phase 4: Channeled Abilities & Throw Spawning

### Task 4.1: ChannelActionSystem Effect Application

**File:** `Assets/Scripts/Weapons/Systems/ChannelActionSystem.cs:138-153`
**Status:** Channel state machine works (start/tick/end transitions, timer, tick counting). Effect application is a Debug.Log stub.

- [x] Implement `ApplyChannelTick`:
  - Raycast from owner in look direction up to `config.Range`
  - If `config.IsHealing` and target has `Health`: create `HealEvent` on target
  - If `!config.IsHealing` and target is enemy: create `DamageEvent` on target (or `PendingCombatHit`)
  - Update `ChannelState.CurrentTarget` for VFX beam rendering
- [ ] Add resource cost deduction when mana/stamina system exists (deferred — no resource system yet)
- [x] Remove the 3 `Debug.Log` statements in the channel lifecycle (or gate them)

**Impact:** Channeled weapons (healing beam, drain life) go through the full state machine but never actually heal or damage.

### Task 4.2: WeaponAnimationEventSystem Throw Spawning

**File:** `Assets/Scripts/Weapons/Systems/WeaponAnimationEventSystem.cs:428`
**Status:** Throw charge calculation works. Projectile entity never spawned at release.

- [x] On `ThrowRelease` event: create projectile entity via ECB
- [x] Set position to throw point, velocity from `throwForce * throwDirection`
- [x] Connect to existing `ProjectileSystem` flight handling
- [ ] Fire effect request (line 199 TODO) — **unblocked by EPIC 16.7**: create `VFXRequest` entity with `VFXTypeIds.AbilityFireBurst` via ECB on throw release

**Impact:** Throw weapons (grenades, javelins) calculate charge power but never release the projectile.

---

## Phase 5: Polish TODOs (Low Priority)

These are nice-to-have improvements that don't block gameplay. Can be deferred indefinitely.

### Task 5.1: Combat Resolution Polish

**CombatResolutionSystem.cs:**
- [x] Line 143: Implement `ModifierType.Lifesteal` — apply heal to attacker equal to `modifier.Value * damage`.
- [ ] Line 147: Create knockback event entity — deferred, needs knockback system design first.
- [x] Line 342: Read actual `Health.Current / Health.Max` instead of hardcoded `HealthPercent = 1f`.

### Task 5.2: Weapon Spread Movement Penalty

**WeaponSpreadSystem.cs:67:**
- [x] Add movement speed check via `PhysicsVelocity` or `CharacterControllerState`
- [x] Moving increases spread recovery time or adds spread per frame
- [x] Standing still improves aim stability

### Task 5.3: Stealth Surface Material Integration ✅

**RESOLVED by EPIC 16.10** — `StealthSystem` now reads `GroundSurfaceState.SurfaceId` and looks up `NoiseMultiplier` from `SurfaceGameplayBlob`. `FallDetectionSystem` `SurfaceMaterialId = 0` TODO also fixed.

**StealthSystem.cs:84:**
- [x] Integrate with `SurfaceMaterialId` from ground raycast — via `GroundSurfaceState` + `SurfaceGameplayConfigSingleton` BlobAsset lookup
- [x] Hard surfaces (metal, tile) multiply noise by 1.5x — configurable via `SurfaceGameplayConfig` SO
- [x] Soft surfaces (grass, carpet) multiply noise by 0.5x — configurable via `SurfaceGameplayConfig` SO
- [x] Requires SurfaceMaterial system to be built — **EPIC 16.10 implemented** (GroundSurfaceQuerySystem, SurfaceGameplayConfigSystem, BlobAsset pipeline)

### Task 5.4: AI CircleStrafe Behavior ✅

**AICombatBehaviorSystem.cs:100:**
- [x] When in melee range, strafe laterally around target instead of standing still
- [x] Random direction changes every 1-3 seconds
- [x] Cosmetic polish — AI already approaches and attacks correctly

### Task 5.5: EnemyHealthBar Level/Elite Display

**EnemyHealthBar.cs:253-254:**
- [ ] `SetLevel(int level)` — render level number on mesh health bar (requires TextMesh or SDF text)
- [ ] `SetElite(bool isElite)` — render elite marker/glow on health bar
- [ ] Currently empty with comment "No text on Mesh yet"

### Task 5.6: Item Equip Duration from Data ✅

**ItemEquipSystem.cs:194:**
- [x] Read `EquipDuration` from `ItemDefinition` ScriptableObject instead of hardcoded `0.5f`
- [x] Allows per-weapon equip speed tuning (pistol fast, heavy weapon slow)

---

## Phase 6: Debug Logging Cleanup (Bulk)

69 files in `Assets/Scripts/Player/Systems/` contain `Debug.Log` statements. Most are legitimate development diagnostics but should be gated for release builds.

### Task 6.1: Establish Debug Logging Convention

- [x] Extended existing `Assets/Scripts/Core/DebugLog.cs` with category-based conditional logging (Combat, Items, AI, Weapons, Aggro, Surface):

```csharp
public static class DIGDebug
{
    [Conditional("DIG_DEBUG_COMBAT")]  public static void Combat(string msg) => Debug.Log($"[COMBAT] {msg}");
    [Conditional("DIG_DEBUG_ITEMS")]   public static void Items(string msg) => Debug.Log($"[ITEMS] {msg}");
    [Conditional("DIG_DEBUG_AI")]      public static void AI(string msg) => Debug.Log($"[AI] {msg}");
    [Conditional("DIG_DEBUG_PHYSICS")] public static void Physics(string msg) => Debug.Log($"[PHYS] {msg}");
    [Conditional("DIG_DEBUG_NET")]     public static void Net(string msg) => Debug.Log($"[NET] {msg}");
    [Conditional("DIG_DEBUG_WEAPONS")] public static void Weapons(string msg) => Debug.Log($"[WEAPONS] {msg}");
}
```

### Task 6.2: Migrate Production Systems

Prioritized by frequency and system criticality:

**High-traffic systems (log every frame or every hit):**
- [x] `ChannelActionSystem.cs` — 3 Debug.Log calls in channel lifecycle
- [x] `DamageDebugLogSystem` in `TestSystems.cs` — logs ALL health changes
- [x] `KillFeedSystem` in `TestSystems.cs` — logs ALL kills/assists

**Medium-traffic systems:**
- [x] Systems in `Assets/Scripts/Items/Systems/` (7 files with debug toggles)
- [x] Systems in `Assets/Scripts/Weapons/Systems/` (3 files)
- [x] `DeathTransitionSystem.cs`

**Low-traffic systems (log on state transitions only):**
- [x] `PlayerAnimationStateSystem.cs`, `CharacterControllerSystem.cs`, etc.
- [x] These can remain as-is until the convention is established

### Task 6.3: DemoTools Audit

The `Assets/Scripts/DemoTools/` directory contains 10 files. After Phase 0 deletions (3 files), 7 remain:

| File | Keep? | Reason |
|------|-------|--------|
| `AnimatorVerboseLogger.cs` | Yes | Active debugging tool for animator issues |
| `CacheStatsDisplay.cs` | Yes | Performance monitoring overlay |
| `ClimbDebugVisualizer.cs` | Yes | Climb system debug gizmos |
| `CollisionDebugVisualizer.cs` | Yes | Physics debug gizmos |
| `CombatDiagnostics.cs` | Yes | Combat flow tracing (well-structured) |
| `CombatDiagnosticsController.cs` | Yes | Inspector control for above |
| `ParadigmDemoUI.cs` | Yes | Runtime paradigm switching (useful for QA) |

- [x] Gate all DemoTools with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to exclude from release builds
- [x] These are **legitimate debug tools** — well-structured, documented, and useful

---

## Items NOT Requiring Action

The following were investigated and found to be correct/intentional:

| Item | Reason |
|------|--------|
| `global::Player.Systems.*` references | Valid C# — `global::` prefix is legal for fully-qualified names in `Player.Systems` namespace |
| Gravity system | Fully implemented (`GravityZoneSystem`, `GravityOverride`, integrated with `PlayerMovementSystem`) |
| Empty `OnDestroy(ref SystemState)` methods | Normal ISystem pattern — cleanup not always needed |
| `VoxelProfiler` empty methods in `#else` block | Intentional release-build no-ops via conditional compilation |
| `ViewModelBase` virtual stubs | Correct MVVM base class pattern — subclasses override as needed |
| Voxel Editor Module empty methods | Interface compliance for `IVoxelModule` — editor-only code |
| `PerformanceCaptureSession` release stubs | Full implementation under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, stubs are correct |
| `EquipmentProviderBindingSystem` debug fields | Inspector-only display fields (Unity reflection pattern) |
| `ProneSystem.SafeStandCheck` | Full implementation exists at line 259 — permissive stub at 225 is dead code (covered in Phase 0) |
| BUGFIX comments | Documentation of past fixes — valuable for maintainability, do not remove |

---

## Verification Checklist

| # | Test | Phase | Expected |
|---|------|-------|----------|
| 1 | Clean build with zero warnings | 0-1 | No CS0162, CS0414, CS0618 warnings |
| 2 | Existing enemies (BoxingJoe) still take damage | 2 | Hitscan/melee damage unchanged |
| 3 | Firearms deal damage via ShootableActionSystem | 2 | Bullet hits reduce enemy health |
| 4 | Impact grenades explode on contact | 3 | Area damage applied at impact point |
| 5 | Channeled weapons heal/damage targets | 4 | Beam applies effect per tick |
| 6 | Thrown weapons spawn projectiles | 4 | Grenade throw creates flying entity |
| 7 | No Debug.Log spam in release build | 6 | Console clean when DIG_DEBUG not defined |
| 8 | DemoTools excluded from release | 6 | No DemoTools code in shipping build |
| 9 | All existing gameplay unchanged | All | Regression test full combat loop |

---

## Priority Order

```
Phase 0: Deletions .............. [~1 hour]  Zero risk, immediate warning reduction
Phase 1: Warning Elimination .... [~2 hours] Manual but mechanical, big warning count drop
Phase 2: ShootableActionSystem .. [~3 hours] CRITICAL — firearms don't work
Phase 3: Impact Explosions ...... [~1 hour]  High — impact grenades broken
Phase 4: Channel + Throw ........ [~3 hours] Medium — channeled abilities stubbed
Phase 5: Polish TODOs ........... [~4 hours] Low — deferred indefinitely OK
Phase 6: Debug Convention ....... [~4 hours] Low — bulk migration, no gameplay impact
```

**Recommendation:** Execute Phases 0-1 first (immediate warning cleanup), then Phase 2 (critical gameplay). Phases 3-4 next sprint. Phases 5-6 whenever convenient.

---

## File Summary

### Files to Delete (Phase 0)

| File | Reason |
|------|--------|
| `Items/Interfaces/EquipmentSlotConfig.cs` | `[Obsolete]`, replaced by `EquipmentSlotDefinition` |
| `Assets/Prefabs/EquipmentSlot.asset` | Instance of deprecated type |
| `Assets/Prefabs/MainHand.asset` | Instance of deprecated type |
| `Assets/Prefabs/OffHand.asset` | Instance of deprecated type |
| `Visuals/UI/FlashlightHUD.cs` | Temporary, replaced by MVVM `FlashlightHUDView` |
| `DemoTools/WeaponPositionDiagnosticSystem.cs` | Temporary diagnostic, marked for removal |
| `DemoTools/PlayerTelemetry.cs` | Empty placeholder, no-op methods |
| `DemoTools/BoneNameList.cs` | Empty file |

### Files to Modify (Phase 1)

| File | Change |
|------|--------|
| `Items/Systems/ItemSetSwitchSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemSpawnSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemSwitchInputSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemEquipSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/InventoryBindingSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemStateSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemPickupSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/ItemVisualSystem.cs` | Debug toggle → conditional compilation |
| `Items/Authoring/StartingInventoryAuthoring.cs` | Debug toggle → conditional compilation |
| `Player/Systems/IK/FootIKSystem.cs` | Debug toggle → conditional compilation |
| `Player/Systems/IK/AimDirectionSystem.cs` | Debug toggle → conditional compilation |
| `Player/Systems/CollisionRelevancySystem.cs` | Debug toggle → conditional compilation |
| `Player/Systems/RideMountDetectionSystem.cs` | Debug toggle → conditional compilation |
| `Player/Systems/ProneSystem.cs` | Remove dead SafeStandCheck stub |
| `Weapons/Systems/WeaponDebugSystem.cs` | Debug toggle → conditional compilation |
| `Weapons/Systems/SweptMeleeHitboxSystem.cs` | Debug toggle → conditional compilation |
| `Weapons/Systems/ProjectileSystem.cs` | Debug toggle → conditional compilation |
| `Runtime/Survival/Environment/Systems/EnvironmentZoneDetectionSystem.cs` | Debug toggle → conditional compilation |
| `Runtime/Ship/LocalSpace/Systems/ShipMovementSystem.cs` | Debug toggle → conditional compilation |
| `Targeting/Systems/CursorClickTargetSystem.cs` | Debug toggle → conditional compilation |
| `Targeting/Systems/CursorHoverSystem.cs` | Debug toggle → conditional compilation |
| `Items/Systems/EquipmentProviderBindingSystem.cs` | Debug toggle → conditional compilation |
| `AI/Systems/AddSpawnSystem.cs` | Disable empty system (RequireForUpdate on impossible singleton) |
| `Combat/Bridges/TelegraphVisualBridge.cs` | Disable empty system |
| `Player/Systems/TestSystems.cs` | Gate with `#if UNITY_EDITOR` |
| `Vision/Debug/VisionDebugTester.cs` | Add CS0414 pragma |
| `Aggro/Debug/AggroDebugTester.cs` | Add CS0414 pragma |
| `Runtime/Survival/Explosives/Components/ExplosiveComponents.cs` | Remove VoxelDamageRequest_Legacy |
| `Player/UI/Views/FlashlightHUDView.cs` | Remove obsolete RegisterWithECS method |

### Files to Implement (Phases 2-4)

| File | Change |
|------|--------|
| `Weapons/Systems/ShootableActionSystem.cs` | Implement hitscan damage + projectile spawn |
| `Weapons/Systems/ProjectileSystem.cs` | Implement impact explosion event |
| `Weapons/Systems/ChannelActionSystem.cs` | Implement effect application (heal/damage) |
| `Weapons/Systems/WeaponAnimationEventSystem.cs` | Implement throw projectile spawning |

### Modified Files (Phase 6)

| File | Purpose |
|------|---------|
| `Core/DebugLog.cs` | Extended with Combat, Items, AI, Weapons, Aggro, Surface categories |

---

## Design Considerations

### Remaining Items & Dependencies

All unchecked items are blocked on systems that don't exist yet:

| Item | Blocker | System Needed |
|------|---------|---------------|
| Task 4.1: Resource cost deduction | Mana/stamina/resource system | Needs EPIC for player resource management (mana pool, stamina bar, resource costs on abilities) |
| Task 4.2: Throw VFX effect request | ~~VFX pipeline~~ **RESOLVED (EPIC 16.7)** | `VFXRequest` entity pipeline now exists. Create request with `VFXTypeIds` constant + `VFXCategory.Combat` via ECB. Needs VFX prefab asset. |
| Task 5.1: Knockback event | Knockback system | Needs physics-based knockback: `KnockbackEvent` IComponentData → `KnockbackSystem` applies force over frames, respects CC immunity |
| Task 5.3: Surface material integration | ~~SurfaceMaterial system~~ **RESOLVED (EPIC 16.10)** | `GroundSurfaceState` + `SurfaceGameplayBlob` pipeline. StealthSystem reads NoiseMultiplier, FallDetectionSystem reads FallDamageMultiplier + SurfaceMaterialId. |
| Task 5.5: Health bar level/elite text | Text rendering on mesh | Needs TextMeshPro or SDF text rendering on ECS mesh health bars. Currently no text rendering pipeline for world-space ECS UI |

None of these blockers are critical — all affected systems have working fallbacks (no cost deduction = free abilities, no knockback = damage only, no surface material = uniform noise, no level text = no display). They are polish items that become possible once the underlying systems exist.

### CI/CD Integration

To enforce zero-warning builds going forward:
1. Add `-warnaserror` to the CI build script's compiler arguments (Unity Build Pipeline → Additional compiler arguments)
2. Exceptions: `CS0618` (obsolete warnings) during migration periods — use `#pragma warning disable CS0618` explicitly around known transitional code
3. Pre-commit hook: `dotnet build --warnaserror` on changed `.cs` files
4. The `DIG_DEBUG_*` conditional compilation defines should NOT be set in CI builds — ensures debug logging is compiled out

### Regression Testing Strategy

Current testing is manual ("test with BoxingJoe"). For AAA quality, recommend:
1. **PlayMode tests** for critical combat paths: spawn enemy → apply damage → verify health change → verify death state → verify corpse lifecycle
2. **EditMode tests** for pure logic: `LootTableResolver.Resolve()` with known seed → assert expected drops
3. **Automated build verification:** CI runs all PlayMode tests after clean build
4. This is out of scope for 16.5 (which is a cleanup EPIC), but should be a future EPIC focused on test infrastructure
