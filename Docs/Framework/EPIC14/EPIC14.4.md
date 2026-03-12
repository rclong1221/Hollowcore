# EPIC14.4 - New Weapons, Spells, and Animation Content

**Status:** Planned
**Dependencies:** EPIC14.3 (Data-driven config system must be in place first)
**Goal:** Add new weapon types and magic spells using the data-driven configuration system.

---

## Overview

With the data-driven `ItemAnimationConfig` system from EPIC14.3, adding new content becomes a **prefab authoring task** rather than code modification. This epic covers the initial content expansion.

---

## Prerequisites from EPIC14.3

> [!WARNING]
> Before Phase 2 (Off-Hand Items), the following **must** be addressed:

| Blocker | Location | Issue |
|---------|----------|-------|
| Off-Hand Suppression | `WeaponEquipVisualBridge.cs` (lines 918-923) | Currently suppresses `Slot1ItemID` for **any** non-Pistol/non-Bow main hand weapon. This breaks Sword+Shield combos. |
| Missing `IsTwoHanded` Flag | `ItemAnimationConfig` | No way to distinguish one-handed vs two-handed melee. |

**Required Fix:**
- Add `IsTwoHanded` boolean to `ItemAnimationConfig` and `ItemAnimationConfigAuthoring`.
- Refactor `WeaponEquipVisualBridge` to only suppress off-hand if `config.IsTwoHanded == true`.

> [!NOTE]
> **Bridge Work:** The `IsTwoHanded` field added in Phase 0 will be migrated to `WeaponCategoryDefinition.GripType` in EPIC 14.5. This is intentional short-term work to unblock testing.

---

## Relationship to Future EPICs

This EPIC creates content using the current architecture. Future EPICs will refactor the architecture, and this content will be migrated.

| What 14.4 Creates | What 14.5 Changes | Migration Path |
|-------------------|-------------------|----------------|
| `IsTwoHanded` boolean on `ItemAnimationConfig` | Becomes `GripType` enum on `WeaponCategoryDefinition` | Auto-migrate: `true` → `TwoHanded`, `false` → `OneHanded` |
| Hardcoded `ItemID` values (25-31, 61-67) | IDs auto-generated from `WeaponCategoryDefinition` | Auto-migrate: create SO per unique ID |
| `OffHandUseRequest` component | Merged into `InputProfileDefinition` system | Auto-migrate: extract bindings to profiles |
| Manual Animator state additions | `Animation Integration Assistant` in 14.6 | States remain, tool validates them |

> [!IMPORTANT]
> **All content created in 14.4 will be automatically migrated by 14.5's Migration Tool.** No manual rework required.

### Intentional Short-Term Decisions

| Decision | Why Temporary | Future |
|----------|---------------|--------|
| `ItemID` as int | Quick to implement, matches current Animator params | 14.5 uses SO references |
| `OffHandUseInputSystem` | Minimal input routing for shield block | 14.5's `InputProfileDefinition` handles all |
| Manual Animator states | Needed for testing | 14.6 Animation Assistant validates |

---

## Two-Handed vs One-Handed Weapons

| Weapon Type | `IsTwoHanded` | Off-Hand Behavior |
|-------------|---------------|-------------------|
| Greatsword | `true` | Off-hand visuals hidden |
| Assault Rifle | `true` | Off-hand visuals hidden |
| Sword | `false` | Off-hand allowed (Shield, Torch) |
| Knife | `false` | Off-hand allowed |
| Pistol | `false` | Off-hand can be another Pistol (Dual Wield) |
| Bow | `false` | Off-hand hidden (special case: arrows) |

---

## Off-Hand Input Bindings

> [!IMPORTANT]
> Off-hand actions require dedicated input routing.

| Input | Action | Notes |
|-------|--------|-------|
| Right Mouse Button | Off-Hand Use (Block/Attack) | Currently not routed to ECS |
| `Alt` + `1-9` | Equip to Off-Hand Slot | Implemented in `DIGEquipmentProvider` |

**Phase 2 Tasks (Simplified Approach):**

> [!NOTE]
> We use a minimal implementation here. EPIC 14.5 will refactor this into the `InputProfileDefinition` system.

- [ ] Create `OffHandUseRequest` ECS component (simple: just `Pending` bool)
- [ ] Wire RMB directly in `DIGEquipmentProvider` → set `OffHandUseRequest.Pending = true`
- [ ] Create `OffHandUseSystem` to process requests (minimal, not full input system)

This keeps the implementation small and migration-friendly.

---

## Weapon Categories to Expand

### Magic Spells (ItemIDs 61-70+)

| Spell | ItemID | Substate | Behavior | Notes |
|:------|:-------|:---------|:---------|:------|
| Fireball Light | 61 | 0 | Instant cast, no lock | Default magic state |
| Fireball Heavy | 61 | 1 | Charged cast, short lock | Hold to charge |
| Particle Stream | 62 | 2 | Channeled, full lock | Hold to sustain beam |
| Heal | 63 | 3 | Self-cast, short lock | Area heal effect |
| Ricochet | 64 | 4 | Instant cast | Bouncing projectile |
| Shield Bubble | 65 | 5 | Channeled ability | Uses AbilityIndex instead? |
| Ice Spear | 66 | 0 | NEW: Charged projectile | Long charge, high damage |
| Lightning Chain | 67 | 0 | NEW: Instant, multi-target | Chain lightning effect |

