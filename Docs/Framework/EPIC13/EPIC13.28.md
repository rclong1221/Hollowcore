# EPIC 13.28 - Weapon Animation Input System

## Status: 🟡 IN PROGRESS

---

## Implementation Summary

| Feature | Status | Notes |
|---------|--------|-------|
| **Gun - Fire (Left Click)** | ✅ WORKING | SubstateIndex=2 |
| **Gun - Aim (Right Click)** | 🧪 NEEDS TEST | ECS override fixed |
| **Gun - Auto-Fire (Hold Left)** | ✅ IMPLEMENTED | 10 shots/sec rate limit |
| **Gun - Reload (R Key)** | ✅ IMPLEMENTED | Not tested |
| **Melee - Attack (Left Click)** | ✅ IMPLEMENTED | Not tested |
| **Melee - Combo (Rapid Clicks)** | ✅ IMPLEMENTED | 0.8s window, not tested |
| **Melee - Block (Right Click)** | ✅ IMPLEMENTED | Not tested |
| **Bow - Draw (Right Click)** | ✅ IMPLEMENTED | Not tested |
| **Bow - Release (Left Click)** | ✅ IMPLEMENTED | Not tested |
| **Editor Tools** | ✅ REWRITTEN | Limb-based layers |

### Legend
- ✅ WORKING = Implemented AND verified in-game
- ✅ IMPLEMENTED = Code complete, not yet tested in-game
- 🧪 NEEDS TEST = Just fixed, awaiting verification
- ❌ NOT DONE = Not yet implemented
- 🔄 IN PROGRESS = Currently being worked on

---

## Overview

This document defines the complete input-to-animation system for ALL weapon types. The goal is to map standard game inputs (Left Click, Right Click, R) to the correct animator parameters based on the currently equipped weapon type.

---

## Core Principle

**Same Input → Different Animation Based on Weapon Type**

The `MovementSetID` animator parameter tells the Opsive animator which weapon category animations to use:
- `MovementSetID = 0` → Gun animations (fire, aim, reload)
- `MovementSetID = 1` → Melee animations (swing, combo, block)
- `MovementSetID = 2` → Bow animations (draw, aim, release)

The bridge sets the correct `MovementSetID` when a weapon is equipped, then standard inputs just need to set `Slot0ItemStateIndex` and `Slot0ItemSubstateIndex`.

---

## Input Mapping Table

| Input | Guns (MovementSetID=0) | Melee (MovementSetID=1) | Bow (MovementSetID=2) |
|-------|------------------------|-------------------------|----------------------|
| **Left Click** | Fire (State=2, Sub=2) | Attack (State=2, Sub=1→3 combo) | Release arrow (State=2) |
| **Left Click Hold** | Auto-fire (repeat State=2, Sub=2) | Charge attack (optional) | — |
| **Right Click Hold** | Aim (State=1, Aiming=true) | Guard/Block (State=8) | Draw bow (State=1, Aiming=true) |
| **Right Click Release** | Stop aim (State=0) | Stop block | Cancel draw |
| **R Key** | Reload (State=3) | — | — |

---

## Animator Parameters (Opsive ClimbingDemo)

### Core Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| `Slot0ItemID` | int | Weapon identifier (determines which weapon animations to use) |
| `Slot0ItemStateIndex` | int | Current action state (0=idle, 1=aim, 2=use, 3=reload, etc.) |
| `Slot0ItemSubstateIndex` | int | Sub-action (combo index, fire mode, etc.) |
| `Slot0ItemStateIndexChange` | trigger | Fires to signal state change to animator |
| `MovementSetID` | int | Weapon category (0=Guns, 1=Melee, 2=Bow) |
| `Aiming` | bool | True when aiming (right-click held) |

### State Index Values
| Index | Action | Used By |
|-------|--------|---------|
| 0 | Idle | All |
| 1 | Aim | Guns, Bow |
| 2 | Use/Fire/Attack | All |
| 3 | Reload | Guns only |
| 4 | Equip | All |
| 5 | Unequip | All |
| 6 | Drop | All |
| 7 | Secondary Attack | Melee |
| 8 | Block | Shield/Melee |
| 9 | Parry | Shield/Melee |

---

## Weapon Prefabs - Complete Reference

