# EPIC 18.18: Targeting & Attack Coverage for All Game Modes — Setup Guide

**Status:** Planned (audit complete, implementation pending)
**Last Updated:** March 5, 2026
**Requires:** EPIC 15.20 (Input Paradigm Framework), EPIC 15.18 (Cursor Hover & Click-to-Select), EPIC 18.15 (Click-to-Move & WASD Gating)

---

## Overview

EPIC 18.18 audits how targeting and attack behavior works across all 7 input profiles (Shooter, ShooterHybrid, MMO, ARPG Classic, ARPG Hybrid, MOBA, TwinStick). It documents the expected per-paradigm targeting semantics, identifies gaps in the current implementation, and proposes solutions.

This setup guide covers:
- How to configure and verify targeting behavior per paradigm using existing assets
- The current per-paradigm targeting/attack behavior matrix
- Known gaps and workarounds
- Where each piece of the targeting pipeline lives

---

## Prerequisites

- EPIC 15.20: Input Paradigm Framework set up (ParadigmStateMachine, profiles, subsystems in scene)
- EPIC 15.18: Cursor Hover & Click-to-Select systems present in subscene
- EPIC 18.15: Click-to-Move & WASD Gating applied (for MOBA/ARPG movement)
- Input profiles configured in `Assets/Data/Input/Profiles/`
- `TargetDataAuthoring` on the player prefab (sets initial targeting mode)

---

## 1. Input Profile Targeting Reference

Each input profile defines cursor, movement, and combat map behavior. Targeting mode is **not yet a profile field** (Gap 1 below) — it is currently baked via `TargetDataAuthoring.InitialMode` on the player prefab.

### Profile Location

`Assets/Data/Input/Profiles/`

Select any `.asset` file in the Project window to view its settings in the Inspector.

### Profile → Targeting Behavior Matrix

| Profile | Paradigm | Cursor | Combat Map | LMB Action | RMB Action | Expected Targeting |
|---------|----------|--------|------------|------------|------------|-------------------|
| **Profile_Shooter** | Shooter | Locked | Combat_Shooter | Attack (Fire) | Aim | CameraRaycast — crosshair at screen center |
| **Profile_ShooterHybrid** | Shooter | Locked (Alt frees) | Combat_Shooter | Attack (Fire) | Aim | CameraRaycast (locked) / ClickSelect (Alt held) |
| **Profile_MMO** | MMO | Free | Combat_MMO | SelectTarget | CameraOrbit | ClickSelect — click enemy to select |
| **Profile_ARPG_Classic** | ARPG | Free | Combat_ARPG | AttackAtCursor (Fire) | MoveToClick | CursorAim — fire toward cursor position |
| **Profile_ARPG_Hybrid** | ARPG | Free | Combat_ARPG | AttackAtCursor (Fire) | MoveToClick | CursorAim — fire toward cursor position |
| **Profile_MOBA** | MOBA | Free | Combat_MOBA | AttackAtCursor (Fire) | Move (RMB) | CursorAim — fire toward cursor position |
| **Profile_TwinStick** | TwinStick | Free | Combat_Shooter | Attack (Fire) | Aim | CursorAim or CameraRaycast |

### Combat Action Map Details

Each paradigm activates a different combat action map via `ParadigmInputManager.ApplyParadigmMaps()`:

| Combat Map | Actions | Paradigms |
|------------|---------|-----------|
| **Combat_Shooter** | Attack, AimDownSights | Shooter, ShooterHybrid, TwinStick |
| **Combat_MMO** | SelectTarget, CameraOrbit | MMO |
| **Combat_ARPG** | AttackAtCursor, MoveToClick | ARPG Classic, ARPG Hybrid |
| **Combat_MOBA** | AttackAtCursor, AttackMove, Stop, HoldPosition | MOBA |

### How Actions Map to PlayerInputState

