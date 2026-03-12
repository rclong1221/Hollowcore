# EPIC 13.26: Opsive Animation System Migration

## Status: IN PROGRESS (IK System Cleaned & Integrated)

## Overview

Migrate from custom animator parameters (`IsClimbing`, `ClimbProgress`, etc.) to Opsive's unified `AbilityIndex`/`AbilityIntData` animation system to unlock the full 156,760 lines of pre-built transitions, blend trees, and corner turn animations in `ClimbingDemo.controller`.

---

## Current Architecture Analysis

### Bridges That Set Animator Parameters

| File | Lines | Parameters Set | Changes Needed |
|------|-------|----------------|----------------|
| [ClimbAnimatorBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs) | 515 | `IsClimbing`, `ClimbProgress`, `ClimbSpeed`, `ClimbHorizontal`, `Speed`, triggers | **Major** - Core climb param changes |
| [AnimatorRigBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs) | 270 | `MoveX`, `MoveY`, `Speed`, `IsCrouch`, `IsProne`, `IsSprint`, `IsGrounded`, `IsJumping`, `VerticalSpeed`, `Lean`, swimming params | **Moderate** - Add `HorizontalMovement`, `ForwardMovement`, `Moving`, `AbilityIndex` |
| [FallAnimatorBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/FallAnimatorBridge.cs) | ~100 | `FallStateIndex`, `FallVelocity`, `IsFalling` | **Review** - May use `AbilityIndex=102` |
| [JumpAnimationBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/JumpAnimationBridge.cs) | ~80 | `IsJumping`, triggers | **Review** - May use `AbilityIndex=103` |
| [ProneAnimatorBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/ProneAnimatorBridge.cs) | ~70 | `IsProne`, `IsCrawling` | Keep - No Opsive equivalent |
| [SlideAnimatorBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/SlideAnimatorBridge.cs) | ~120 | `IsSliding`, `SlideSpeed` | Keep - Custom ability |

### ECS Components That Track State