### GUNS (MovementSetID = 0)

#### AssaultRifle (QuickSlot 1)
| Property | Value |
|----------|-------|
| Prefab | `AssaultRifleWeapon_ECS.prefab` |
| ItemID | 1 (Assault Rifle in ClimbingDemo) |
| MovementSetID | 0 (Guns) |
| Fire Mode | Auto (hold to fire repeatedly) |
| Fire StateIndex | 2 |
| Fire SubstateIndex | 2 |
| Aim StateIndex | 1 |
| Reload StateIndex | 3 |
| Has Muzzle Flash | Yes (`AssaultRifleMuzzleFlash_ECS.prefab`) |
| Shell Eject | Yes (`AssaultRifleShell_ECS.prefab`) |
| Projectile | `AssaultRifleProjectile_ECS.prefab` |
| Drop Prefab | `AssaultRifleDrop_ECS.prefab` |
| Pickup Prefab | `AssaultRiflePickup_ECS.prefab` |

#### Pistol (QuickSlot 6)
| Property | Value |
|----------|-------|
| Prefab | `PistolWeaponBase_ECS.prefab` |
| ItemID | 2 (verify in animator) |
| MovementSetID | 0 (Guns) |
| Fire Mode | Semi (click per shot) |
| Fire StateIndex | 2 |
| Fire SubstateIndex | 2 |
| Aim StateIndex | 1 |
| Reload StateIndex | 3 |
| Has Muzzle Flash | Yes (`PistolMuzzleFlash_ECS.prefab`) |
| Shell Eject | Yes (`PistolShell_ECS.prefab`) |
| Clip | `PistolClip_ECS.prefab` |
| Drop Prefab | `PistolDrop_ECS.prefab` |
| Pickup Prefab | `PistolPickup_ECS.prefab` |

#### Shotgun (QuickSlot 4)
| Property | Value |
|----------|-------|
| Prefab | `ShotgunWeapon_ECS.prefab` |
| ItemID | 3 |
| MovementSetID | 0 (Guns) |
| Fire Mode | Semi (pump action) |
| Fire StateIndex | 2 |
| Fire SubstateIndex | 2 |
| Reload StateIndex | 3 |
| Projectile | `ShotgunProjectile_ECS.prefab` |
| Drop Prefab | `ShotgunDrop_ECS.prefab` |
| Pickup Prefab | `ShotgunPickup_ECS.prefab` |
| Special | Pump animation between shots |

#### SniperRifle (QuickSlot 5)
| Property | Value |
|----------|-------|
| Prefab | `SniperRifleWeapon_ECS.prefab` |
| ItemID | 5 |
| MovementSetID | 0 (Guns) |
| Fire Mode | Semi (bolt action) |
| Fire StateIndex | 2 |
| Aim StateIndex | 1 (zoom scope) |
| Reload StateIndex | 3 |
| Drop Prefab | `SniperRifleDrop_ECS.prefab` |
| Pickup Prefab | `SniperRiflePickup_ECS.prefab` |
| Special | Scope zoom on aim |

#### RocketLauncher (QuickSlot 7)
| Property | Value |
|----------|-------|
| Prefab | `RocketLauncherWeapon_ECS.prefab` |
| ItemID | 6 |
| MovementSetID | 0 (Guns) |
| Fire Mode | Semi |
| Fire StateIndex | 2 |
| Reload StateIndex | 3 |
| Projectile | `RocketProjectile_ECS.prefab` |
| Explosion | `Explosion_ECS.prefab` |
| Drop Prefab | `RocketLauncherDrop_ECS.prefab` |
| Pickup Prefab | `RocketLauncherPickup_ECS.prefab` |
| Special | Explosive projectile with travel time |

---

### MELEE (MovementSetID = 1)

#### Katana (QuickSlot 2)
| Property | Value |
|----------|-------|
| Prefab | `KatanaWeapon_ECS.prefab` |
| ItemID | 24 |
| MovementSetID | 1 (Melee) |
| Attack Mode | Combo (3-hit chain) |
| Attack StateIndex | 2 |
| Combo SubstateIndex | 1 → 2 → 3 (cycling) |
| Block StateIndex | 8 (optional) |
| Drop Prefab | `KatanaDrop_ECS.prefab` |
| Pickup Prefab | `KatanaPickup_ECS.prefab` |
| Trail Effect | Yes (sword trail) |
| Impact | `MeleeImpact_ECS.prefab` |

