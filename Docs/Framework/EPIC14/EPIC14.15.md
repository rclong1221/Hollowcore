# EPIC14.15 - First Person Arms Animation Integration

**Status:** Planned
**Dependencies:** EPIC14.12 (Agility Pack), EPIC14.13 (Swimming Pack), EPIC14.14 (Ride System)
**Goal:** Copy first-person arm animation states from FirstPersonArmsDemo.controller AND SwimmingFirstPersonArmsDemo.controller to ClimbingDemo.controller, enabling proper weapon-specific arm animations including underwater weapons.

---

## Overview

Two source controllers contain first-person arm animations:

1. **FirstPersonArmsDemo.controller** - Standard weapons (rifles, pistols, melee, etc.)
2. **SwimmingFirstPersonArmsDemo.controller** - Standard weapons PLUS swimming-specific weapons (Trident, Underwater Gun)

The **ClimbingDemo.controller** (our target) currently lacks these weapon-specific arm states.

This EPIC documents the complete state/transition structure that must be copied and wired up from BOTH source controllers.

**Source Controllers:**
- `Assets/Art/Animations/Opsive/Animator/Characters/FirstPersonArmsDemo.controller`
- `Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/SwimmingFirstPersonArmsDemo.controller`

**Target Controller:** `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller`

---

## Controller Comparison

### Layer Comparison

| Layer | FirstPersonArmsDemo | ClimbingDemo | Notes |
|-------|---------------------|--------------|-------|
| **Base Layer** | Weapon state machines (Assault Rifle, Bow, etc.) | Movement abilities (Jump, Fall, Dodge, etc.) | Different purposes - both needed |
| **Left Arm Layer** | Per-weapon left arm states | Per-weapon states (incomplete) | Needs weapon states copied |
| **Right Arm Layer** | Per-weapon right arm states | Per-weapon states (incomplete) | Needs weapon states copied |
| **Upperbody Layer** | Interact, Ride, Drive, melee | Present but may be incomplete | Compare and merge |
| **Left Hand Layer** | N/A | Present in Climbing | ClimbingDemo extra |
| **Right Hand Layer** | N/A | Present in Climbing | ClimbingDemo extra |
| **Arms Layer** | N/A | Present in Climbing | ClimbingDemo extra |

### Parameter Comparison

Both controllers use the same Opsive parameter set:

| Parameter | Type | Purpose |
|-----------|------|---------|
| HorizontalMovement | Float | Left/Right blend |
| ForwardMovement | Float | Forward/Back blend |
| Pitch | Float | Look up/down |
| Yaw | Float | Look left/right |
| Speed | Float | Movement speed |
| Height | Float | Stance height (0=Stand, 1=Crouch, 2=Prone) |
| Moving | Bool | Is character moving |
| Aiming | Bool | Is aiming down sights |
| MovementSetID | Int | Movement set (0=Normal, 1=Swim, etc.) |
| AbilityIndex | Int | Active ability (1=Jump, 2=Fall, etc.) |
| AbilityChange | Trigger | Triggers ability transition |
| AbilityIntData | Int | Ability sub-state data |
| AbilityFloatData | Float | Ability blend data |
| Slot0ItemID | Int | Main hand weapon ID |
| Slot0ItemStateIndex | Int | Weapon state (Idle, Aim, Fire, etc.) |
| Slot0ItemStateIndexChange | Trigger | Weapon state change trigger |
| Slot0ItemSubstateIndex | Int | Weapon sub-state |
| Slot1ItemID | Int | Off-hand item ID |
| Slot1ItemStateIndex | Int | Off-hand state |
| Slot1ItemStateIndexChange | Trigger | Off-hand state change trigger |
| Slot1ItemSubstateIndex | Int | Off-hand sub-state |
| LegIndex | Float | Leg blend (0-1) |

---

## FirstPersonArmsDemo.controller Structure

### Base Layer Sub-State Machines

Contains 13 weapon/ability state machines:

