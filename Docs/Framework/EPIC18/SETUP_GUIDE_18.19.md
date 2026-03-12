# EPIC 18.19: Unified Targeting, Ability & Attack System — Setup Guide

**Status:** IN PROGRESS (Phases 1-8 implemented)
**Last Updated:** March 6, 2026
**Requires:** EPIC 15.20 (Input Paradigm Framework), EPIC 18.18 (Targeting & Attack Coverage), EPIC 16.8 (Player Resource Framework)

---

## Overview

EPIC 18.19 builds a unified, per-paradigm combat pipeline that:
- Bridges paradigm switching to targeting mode and lock behavior
- Provides a full player ability system (input, validation, cast phases, effects)
- Ensures weapons and abilities fire correctly in all paradigms (Shooter, MMO, ARPG, MOBA, TwinStick)

This setup guide covers:
- How to configure input profiles for targeting
- How to create and assign abilities
- How to set up the player prefab
- How to verify targeting works per paradigm
- Debugging with the `[TARGETING]` diagnostic system

---

## Prerequisites

- EPIC 15.20: ParadigmStateMachine, subsystems, and profiles in scene
- EPIC 18.18: Targeting audit complete (targeting system awareness)
- EPIC 16.8: Player Resource Framework set up (for ability costs)
- Input profiles configured in `Assets/Data/Input/Profiles/`
- `TargetDataAuthoring` on the player prefab
- `TargetingConfigurable` and `LockBehaviorConfigurable` MonoBehaviours in the scene (on the same GameObject as other `IParadigmConfigurable` subsystems)

---

## 1. Input Profile Setup — Targeting Fields

Each `InputParadigmProfile` asset now has a **Targeting** section that controls what targeting mode and lock behavior activate when switching to that paradigm.

### Location

`Assets/Data/Input/Profiles/` — select any `.asset` file in the Project window.

### Inspector Fields

| Field | Type | Description |
|-------|------|-------------|
| **Default Targeting Mode** | `TargetingMode` dropdown | Which targeting mode activates for this paradigm |
| **Default Lock Behavior** | `LockBehaviorType` dropdown | Which lock-on behavior activates for this paradigm |

### Recommended Defaults Per Profile

| Profile | Default Targeting Mode | Default Lock Behavior |
|---------|------------------------|-----------------------|
| Profile_Shooter | `CameraRaycast` | `HardLock` |
| Profile_ShooterHybrid | `CameraRaycast` | `HardLock` |
| Profile_MMO | `ClickSelect` | `SoftLock` |
| Profile_ARPG_Classic | `CursorAim` | `IsometricLock` |
| Profile_ARPG_Hybrid | `CursorAim` | `IsometricLock` |
| Profile_MOBA | `CursorAim` | `IsometricLock` |
| Profile_TwinStick | `CursorAim` | `TwinStick` |

> **Note:** When you create a new profile and set its `paradigm` enum, the targeting defaults are auto-populated via `OnValidate()` if they are still at the default (CameraRaycast / HardLock). If you need a non-standard combination, set the fields manually after setting the paradigm.

### TargetingMode Values

| Mode | Value | Use Case |
|------|-------|----------|
| `CameraRaycast` | 0 | Crosshair aim at screen center (Shooter) |
| `CursorAim` | 1 | Fire toward mouse cursor world position (ARPG, MOBA, TwinStick) |
| `AutoTarget` | 2 | Auto-lock nearest valid enemy |
| `LockOn` | 3 | Manual lock-on with tab cycling (Souls-like) |
| `ClickSelect` | 4 | Click enemy to select, abilities target selection (MMO) |

### LockBehaviorType Values

| Type | Value | Use Case |
|------|-------|----------|
| `HardLock` | 1 | Camera locks onto target (Shooter) |
| `SoftLock` | 2 | Free camera, character rotates toward target (MMO) |
| `IsometricLock` | 3 | Fixed isometric camera, character faces target (ARPG, MOBA) |
| `TwinStick` | 5 | Independent aim, sticky aim assist (TwinStick) |

---

## 2. Scene Setup — Paradigm Subsystems

### Required MonoBehaviours