| Component | Location | Key Fields | Used By |
|-----------|----------|------------|---------|
| `FreeClimbState` | [FreeClimbComponents.cs:250](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Components/FreeClimbComponents.cs#L250) | `IsClimbing`, `IsTransitioning`, `IsClimbingUp` (vault), `IsWallJumping` | `PlayerAnimationStateSystem`, `FreeClimb*Systems` |
| `PlayerAnimationState` | [PlayerAnimationStateComponent.cs:12](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Components/PlayerAnimationStateComponent.cs#L12) | `IsClimbing`, `ClimbProgress`, movement/swimming state | All bridges via `IPlayerAnimationBridge.ApplyAnimationState()` |

### Current Data Flow

```
FreeClimbState.IsClimbing (ECS)
    ↓ PlayerAnimationStateSystem (line 55)
PlayerAnimationState.IsClimbing (replicated)
    ↓ PlayerAnimatorBridgeSystem
ClimbAnimatorBridge.ApplyAnimationState() (line 465)
    ↓
Animator.SetBool("IsClimbing", true)
```

---

## Opsive Parameter System

### Key Parameters in ClimbingDemo.controller

| Parameter | Type | Values | Purpose |
|-----------|------|--------|---------|
| `AbilityIndex` | int | 0=none, 501=ShortClimb, 502=Ladder, **503=FreeClimb** | Which ability is active |
| `AbilityChange` | bool | Trigger | Pulse true on ability change |
| `AbilityIntData` | int | 0-8 (see below) | Sub-state within ability |
| `AbilityFloatData` | float | -1 to 1 | Move direction blend |
| `HorizontalMovement` | float | -1 to 1 | Left/right input |
| `ForwardMovement` | float | -1 to 1 | Up/down input |
| `LegIndex` | int | 0 or 1 | Which leg is stepping |
| `Speed` | float | 0+ | ✅ Already used |
| `Moving` | bool | | Is character moving |

### FreeClimb AbilityIntData Values

From [FreeClimb.cs ClimbState enum](file:///Users/dollerinho/Desktop/DIG/OPSIVE/untitled%20folder/Opsive/UltimateCharacterController/Add-Ons/Climbing/Scripts/FreeClimb.cs#L121):

| Value | Name | Your ECS Equivalent |
|-------|------|---------------------|
| 0 | BottomMount | `FreeClimbState.IsTransitioning` + just mounted |
| 1 | TopMount | Mount from above (not implemented) |
| 2 | Climb | `FreeClimbState.IsClimbing && !IsTransitioning` |
| 3 | InnerCorner | 90° turn into wall (not implemented) |
| 4 | OuterCorner | 90° turn away from wall (not implemented) |
| 5 | BottomDismount | Dismount + input down |
| 6 | TopDismount | `FreeClimbState.IsClimbingUp` (vault) |
| 7 | HorizontalHangStart | From hang (not implemented) |
| 8 | VerticalHangStart | From hang (not implemented) |

---

## Implementation Phases

### Phase 1: Create Constants File
**File:** `Assets/Scripts/Player/Animation/OpsiveAnimatorConstants.cs` [NEW]

```csharp
public static class OpsiveAnimatorConstants
{
    // Ability Indices
    public const int ABILITY_NONE = 0;
    public const int ABILITY_SHORT_CLIMB = 501;
    public const int ABILITY_LADDER_CLIMB = 502;
    public const int ABILITY_FREE_CLIMB = 503;
    
    // FreeClimb AbilityIntData
    public const int CLIMB_BOTTOM_MOUNT = 0;
    public const int CLIMB_TOP_MOUNT = 1;
    public const int CLIMB_CLIMBING = 2;
    public const int CLIMB_INNER_CORNER = 3;
    public const int CLIMB_OUTER_CORNER = 4;
    public const int CLIMB_BOTTOM_DISMOUNT = 5;
    public const int CLIMB_TOP_DISMOUNT = 6;
}
```

---

### Phase 2: Modify PlayerAnimationState Component
**File:** [PlayerAnimationStateComponent.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Components/PlayerAnimationStateComponent.cs)

**Add new fields (line 39+):**
```csharp
[GhostField] public int AbilityIndex;       // Currently active ability
[GhostField] public int AbilityIntData;     // Sub-state within ability
[GhostField] public float AbilityFloatData; // Blend direction
[GhostField] public bool AbilityChange;     // Trigger on transitions
```

**Keep existing fields** - Use for bridge internal tracking, just don't send to Animator.

---

### Phase 3: Modify PlayerAnimationStateSystem
**File:** [PlayerAnimationStateSystem.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Systems/PlayerAnimationStateSystem.cs)

**Update OnUpdate() (line 52-63) to set Opsive params:**

```csharp
if (hasClimbing)
{
    var cs = climbingLookup[entity];
    
    // Opsive params
    if (cs.IsClimbing || cs.IsTransitioning)
    {
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_FREE_CLIMB;
        
        // Determine sub-state
        if (cs.IsClimbingUp)
            anim.AbilityIntData = OpsiveAnimatorConstants.CLIMB_TOP_DISMOUNT;
        else if (cs.IsTransitioning && anim.AbilityIntData == 0)
            anim.AbilityIntData = OpsiveAnimatorConstants.CLIMB_BOTTOM_MOUNT;
        else if (!cs.IsTransitioning)
            anim.AbilityIntData = OpsiveAnimatorConstants.CLIMB_CLIMBING;
            
        // Movement direction for blend trees
        anim.AbilityFloatData = input.Horizontal;
    }
    else
    {
        // Just stopped climbing - trigger dismount
        if (anim.AbilityIndex == OpsiveAnimatorConstants.ABILITY_FREE_CLIMB)
        {
            anim.AbilityIntData = OpsiveAnimatorConstants.CLIMB_BOTTOM_DISMOUNT;
            anim.AbilityChange = true;
        }
        anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_NONE;
    }
    
    // Keep legacy for internal tracking (don't remove yet)
    anim.IsClimbing = cs.IsClimbing;
    anim.ClimbProgress = cs.IsClimbing ? 0.5f : 0f;
}
```

---

### Phase 4: Modify ClimbAnimatorBridge
**File:** [ClimbAnimatorBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs)

**Add new serialized fields (after line 53):**
```csharp
[Header("Opsive Parameters")]
[SerializeField] string paramAbilityIndex = "AbilityIndex";
[SerializeField] string paramAbilityChange = "AbilityChange";
[SerializeField] string paramAbilityIntData = "AbilityIntData";
[SerializeField] string paramAbilityFloatData = "AbilityFloatData";
[SerializeField] string paramHorizontalMovement = "HorizontalMovement";
[SerializeField] string paramForwardMovement = "ForwardMovement";
```

**Add hash fields (after line 141):**
```csharp
private int h_AbilityIndex;
private int h_AbilityChange;
private int h_AbilityIntData;
private int h_AbilityFloatData;
private int h_HorizontalMovement;
private int h_ForwardMovement;
```

**Update CacheHashes() (line 278)** to include new hashes.

**Update ApplyAnimationState() (line 465):**
```csharp
// Set Opsive params
if (h_AbilityIndex != 0)
    animator.SetInteger(h_AbilityIndex, state.AbilityIndex);
if (h_AbilityChange != 0 && state.AbilityChange)
    animator.SetTrigger(h_AbilityChange);
if (h_AbilityIntData != 0)
    animator.SetInteger(h_AbilityIntData, state.AbilityIntData);
if (h_AbilityFloatData != 0)
    animator.SetFloat(h_AbilityFloatData, state.AbilityFloatData);

// Movement for blend trees
if (h_HorizontalMovement != 0)
    animator.SetFloat(h_HorizontalMovement, state.MoveInput.x);
if (h_ForwardMovement != 0)
    animator.SetFloat(h_ForwardMovement, state.MoveInput.y);
```

---

### Phase 5: Modify AnimatorRigBridge
**File:** [AnimatorRigBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs)

**Add after line 33:**
```csharp
[Header("Opsive Locomotion")]
[SerializeField] string ParamHorizontalMovement = "HorizontalMovement";
[SerializeField] string ParamForwardMovement = "ForwardMovement";
[SerializeField] string ParamMoving = "Moving";
```

**Add hash caching and set in ApplyAnimationState().**

---

### Phase 6: IK Integration
**File:** `Assets/Scripts/Player/Animation/OpsiveClimbingIK.cs` [NEW]

We have replaced the legacy procedural IK system (`FreeClimbIKController.cs`, `ProceduralClimbingIK.cs`, `TwoBoneIK.cs`) with a unified solution:

1. **OpsiveClimbingIK**: A new component that exactly replicates Opsive's `CharacterIKBase` logic within the standardized `OnAnimatorIK` callback. It handles hand/foot placement by raycasting against the wall normal provided by ECS.
2. **Bridge Integration**: `PlayerAnimatorBridgeSystem` feeds ECS climbing state (Grip Position, Wall Normal) directly to `ClimbAnimatorBridge`, which updates the `OpsiveClimbingIK` component.
3. **Clean Architecture**: Removed intermediate procedural controllers and `ClimbingSkeleton` caching.

**Benefits:**
- Exact visual parity with Opsive behavior.
- Support for Unity's humanoid retargeting.
- Removed ~1000 lines of complex procedural code.

---

### Phase 7: Prefab & Controller Updates

1. Assign `ClimbingDemo.controller` to character Animator
2. Configure each FBX: Rig → Humanoid → Create From This Model
3. Update Inspector fields on bridges to Opsive param names

---

## Files Changed Summary

| File | Action | Effort |
|------|--------|--------|
| `OpsiveAnimatorConstants.cs` | NEW | 30 min |
| `PlayerAnimationStateComponent.cs` | MODIFY - add 4 fields | 15 min |
| `PlayerAnimationStateSystem.cs` | MODIFY - Opsive state mapping | 2 hr |
| `ClimbAnimatorBridge.cs` | MODIFY - Opsive params + IK Simplify | 1 hr |
| `AnimatorRigBridge.cs` | MODIFY - add locomotion params | 30 min |
| `OpsiveClimbingIK.cs` | NEW | 1 hr |
| `FreeClimbIKController.cs` | DELETE | - |
| `ProceduralClimbingIK.cs` | DELETE | - |
| `ClimbIKSolvers.cs` | DELETE | - |
| `ClimbingSkeleton.cs` | DELETE | - |
| Character Prefab | UPDATE - Animator Controller | 30 min |

**Total Estimate:** 5-6 hours coding + 2-3 hours testing

---

## Testing Checklist

### Locomotion
- [ ] Idle plays when stationary
- [ ] Walk/run blends with `Speed`
- [ ] `HorizontalMovement`/`ForwardMovement` drive strafe blend trees

### Climbing
- [ ] `AbilityIndex=503` triggers climb layer
- [ ] BottomMount animation plays on mount
- [ ] Climbing animation blends with input
- [ ] TopDismount (vault) plays when `IsClimbingUp=true`
- [ ] BottomDismount plays when dropping off

### Transitions
- [ ] Smooth blend into climbing
- [ ] Smooth blend out of climbing
- [ ] No T-pose during transitions

---

## Rollback Plan

```bash
git log --oneline -5
git reset --hard <pre-migration-commit>
```

---

## Dependencies

- EPIC 13.20: Free Climb System (✅ Complete)
- EPIC 13.25: Procedural Climbing IK (In Progress)
- Opsive ClimbingDemo.controller (✅ Copied)

---

## Appendix A: Complete Opsive Animator Parameter Reference

This section documents ALL animator parameters found in the Opsive system and related animation bridges.

### Parameter Type Legend
| Type ID | Type Name | Description |
|---------|-----------|-------------|
| 1 | Float | Continuous value (-1 to 1, 0 to 1, etc.) |
| 3 | Integer | Discrete value (state IDs, indices) |
| 4 | Bool | True/false toggle |
| 9 | Trigger | One-shot pulse (auto-resets) |

---

### A.1 Opsive Core Locomotion Parameters

These parameters are defined in `Demo.controller` and `ClimbingDemo.controller`:

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `HorizontalMovement` | Float | -1 to 1 | Horizontal input for blend trees (strafe left/right) |
| `ForwardMovement` | Float | -1 to 1 | Forward/backward movement input for blend trees |
| `Speed` | Float | 0+ | Overall movement speed (0=Idle, 1=Walk, 2-3=Run/Sprint) |
| `Moving` | Bool | true/false | Is character currently moving |
| `Height` | Float | 0-2 | Height state (0=Standing, 1=Crouching, 2=Prone) |
| `Pitch` | Float | degrees | Pitch angle for up/down looking |
| `Yaw` | Float | degrees | Yaw angle for left/right looking |
| `LegIndex` | Float | 0 or 1 | Which leg is stepping for footstep blending |
| `MovementSetID` | Integer | 0, 2, etc. | Movement set (0=Combat, 2=Adventure) |
| `Aiming` | Bool | true/false | Is character aiming a weapon |

---

### A.2 Opsive Ability System Parameters

The core ability state machine parameters:

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `AbilityIndex` | Integer | See table below | Active ability ID (0=None) |
| `AbilityChange` | Trigger | pulse | Triggered when ability changes (fires once per transition) |
| `AbilityIntData` | Integer | 0-8+ | Sub-state within ability (ability-specific meaning) |
| `AbilityFloatData` | Float | -1 to 1 | Blend direction within ability |

#### AbilityIndex Values (from `[DefaultAbilityIndex]` attributes in Opsive source)

| Value | Ability Name | Source File | Description |
|-------|--------------|-------------|-------------|
| 0 | None | - | No ability active |
| 1 | Jump | Jump.cs | Jumping |
| 3 | HeightChange (Crouch) | HeightChange.cs | Crouching/prone stance |
| 6 | SpeedChange (Sprint) | SpeedChange.cs | Sprinting |
| 102 | Fall | Fall.cs | Falling |
| 104 | Hang | Hang.cs (Agility Pack) | Hanging from ledge |
| 301 | Swim | Swim.cs (Swimming Pack) | Swimming in water |
| 501 | ShortClimb | ShortClimb.cs (Climbing Pack) | Short climb/vault |
| 502 | LadderClimb | LadderClimb.cs (Climbing Pack) | Ladder climbing |
| 503 | FreeClimb | FreeClimb.cs (Climbing Pack) | Free climbing on surfaces |

#### FreeClimb AbilityIntData Values (AbilityIndex=503)

From `FreeClimb.ClimbState` enum in `OPSIVE/untitled folder/.../Climbing/Scripts/FreeClimb.cs:121`:

| Value | State Name | Description |
|-------|------------|-------------|
| 0 | BottomMount | Mounting from below |
| 1 | TopMount | Mounting from above |
| 2 | Climb | Active climbing movement |
| 3 | InnerCorner | 90° turn into wall |
| 4 | OuterCorner | 90° turn away from wall |
| 5 | BottomDismount | Dismounting downward |
| 6 | TopDismount | Vaulting over ledge |
| 7 | HorizontalHangStart | Starting from horizontal hang |
| 8 | VerticalHangStart | Starting from vertical hang |
| 9 | None | No climb state |

#### LadderClimb AbilityIntData Values (AbilityIndex=502)

From `LadderClimb.ClimbState` enum in `OPSIVE/untitled folder/.../Climbing/Scripts/LadderClimb.cs:75`:

| Value | State Name | Description |
|-------|------------|-------------|
| 0 | BottomMount | Mounting from bottom |
| 1 | TopMount | Mounting from top |
| 2 | AirMount | Mounting from air |
| 3 | Climb | Active climbing |
| 4 | BottomDismount | Dismounting from bottom |
| 5 | TopDismount | Dismounting from top |
| 6 | HangStart | Starting from Hang ability |
| 7 | None | No state |

#### Hang AbilityIntData Values (AbilityIndex=104)

From `Hang.HangState` enum in `OPSIVE/untitled folder/.../Agility/Scripts/Hang.cs:127`:

| Value | State Name | Description |
|-------|------------|-------------|
| 0 | MoveToStart | Moving into starting position |
| 1 | DropToStart | Dropping to start position |
| 2 | Shimmy | Shimmying across object |
| 3 | TransferUp | Transferring vertically up |
| 4 | TransferRight | Transferring horizontally right |
| 5 | TransferLeft | Transferring horizontally left |
| 6 | TransferDown | Transferring vertically down |
| 7 | PullUp | Pulling up to end ability |
| 8 | LadderClimbStart | Starting from Ladder Climb |
| 9 | HorizontalFreeClimbStart | Starting from horizontal Free Climb |
| 10 | VerticalFreeClimbStart | Starting from vertical Free Climb |
| 11 | None | No state |

#### Swim AbilityIntData Values (AbilityIndex=301)

From `Swim.SwimStates` enum in `OPSIVE/untitled folder/.../Swimming/Scripts/Swim.cs:106`:

| Value | State Name | Description |
|-------|------------|-------------|
| 0 | EnterWaterFromAir | Entered water from air |
| 1 | SurfaceSwim | Swimming on surface |
| 2 | UnderwaterSwim | Swimming underwater |
| 3 | ExitWaterMoving | Exiting while moving |
| 4 | ExitWaterIdle | Exiting while idle |

---

### A.3 Player Animation Bridge Parameters

These are set by the custom bridge scripts in `Assets/Scripts/Player/Bridges/`:

#### AnimatorRigBridge.cs - Standard Movement

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `MoveX` | Float | -1 to 1 | Horizontal input axis |
| `MoveY` | Float | -1 to 1 | Vertical input axis |
| `IsSprint` | Bool | true/false | Is character sprinting |
| `IsCrouch` | Bool | true/false | Is character crouching |
| `IsProne` | Bool | true/false | Is character in prone state |
| `IsGrounded` | Bool | true/false | Is character on ground |
| `IsJumping` | Bool | true/false | Is character jumping/in air |
| `VerticalSpeed` | Float | m/s | Vertical velocity for jump/fall blending |
| `Lean` | Float | -1 to 1 | Lean angle for turning |
| `IsSliding` | Bool | true/false | Is character sliding |

#### AnimatorRigBridge.cs - Swimming

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsSwimming` | Bool | true/false | Is character swimming |
| `IsUnderwater` | Bool | true/false | Is character underwater |
| `SwimActionState` | Integer | state ID | Current swim action state |
| `SwimInputMagnitude` | Float | 0-1 | Swim input intensity |

#### AnimatorRigBridge.cs & LandingAnimatorBridge.cs - Landing

| Parameter Name | Type | Source | Purpose |
|----------------|------|--------|---------|
| `LandingTrigger` | Trigger | AnimatorRigBridge | Triggered when landing |
| `LandTrigger` | Trigger | ProneAnimatorBridge, FallAnimatorBridge | Alternate landing trigger |
| `LandingIntensity` | Float | LandingAnimatorBridge | Landing impact intensity |
| `IsRecovering` | Bool | LandingAnimatorBridge | Is recovering from landing |
| `RecoveryProgress` | Float | LandingAnimatorBridge | Recovery animation progress |

---

### A.4 Climbing Parameters (ClimbAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsClimbing` | Bool | true/false | Is character currently climbing |
| `ClimbProgress` | Float | 0-1 | Climb progress (0=bottom, 1=top) |
| `ClimbSpeed` | Float | 0+ | Vertical climb speed for animation blending |
| `ClimbHorizontal` | Float | -1 to 1 | Horizontal movement during climbing |
| `GrabTrigger` | Trigger | pulse | Fired when grabbing new anchor |
| `ReleaseTrigger` | Trigger | pulse | Fired when releasing/dismounting |
| `ClimbUpTrigger` | Trigger | pulse | Fired when climbing up/vaulting over ledge |

---

### A.5 Prone/Crouch Parameters (ProneAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsCrouching` | Bool | true/false | Is character crouching |
| `IsCrawling` | Bool | true/false | Is character crawling (moving while prone) |
| `ProneBlend` | Float | 0-1 | Blend value (0=stand, 0.5=crouch, 1=prone) |

---

### A.6 Slide Parameters (SlideAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsSliding` | Bool | true/false | Is character sliding |
| `SlideSpeed` | Float | m/s | Current slide speed |
| `SlideTriggerType` | Integer | 0-2 | Slide type (0=Manual, 1=Slope, 2=Slippery) |

---

### A.7 Mantle/Vault Parameters (MantleAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `MantleTrigger` | Trigger | pulse | Start mantle animation |
| `IsMantling` | Bool | true/false | Is character mantling |
| `MantleProgress` | Float | 0-1 | Mantle animation progress |
| `VaultTrigger` | Trigger | pulse | Start vault animation |
| `IsVaulting` | Bool | true/false | Is character vaulting |
| `VaultProgress` | Float | 0-1 | Vault animation progress |

---

### A.8 Dodge Parameters

#### DodgeRollAnimatorBridge.cs

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `RollTrigger` | Trigger | pulse | Start roll animation |
| `IsRolling` | Bool | true/false | Is character rolling |
| `RollProgress` | Float | 0-1 | Roll animation progress |

#### DodgeDiveAnimatorBridge.cs

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `DodgeDive` | Trigger | pulse | Start dodge/dive animation |
| `IsDiving` | Bool | true/false | Is character diving |
| `DiveProgress` | Float | 0-1 | Dive animation progress |

---

### A.9 Fall Parameters (FallAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsFalling` | Bool | true/false | Is character falling |
| `FallVelocity` | Float | m/s | Fall speed/velocity for animation blending |
| `FallStateIndex` | Integer | state ID | Current fall state index |
| `LandTrigger` | Trigger | pulse | Triggered on landing |

---

### A.10 Combat Parameters

#### TackleAnimatorBridge.cs

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `TackleTrigger` | Trigger | pulse | Start tackle lunge |
| `TackleHitTrigger` | Trigger | pulse | Successful hit reaction |
| `TackleMissTrigger` | Trigger | pulse | Whiff/stumble reaction |
| `IsTackling` | Bool | true/false | Is character tackling |
| `TackleSpeed` | Float | 0-1 | Tackle speed/intensity |

#### KnockdownAnimatorBridge.cs

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `StaggerTrigger` | Trigger | pulse | Start stagger animation |
| `IsStaggered` | Bool | true/false | Is character staggered |
| `StaggerIntensity` | Float | 0-1 | Stagger intensity |
| `KnockdownTrigger` | Trigger | pulse | Start knockdown animation |
| `RecoveryTrigger` | Trigger | pulse | Start get-up animation |
| `IsKnockedDown` | Bool | true/false | Is character knocked down |
| `IsRecovering` | Bool | true/false | Is character recovering (getting up) |
| `KnockdownIntensity` | Float | 0-1 | Knockdown intensity based on impact |

---

### A.11 Interaction Parameters (InteractionAnimatorBridge.cs)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `IsInteracting` | Bool | true/false | Is character interacting with object |
| `InteractionId` | Integer | ID | ID of current interaction |
| `InteractTrigger` | Trigger | pulse | Start interaction animation |
| `InteractionProgress` | Float | 0-1 | Interaction completion progress |
| `InteractionPhase` | Integer | phase ID | Current interaction phase |

---

### A.12 Item/Equipment Parameters (Demo.controller)

| Parameter Name | Type | Range/Values | Purpose |
|----------------|------|--------------|---------|
| `Slot0ItemID` | Integer | item ID | Item ID in slot 0 |
| `Slot0ItemStateIndex` | Integer | state ID | Animation state of slot 0 item |
| `Slot0ItemStateIndexChange` | Trigger | pulse | Triggered on slot 0 state change |
| `Slot0ItemSubstateIndex` | Integer | substate ID | Sub-state of slot 0 item |
| `Slot1ItemID` | Integer | item ID | Item ID in slot 1 |
| `Slot1ItemStateIndex` | Integer | state ID | Animation state of slot 1 item |
| `Slot1ItemStateIndexChange` | Trigger | pulse | Triggered on slot 1 state change |
| `Slot1ItemSubstateIndex` | Integer | substate ID | Sub-state of slot 1 item |
| `PrimaryItemID` | Integer | item ID | Primary weapon item ID |
| `PrimaryItemStateIndex` | Integer | state ID | Primary weapon state |
| `SecondaryItemStateIndex` | Integer | state ID | Secondary weapon state |
| `FirstPerson` | Bool | true/false | Is in first-person view |

---

### A.13 Parameter Statistics Summary

| Category | Float | Integer | Bool | Trigger | Total |
|----------|-------|---------|------|---------|-------|
| Locomotion | 6 | 1 | 2 | 0 | 9 |
| Ability System | 1 | 2 | 0 | 1 | 4 |
| Movement/Stance | 4 | 0 | 6 | 0 | 10 |
| Swimming | 1 | 1 | 2 | 0 | 4 |
| Landing | 2 | 0 | 1 | 2 | 5 |
| Climbing | 2 | 0 | 1 | 3 | 6 |
| Prone/Crouch | 1 | 0 | 2 | 0 | 3 |
| Slide | 1 | 1 | 1 | 0 | 3 |
| Mantle/Vault | 2 | 0 | 2 | 2 | 6 |
| Dodge | 2 | 0 | 2 | 2 | 6 |
| Fall | 1 | 1 | 1 | 1 | 4 |
| Combat | 2 | 0 | 4 | 5 | 11 |
| Interaction | 1 | 2 | 1 | 1 | 5 |
| Equipment | 0 | 9 | 1 | 2 | 12 |
| **TOTAL** | **26** | **17** | **26** | **19** | **88** |

---

### A.14 Key Source Files

#### Project Animation Bridges

| File | Description |
|------|-------------|
| `Assets/Scripts/Player/Animation/OpsiveAnimatorConstants.cs` | Core ability index constants |
| `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs` | Main locomotion parameters |
| `Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs` | Climbing parameters |
| `Assets/Art/Animations/Opsive/Animator/Characters/Demo.controller` | Main animator controller |
| `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller` | Climbing animator controller |

#### Opsive Core Source Files

| File | Description |
|------|-------------|
| `OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/AnimatorMonitor.cs` | Core animator parameter definitions and hash caching |
| `OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/Abilities/Ability.cs` | Base ability class with AbilityIndexParameter |

#### Opsive Add-On Ability Source Files

| File | DefaultAbilityIndex | Description |
|------|---------------------|-------------|
| `OPSIVE/untitled folder/.../Climbing/Scripts/FreeClimb.cs` | 503 | Free climbing ability with ClimbState enum |
| `OPSIVE/untitled folder/.../Climbing/Scripts/LadderClimb.cs` | 501 | Ladder climbing with ClimbState enum |
| `OPSIVE/untitled folder/.../Climbing/Scripts/ShortClimb.cs` | 501 | Short climb/vault ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/Hang.cs` | 104 | Hanging from ledges with HangState enum |
| `OPSIVE/untitled folder/.../Agility/Scripts/Dodge.cs` | - | Dodge ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/Roll.cs` | - | Roll ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/Vault.cs` | - | Vault ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/Balance.cs` | - | Balance ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/Crawl.cs` | - | Crawl ability |
| `OPSIVE/untitled folder/.../Agility/Scripts/LedgeStrafe.cs` | - | Ledge strafe ability |
| `OPSIVE/untitled folder/.../Swimming/Scripts/Swim.cs` | 301 | Swimming with SwimStates enum |
| `OPSIVE/untitled folder/.../Swimming/Scripts/Dive.cs` | - | Diving ability |

---

### A.15 Parameter Caching Best Practice

All parameters should be cached using `Animator.StringToHash()` for performance:

```csharp
// In class fields
private int h_AbilityIndex;
private int h_Speed;
private int h_Moving;

// In initialization
void CacheHashes()
{
    h_AbilityIndex = Animator.StringToHash("AbilityIndex");
    h_Speed = Animator.StringToHash("Speed");
    h_Moving = Animator.StringToHash("Moving");
}

// In update
animator.SetInteger(h_AbilityIndex, 503);
animator.SetFloat(h_Speed, 2.0f);
animator.SetBool(h_Moving, true);
```

Always validate parameter existence before use to prevent runtime errors.