| State Machine | Purpose | States |
|---------------|---------|--------|
| **Assault Rifle** | AR arm animations | Idle, Fire, Reload, Aim variants |
| **Bow** | Bow animations | Draw, Hold, Release, Idle |
| **Body** | Unarmed animations | Punch, Block, Interact |
| **Flashlight** | Flashlight item | On/Off, Aim variants |
| **Frag Grenade** | Grenade throwing | Idle, Throw variants |
| **Sword** | One-hand sword | Attack 1-N, Block, Parry, Special |
| **Katana** | Two-hand katana | Attack 1-N, Block, Special, Counter |
| **Knife** | Combat knife | Stab, Slash, Block |
| **Magic** | Magic casting | Cast, Heal, Channel |
| **Pistol** | Handgun | Idle, Aim, Fire, Reload |
| **Shotgun** | Shotgun | Idle, Aim, Fire, Pump, Reload |
| **Sniper Rifle** | Sniper | Idle, Aim, Fire, Scope, Reload |
| **Rocket Launcher** | RPG | Idle, Aim, Fire, Reload |

### Left Arm Layer Sub-State Machines

Contains 4 weapon state machines (left-arm specific):

| State Machine | Purpose |
|---------------|---------|
| **Bow** | Left arm bow hold |
| **Dual Pistol** | Left hand dual wield |
| **Frag Grenade** | Left hand grenade |
| **Shield** | Shield block/parry |

### Right Arm Layer Sub-State Machines

Contains 10 weapon state machines (right-arm specific):

| State Machine | Purpose |
|---------------|---------|
| **Assault Rifle** | Right arm AR grip |
| **Dual Pistol** | Right hand dual wield |
| **Frag Grenade** | Right hand grenade |
| **Katana** | Right hand katana grip |
| **Knife** | Right hand knife |
| **Pistol** | Right hand pistol |
| **Rocket Launcher** | Right arm RPG |
| **Shotgun** | Right arm shotgun |
| **Sniper Rifle** | Right arm sniper |
| **Sword** | Right hand sword |

### Upperbody Layer Sub-State Machines

Contains 8 sub-state machines:

| State Machine | Purpose |
|---------------|---------|
| **Interact** | Item pickup, door open, etc. |
| **Body** | Unarmed upper body |
| **Katana** | Katana attacks |
| **Knife** | Knife attacks |
| **Sword** | Sword attacks |
| **Sword Shield** | Sword + Shield combo |
| **Ride** | Mount/Ride poses |
| **Drive** | Vehicle driving poses |

---

## SwimmingFirstPersonArmsDemo.controller Structure

This controller extends FirstPersonArmsDemo with **swimming-specific weapons** (Trident, Underwater Gun).

### Layers (4 total - same as FirstPersonArmsDemo)

| Layer | Purpose |
|-------|---------|
| **Base Layer** | Weapon state machines (includes swimming weapons) |
| **Left Arm Layer** | Per-weapon left arm states |
| **Right Arm Layer** | Per-weapon right arm states |
| **Upperbody Layer** | Melee attacks, interactions, Trident attacks |

### Base Layer Sub-State Machines (14 total)

Includes all 13 from FirstPersonArmsDemo PLUS 2 swimming-specific:

| State Machine | Purpose | States | Swimming-Specific? |
|---------------|---------|--------|-------------------|
| **Assault Rifle** | AR arm animations | Idle, Fire, Reload, Aim, Equip/Unequip | No |
| **Bow** | Bow animations | Draw, Hold, Release, Idle | No |
| **Body** | Unarmed animations | Punch, Block | No |
| **Frag Grenade** | Grenade throwing | Throw variants | No |
| **Sword** | One-hand sword | Attack, Block, Parry | No |
| **Katana** | Two-hand katana | Attack, Counter | No |
| **Knife** | Combat knife | Stab, Slash | No |
| **Pistol** | Handgun | Idle, Aim, Fire, Reload | No |
| **Shotgun** | Shotgun | Idle, Aim, Fire, Reload | No |
| **Sniper Rifle** | Sniper | Idle, Aim, Fire, Reload | No |
| **Rocket Launcher** | RPG | Idle, Aim, Fire, Reload | No |
| **Shield** | Shield (off-hand) | Block, Parry | No |
| **Trident** | Underwater melee | Idle, Aim, Equip, Unequip | **YES** |
| **Underwater Gun** | Underwater ranged | Idle, Fire, Dry Fire, Equip, Unequip | **YES** |