| Action | PlayerInputState Field | Used By |
|--------|------------------------|---------|
| Attack | `Fire` | WeaponFireSystem |
| SelectTarget | `Select` | CursorClickTargetSystem |
| AttackAtCursor | `Fire` | WeaponFireSystem + CursorClickTargetSystem |
| MoveToClick | `Aim` | ClickToMoveHandler (ARPG) |
| CameraOrbit | `CameraOrbit` | CameraOrbitController (MMO) |

---

## 2. Targeting Systems Reference

These are the systems that currently drive targeting. They run based on cursor state and scene presence — **not** based on paradigm.

### ECS Systems (Always Active)

| System | File | Activation | Writes |
|--------|------|------------|--------|
| **PlayerFacingSystem** | `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | Always runs | `TargetData.AimDirection` based on FacingMode |
| **CursorHoverSystem** | `Assets/Scripts/Targeting/Systems/CursorHoverSystem.cs` | Only when `IsCursorFree == true` | `CursorHoverResult` singleton |
| **CursorClickTargetSystem** | `Assets/Scripts/Targeting/Systems/CursorClickTargetSystem.cs` | Only when `IsCursorFree == true` | `TargetData.TargetEntity`, `TargetData.Mode` |

### MonoBehaviour Targeting Implementations (Scene-Placed)

| Component | File | Purpose |
|-----------|------|---------|
| **CameraRaycastTargeting** | `Assets/Scripts/Targeting/Implementations/CameraRaycastTargeting.cs` | Raycast from screen center — Shooter/TPS |
| **CursorAimTargeting** | `Assets/Scripts/Targeting/Implementations/CursorAimTargeting.cs` | Raycast from cursor position — ARPG/MOBA |
| **LockOnTargeting** | `Assets/Scripts/Targeting/Implementations/LockOnTargeting.cs` | Manual lock-on with tab cycle — Souls-like |
| **AutoTargetTargeting** | `Assets/Scripts/Targeting/Implementations/AutoTargetTargeting.cs` | Auto-lock nearest enemy |

These MonoBehaviours run if present in the scene. They are **not** automatically enabled/disabled by paradigm switching (see Gap 6 below).

### AimDirection Resolution Chain

`WeaponFireSystem` reads `TargetData.AimDirection` to determine fire direction. The value is written by different systems depending on the active `FacingMode` in `ParadigmSettings`:

| FacingMode | Source | Paradigm |
|------------|--------|----------|
| **CameraForward** | Camera yaw/pitch → 3D direction | Shooter, ShooterHybrid |
| **MovementDirection** | Character movement direction | ARPG (when moving) |
| **CursorDirection** | Player-to-cursor direction | TwinStick |
| **TargetLocked** | Player-to-target direction | Any (when locked on) |

---

## 3. TargetingMode & TargetData Configuration

### TargetingMode Enum

Located at `Assets/Scripts/Targeting/TargetingMode.cs`:

| Mode | Value | Description | Intended Paradigms |
|------|-------|-------------|-------------------|
| **CameraRaycast** | 0 | Fire toward screen center / crosshair | Shooter, TwinStick |
| **CursorAim** | 1 | Fire toward mouse cursor world position | ARPG, MOBA |
| **AutoTarget** | 2 | Auto-lock nearest valid enemy | Fast-paced action |
| **LockOn** | 3 | Manual lock-on with tab cycling | Souls-like |
| **ClickSelect** | 4 | Click enemy to select, then use abilities | MMO |

### Setting the Initial Targeting Mode

Currently, the targeting mode is set at bake time via the player prefab:

1. Select the player prefab (e.g., `Warrok_Server`)
2. Find the **TargetDataAuthoring** component
3. Set **Initial Mode** to the desired `TargetingMode` value

> **Important:** This value is baked once and does **not** change when the player switches paradigms at runtime. This is the primary gap addressed by EPIC 18.18 Phase 1.

### TargetingConfig ScriptableObject

Located at `Assets/Scripts/Targeting/TargetingConfig.cs`. This is a separate configuration asset for advanced targeting tuning:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Targeting Mode** | `TargetingMode` | CameraRaycast | Which targeting implementation |
| **Max Target Range** | float | 100 | Maximum detection/lock distance (meters) |
| **Valid Target Layers** | LayerMask | — | Which physics layers are targetable |
| **Require Line Of Sight** | bool | true | Whether targets must be visible |
| **Auto Target On Use** | bool | false | Auto-acquire target when weapon fires |
| **Sticky Targeting** | bool | false | Keep target after weapon use ends |
| **Target Priority** | `TargetPriority` | Nearest | Selection priority (Nearest, LowestHealth, HighestThreat, CursorProximity) |
| **Aim Assist Strength** | float (0-1) | 0.2 | Aim magnetism toward valid targets |
| **Aim Assist Radius** | float | 2 | Detection radius for aim assist |
| **Lock On Max Angle** | float | 30 | Maximum angle from look direction for lock-on acquisition |
| **Lock On Max Distance** | float | 30 | Maximum distance for lock-on acquisition |

Factory presets are available:
- `CreateDIGPreset()` — TPS shooter (CameraRaycast, 200m range, 0.2 aim assist)
- `CreateARPGPreset()` — Isometric ARPG (CursorAim, 15m range, no aim assist)

---

## 4. Lock Behavior Configuration

### LockBehaviorType Enum

Located at `Assets/Scripts/Targeting/Core/LockBehaviorType.cs`:

| Type | Value | Description | Intended Paradigm |
|------|-------|-------------|-------------------|
| **None** | 0 | No lock behavior, UI indicators only | — |
| **HardLock** | 1 | Camera locks onto target, player strafes | Shooter |
| **SoftLock** | 2 | Free camera, character rotates + aim assist | MMO |
| **IsometricLock** | 3 | Fixed camera, character faces target direction | ARPG, MOBA |
| **OverTheShoulder** | 4 | Offset camera, ADS support | Shooter (ADS) |
| **TwinStick** | 5 | Independent aim, sticky aim assist | TwinStick |
| **FirstPerson** | 6 | Camera IS the view, magnetism only | FPS |

### ActiveLockBehavior Singleton

The `ActiveLockBehavior` ECS singleton controls all lock behavior globally. Key tuning fields:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **BehaviorType** | `LockBehaviorType` | HardLock | Current lock mode |
| **Features** | `LockFeatureFlags` | — | Bitmask: MultiLock, PartTargeting, PredictiveAim, etc. |
| **InputMode** | `LockInputMode` | Toggle | Toggle, Hold, ClickTarget, AutoNearest, HoverTarget |
| **CharacterRotationStrength** | float (0-1) | — | How much character rotates toward target |
| **AimMagnetismStrength** | float (0-1) | — | Aim pull toward target |
| **StickyAimStrength** | float (0-1) | — | Sensitivity slowdown when over targets |
| **CameraTrackingSpeed** | float (deg/sec) | — | Camera rotation speed for HardLock |
| **MaxLockRange** | float | 30 | Distance to acquire/maintain lock |
| **MaxLockAngle** | float | 30-45 | Angle from look direction for acquisition |
| **DefaultHeightOffset** | float | 1.5 | Lock point height (chest height) |

### Changing Lock Behavior at Runtime

Use `LockBehaviorHelper` (static utility in `Assets/Scripts/Targeting/Systems/LockBehaviorDispatcherSystem.cs`):

```csharp
// Set lock behavior mode
LockBehaviorHelper.SetMode(LockBehaviorType.SoftLock);