### Off-Hand Items

| Item | ItemID | Slot | Behavior |
|:-----|:-------|:-----|:---------|
| Shield | 26 | Slot1 | Block on right-click |
| Torch | 27 | Slot1 | NEW: Light source, can attack |
| Off-Hand Dagger | 28 | Slot1 | NEW: Fast off-hand attacks |

### New Melee Weapons

| Weapon | ItemID | Combo | Two-Handed | Notes |
|:-------|:-------|:------|:-----------|:------|
| Sword | 25 | 2-hit | No | Already hooked up |
| Greatsword | 29 | 3-hit | **Yes** | Slower, more damage |
| Spear | 30 | 2-hit | No | Thrust-focused |
| Axe | 31 | 2-hit | No | Heavy swings |

---

## Tasks

### Phase 0: Prerequisites (from EPIC14.3 findings)
- [ ] Add `IsTwoHanded` field to `ItemAnimationConfig` and `ItemAnimationConfigAuthoring`
- [ ] Refactor `WeaponEquipVisualBridge` (lines 918-923) to use `config.IsTwoHanded` instead of "not Pistol, not Bow"
- [ ] Update `ItemConfigAutomator` to set `IsTwoHanded = true` for Rifles and Greatswords

### Phase 1: Magic Spell Expansion
- [ ] Create prefab: `IceSpear.prefab` with `ItemID=66`, `Category=Magic`, `LockMovement=true`, `UseDuration=1.5`
- [ ] Create prefab: `LightningChain.prefab` with `ItemID=67`, `Category=Magic`, `LockMovement=false`
- [ ] Ensure SubstateIndex cycling works (scroll wheel or number keys)
- [ ] Add visual effects for each new spell

### Phase 2: Off-Hand Items
- [ ] Create `OffHandUseRequest` component and `OffHandUseInputSystem`
- [ ] Complete Shield integration from EPIC14.1 using data-driven config
- [ ] Create prefab: `Torch.prefab` with `Category=Shield`, off-hand weapon
- [ ] Create prefab: `OffHandDagger.prefab` with dual-wield support

### Phase 3: New Melee Weapons
- [ ] Create prefab: `Greatsword.prefab` with `ItemID=29`, `ComboCount=3`, `IsTwoHanded=true`
- [ ] Ensure Animator has corresponding sub-state machine (or uses Sword states)
- [ ] Create prefab: `Spear.prefab` with `ItemID=30`, `ComboCount=2`
- [ ] Create prefab: `Axe.prefab` with `ItemID=31`, `ComboCount=2`

### Phase 4: Animator State Machine Updates
- [ ] Add new sub-state machines to `ClimbingDemo.controller` if needed
- [ ] Create editor tool to add states (like `SwordStateAdder.cs`)
- [ ] Verify transitions work with new ItemIDs

---

## Designer Workflow

**Adding a completely new weapon:**

1. **Art:** Create weapon model, import animations
2. **Animator:** Add sub-state machine to controller (or reuse existing)
3. **Prefab:**
   - Create weapon entity prefab
   - Add `ItemAnimationConfigAuthoring`
   - Set `AnimatorItemID` to unused ID
   - Configure `Category`, `ComboCount`, `IsTwoHanded`, `LockMovement`, etc.
4. **Inventory:** Add to item database / drop tables
5. **Test:** Equip and verify animations play

**No code changes required.**

---

## Animation Requirements

For each new weapon, the Animator Controller needs:
- Equip state (from Idle)
- Unequip state (back to Idle)
- Use/Attack states (number depends on ComboCount)
- Aim state (for ranged/magic)

Transitions must be conditioned on:
- `Slot0ItemID == [AnimatorItemID]`
- `Slot0ItemStateIndex` for action selection
- `Slot0ItemSubstateIndex` for variations

---

## Verification Checklist

### Phase 0: Prerequisites
- [ ] Sword + Shield equip together (off-hand not suppressed)
- [ ] Greatsword hides off-hand
- [ ] Assault Rifle still hides off-hand

### Phase 1: Magic
- [ ] Ice Spear charges and fires with correct animation
- [ ] Lightning Chain casts instantly with no movement lock
- [ ] Debug log shows correct `ItemAnimationConfig` values

### Phase 2: Off-Hand
- [ ] Right Mouse Button triggers Shield block animation
- [ ] Shield visually renders while Sword is in main hand
- [ ] Torch provides light and can attack

### Phase 3: Melee
- [ ] Greatsword plays 3-hit combo
- [ ] Spear plays thrust animations
- [ ] All weapons have correct equip/unequip transitions

---

## Success Criteria

- [ ] At least 2 new magic spells added via data-driven config only
- [ ] Shield fully functional in off-hand with Sword
- [ ] `IsTwoHanded` flag correctly differentiates weapon types
- [ ] At least 1 new melee weapon added
- [ ] Designers can add content without code changes