### Trident State Machine (Swimming-Specific)

| State | Purpose |
|-------|---------|
| **Idle** | Default underwater hold |
| **Aim** | Aiming stance |
| **Equip From Idle** | Equip animation |
| **Equip From Aim** | Equip while aiming |
| **Unequip From Idle** | Unequip animation |
| **Unequip From Aim** | Unequip while aiming |

### Underwater Gun State Machine (Swimming-Specific)

| State | Purpose |
|-------|---------|
| **Idle** | Default underwater hold |
| **Fire** | Shooting animation |
| **Dry Fire** | Out of ammo click |
| **Equip** | Equip animation |
| **Unequip** | Unequip animation |

### Left Arm Layer Sub-State Machines (4 total)

Same as FirstPersonArmsDemo:

| State Machine | Purpose |
|---------------|---------|
| **Bow** | Left arm bow hold |
| **Dual Pistol** | Left hand dual wield |
| **Frag Grenade** | Left hand grenade |
| **Shield** | Shield block/parry |

### Right Arm Layer Sub-State Machines (10 total)

Same as FirstPersonArmsDemo:

| State Machine | Purpose |
|---------------|---------|
| **Assault Rifle** | Right arm AR grip |
| **Dual Pistol** | Right hand dual wield |
| **Frag Grenade** | Right hand grenade |
| **Katana** | Right hand katana |
| **Knife** | Right hand knife |
| **Pistol** | Right hand pistol |
| **Rocket Launcher** | Right arm RPG |
| **Shotgun** | Right arm shotgun |
| **Sniper Rifle** | Right arm sniper |
| **Sword** | Right hand sword |

### Upperbody Layer Sub-State Machines (8 total)

Same as FirstPersonArmsDemo PLUS Trident attacks:

| State Machine | Purpose | Swimming-Specific? |
|---------------|---------|-------------------|
| **Interact** | Item pickup, door open | No |
| **Body** | Unarmed upper body | No |
| **Katana** | Katana attacks | No |
| **Knife** | Knife attacks | No |
| **Sword** | Sword attacks | No |
| **Sword Shield** | Sword + Shield combo | No |
| **Ride** | Mount/Ride poses | No |
| **Trident** | Trident attacks | **YES** |

### Trident Upperbody States

| State | Purpose |
|-------|---------|
| **Attack 1 From Idle** | Thrust attack from idle |
| **Attack 1 From Aim** | Thrust attack while aiming |

---

## ClimbingDemo.controller Current Structure

### Base Layer Sub-State Machines (21 total)

| State Machine | Status | Notes |
|---------------|--------|-------|
| Movement | Present | Locomotion blend trees |
| Idle | Present | Idle poses |
| Crouch | Present | Crouch movement |
| Fall | Present | Fall states |
| Jump | Present | Jump states |
| Start | Present | Start movement |
| Stop | Present | Stop movement |
| Quick Turn | Present | 180 turn |
| Interact | Present | Basic interact |
| Ride | Present | Mount riding |
| Aim Idle | Present | Aim idle |
| Aim Movement | Present | Aim movement |
| **Dodge** | Present | Agility Pack |
| **Roll** | Present | Agility Pack |
| **Vault** | Present | Agility Pack |
| **Ledge Strafe** | Present | Agility Pack |
| **Balance** | Present | Agility Pack |
| **Swim** | Present | Swimming Pack |
| **Dive** | Present | Swimming Pack |
| **Drown** | Present | Swimming Pack |
| **Climb From Water** | Present | Swimming Pack |

### Missing from Base Layer

The Base Layer is movement/ability focused, not weapon-focused. That's correct - weapon animations go in arm layers.