// Query current mode
LockBehaviorType current = LockBehaviorHelper.GetCurrentMode();
```

Factory presets are available on `ActiveLockBehavior`:
- `ActiveLockBehavior.HardLock()` — TPS shooter default
- `ActiveLockBehavior.SoftLock()` — MMO tab-target
- `ActiveLockBehavior.FirstPerson()` — FPS mode
- `ActiveLockBehavior.MechCombat()` — Multi-lock + predictive aim

> **Note:** `LockBehaviorHelper.SetMode()` exists but is currently **never called** by the paradigm system. Lock behavior does not change when switching paradigms. This is addressed by EPIC 18.18 Phase 1.

---

## 5. Per-Paradigm Expected Behavior

### Shooter (Profile_Shooter)

| Aspect | Behavior |
|--------|----------|
| Cursor | Locked to screen center |
| Targeting | CameraRaycast — crosshair/reticle at center |
| AimDirection | From camera yaw/pitch (FacingMode = CameraForward) |
| LMB | Attack → `Fire` → WeaponFireSystem reads AimDirection |
| RMB | Aim Down Sights |
| Click-Select | Disabled — `CursorHoverSystem` / `CursorClickTargetSystem` do not run (cursor locked) |
| Lock-On | Tab key cycles targets (HardLock mode) |

### Shooter Hybrid (Profile_ShooterHybrid)

| Aspect | Behavior |
|--------|----------|
| Cursor | Locked by default; **Alt** temporarily frees cursor |
| When Locked | Identical to Shooter |
| When Free (Alt) | `CursorHoverSystem` and `CursorClickTargetSystem` activate. LMB = Fire + click-select. Behavior matches MMO during free-cursor phase. |

### MMO (Profile_MMO)

| Aspect | Behavior |
|--------|----------|
| Cursor | Free (visible, unlocked) |
| Targeting | ClickSelect — LMB click on enemy to select |
| AimDirection | Player → selected target direction |
| LMB | SelectTarget → `CursorClickTargetSystem` sets `TargetData.TargetEntity` |
| RMB | CameraOrbit — hold to rotate camera |
| Attack | Abilities/auto-attack use `TargetData.TargetEntity`. No direct "Attack" in Combat_MMO. |
| Clear Target | RMB click (clears selection while orbiting) |

### ARPG Classic (Profile_ARPG_Classic)

| Aspect | Behavior |
|--------|----------|
| Cursor | Free |
| Movement | Click-to-move via LMB (WASD disabled) |
| Targeting | CursorAim — fire toward cursor world position |
| LMB on ground | Click-to-move (path to clicked position) |
| LMB on enemy | AttackAtCursor (Fire) + select enemy |
| RMB | MoveToClick (secondary move input) |
| LMB Conflict | See Gap 3 below — both `ClickToMoveHandler` and `CursorClickTargetSystem` react to LMB |

### ARPG Hybrid (Profile_ARPG_Hybrid)

| Aspect | Behavior |
|--------|----------|
| Cursor | Free |
| Movement | WASD only (click-to-move disabled) |
| Targeting | CursorAim — fire toward cursor world position |
| LMB | AttackAtCursor (Fire) — no LMB move conflict |
| RMB | MoveToClick |

### MOBA (Profile_MOBA)

| Aspect | Behavior |
|--------|----------|
| Cursor | Free |
| Movement | RMB click-to-move (WASD disabled) |
| Targeting | CursorAim — fire toward cursor world position |
| LMB | AttackAtCursor (Fire) + click-select |
| RMB | Move (click-to-move) — also clears target selection (see Gap 4) |
| AttackMove | A-key + click — move to point, auto-attack enemies along the way |

### TwinStick (Profile_TwinStick)

| Aspect | Behavior |
|--------|----------|
| Cursor | Free |
| Movement | WASD |
| Combat Map | Combat_Shooter (same as Shooter) |
| Targeting | CursorDirection facing — character faces cursor position |
| LMB | Attack (Fire) — cursor free so `CursorClickTargetSystem` also runs |
| RMB | Aim |
| AimDirection | From character facing/cursor direction |

---

## 6. Known Gaps & Workarounds

These are documented gaps in the current targeting system that EPIC 18.18 plans to address.

### Gap 1: No Targeting Mode on InputParadigmProfile

**Problem:** `InputParadigmProfile` has cursor, movement, camera, and facing fields — but no targeting mode field. `TargetingMode` is baked once via `TargetDataAuthoring.InitialMode` and never changes at runtime.

**Impact:** Switching from Shooter (CameraRaycast) to ARPG (should be CursorAim) does not change the targeting mode. All paradigms use whatever was baked.

**Current workaround:** Set `TargetDataAuthoring.InitialMode` to the mode most commonly used. For mixed-paradigm testing, use `TargetingModeTester` (debug component) to manually switch modes at runtime.

**Planned fix:** Add `defaultTargetingMode` field to `InputParadigmProfile`. On paradigm switch, a new `TargetingConfigurable` (`IParadigmConfigurable`) writes `TargetData.Mode` from the profile.

### Gap 2: Lock Behavior Does Not Change Per Paradigm

**Problem:** `ActiveLockBehavior` singleton defaults to `HardLock` and never changes. `LockBehaviorHelper.SetMode()` exists but is never called by the paradigm system.

**Impact:** Switching to ARPG should use `IsometricLock`, but stays on `HardLock`.

**Current workaround:** Manually call `LockBehaviorHelper.SetMode()` from a test script or debug console.

**Planned fix:** Add `defaultLockBehavior` field to `InputParadigmProfile`. A new `LockBehaviorConfigurable` calls `SetMode()` during paradigm transitions.

### Gap 3: ARPG Classic LMB Dual Use (Move vs Attack)

**Problem:** In ARPG Classic, LMB triggers **both** `ClickToMoveHandler` (click-to-move) and `CursorClickTargetSystem` (click-to-select/attack). Both systems react to the same click on the same frame.

**Current behavior:**
- LMB on **ground**: `ClickToMoveHandler` starts a path, `CursorClickTargetSystem` clears selection (ground click)
- LMB on **enemy**: `CursorClickTargetSystem` selects + fires, `ClickToMoveHandler` may also try to path to the enemy's position

**Workaround:** This partially works because `ClickToMoveHandler` raycasts against ground geometry (Unity.Physics), while `CursorClickTargetSystem` raycasts against entity colliders. If the enemy collider is on a different layer than the ground, they don't conflict. Verify your layers separate enemies from walkable ground.

**Planned fix:** `ClickToMoveHandler` should check `CursorHoverResult` — if an entity is under the cursor, skip the move. Ground click = move, entity click = attack.

### Gap 4: MOBA RMB = Move + Clear Target

**Problem:** In MOBA, RMB is both the move button and `CursorClickTargetSystem`'s clear button. Every RMB click clears the current target selection in addition to starting movement.

**Impact:** This may be acceptable — in most MOBAs, clicking to move deselects the current target. If this behavior is undesirable, it would need to be changed.

### Gap 5: No Dedicated Combat_TwinStick Map

**Problem:** TwinStick reuses `Combat_Shooter`. This is intentional (same Attack/Aim bindings) but means TwinStick has no paradigm-specific actions. If TwinStick needs different bindings in the future, a dedicated map would need to be created.

### Gap 6: MonoBehaviour Targeting Systems Are Not Paradigm-Aware

**Problem:** `CameraRaycastTargeting`, `CursorAimTargeting`, `LockOnTargeting`, `AutoTargetTargeting` are MonoBehaviours that run if present in the scene. They are not enabled/disabled based on the active paradigm.

**Impact:** If both `CameraRaycastTargeting` and `CursorAimTargeting` are in the scene, they may both write to `TargetData` on the same frame, causing flickering or incorrect aim.

**Workaround:** Only place the targeting MonoBehaviour appropriate for your primary paradigm in the scene. For multi-paradigm testing, disable the ones you don't need.

**Planned fix:** Replace MonoBehaviour targeting implementations with paradigm-gated ECS systems (EPIC 18.19 Phase 2).

### Gap 7: Melee vs Ranged Targeting Mismatch

**Problem:** `WeaponFireSystem` reads `TargetData.AimDirection` for ranged fire direction. `MeleeActionSystem` uses hitbox detection and combo state — it does not read `TargetData` for aim. Melee targeting relies on character facing, which depends on `FacingMode`.

**Impact:** In CursorAim paradigms (ARPG/MOBA), melee attacks use character facing direction, not cursor direction. This may feel inconsistent if the character faces one direction but the cursor is elsewhere.

---

## 7. Verification Checklist

### Per-Paradigm Test Matrix

| Test | Paradigm | Steps | Expected Result |
|------|----------|-------|-----------------|
| Shooter aim | Shooter | LMB fire at crosshair | Projectile/hitscan goes toward screen center |
| Shooter lock | Shooter | Tab near enemy | Target locks, camera tracks enemy (HardLock) |
| Shooter no click-select | Shooter | LMB click in world | No entity selection (cursor locked) |
| Hybrid cursor free | ShooterHybrid | Hold Alt, LMB click enemy | Enemy selected, target indicator appears |
| Hybrid cursor lock | ShooterHybrid | Release Alt | Cursor locks, hover/click systems stop |
| MMO select | MMO | LMB click enemy | `TargetData.TargetEntity` set, target frame appears |
| MMO clear | MMO | RMB click empty ground | Target cleared |
| MMO orbit | MMO | Hold RMB + drag | Camera orbits (no target change) |
| ARPG ground click | ARPG Classic | LMB click ground | Character paths to click point |
| ARPG enemy click | ARPG Classic | LMB click enemy | Enemy selected + attack fires |
| ARPG WASD blocked | ARPG Classic | Press WASD | No movement (WASD gated) |
| ARPG Hybrid aim | ARPG Hybrid | LMB fire | Projectile goes toward cursor (not screen center) |
| MOBA move | MOBA | RMB click ground | Character paths to click point |
| MOBA attack | MOBA | LMB click enemy | Attack fires at enemy |
| MOBA attack-move | MOBA | A + click ground | Character moves, auto-attacks enemies along path |
| TwinStick aim | TwinStick | Move cursor, LMB fire | Character faces cursor, fires in cursor direction |

### Round-Trip Paradigm Switching

| From | To | Verify |
|------|----|--------|
| Shooter | MMO | Cursor frees, LMB selects instead of fires |
| MMO | ARPG Classic | Click-to-move activates, WASD disabled |
| ARPG Classic | MOBA | Click button changes from LMB to RMB |
| MOBA | Shooter | Cursor locks, WASD re-enables, click-to-move stops |
| Shooter | TwinStick | Cursor frees, facing changes to cursor direction |
| TwinStick | Shooter | Cursor locks, facing returns to camera forward |

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| All paradigms use CameraRaycast targeting | TargetingMode baked at startup, never changes | Expected current behavior (Gap 1). Use TargetingModeTester for debug override. |
| Lock behavior stays HardLock in all paradigms | LockBehaviorHelper.SetMode() never called | Expected current behavior (Gap 2). Manually call SetMode() for testing. |
| Both CameraRaycast and CursorAim fighting | Two MonoBehaviour targeting components in scene | Remove the one not needed for current testing paradigm |
| LMB click on enemy in ARPG also triggers move | ClickToMoveHandler and CursorClickTargetSystem both process LMB | Expected current behavior (Gap 3). Verify layer separation. |
| No hover highlight on enemies | CursorHoverSystem only runs when cursor is free | Check `InputSchemeManager.IsCursorFree` — Shooter has cursor locked |
| Weapon fires north (0,0,1) regardless of aim | PlayerFacingSystem not writing AimDirection | Check ParadigmSettings.IsValid and FacingMode. Check PlayerFacingSystem exists in PredictedFixedStep. |
| Target indicator not appearing on click-selected enemy | CursorClickTargetSystem not running | Only runs when `IsCursorFree == true`. Verify paradigm has `cursorFreeByDefault: true`. |
| AimDirection stale after paradigm switch | PlayerFacingSystem reads stale FacingMode | Verify ParadigmSettingsSyncSystem syncs FacingMode from MovementRouter on paradigm transition |
| Melee hits wrong direction in ARPG | MeleeActionSystem uses character facing, not cursor aim | Expected behavior (Gap 7). Melee follows character rotation, not TargetData. |

---

## 9. File Locations

### Input Profiles

| File | Description |
|------|-------------|
| `Assets/Data/Input/Profiles/Profile_Shooter.asset` | Shooter paradigm profile |
| `Assets/Data/Input/Profiles/Profile_ShooterHybrid.asset` | Shooter Hybrid paradigm profile |
| `Assets/Data/Input/Profiles/Profile_MMO.asset` | MMO paradigm profile |
| `Assets/Data/Input/Profiles/Profile_ARPG_Classic.asset` | ARPG Classic paradigm profile |
| `Assets/Data/Input/Profiles/Profile_ARPG_Hybrid.asset` | ARPG Hybrid paradigm profile |
| `Assets/Data/Input/Profiles/Profile_MOBA.asset` | MOBA paradigm profile |
| `Assets/Data/Input/Profiles/Profile_TwinStick.asset` | TwinStick paradigm profile |

### Targeting Systems

| File | Description |
|------|-------------|
| `Assets/Scripts/Targeting/TargetingMode.cs` | TargetingMode enum + TargetPriority enum |
| `Assets/Scripts/Targeting/TargetData.cs` | TargetData ECS component (ghost-replicated) |
| `Assets/Scripts/Targeting/TargetingConfig.cs` | TargetingConfig ScriptableObject |
| `Assets/Scripts/Targeting/Core/LockBehaviorType.cs` | LockBehaviorType enum + ActiveLockBehavior singleton |
| `Assets/Scripts/Targeting/Systems/LockBehaviorDispatcherSystem.cs` | LockBehaviorHelper + dispatcher |
| `Assets/Scripts/Targeting/Systems/CursorHoverSystem.cs` | Cursor hover detection (cursor-free only) |
| `Assets/Scripts/Targeting/Systems/CursorClickTargetSystem.cs` | Click-to-select targeting (cursor-free only) |
| `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | Writes TargetData.AimDirection per FacingMode |

