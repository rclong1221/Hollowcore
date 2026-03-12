# Setup Guide: EPIC 14.1 (Animator Hookups)

## Overview

This epic implements hardcoded animator hookups for Magic, Dual Pistol, Sword, and Shield weapons. It aligns C# logic in `WeaponEquipVisualBridge` with the precise `ClimbingDemo.controller` Animator structure.

> **Note:** This uses hardcoded constants for rapid prototyping. EPIC14.3 will refactor to data-driven configuration.

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `WeaponEquipVisualBridge` | `Assets/Scripts/Items/Bridges/` | Drives animator parameters, shows/hides weapon models |
| `ClimbingDemo.controller` | `Assets/Art/AddOns/Climbing/` | Opsive animator with weapon sub-state machines |

---

## Animator Parameter Reference

### Core Weapon Parameters

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `Slot0ItemID` | Int | See table below | Main hand weapon ID |
| `Slot1ItemID` | Int | 2, 26 | Off-hand weapon ID (Dual Pistol, Shield) |
| `Slot0ItemStateIndex` | Int | 0-7 | Action state (see below) |
| `Slot0ItemSubstateIndex` | Int | 0-5 | Variations (spell type, combo step) |
| `Slot0ItemStateIndexChange` | Trigger | - | **Must fire** alongside StateIndex updates |
| `Aiming` | Bool | true/false | Hip vs ADS stance |
| `MovementSetID` | Int | 0-2 | Animation style (0=Guns, 1=Melee, 2=Bow) |

### Weapon Item IDs

| Item | ID | MovementSetID | Notes |
|------|:--:|:-------------:|-------|
| Assault Rifle | 1 | 0 | Default gun |
| Dual Pistol | 2 | 0 | Requires both Slot0 AND Slot1 = 2 |
| Shotgun | 3 | 0 | |
| Bow | 4 | 2 | Special draw/release logic |
| Sniper Rifle | 5 | 0 | |
| Rocket Launcher | 6 | 0 | |
| Knife | 23 | 1 | Melee |
| Katana | 24 | 1 | Melee with combos |
| Sword | 25 | 1 | Melee with combos |
| Shield | 26 | 0 | Off-hand only (Slot1) |
| Grenade | 41 | 0 | Throwable |
| Magic | 61-65 | 0 | See Magic Spells section |

### State Index Values

| Value | State | Usage |
|:-----:|-------|-------|
| 0 | Idle | Default state |
| 2 | Use/Fire | Attack action |
| 3 | Reload | Reloading weapon |
| 4 | Equip | Playing equip animation |
| 5 | Unequip | Playing unequip animation |
| 6 | Drop | Dropping item |
| 7 | Melee Attack | Melee-specific attack state |

---

## Weapon Setup Instructions

### Magic (ID 61-65)

**Prerequsites:**
- Weapon prefab with `UsableAction` component
- `AnimatorItemID` set to 61-65 on the weapon authoring

**Animator Flow:**
1. `Slot0ItemID = 61` → Enters Magic sub-state machine
2. `Slot0ItemSubstateIndex` selects spell:
   - 0: Fireball Light
   - 1: Fireball Heavy
   - 2: Particle Stream
   - 3: Heal
   - 4: Ricochet
   - 5: Shield Bubble
3. `Slot0ItemStateIndex = 2` → Triggers cast animation

**Movement Lock:**
During channeled spells, movement is locked. Configure via `WeaponEquipVisualBridge.CancelCastOnMove`:
- `false` (default): Movement input blocked during cast
- `true`: Movement input cancels the spell

---

### Dual Pistol (ID 2)

**Critical Setup:**
Dual pistol requires BOTH slots to have ID 2:
- `Slot0ItemID = 2` (Main hand)
- `Slot1ItemID = 2` (Off hand)

**Prefab Requirements:**
1. Create TWO pistol prefabs or use dynamic dual-wield detection
2. Both must have `AnimatorItemID = 2`

**How It Works:**
- `WeaponEquipVisualBridge.IsPistolItemID()` detects pistol in both slots
- When both equipped, dual pistol animations play automatically
- Single pistol in main hand only → single pistol animations

---

### Shield (ID 26)

**Slot Configuration:**
Shield is an **off-hand** item:
- `Slot1ItemID = 26`
- Main hand can be any weapon (e.g., Sword)

**Blocking:**
- Right-click (aim) triggers `_shieldBlocking = true`
- This sets `Slot1ItemStateIndex = 2` (Block state)

**Prefab Setup:**
1. Shield prefab with `AnimatorItemID = 26`
2. Add `ShieldState` component for blocking logic (if using ECS)

---

### Sword (ID 25)

**Combo System:**
Sword attacks chain via `Slot0ItemSubstateIndex`:
- Click 1: `SubstateIndex = 1` → Attack 1 Light
- Click 2: `SubstateIndex = 2` → Attack 2 Light
- etc.

**Combo Window:**
- 0.8 seconds between attacks (configurable via `COMBO_WINDOW` constant)
- Timer resets on each attack

---

## Verification Checklist

### Basic Weapon Switching
- [ ] Press 1-9 to switch main hand weapons
- [ ] Weapon model shows/hides correctly
- [ ] Animator transitions to correct weapon sub-state machine

### Magic
- [ ] Equip magic (slot with ID 61)
- [ ] Left-click casts spell (Fireball Light by default)
- [ ] Movement locked during casting

### Dual Pistol
- [ ] Equip pistol in main hand (single pistol anims)
- [ ] Press Alt+2 to equip off-hand pistol
- [ ] Both equipped → dual pistol animations

### Shield + Sword
- [ ] Equip sword (main hand)
- [ ] Press Alt+# to equip shield (off hand)
- [ ] Right-click blocks with shield
- [ ] Left-click attacks with sword

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Weapon not transitioning | Verify `Slot0ItemStateIndexChange` trigger is firing via debug logs |
| Stuck in idle after equip | Check `ForceWeaponSubStateMachine()` is being called |
| Dual pistol not triggering | Ensure BOTH `Slot0ItemID` and `Slot1ItemID` are 2 |
| Shield not blocking | Verify `Slot1ItemStateIndex` is set to 2 on right-click |
| Magic not casting | Check `Slot0ItemStateIndex = 2` is set on fire input |
| Wrong weapon layer weight | Enable `DebugLogging` and check `WEAPON_DEBUG` logs |

---

## Debug Logging

Enable `WeaponEquipVisualBridge.DebugLogging = true` in Inspector.

**Log Filters:**
- `[ANIMATOR_PARAM]` - Parameter changes
- `[WEAPON_DEBUG]` - Layer weights, sub-state transitions
- `[BOW_DEBUG]` - Bow-specific state tracking