#### Knife (QuickSlot 3)
| Property | Value |
|----------|-------|
| Prefab | `KnifeWeapon_ECS.prefab` |
| ItemID | 23 |
| MovementSetID | 1 (Melee) |
| Attack Mode | Fast stab/slash |
| Attack StateIndex | 2 |
| Attack SubstateIndex | 1-2 |
| Drop Prefab | `KnifeDrop_ECS.prefab` |
| Pickup Prefab | `KnifePickup_ECS.prefab` |
| Trail Effect | Yes (`KnifeTrail_ECS.prefab`) |
| Impact | `MeleeImpact_ECS.prefab` |

#### Sword
| Property | Value |
|----------|-------|
| Prefab | `SwordWeapon_ECS.prefab` |
| ItemID | TBD (check animator) |
| MovementSetID | 1 (Melee) |
| Attack Mode | Combo |
| Attack StateIndex | 2 |
| Drop Prefab | `SwordDrop_ECS.prefab` |
| Pickup Prefab | `SwordPickup_ECS.prefab` |

#### Body (Unarmed)
| Property | Value |
|----------|-------|
| Prefab | `Body_ECS.prefab` |
| ItemID | 0 or specific unarmed ID |
| MovementSetID | 1 (Melee) |
| Attack Mode | Punch/Kick |
| Attack StateIndex | 2 |

---

### BOW (MovementSetID = 2)

#### Bow (QuickSlot 2 alternate or dedicated slot)
| Property | Value |
|----------|-------|
| Prefab | `BowWeapon_ECS.prefab` |
| ItemID | 4 |
| MovementSetID | 2 (Bow) |
| Fire Mode | Draw and release |
| Draw StateIndex | 1 (while holding right-click) |
| Release StateIndex | 2 (on left-click release) |
| Aiming | true (while drawing) |
| Arrow Prefab | `ArrowProjectile_ECS.prefab` |
| Drop Prefab | `BowDrop_ECS.prefab` |
| Pickup Prefab | `BowPickup_ECS.prefab` |

---

### THROWABLES

#### FragGrenade (QuickSlot 8)
| Property | Value |
|----------|-------|
| Prefab Left | `FragGrenadeWeaponLeft_ECS.prefab` |
| Prefab Right | `FragGrenadeWeaponRight_ECS.prefab` |
| ItemID | 41 |
| MovementSetID | 0 (uses gun-style throw) |
| Throw Mode | Hold to charge, release to throw |
| Throw StateIndex | 2 |
| Projectile | `FragGrenadeProjectile_ECS.prefab` |
| Explosion | `Explosion_ECS.prefab` |
| Drop Prefab | `FragGrenadeDrop_ECS.prefab` |
| Pickup Prefab | `FragGrenadePickup_ECS.prefab` |

---

### SHIELD

#### Shield
| Property | Value |
|----------|-------|
| Prefab | `Shield_ECS.prefab` |
| ItemID | TBD |
| Block StateIndex | 8 |
| Parry StateIndex | 9 |
| Block Mode | Hold right-click |
| Parry Mode | Timed block (first 0.2s) |
| Drop Prefab | `ShieldDrop_ECS.prefab` |
| Pickup Prefab | `ShieldPickup_ECS.prefab` |
| Effect | `ShieldBubble_ECS.prefab`, `ShieldBubbleParticle_ECS.prefab` |

---

### UTILITY (Non-Combat)

#### Flashlight
| Property | Value |
|----------|-------|
| Prefab | `Flashlight_ECS.prefab` |
| ItemID | TBD |
| Action | Toggle on/off |
| StateIndex | 2 (toggle) |
| Drop Prefab | `FlashlightDrop_ECS.prefab` |
| Pickup Prefab | `FlashlightPickup_ECS.prefab` |

---

### MAGIC

#### Fireball (Magic Item)
| Property | Value |
|----------|-------|
| Prefab | `Fireball_ECS.prefab` |
| Projectile | `FireballProjectile_ECS.prefab` |
| ItemID | TBD |
| Cast StateIndex | 2 |