### Targeting Implementations (MonoBehaviours)

| File | Description |
|------|-------------|
| `Assets/Scripts/Targeting/Implementations/CameraRaycastTargeting.cs` | Screen-center raycast (Shooter) |
| `Assets/Scripts/Targeting/Implementations/CursorAimTargeting.cs` | Cursor-to-world raycast (ARPG/MOBA) |
| `Assets/Scripts/Targeting/Implementations/LockOnTargeting.cs` | Manual lock-on with tab cycle |
| `Assets/Scripts/Targeting/Implementations/AutoTargetTargeting.cs` | Auto-lock nearest enemy |

### Paradigm System

| File | Description |
|------|-------------|
| `Assets/Scripts/Core/Input/Paradigm/InputParadigmProfile.cs` | Profile ScriptableObject definition |
| `Assets/Scripts/Core/Input/Paradigm/ParadigmStateMachine.cs` | Paradigm transition state machine |
| `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs` | ECS singleton (FacingMode, ActiveParadigm, etc.) |

### Weapon / Attack Systems

| File | Description |
|------|-------------|
| `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs` | Reads TargetData.AimDirection for fire direction |
| `Assets/Scripts/Weapons/Systems/MeleeActionSystem.cs` | Melee hitbox timing and combos (uses character facing) |

---

## Related Documentation

| Document | Description |
|----------|-------------|
| `Docs/EPIC18/EPIC18.18.md` | Full technical specification, codebase audit, and proposed solutions |
| `Docs/EPIC18/EPIC18.19.md` | Unified Targeting, Ability & Attack System (builds on 18.18 audit) |
| `Docs/EPIC18/SETUP_GUIDE_18.15.md` | Click-to-Move & WASD Gating setup |