### Left Arm Layer / Right Arm Layer

These layers exist but may be incomplete compared to FirstPersonArmsDemo. Need to compare weapon state machines.

---

## States to Copy

### Phase 1: Base Layer Weapon States

Copy entire weapon sub-state machines from FirstPersonArmsDemo Base Layer:

| Source State Machine | States to Copy | Priority |
|----------------------|----------------|----------|
| **Assault Rifle** | All states (Idle, Aim, Fire, Reload, Dry Fire, Equip, Unequip variants) | High |
| **Pistol** | All states | High |
| **Shotgun** | All states | High |
| **Sniper Rifle** | All states | Medium |
| **Rocket Launcher** | All states | Medium |
| **Bow** | All states | Medium |
| **Frag Grenade** | All states (Throw variants) | High |
| **Sword** | All states (Attack 1-N, Parry, Counter, Special) | Medium |
| **Katana** | All states | Low |
| **Knife** | All states | Medium |
| **Magic** | All states | Low |
| **Flashlight** | All states | Low |
| **Body** | All states (Punch, Block) | Medium |

### Phase 2: Left Arm Layer States

Copy from FirstPersonArmsDemo Left Arm Layer:

| Source State Machine | States |
|----------------------|--------|
| **Bow** | Left arm draw, hold, release |
| **Dual Pistol** | Left hand dual wield states |
| **Frag Grenade** | Left hand grenade |
| **Shield** | Block, parry, bash |

### Phase 3: Right Arm Layer States

Copy from FirstPersonArmsDemo Right Arm Layer:

| Source State Machine | States |
|----------------------|--------|
| **Assault Rifle** | Right arm grip states |
| **Dual Pistol** | Right hand dual wield |
| **Frag Grenade** | Right hand grenade |
| **Katana** | Right hand katana |
| **Knife** | Right hand knife |
| **Pistol** | Right hand pistol |
| **Rocket Launcher** | Right arm RPG |
| **Shotgun** | Right arm shotgun |
| **Sniper Rifle** | Right arm sniper |
| **Sword** | Right hand sword |

### Phase 4: Upperbody Layer States

Copy from FirstPersonArmsDemo Upperbody Layer:

| Source State Machine | States | Notes |
|----------------------|--------|-------|
| **Interact** | Pickup, Door, Use | General interaction anims |
| **Sword Shield** | All combo states | If sword+shield supported |
| **Ride** | Ride poses | May already exist |
| **Drive** | Drive poses | If vehicles supported |

---

## Transition Conditions

### AnyState Transitions

Weapon state machines use these transition patterns:

```
AnyState → [Slot0ItemID == X] → Weapon/Idle
AnyState → [Slot0ItemStateIndex == Y && Slot0ItemStateIndexChange] → Weapon/Action
```

### Entry/Exit Transitions

```
Entry → Default State (usually Idle or Equip)
State → [Slot0ItemID != X] → Exit
State → [Slot0ItemStateIndex == 0] → Idle
```

### Weapon State Index Values

| Slot0ItemStateIndex | Meaning |
|---------------------|---------|
| 0 | Idle |
| 1 | Equip |
| 2 | Unequip |
| 3 | Fire |
| 4 | Reload |
| 5 | Aim |
| 6 | Use (melee attack) |
| 7+ | Weapon-specific |

### Weapon Item IDs (Slot0ItemID)

| ItemID | Weapon | Notes |
|--------|--------|-------|
| 1 | Assault Rifle | Primary example |
| 2 | Pistol | |
| 3 | Shotgun | |
| 4 | Sniper Rifle | |
| 5 | Rocket Launcher | |
| 6 | Bow | |
| 7 | Sword | One-hand |
| 8 | Katana | Two-hand |
| 9 | Knife | |
| 10 | Frag Grenade | Consumable |
| 11 | Flashlight | Utility |
| 12 | Shield | Off-hand (Slot1) |

*Note: Actual ItemIDs depend on your item database configuration.*

---

## Blend Tree Analysis