#### ParticleStream (Magic Item)
| Property | Value |
|----------|-------|
| Prefab | `ParticleStream_ECS.prefab` |
| Particle | `ParticleStreamParticle_ECS.prefab` |
| Cast Mode | Channeled (hold) |

#### Teleport (Magic Item)
| Property | Value |
|----------|-------|
| Prefab | `Teleport_ECS.prefab` |
| Start VFX | `TeleportStartParticle_ECS.prefab` |
| End VFX | `TeleportEndParticle_ECS.prefab` |
| Cast StateIndex | 2 |

---

## QuickSlot Mapping (Current Configuration)

| QuickSlot | Weapon | ItemID | MovementSetID | Category |
|-----------|--------|--------|---------------|----------|
| 1 | Assault Rifle | 1 | 0 | Gun |
| 2 | Katana | 24 | 1 | Melee |
| 3 | Knife | 23 | 1 | Melee |
| 4 | Shotgun | 3 | 0 | Gun |
| 5 | Sniper Rifle | 5 | 0 | Gun |
| 6 | Pistol | 2 | 0 | Gun |
| 7 | Rocket Launcher | 6 | 0 | Gun |
| 8 | Frag Grenade | 41 | 0 | Throwable |
| 9 | (unused) | — | — | — |

**SlotMovementSetIDs Array:** `{ 0, 0, 1, 1, 0, 0, 0, 0, 0, 0 }`

---

## Implementation Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         INPUT LAYER                                  │
├─────────────────────────────────────────────────────────────────────┤
│  Mouse.leftButton     → Fire1 (Use/Attack)                          │
│  Mouse.rightButton    → Fire2 (Aim/Block)                           │
│  Keyboard.rKey        → Reload                                       │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    INPUT PROCESSING LAYER                            │
│              (WeaponEquipVisualBridge.HandleInput())                │
├─────────────────────────────────────────────────────────────────────┤
│  1. Get current weapon's MovementSetID                              │
│  2. Determine weapon category (Gun/Melee/Bow)                       │
│  3. Map input to appropriate StateIndex based on category           │
│  4. Handle hold vs press vs release                                 │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    ANIMATOR PARAMETER LAYER                          │
│              (WeaponEquipVisualBridge.SetAnimatorState())           │
├─────────────────────────────────────────────────────────────────────┤
│  Slot0ItemID          = weapon.ItemID                               │
│  MovementSetID        = weapon.MovementSetID                        │
│  Slot0ItemStateIndex  = computed from input + category              │
│  Slot0ItemSubstateIndex = action-specific (combo, fire mode)        │
│  Aiming               = rightClickHeld                              │
│  Slot0ItemStateIndexChange → trigger                                │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    OPSIVE ANIMATOR CONTROLLER                        │
│                    (ClimbingDemo.controller)                         │
├─────────────────────────────────────────────────────────────────────┤
│  Layer transitions based on:                                        │
│  - Slot0ItemID selects weapon sub-state machine                     │
│  - MovementSetID selects stance/locomotion                          │
│  - Slot0ItemStateIndex triggers action animations                   │
│  - Slot0ItemSubstateIndex selects variation                         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Implementation Tasks