The following `IParadigmConfigurable` MonoBehaviours must be present in the scene on the same (or sibling) GameObject as the other paradigm subsystems (CursorController, CameraOrbitController, MovementRouter, etc.):

| Component | File | Config Order | Purpose |
|-----------|------|-------------|---------|
| **TargetingConfigurable** | `Assets/Scripts/Core/Input/Paradigm/Subsystems/TargetingConfigurable.cs` | 250 | Writes `ParadigmSettings.ActiveTargetingMode` on paradigm switch |
| **LockBehaviorConfigurable** | `Assets/Scripts/Core/Input/Paradigm/Subsystems/LockBehaviorConfigurable.cs` | 255 | Calls `LockBehaviorHelper.SetMode()` on paradigm switch |

### How to Add

1. In the Hierarchy, find the GameObject that holds your paradigm subsystems (typically named `ParadigmSubsystems` or similar — look for `CursorController`, `MovementRouter`, etc.)
2. **Add Component** > search for `TargetingConfigurable` > add it
3. **Add Component** > search for `LockBehaviorConfigurable` > add it
4. No Inspector fields to configure — they read from the active `InputParadigmProfile`

These components register themselves automatically with `ParadigmStateMachine` via the `IParadigmConfigurable` interface.

---

## 3. Player Prefab Setup — Ability Loadout

### AbilityLoadoutAuthoring

Add this authoring component to the player prefab to bake ability slots and state.

1. Open the player prefab (e.g., `Warrok_Server`)
2. **Add Component** > search for `AbilityLoadoutAuthoring`
3. In the Inspector, assign an `AbilityLoadoutSO` asset to the **Loadout** field

This bakes the following onto the player entity at build/subscene import:
- `PlayerAbilityState` component (cast phase tracking, GCD)
- `PlayerAbilitySlot` buffer (6 slots, pre-filled from loadout defaults)

### Existing Setup

The current player prefab should already have `AbilityLoadoutAuthoring` pointing to:

`Assets/Resources/AbilityLoadout.asset`

> **Important:** The `AbilityLoadoutSO` in `Resources/` is loaded by `AbilityDatabaseBootstrapSystem` at runtime to create the `AbilityDatabaseRef` blob singleton. It must be named `AbilityLoadout` and located in a `Resources/` folder.

---

## 4. Creating Abilities

### Create an Ability Definition

1. In the Project window, right-click in `Assets/Data/Abilities/`
2. **Create** > **DIG** > **Combat** > **Ability Definition**
3. Name the asset (e.g., `Fireball`, `Shield_Bash`, `Heal`)
4. Configure fields in the Inspector (see below)

### Ability Definition Inspector Fields

#### Identity

| Field | Description |
|-------|-------------|
| **Ability Id** | Unique integer ID. Must be unique across all ability assets. |
| **Display Name** | Name shown in the ability bar UI |
| **Description** | Tooltip text |
| **Icon** | Sprite for the hotbar slot |
| **Category** | `Attack`, `Heal`, `Buff`, `Debuff`, `Utility`, `Movement` |
| **Paradigm Flags** | Bitmask — which paradigms can use this ability (default: All) |

#### Targeting

| Field | Description |
|-------|-------------|
| **Target Type** | How the ability acquires targets (see table below) |
| **Range** | Maximum cast range in world units |
| **Radius** | AoE/cone/cleave radius |
| **Angle** | Cone/cleave half-angle in degrees |
| **Max Targets** | Maximum targets hit per activation |
| **Requires Line Of Sight** | Target must be visible |
| **Requires Target** | Must have a valid `TargetEntity` to cast (MMO-style) |

**Target Type Reference:**

| Type | Description | Uses |
|------|-------------|------|
| `Self` | No targeting needed | Buffs, self-heals |
| `SingleTarget` | Requires `TargetEntity` | Direct damage/heal on selected enemy |
| `GroundTarget` | Uses `TargetPoint` (cursor position) | AoE at cursor (like Diablo meteor) |
| `Cone` | `AimDirection` + angle | Frontal cone attack |
| `Line` | `AimDirection` + range | Skillshot line |
| `AoE` | `TargetPoint` + radius | Area damage centered on cursor/self |
| `Cleave` | `AimDirection` + arc | Melee sweep |
| `Projectile` | `AimDirection` + range | Fires projectile toward aim direction. If soft-target exists, locks on. |