### Locomotion Blend Trees

Many states use Height-based blend trees:

```yaml
BlendTree:
  BlendParameter: Height
  Children:
    - Standing Animation (Height = 0)
    - Crouching Animation (Height = 1)
```

### Yaw-based Blend Trees

Some idle states use Yaw for look direction:

```yaml
BlendTree:
  BlendParameter: Yaw
  Children:
    - Look Left (Yaw = -16)
    - Look Center (Yaw = 0)
    - Look Right (Yaw = 16)
```

### Movement Blend Trees

Walk/Run states use 2D blend:

```yaml
BlendTree:
  BlendType: 2D Freeform
  BlendParameterX: HorizontalMovement
  BlendParameterY: ForwardMovement
  Children:
    - Walk Forward
    - Walk Backward
    - Walk Left
    - Walk Right
    - Idle
```

---

## Animation Clips Referenced

### Sample Weapon Animation Clips

From FirstPersonArmsDemo.controller:

| State | Animation Clip GUID | Notes |
|-------|---------------------|-------|
| Assault Rifle Idle | bcddd141483902b4d8c3fe4b1b8c7db0 | Standing idle |
| Assault Rifle Crouch Idle | 2e3df2f725cf42a4b8ad8eef5d81f647 | Crouching idle |
| Attack Pull Back | (BlendTree) | Melee wind-up |

*Note: Full clip mapping requires parsing all Motion references in controller.*

---

## Implementation Plan

### Phase 0: Create Copier Tool (BLOCKING)

**CRITICAL:** Must create editor tool that copies BOTH states AND transitions, similar to `AnimatorAbilityCopier.cs` from EPIC14.12.

**New Tool:** `Assets/Editor/AnimatorArmsCopier.cs`
**Menu Location:** `DIG > Animation > Copy First Person Arms States`

```csharp
// Menu: DIG > Animation > Copy First Person Arms States
public static void CopyFirstPersonArmsStates()
{
    // 1. Load source and target controllers
    var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(
        "Assets/Art/Animations/Opsive/Animator/Characters/FirstPersonArmsDemo.controller");
    var target = AssetDatabase.LoadAssetAtPath<AnimatorController>(
        "Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller");

    // 2. For each layer in source:
    foreach (var sourceLayer in source.layers)
    {
        // Find matching layer in target (or create if missing)
        var targetLayer = FindOrCreateLayer(target, sourceLayer.name);

        // Copy weapon sub-state machines (preserving existing states)
        CopySubStateMachines(sourceLayer.stateMachine, targetLayer.stateMachine);

        // CRITICAL: Copy AnyState transitions with ALL conditions
        CopyAnyStateTransitions(sourceLayer.stateMachine, targetLayer.stateMachine);

        // Copy blend trees (deep copy, not just motion references)
        CopyBlendTrees(sourceLayer.stateMachine, targetLayer.stateMachine);
    }

    // 3. Ensure all parameters exist in target
    EnsureParametersExist(source, target);

    // 4. Save target controller
    EditorUtility.SetDirty(target);
    AssetDatabase.SaveAssets();
}
```

**What Must Be Copied (Per State Machine):**

| Element | Copy Method | Notes |
|---------|-------------|-------|
| **States** | Deep copy with Motion, Speed, IK settings | Preserve fileIDs |
| **Sub-State Machines** | Recursive copy | Nested state machines |
| **AnyState Transitions** | Copy with ALL conditions | Slot0ItemID, Slot0ItemStateIndex, etc. |
| **Entry Transitions** | Copy with conditions | Entry → default state |
| **Exit Transitions** | Copy with conditions | State → Exit |
| **Internal Transitions** | Copy between states | State → State |
| **Blend Trees** | Deep copy children, thresholds, parameters | Not just motion refs |
| **Transition Conditions** | Copy condition mode, parameter, threshold | Critical for weapon switching |

**Transition Condition Types to Handle:**