### Task 13.28.1 - Remove Debug Keys
- [x] Remove P and O debug key handling from `LateUpdate()`
- [x] Keep `ForceTestRifleAnimation()` and `DumpCompleteAnimatorState()` methods for future debugging (but don't call them)

### Task 13.28.2 - Add Input Handling System
Create new method `HandleWeaponInput()` in `WeaponEquipVisualBridge.cs`:

```csharp
private void HandleWeaponInput()
{
    if (PlayerAnimator == null) return;
    
    var keyboard = Keyboard.current;
    var mouse = Mouse.current;
    if (keyboard == null || mouse == null) return;
    
    int movementSetID = PlayerAnimator.GetInteger(_hashMovementSetID);
    
    // Determine weapon category
    bool isGun = (movementSetID == 0);
    bool isMelee = (movementSetID == 1);
    bool isBow = (movementSetID == 2);
    
    // Right Click - Aim/Block
    HandleSecondaryInput(mouse.rightButton, isGun, isMelee, isBow);
    
    // Left Click - Use/Fire/Attack
    HandlePrimaryInput(mouse.leftButton, isGun, isMelee, isBow);
    
    // R Key - Reload (guns only)
    if (isGun && keyboard.rKey.wasPressedThisFrame)
    {
        TriggerReload();
    }
}
```

### Task 13.28.3 - Implement Gun Input Handlers
```csharp
// Guns: Left click = fire, Right click = aim, R = reload
private void HandleGunPrimary(bool pressed, bool held, bool released)
{
    if (pressed || held) // Auto-fire support
    {
        SetAnimatorState(stateIndex: 2, substateIndex: 2, triggerChange: pressed); // SubstateIndex=2 matches animator Fire transition
    }
    else if (released)
    {
        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: false);
    }
}

private void HandleGunSecondary(bool pressed, bool held, bool released)
{
    if (pressed)
    {
        PlayerAnimator.SetBool(_hashAiming, true);
        SetAnimatorState(stateIndex: 1, substateIndex: 0, triggerChange: true);
    }
    else if (released)
    {
        PlayerAnimator.SetBool(_hashAiming, false);
        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
    }
}
```

### Task 13.28.4 - Implement Melee Input Handlers
```csharp
// Melee: Left click = attack combo, Right click = block
private int _meleeComboIndex = 0;
private float _meleeComboTimer = 0f;
private const float COMBO_WINDOW = 0.8f;

private void HandleMeleePrimary(bool pressed, bool held, bool released)
{
    if (pressed)
    {
        // Advance combo if within window
        if (_meleeComboTimer > 0)
            _meleeComboIndex = (_meleeComboIndex % 3) + 1; // 1→2→3→1
        else
            _meleeComboIndex = 1;
            
        _meleeComboTimer = COMBO_WINDOW;
        SetAnimatorState(stateIndex: 2, substateIndex: _meleeComboIndex, triggerChange: true);
    }
}

private void HandleMeleeSecondary(bool pressed, bool held, bool released)
{
    if (held)
    {
        SetAnimatorState(stateIndex: 8, substateIndex: 0, triggerChange: pressed); // Block
    }
    else if (released)
    {
        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
    }
}
```

### Task 13.28.5 - Implement Bow Input Handlers
```csharp
// Bow: Right click = draw, Left click = release
private bool _bowDrawing = false;
private float _bowDrawProgress = 0f;

private void HandleBowPrimary(bool pressed, bool held, bool released)
{
    if (pressed && _bowDrawing)
    {
        // Release arrow
        SetAnimatorState(stateIndex: 2, substateIndex: 1, triggerChange: true);
        _bowDrawing = false;
        _bowDrawProgress = 0f;
        PlayerAnimator.SetBool(_hashAiming, false);
    }
}

private void HandleBowSecondary(bool pressed, bool held, bool released)
{
    if (pressed)
    {
        // Start drawing
        _bowDrawing = true;
        PlayerAnimator.SetBool(_hashAiming, true);
        SetAnimatorState(stateIndex: 1, substateIndex: 0, triggerChange: true);
    }
    else if (held && _bowDrawing)
    {
        // Continue drawing
        _bowDrawProgress = Mathf.Min(_bowDrawProgress + Time.deltaTime, 1f);
    }
    else if (released)
    {
        // Cancel draw without firing (if not fired via left click)
        if (_bowDrawing)
        {
            _bowDrawing = false;
            _bowDrawProgress = 0f;
            PlayerAnimator.SetBool(_hashAiming, false);
            SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
        }
    }
}
```

### Task 13.28.6 - Reload Handler
```csharp
private void TriggerReload()
{
    SetAnimatorState(stateIndex: 3, substateIndex: 0, triggerChange: true);
    // Reload will auto-return to idle via animator
}
```

### Task 13.28.7 - Unified State Setter
```csharp
private void SetAnimatorState(int stateIndex, int substateIndex, bool triggerChange)
{
    PlayerAnimator.SetInteger(_hashSlotItemState, stateIndex);
    PlayerAnimator.SetInteger(_hashSlotItemSubstate, substateIndex);
    
    if (triggerChange)
    {
        PlayerAnimator.SetTrigger(_hashSlotItemChange);
    }
    
    if (DebugLogging)
    {
        Debug.Log($"[WEAPON_INPUT] Set StateIndex={stateIndex} SubstateIndex={substateIndex} Trigger={triggerChange}");
    }
}
```

### Task 13.28.8 - Throwable Input Handler
```csharp
// Throwable: Hold left click = charge, Release = throw
private float _throwChargeProgress = 0f;
private bool _throwCharging = false;

private void HandleThrowablePrimary(bool pressed, bool held, bool released)
{
    if (pressed)
    {
        _throwCharging = true;
        _throwChargeProgress = 0f;
        SetAnimatorState(stateIndex: 2, substateIndex: 0, triggerChange: true); // Begin charge anim
    }
    else if (held && _throwCharging)
    {
        _throwChargeProgress = Mathf.Min(_throwChargeProgress + Time.deltaTime, 1f);
        // Could set substateIndex based on charge level
    }
    else if (released && _throwCharging)
    {
        // Throw with power based on charge
        SetAnimatorState(stateIndex: 2, substateIndex: 1, triggerChange: true); // Throw anim
        _throwCharging = false;
        _throwChargeProgress = 0f;
    }
}
```

---

## ECS Integration (Future)

Currently, the input handling is in `WeaponEquipVisualBridge` (MonoBehaviour). 

For proper ECS architecture, this should eventually move to:

1. **PlayerInputSystem** → Reads raw input, writes to `PlayerInput` component
2. **PlayerToItemInputSystem** → Maps input to `UseRequest`, `AimRequest`, `ReloadRequest`
3. **WeaponActionSystems** → Process requests, update weapon state components
4. **WeaponEquipVisualBridge** → Reads ECS state, drives animator (no input handling)

This refactor is deferred. Current implementation keeps input in the bridge for simplicity.

---

## Files to Modify

| File | Changes |
|------|---------|
| `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs` | Add `HandleWeaponInput()`, input handlers, remove P/O debug keys |
| `Assets/Scripts/Weapons/Components/WeaponActionComponents.cs` | Ensure melee combo state exists |
| (none) | Animator controller is already set up correctly |

---

## VFX Prefabs Reference

### Muzzle Flashes
- `AssaultRifleMuzzleFlash_ECS.prefab`
- `PistolMuzzleFlash_ECS.prefab`
- `MuzzleFlash_ECS.prefab` (generic)

### Shell Ejection
- `AssaultRifleShell_ECS.prefab`
- `PistolShell_ECS.prefab`
- `Shell_ECS.prefab` (generic)

### Impact Effects
- `MeleeImpact_ECS.prefab`
- `MeleeSpark_ECS.prefab`
- `Spark_ECS.prefab`
- `Ricochet_ECS.prefab`
- `Blood_ECS.prefab`

### Dust/Particle Effects
- `BulletDirtDust_ECS.prefab`
- `BulletSandDust_ECS.prefab`
- `MeleeDirtDust_ECS.prefab`
- `MeleeSandDust_ECS.prefab`
- `MeleeGrassParticle_ECS.prefab`
- `GrassParticle_ECS.prefab`
- `SandDust_ECS.prefab`

### Water Effects
- `MeleeWaterSplash_ECS.prefab`
- `FootstepWaterSplash_ECS.prefab`
- `ShellWaterSplash_ECS.prefab`
- `EntranceSplash_ECS.prefab`
- `LimbSplash_ECS.prefab`

### Explosions
- `Explosion_ECS.prefab`
- `BigCrateExplosion_ECS.prefab`
- `CrateExplosion_ECS.prefab`

### Weapon Trails
- `KnifeTrail_ECS.prefab`

### Counter VFX
- `FirstPersonCounterVFX_ECS.prefab`
- `ThirdPersonCounterVFX_ECS.prefab`

---

## Testing Checklist

### Guns (AssaultRifle, Pistol, Shotgun, SniperRifle, RocketLauncher)
- [x] Left click fires weapon (StateIndex=2) ✅ VERIFIED
- [ ] Hold left click for auto-fire (AssaultRifle) - implemented, not tested
- [ ] Right click hold enables aim (Aiming=true) - ECS fix applied, needs test
- [ ] Right click release disables aim
- [ ] R key triggers reload (StateIndex=3) - implemented, not tested
- [ ] Reload animation plays and returns to idle

### Melee (Katana, Knife, Sword) - ALL IMPLEMENTED, NOT TESTED
- [ ] Left click swings weapon (StateIndex=2)
- [ ] Rapid left clicks chain combo (SubstateIndex 1→2→3)
- [ ] Combo resets after timeout (0.8s)
- [ ] Right click hold enables block (StateIndex=8)
- [ ] Right click release exits block

### Bow - ALL IMPLEMENTED, NOT TESTED
- [ ] Right click starts drawing (Aiming=true, StateIndex=1)
- [ ] Left click while drawing releases arrow (StateIndex=2)
- [ ] Right click release without firing cancels draw
- [ ] Arrow projectile spawns on release

### Throwable (Grenade) - DEFERRED (uses gun handler)
- [ ] Hold left click charges throw
- [ ] Release left click throws grenade
- [ ] Grenade projectile spawns

### Shield - NOT IMPLEMENTED
- [ ] Right click hold raises shield (StateIndex=8)
- [ ] Perfect timing triggers parry (StateIndex=9)
- [ ] Right click release lowers shield

---

## Notes

- **Auto-fire rate** for guns should be controlled by weapon component, not raw input
- **Melee combo window** is currently hardcoded; should come from weapon component
- **Bow draw speed** should be weapon-specific
- **Animation return to idle** is handled by animator state machine, not by code
- **Fire2 (right click)** should NOT fire weapons - only aim/block

---

## Dependencies

- Unity InputSystem package (already installed)
- Opsive ClimbingDemo animator controller (already configured)
- WeaponEquipVisualBridge (already exists)
- ECS weapon state components (already exist)

---

## Status

| Task | Status |
|------|--------|
| 13.28.1 - Remove Debug Keys | ✅ Complete |
| 13.28.2 - Add Input Handling System | ✅ Complete |
| 13.28.3 - Gun Input Handlers | ✅ Complete |
| 13.28.4 - Melee Input Handlers | ✅ Complete |
| 13.28.5 - Bow Input Handlers | ✅ Complete |
| 13.28.6 - Reload Handler | ✅ Complete |
| 13.28.7 - Unified State Setter | ✅ Complete |
| 13.28.8 - Throwable Input Handler | Deferred (uses gun handler) |
| 13.28.9 - Fix Gun Fire SubstateIndex | ✅ Complete (changed from 1 to 2) |
| 13.28.10 - Fix ECS Aiming Override | ✅ Complete (input-based aiming takes priority) |
| 13.28.11 - Rewrite Animator Editor Tools | ✅ Complete (limb-based layers) |
| Testing - Guns (Fire) | ✅ Verified (Left click fires) |
| Testing - Guns (Aim) | 🧪 Pending verification |
| Testing - Melee | Ready to Test |
| Testing - Bow | Ready to Test |
| Testing - Throwable | Ready to Test |

---

## Key Implementation Fixes

### Fix 1: Fire SubstateIndex = 2 (not 1)
The Opsive ClimbingDemo animator has Fire transitions that expect `SubstateIndex=2`, not 1. This was discovered by inspecting the Animator Controller's Fire transition conditions.

### Fix 2: Input-Based Aiming Takes Priority Over ECS
The bridge has two sources of aiming state:
1. **ECS-based**: Reads `WeaponAimState.IsAiming` component every frame
2. **Input-based**: Sets `_isAiming` flag from right-click

The ECS-based logic was overwriting the input-based aiming every frame. Fixed by adding `!_isAiming` checks:

```csharp
// Line ~816 - Skip ECS override when input-based aiming is active
if (!_isAiming && itemEntity != Entity.Null && ...)
{
    var aimState = _entityManager.GetComponentData<WeaponAimState>(itemEntity);
    PlayerAnimator.SetBool(_hashAiming, aimState.IsAiming);
}

// Line ~1124 - Same pattern
if (_hashAiming != 0 && !_isAiming)
{
    PlayerAnimator.SetBool(_hashAiming, isAiming);
}
```

### Fix 3: Editor Tools for Limb-Based Layers
The animator controller uses limb-based layers (Base Layer, Upperbody Layer, Arms Layer, etc.), NOT weapon-specific layers. Updated both editor tools:
- `AnimatorControllerAnalyzer.cs` - Compares Demo.controller vs ClimbingDemo.controller
- `AnimatorStateCopier.cs` - Copies missing states with integration to Analyzer