#### Timing

| Field | Description |
|-------|-------------|
| **Telegraph Duration** | Visual warning phase (0 = skip) |
| **Cast Time** | Wind-up time (0 = instant cast) |
| **Active Duration** | Damage delivery window |
| **Recovery Time** | Post-cast lockout |
| **Cooldown** | Per-ability cooldown |
| **Global Cooldown** | GCD contribution (0 = off-GCD) |
| **Tick Interval** | For channeled abilities (0 = single hit) |

#### Charges

| Field | Description |
|-------|-------------|
| **Max Charges** | 0 = standard cooldown. 2+ = charge-based (like Tracer blink) |
| **Charge Regen Time** | Seconds per charge regeneration |

#### Resource Cost

| Field | Description |
|-------|-------------|
| **Cost Resource** | `None`, `Mana`, `Stamina`, `Energy`, etc. |
| **Cost Timing** | `OnCast`, `PerTick`, `OnComplete` |
| **Cost Amount** | Amount consumed |

#### Damage / Healing

| Field | Description |
|-------|-------------|
| **Damage Base** | Base damage or healing amount |
| **Damage Variance** | Random +/- range |
| **Damage Type** | `Physical`, `Fire`, `Ice`, `Lightning`, `Poison`, etc. |
| **Hit Count** | Hits per activation |
| **Can Crit** | Whether this ability can critically hit |

#### Cast Behavior

| Field | Description |
|-------|-------------|
| **Cast Movement** | `Free` (can move), `Slowed`, `Rooted` (cannot move during cast) |
| **Interruptible** | Can be interrupted by damage/CC during casting |

#### On-Hit Modifiers

Two modifier slots for on-hit status effects (slow, burn, stun, etc.):

| Field | Description |
|-------|-------------|
| **Modifier Type** | Status effect type |
| **Chance** | Proc chance (0.0 - 1.0) |
| **Duration** | Effect duration in seconds |
| **Intensity** | Effect strength |

### Existing Abilities

| Asset | Location | Description |
|-------|----------|-------------|
| `Basic_Attack` | `Assets/Data/Abilities/Basic_Attack.asset` | Melee auto-attack |
| `Fireball` | `Assets/Data/Abilities/Fireball.asset` | Ranged projectile |
| `Heal` | `Assets/Data/Abilities/Heal.asset` | Self-heal |

---

## 5. Configuring the Ability Loadout

The `AbilityLoadoutSO` defines which abilities are available and which slots they default to.

### Location

`Assets/Resources/AbilityLoadout.asset`

### Inspector Fields

| Field | Description |
|-------|-------------|
| **Abilities** | Array of `AbilityDefinitionSO` references. Order = runtime blob index. |
| **Default Slot Ability Ids** | Array of 6 integers — the designer-facing ability ID for each slot (0-5). Use `-1` for empty slots. |

### How Slot Assignment Works

1. Each `AbilityDefinitionSO` has an `abilityId` field (designer-assigned integer)
2. The `Default Slot Ability Ids` array maps slot indices (0-5) to those designer IDs
3. At bake time, designer IDs are resolved to blob array indices (runtime IDs)
4. If an ability ID doesn't match any entry in the `Abilities` array, the slot bakes as empty

### Example Configuration

```
Abilities array:
  [0] Basic_Attack  (abilityId = 1)
  [1] Fireball      (abilityId = 2)
  [2] Heal          (abilityId = 3)

Default Slot Ability Ids: [1, 2, 3, -1, -1, -1]
  Slot 0 → Basic_Attack
  Slot 1 → Fireball
  Slot 2 → Heal
  Slots 3-5 → Empty
```

> **Important:** After changing the loadout, you must **reimport the subscene** containing the player prefab for changes to take effect (baking regenerates the ECS components).

---

## 6. Editor Tooling — Ability Management

### Combat Workstation — Ability Module

Open via **Window** > **DIG** > **Combat Workstation**, then select the **Ability Management** tab.

Location: `Assets/Editor/CombatWorkstation/Modules/AbilityManagementModule.cs`

