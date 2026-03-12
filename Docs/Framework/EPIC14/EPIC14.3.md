# EPIC14.3 - Data-Driven Weapon Animation Configuration

**Status:** Planned
**Dependencies:** EPIC14.1 (Animator hookups working), EPIC14.2 (Equipment system)
**Goal:** Replace hardcoded weapon constants with a component-based, designer-friendly configuration system.

---

## Problem Statement

Currently, weapon behavior is hardcoded in `WeaponEquipVisualBridge.cs`:
- `ITEMID_DUAL_PISTOL = 2`, `ITEMID_MAGIC_MIN = 61`, etc.
- `MAGIC_CAST_DURATION = 0.5f` applies to ALL magic spells
- Adding a new weapon requires modifying C# code
- Designers cannot tweak weapon behavior without programmer intervention

This approach does not scale as weapon/spell variety increases.

---

## Solution: `ItemAnimationConfig` as Single Source of Truth

> [!IMPORTANT]
> **The weapon prefab is the ONLY place animation/behavior data should be configured.**

Each weapon entity carries its own `ItemAnimationConfig` component. When adding a weapon to inventory, you just drag the prefab—all animation data is read from the component automatically.

**Key Benefit:** No more managing separate arrays of IDs, slots, or animation parameters. Configure once on the prefab, use everywhere.

---


## Component Design

### `ItemAnimationConfig` (ECS Component)
| Field | Type | Description |
|:------|:-----|:------------|
| `AnimatorItemID` | int | Value to set `Slot0ItemID` (e.g., 61 for Fireball) |
| `MovementSetID` | int | 0=Combat/Gun, 1=Melee, 2=Bow, 3=Magic |
| `WeaponType` | enum | Reuses `AnimationWeaponType` from `IEquipmentProvider` |
| `ComboCount` | int | Number of attack combo steps (for melee) |
| `UseDuration` | float | How long "Use" animation plays before returning to idle |
| `LockMovementDuringUse` | bool | If true, character cannot move while using weapon |
| `CancelUseOnMove` | bool | If true, movement input cancels current action |
| `IsChanneled` | bool | True for sustained actions (Particle Stream, Bow Draw) |
| `RequireAimToFire` | bool | True for weapons that need right-click aim before fire |

### `ItemAnimationConfigAuthoring` (MonoBehaviour for Prefabs)
- Exposes all above fields in Inspector
- Bakes to `ItemAnimationConfig` component during conversion
- Provides Inspector tooltips and validation

---

## Tasks

## Implementation Checklist

### Phase 1: Component Creation
- [x] Create `ItemAnimationConfig` struct in `ItemComponents.cs`
- [x] Create `ItemAnimationConfigAuthoring.cs` for prefab Inspector

### Phase 2: Bridge Refactor
- [x] Remove hardcoded `ITEMID_*` constants from `WeaponEquipVisualBridge.cs`
- [x] Add method to read `ItemAnimationConfig` from active weapon entity
- [x] Replace `switch` statements with config field reads

### Phase 3: Provider Update
- [x] Update `DIGEquipmentProvider` to read `ItemAnimationConfig`
- [x] Remove hardcoded switch statements in Provider (Legacy logic kept as fallback)

### Phase 4: Prefab Updates
- [x] Create Automation Tool (`ItemConfigAutomator`) to bulk-update prefabs
- [x] **ACTION REQUIRED:** Run `Tools > DIG > Update Item Animation Configs` in Unity Editor
- [x] Manual verification of key prefabs (Bow, Magic, Shield)

### Phase 5: Verification & Documentation
- [x] Create `Docs/EPIC14/SETUP_GUIDE_14.3.md` with verification checklist
- [ ] Verify Bow Animation (Draw/Release) matches legacy behavior
- [ ] Verify Magic Cast (Cancellation/Movement Lock) functionality
- [ ] Verify Melee Combo execution using Config timings
- [ ] Verify Shield Blocking functionality

---

## Designer Workflow (Post-Implementation)

1. Create new weapon prefab (e.g., `IceSpear.prefab`)
2. Add `ItemAnimationConfigAuthoring` component in Inspector
3. Set `AnimatorItemID = 66`, `Category = Magic`, `UseDuration = 1.2`, `LockMovement = true`
4. Done! No code changes required.

---

## Migration Notes

- Existing hardcoded behavior becomes the DEFAULT values in config
- Any weapon WITHOUT `ItemAnimationConfig` falls back to Gun behavior
- Backward compatible: old prefabs work until migrated