```csharp
// Condition modes used by Opsive weapon states:
AnimatorConditionMode.If          // Bool true
AnimatorConditionMode.IfNot       // Bool false
AnimatorConditionMode.Greater     // Float/Int >
AnimatorConditionMode.Less        // Float/Int <
AnimatorConditionMode.Equals      // Int ==
AnimatorConditionMode.NotEqual    // Int !=

// Common conditions for weapon states:
// Slot0ItemID == X           (which weapon is equipped)
// Slot0ItemStateIndex == Y   (weapon action: idle/fire/reload)
// Slot0ItemStateIndexChange  (trigger for state change)
// Aiming == true/false
// Height == 0/1              (stand/crouch blend)
```

**Requirements Checklist:**
- [ ] Copy sub-state machines with all child states
- [ ] Copy blend trees (deep copy with children and thresholds)
- [ ] Copy AnyState transitions to weapon states
- [ ] Copy ALL transition conditions (Slot0ItemID, Slot0ItemStateIndex, etc.)
- [ ] Copy transition settings (duration, offset, exit time, interruption)
- [ ] Copy state machine behaviours
- [ ] Create missing layers in target controller
- [ ] Preserve existing states (don't overwrite climbing/swimming states)
- [ ] Handle avatar mask references
- [ ] Log copied states/transitions for verification

### Phase 1: Copy Base Layer Weapon States

- [ ] Run copier tool for Base Layer
- [ ] Verify: Assault Rifle state machine copied
- [ ] Verify: Pistol state machine copied
- [ ] Verify: All 13 weapon state machines present
- [ ] Test: Existing locomotion unaffected
- [ ] Test: Existing abilities (Dodge, Roll, Climb) unaffected

### Phase 2: Copy Arm Layer States

- [ ] Copy Left Arm Layer states (Bow, Dual Pistol, Grenade, Shield)
- [ ] Copy Right Arm Layer states (all 10 weapon types)
- [ ] Verify avatar masks match (or copy masks)
- [ ] Test: Arm layer weight = 1

### Phase 3: Copy Upperbody Layer States

- [ ] Copy Interact state machine
- [ ] Copy Ride state machine (merge with existing)
- [ ] Copy Drive state machine
- [ ] Verify: IKPass = 1 for upperbody

### Phase 4: Wire Up ItemID System

Create bridge to set Slot0ItemID from ECS:

```csharp
// In ClimbAnimatorBridge.cs or new FirstPersonArmsBridge.cs
public void SetEquippedWeapon(int itemId)
{
    animator.SetInteger("Slot0ItemID", itemId);
}

public void SetWeaponState(int stateIndex, bool triggerChange = true)
{
    animator.SetInteger("Slot0ItemStateIndex", stateIndex);
    if (triggerChange)
    {
        animator.SetTrigger("Slot0ItemStateIndexChange");
    }
}
```

### Phase 5: Test Weapon Transitions

- [ ] Equip Assault Rifle → Arms animate to rifle grip
- [ ] Fire Assault Rifle → Fire animation plays
- [ ] Reload Assault Rifle → Reload animation plays
- [ ] Switch to Pistol → Unequip/Equip sequence
- [ ] Aim → Aim animation on arms
- [ ] Test each weapon type

### Phase 6: Copy Swimming Arms States

Copy swimming-specific weapons from SwimmingFirstPersonArmsDemo.controller:

**Base Layer:**
- [ ] Copy **Trident** state machine (6 states: Idle, Aim, Equip/Unequip variants)
- [ ] Copy **Underwater Gun** state machine (5 states: Idle, Fire, Dry Fire, Equip, Unequip)
- [ ] Copy AnyState transitions with Slot0ItemID conditions

**Upperbody Layer:**
- [ ] Copy **Trident** attacks (Attack 1 From Idle, Attack 1 From Aim)

**Verify:**
- [ ] Trident ItemID assigned correctly
- [ ] Underwater Gun ItemID assigned correctly
- [ ] Swimming weapons only equippable while swimming

### Phase 7: Swimming Weapon Integration

Wire up swimming weapons to ECS:

```csharp
// In OpsiveAnimatorConstants.cs
public const int ITEM_TRIDENT = 20;        // Swimming melee
public const int ITEM_UNDERWATER_GUN = 21; // Swimming ranged

// Weapon state indices (same as other weapons)
public const int WEAPON_STATE_IDLE = 0;
public const int WEAPON_STATE_EQUIP = 1;
public const int WEAPON_STATE_UNEQUIP = 2;
public const int WEAPON_STATE_FIRE = 3;
public const int WEAPON_STATE_AIM = 5;
```

**Tasks:**
- [ ] Add Trident and Underwater Gun to item database
- [ ] Create SwimmingWeaponAuthoring component
- [ ] Implement swimming weapon equip restrictions (only while IsSwimming)
- [ ] Test Trident attack animations
- [ ] Test Underwater Gun fire animations
- [ ] Verify weapon unequips when exiting water

### Phase 8: Climbing/Swimming Visibility Integration

- [ ] Verify: Arms hidden during climbing (AbilityIndex = 503)
- [ ] Verify: Arms visible during swimming with weapon equipped
- [ ] Verify: Weapon force-unequip before climb
- [ ] Verify: Swimming weapon auto-unequip when exiting water
- [ ] Add arm visibility toggle in ClimbAnimatorBridge

---

## Swimming Weapon Item IDs

| ItemID | Weapon | Available When |
|--------|--------|----------------|
| 20 | Trident | IsSwimming = true |
| 21 | Underwater Gun | IsSwimming = true |

*Note: These IDs should be configured in your item database.*

---

## Avatar Mask Requirements

### Left Arm Layer Mask

`FirstPersonLeftArmMask.mask` (GUID: 08d061450836fb24ea853ef058c47664)
- Enables: Left Shoulder, Left Upper Arm, Left Lower Arm, Left Hand
- Disables: Everything else

### Right Arm Layer Mask

`FirstPersonRightArmMask.mask` (GUID: ecdacc0cbaec28a46bc4bf0c82cd0a87)
- Enables: Right Shoulder, Right Upper Arm, Right Lower Arm, Right Hand
- Disables: Everything else

### Upperbody Layer Mask

`FirstPersonUpperbodyMask.mask` (GUID: 84906a32b90dd974eb50f12bdca50cb8)
- Enables: Spine, Chest, Shoulders, Arms, Hands, Neck, Head
- Disables: Hips, Legs

---

## ECS Integration

### New Component: EquippedWeaponState

```csharp
public struct EquippedWeaponState : IComponentData
{
    [GhostField] public int ItemId;        // Maps to Slot0ItemID
    [GhostField] public int StateIndex;    // Maps to Slot0ItemStateIndex
    [GhostField] public int SubstateIndex; // Maps to Slot0ItemSubstateIndex
    [GhostField] public bool StateChanged; // Trigger for Slot0ItemStateIndexChange
}
```

### System: WeaponAnimationStateSystem

```csharp
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(ItemEquipSystem))]
public partial struct WeaponAnimationStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Read EquippedWeaponState
        // Write to PlayerAnimationState or dedicated weapon fields
        // ClimbAnimatorBridge reads and applies to animator
    }
}
```

---

## Code Modifications Required

### New Files

| File | Purpose |
|------|---------|
| `FirstPersonArmsBridge.cs` | Manages FP arms visibility and weapon animation |
| `EquippedWeaponState.cs` | ECS component for weapon state |
| `WeaponAnimationStateSystem.cs` | Syncs weapon state to animation |
| `AnimatorArmsCopier.cs` | Editor tool to copy arm states |

### Modified Files

| File | Changes |
|------|---------|
| `ClimbAnimatorBridge.cs` | Add Slot0ItemID/StateIndex setters |
| `PlayerAnimationState.cs` | Add weapon state fields if needed |
| `ItemEquipSystem.cs` | Update EquippedWeaponState |

---

## Verification Checklist

### Controller Setup (Standard Weapons)
- [ ] All 13 Base Layer weapon state machines present
- [ ] All 4 Left Arm Layer state machines present
- [ ] All 10 Right Arm Layer state machines present
- [ ] All 8 Upperbody Layer state machines present
- [ ] AnyState transitions copied with conditions
- [ ] Blend trees properly copied
- [ ] Avatar masks assigned to layers

### Controller Setup (Swimming Weapons)
- [ ] Trident state machine in Base Layer (6 states)
- [ ] Underwater Gun state machine in Base Layer (5 states)
- [ ] Trident attacks in Upperbody Layer (2 states)
- [ ] AnyState transitions for swimming weapons work
- [ ] Swimming weapon ItemIDs configured

### Animation Playback (Standard)
- [ ] Assault Rifle idle plays
- [ ] Assault Rifle fire plays
- [ ] Assault Rifle reload plays
- [ ] Pistol animations work
- [ ] Melee weapon (Sword) attacks play
- [ ] Grenade throw plays
- [ ] Equip/Unequip transitions work

### Animation Playback (Swimming)
- [ ] Trident idle plays while swimming
- [ ] Trident aim plays
- [ ] Trident attack plays (from idle and aim)
- [ ] Trident equip/unequip transitions work
- [ ] Underwater Gun idle plays
- [ ] Underwater Gun fire plays
- [ ] Underwater Gun dry fire plays
- [ ] Underwater Gun equip/unequip transitions work

### Integration
- [ ] Weapon equip sets Slot0ItemID correctly
- [ ] Fire action triggers correct state
- [ ] Reload action triggers correct state
- [ ] Aim action triggers correct state
- [ ] Existing climbing animations still work
- [ ] Existing dodge/roll animations still work

### Swimming Integration
- [ ] Swimming weapons only equippable while IsSwimming
- [ ] Swimming weapons auto-unequip when exiting water
- [ ] Standard weapons disabled while swimming (optional)
- [ ] Arm animations visible during swimming
- [ ] Arms hidden during climbing

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Blend tree deep copy failure** | Critical | Test copier on single weapon first |
| **Avatar mask mismatch** | High | Use same masks as source |
| **Transition condition loss** | Critical | Tool must preserve all conditions |
| **Layer weight conflicts** | Medium | Verify layer weights after copy |
| **ItemID mapping mismatch** | Medium | Document ID mapping clearly |
| **Performance (many layers)** | Low | Profile if issues arise |
| **Swimming weapon state conflicts** | Medium | Test swimming weapons separately |
| **Water exit weapon handling** | Medium | Auto-unequip before exit animation |

---

## Success Criteria

### Standard Weapons
- [ ] All weapon types have working arm animations
- [ ] Fire, Reload, Aim states trigger correctly
- [ ] Equip/Unequip transitions smooth
- [ ] Existing movement/ability animations unaffected
- [ ] No animation stuck states

### Swimming Weapons
- [ ] Trident equips/unequips correctly while swimming
- [ ] Trident attack animations play
- [ ] Underwater Gun fires correctly
- [ ] Swimming weapons auto-unequip on water exit
- [ ] Swimming weapons cannot be equipped on land

### Integration
- [ ] Climbing correctly hides arms
- [ ] Swimming shows arms with weapon
- [ ] Performance acceptable

---

## Future Considerations

### View Mode Integration

Once arm animations work, integrate with ViewMode system:
- First Person mode: Show FP arms model, hide 3P body
- Third Person mode: Hide FP arms, show 3P body
- Weapon attaches to appropriate model

### VR Arms

VR mode could use same arm animations but:
- IK targets from controllers
- Different camera setup
- May need separate arm rig

### Two-Handed Weapons

Some weapons (Bow, Sniper) affect both arms:
- Both arm layers play synchronized
- BlendTree syncs left/right

---

## References

### Opsive Documentation

- Character Controller Manual: Animator Setup
- Item System: Slot-Based Animation

### DIG Documentation

- EPIC14.12: Agility Pack Integration (pattern reference)
- EPIC14.13: Swimming Pack Integration
- EPIC14.14: Ride System Integration