Features:
- Browse all `AbilityDefinitionSO` assets in the project
- Inline property editing
- Validation warnings (duplicate IDs, missing icons, zero damage on Attack abilities)

---

## 7. Per-Paradigm Weapon Targeting — How It Works

Understanding which systems write `TargetData.AimDirection` (the direction weapons fire) is important for debugging.

### Data Flow Per Paradigm

| Paradigm | TargetData.Mode | AimDirection Source | Server Replication |
|----------|-----------------|---------------------|-------------------|
| **Shooter** | CameraRaycast | `PlayerFacingSystem` reads `CameraYaw`/`CameraPitch` from `PlayerInput` | `CameraYaw`/`CameraPitch` (IInputComponentData) |
| **MMO** | ClickSelect | `TargetingModeDispatcherSystem` computes player→target direction | Inherits from Shooter replication path |
| **ARPG/MOBA/TwinStick** | CursorAim | `CursorAimTargetingSystem` raycasts cursor→world (client-only) | `CursorAimDirection` field in `PlayerInput` (IInputComponentData) |

### Why Server Replication Matters

`WeaponFireSystem` hitscan runs **server-only** for authoritative hit detection. The server has no camera, no cursor, and no access to client-only targeting systems. Aim direction reaches the server via `PlayerInput` (auto-replicated each tick via NetCode's `IInputComponentData`):

- **Shooter**: `CameraYaw` + `CameraPitch` fields (existing)
- **CursorAim**: `CursorAimDirection` + `CursorAimValid` fields (added in Phase 8)

`PlayerFacingSystem` writes `TargetData.AimDirection` from the replicated input on the server so `WeaponFireSystem` reads the correct direction.

---

## 8. Debugging — [TARGETING] Diagnostic System

A diagnostic system logs the full targeting pipeline state with a shared `[TARGETING]` filter tag.

### How to Use

1. Enter Play Mode in any paradigm
2. In the Unity Console, type `[TARGETING]` in the search/filter bar
3. Logs appear once per second (rate-limited to avoid spam)

### What the Logs Show

| Log Line | System | Information |
|----------|--------|-------------|
| `[TARGETING] ModeDispatcher \| Mode changed: X -> Y` | TargetingModeDispatcherSystem | Paradigm targeting mode transition |
| `[TARGETING] ModeDispatcher \| Enforced Mode=X on entity (was Y)` | TargetingModeDispatcherSystem | Late-spawned player entity corrected to expected mode |
| `[TARGETING] CursorAimSystem \| screen=(X,Y) hit=... aimDir=... dist=...` | CursorAimTargetingSystem | Cursor raycast result (every ~1s) |
| `[TARGETING] CursorAimSystem \| SoftTarget found: Entity(N:V)` | CursorAimTargetingSystem | Nearest targetable entity near cursor |
| `[TARGETING] PlayerFacingSystem \| SKIP AimDir overwrite (CursorAim mode)` | PlayerFacingSystem | Confirms AimDirection is NOT being stomped |
| `[TARGETING] PlayerFacingSystem \| WRITE AimDir=...` | PlayerFacingSystem | AimDirection written from character rotation |
| `[TARGETING] PlayerFacingSystem \| CameraFwd AimDir=...` | PlayerFacingSystem | AimDirection written from CameraYaw/Pitch (Shooter) |
| `[TARGETING] PlayerFacingSystem \| CursorAim replicated AimDir=...` | PlayerFacingSystem | Server writing AimDirection from replicated input |
| `[TARGETING] === Pipeline Snapshot (t=X) ===` | TargetingDebugSystem | Full state dump (once per second) |
| `[TARGETING] ParadigmSettings \| TargetingMode=... FacingMode=...` | TargetingDebugSystem | Current paradigm singleton values |
| `[TARGETING] TargetData \| Mode=... AimDir=... TargetEntity=...` | TargetingDebugSystem | Player entity's TargetData fields |
| `[TARGETING] CursorScreenPos \| (X, Y)` | TargetingDebugSystem | Current cursor screen position |

### Diagnostic File

`Assets/Scripts/Targeting/Systems/TargetingDebugSystem.cs`

This file is a temporary diagnostic system. **Delete it when debugging is complete** (as noted in the file header).

### Quick Diagnosis Checklist

| Symptom | What to Check in Logs |
|---------|----------------------|
| Weapon fires north in all modes | Look for `TargetData \| Mode=CameraRaycast` when expecting `CursorAim` — mode not being set |
| Weapon fires north in CursorAim | Look for `PlayerFacingSystem \| WRITE AimDir` (not SKIP) — AimDirection being overwritten |
| CursorAimSystem never logs | `TargetData.Mode` is not `CursorAim` — check ModeDispatcher logs |
| AimDirection correct on client but damage goes wrong direction | Server replication issue — check `CursorAim replicated AimDir` log on server |
| Mode stuck at CameraRaycast | `Enforced Mode=CursorAim` log should appear — if not, ModeDispatcher may not be running |

---

## 9. Verification Checklist

### Paradigm Targeting

| Test | Steps | Expected |
|------|-------|----------|
| ARPG cursor aim | Switch to ARPG, move cursor, fire weapon | Hitscan/projectile goes toward cursor position |
| ARPG soft-target | Hover cursor near enemy (within 3m) | `TargetData.TargetEntity` populated, SingleTarget abilities work |
| MMO select + cone | Switch to MMO, click enemy, use cone ability | Cone fires toward selected enemy (not default north) |
| Shooter aim | Switch to Shooter, aim at enemy, fire | Hitscan fires at crosshair (CameraYaw/Pitch path) |
| TwinStick rapid fire | Switch to TwinStick, move cursor, hold fire | Projectiles track cursor direction in real-time |
| Paradigm switch round-trip | Shooter → ARPG → MMO → Shooter | Targeting mode changes correctly each time, no stale state |

### Ability System

| Test | Steps | Expected |
|------|-------|----------|
| Ability cast | Press ability key (1-6) | Cast bar appears, phases progress (Telegraph → Cast → Active → Recovery) |
| Cooldown | Fire ability, try again immediately | Rejected — cooldown icon visible on slot |
| GCD | Fire ability, try different ability during GCD | Queued, fires when GCD expires |
| Resource cost | Fire ability with insufficient resource | Rejected — UI feedback |
| Projectile skillshot | In CursorAim mode, fire Projectile ability with no target near cursor | Projectile fires toward cursor position (skillshot fallback) |
| Self ability | Fire Self-type ability | Applies without targeting |

### Debug Logging

| Test | Steps | Expected |
|------|-------|----------|
| Filter works | Enter Play, filter Console by `[TARGETING]` | Only targeting logs visible |
| Mode enforcement | Start in TwinStick, wait for player spawn | `Enforced Mode=CursorAim on entity (was CameraRaycast)` appears |
| CursorAim active | In ARPG/TwinStick, move cursor | `CursorAimSystem` logs with changing aimDir |
| Skip guard | In CursorAim mode, check PlayerFacingSystem | `SKIP AimDir overwrite (CursorAim mode)` appears (not `WRITE`) |

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Weapon always fires north in CursorAim | `TargetData.Mode` stuck at `CameraRaycast` | Verify `TargetingConfigurable` is in the scene. Check `[TARGETING]` logs for `Enforced Mode` message. |
| Weapon fires north on remote clients | Server doesn't have CursorAimDirection | Verify `PlayerInput.CursorAimValid` is 1 in CursorAim mode. Check `PlayerInputSystem` copies aim direction. |
| Mode changes but AimDirection stays stale | PlayerFacingSystem overwriting cursor aim | Check `[TARGETING]` logs — should see `SKIP` not `WRITE` when in CursorAim mode. |
| CursorAimSystem never runs | `TargetData.Mode != CursorAim` on the player entity | Check `ModeDispatcher` logs. Ensure `TargetingConfigurable` sets the mode. Ensure profile has `CursorAim`. |
| No `[TARGETING]` logs appear | TargetingDebugSystem not in world | It requires `ParadigmSettings` singleton — verify paradigm system is initialized. |
| Abilities fail silently | `TargetEntity == Entity.Null` for SingleTarget | In CursorAim mode: hover cursor within 3m of enemy for soft-target. In MMO: click enemy first. |
| Paradigm switch doesn't change targeting | `TargetingConfigurable` not registered | Ensure it's on a GameObject in the scene and inherits from `MonoBehaviour` + `IParadigmConfigurable`. |
| Abilities unavailable in certain paradigms | `AbilityParadigmFlags` bitmask too restrictive | Check `Paradigm Flags` on the `AbilityDefinitionSO` — default is `All`. |
| Player archetype too large after adding abilities | 16KB entity limit exceeded | Reduce `PlayerAbilitySlot` buffer capacity or move to a child entity. See EPIC 18.19 spec, 16KB Budget Analysis section. |
| Ability loadout changes not reflected at runtime | Subscene not reimported after editing loadout | Right-click the subscene > **Reimport**. Baking only runs on import. |

---

## 11. File Locations

### Paradigm Targeting (Phase 1)

| File | Description |
|------|-------------|
| `Assets/Scripts/Core/Input/Paradigm/InputParadigmProfile.cs` | Profile SO — `defaultTargetingMode`, `defaultLockBehavior` fields |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/TargetingConfigurable.cs` | Writes targeting mode on paradigm switch |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/LockBehaviorConfigurable.cs` | Writes lock behavior on paradigm switch |
| `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs` | ECS singleton — `ActiveTargetingMode` field |

### Targeting Systems (Phases 2 + 8)

| File | Description |
|------|-------------|
| `Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs` | Per-frame mode enforcement + ClickSelect AimDirection |
| `Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs` | Cursor-to-world raycast + soft-target (CursorAim mode) |
| `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | AimDirection per facing mode + CursorAim skip guard + server replication |
| `Assets/Scripts/Targeting/Systems/TargetingDebugSystem.cs` | Diagnostic logger (`[TARGETING]` tag) — **DELETE when done debugging** |
| `Assets/Scripts/Targeting/TargetData.cs` | TargetData ECS component |
| `Assets/Scripts/Targeting/TargetingMode.cs` | TargetingMode enum |

### Server Replication (Phase 8)

| File | Description |
|------|-------------|
| `Assets/Scripts/Shared/Player/PlayerInput_Global.cs` | `CursorAimDirection` + `CursorAimValid` fields |
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | Copies cursor aim into PlayerInput for replication |

### Ability System (Phases 3-7)

| File | Description |
|------|-------------|
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityDefinitionSO.cs` | Per-ability ScriptableObject |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutSO.cs` | Loadout SO (in Resources/) |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutAuthoring.cs` | Baker — add to player prefab |
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilitySlot.cs` | Per-slot buffer (cooldown, charges) |
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilityState.cs` | Active ability state (phase, GCD) |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDef.cs` | Blittable ability definition (blob) |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDatabase.cs` | Blob singleton |
| `Assets/Scripts/Combat/Abilities/Systems/AbilityDatabaseBootstrapSystem.cs` | Loads blob from Resources |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilitySystemGroup.cs` | System group |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityInputSystem.cs` | Input → QueuedSlotIndex |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCooldownSystem.cs` | Cooldown + GCD tick |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityValidationSystem.cs` | Cost/cooldown/range checks |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityExecutionSystem.cs` | Phase state machine |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCostSystem.cs` | Resource deduction |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs` | PendingCombatHit creation |

### Ability Assets

| File | Description |
|------|-------------|
| `Assets/Resources/AbilityLoadout.asset` | Default loadout (must be in Resources/) |
| `Assets/Data/Abilities/` | Individual ability definitions |

### Editor Tooling

| File | Description |
|------|-------------|
| `Assets/Editor/CombatWorkstation/Modules/AbilityManagementModule.cs` | Ability browser/editor in Combat Workstation |

---

## Related Documentation

| Document | Description |
|----------|-------------|
| `Docs/EPIC18/EPIC18.19.md` | Full technical specification (all 8 phases) |
| `Docs/EPIC18/EPIC18.18.md` | Targeting & Attack Coverage audit |
| `Docs/EPIC18/SETUP_GUIDE_18.18.md` | Targeting system setup (pre-18.19 reference) |
| `Docs/EPIC18/SETUP_GUIDE_18.15.md` | Click-to-Move & WASD Gating setup |
