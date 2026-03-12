# EPIC14.13 - Opsive Swimming Pack Animation Integration

**Status:** Planning
**Dependencies:** EPIC14.12 (Agility Pack / State Copier Tool), ClimbingDemo.controller
**Goal:** Integrate Opsive Swimming Pack animations into DIG's hybrid ECS/MonoBehaviour animation system.

---

## Overview

The Opsive Swimming Pack provides water-based movement animations (swimming, diving, drowning, climb from water). DIG already has a **fully functional ECS swimming system** (`SwimmingState`, `BreathState`, etc.) but currently uses a custom `SwimActionState` pattern for animation. This EPIC will add the Opsive animation states to leverage their polished swimming animations.

**Source Controller:** `Assets/Art/Animations/Opsive/AddOns/Swimming/SwimmingDemo.controller`
**Target Controller:** `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller`

---

## Current Architecture

### Existing ECS Swimming System (Already Implemented)

```
┌─────────────────────────────────────────────────────────────────┐
│ ECS Components (Assets/Scripts/Swimming/Components/)            │
├─────────────────────────────────────────────────────────────────┤
│ SwimmingState : IComponentData                                  │
│   - IsSwimming (bool)                                           │
│   - IsSubmerged (bool) - head underwater                        │
│   - WaterSurfaceY, SubmersionDepth                              │
│                                                                 │
│ BreathState : IComponentData                                    │
│   - CurrentBreath, MaxBreath                                    │
│   - IsHoldingBreath                                             │
│   - DrowningDamageTimer, DrowningDamagePerTick                  │
│                                                                 │
│ SwimmingEvents : IComponentData                                 │
│   - OnEnterWater, OnExitWater (one-frame flags)                 │
│   - OnSurface, OnSubmerge                                       │
│   - OnStartSwimming, OnStopSwimming                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ ECS Systems (Assets/Scripts/Swimming/Systems/)                  │
├─────────────────────────────────────────────────────────────────┤
│ WaterDetectionSystem - Detects water zones, sets SwimmingState  │
│ SwimmingMovementSystem - Handles swimming physics               │
│ SwimmingControllerSystem - Manages collider/gravity changes     │
│ SwimmingEventSystem - Fires one-frame event flags               │
│ DrowningSystem - Handles breath and damage                      │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ PlayerAnimationStateSystem (Already Integrates Swimming)        │
├─────────────────────────────────────────────────────────────────┤
│ Reads SwimmingState, sets PlayerAnimationState:                 │
│   - anim.IsSwimming = ss.IsSwimming                             │
│   - anim.IsUnderwater = ss.IsSubmerged                          │
│   - anim.SwimActionState (0-4 custom pattern)                   │
│   - anim.SwimInputMagnitude                                     │
│                                                                 │
│ MISSING: Does NOT set AbilityIndex = 301 for Opsive animations  │
└─────────────────────────────────────────────────────────────────┘
```

### Current SwimActionState (Custom Pattern)

```csharp
// Current implementation in PlayerAnimationStateSystem.cs:210-260
// Uses custom SwimActionState values (Invector-style):
// 0 = Not swimming
// 1 = Surface swimming
// 2 = Underwater
// 3 = Swimming down
// 4 = Swimming up
```

### Required: Add Opsive AbilityIndex Support

The Opsive SwimmingDemo.controller uses `AbilityIndex` + `AbilityIntData`:

| Ability | AbilityIndex | AbilityIntData Values |
|---------|--------------|----------------------|
| **Swim** | 301 | 0=EnterFromAir, 1=Surface, 2=Underwater, 3=ExitMoving, 4=ExitIdle |
| **Dive** | 302 | 0=Shallow, 1=High, 2=EnterWater |
| **ClimbFromWater** | 303 | 0=NotInPosition, 1=IdleClimb, 2=MovingClimb |
| **Drown** | 304 | N/A |

---

## AnimatorMonitor Disable

Same as EPIC14.12 - Opsive's `AnimatorMonitor` is already disabled in `ClimbAnimatorBridge.Awake()`:

```csharp
void DisableOpsiveAnimatorMonitor()
{
    var opsiveMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.AnimatorMonitor>();
    if (opsiveMonitor != null)
    {
        opsiveMonitor.enabled = false;
    }

    var childMonitor = animator.GetComponent<Opsive.UltimateCharacterController.Character.ChildAnimatorMonitor>();
    if (childMonitor != null)
    {
        childMonitor.enabled = false;
    }
}
```

This ensures only our ECS systems control `AbilityIndex`.

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.12** | State Copier tool needed to copy swimming states + transitions |
| **13.26** | ClimbAnimatorBridge handles AbilityIndex for all abilities |
| **12.3** | Existing swimming ECS system (SwimmingState, BreathState) |

---

## Swimming Abilities Summary

| Ability | AbilityIndex | Opsive Class | DIG Status |
|---------|--------------|--------------|------------|
| **Swim** | 301 | `Swim.cs` | ECS exists (`SwimmingState`), needs Opsive animation hookup |
| **Dive** | 302 | `Dive.cs` | NOT implemented - platform dive into water |
| **ClimbFromWater** | 303 | `ClimbFromWater.cs` | NOT implemented - climb out of water onto ledge |
| **Drown** | 304 | `Drown.cs` | ECS exists (`BreathState`, `DrowningSystem`), needs animation |

---

## AbilityIntData Values Per Ability

### Swim (301) - from `Swim.cs:106-112`

| IntData | SwimStates Enum | Description |
|---------|-----------------|-------------|
| 0 | EnterWaterFromAir | Character fell/jumped into water |
| 1 | SurfaceSwim | Swimming on the surface |
| 2 | UnderwaterSwim | Swimming underwater |
| 3 | ExitWaterMoving | Exiting water while moving forward |
| 4 | ExitWaterIdle | Exiting water while idle |

### Dive (302) - from `Dive.cs:56-60`

| IntData | DiveStates Enum | Description |
|---------|-----------------|-------------|
| 0 | Shallow | Dive from low height |
| 1 | High | Dive from high platform |
| 2 | EnterWater | About to enter water |

### ClimbFromWater (303) - from `ClimbFromWater.cs:54`

| IntData | Description |
|---------|-------------|
| 0 | Not in position (moving to climb point) |
| 1 | Idle climb (wasn't moving when started) |
| 2 | Moving climb (was moving when started) |

### Drown (304)

- No IntData variants - single drown animation

---

## Missing States from Analyzer (Screenshot)

### Base Layer (18 missing)

**Swim States:**
- Swim/Fall In Water
- Swim/Surface Idle
- Swim/Surface Swim
- Swim/Underwater Swim
- Swim/Dive From Surface
- Swim/Exit Water Moving
- Swim/Exit Water Idle

**Dive States:**
- Dive/Shallow Dive/Dive Start
- Dive/Shallow Dive/Enter Water
- Dive/Shallow Dive/End
- Dive/Shallow Dive/Dive Fall
- Dive/High Dive/End
- Dive/High Dive/Enter Water
- Dive/High Dive/Dive Start
- Dive/High Dive/Dive Fall

**Climb From Water States:**
- Climb From Water/Climb From Water Moving
- Climb From Water/Climb From Water Idle

**Drown State:**
- Drown/Drown

### Full Body Layer (5 missing)

- Sword/Attack Idle
- Body/Attack Idle
- Knife/Attack Idle
- Katana/Attack Idle
- Sword Shield/Attack Idle

(Note: Full Body Layer states are weapon-related, not swimming-specific)

---

## State Copier Tool Limitations

Same as EPIC14.12 - the current `AnimatorStateCopier.cs` only copies:
- ✅ States with motions
- ✅ StateMachineBehaviours

Does NOT copy:
- ❌ Transitions
- ❌ Transition conditions (AbilityIndex == 301, etc.)
- ❌ Parameters

**Blocked by EPIC14.12 Phase 0** - Need the new `AnimatorAbilityCopier` tool first.

---

## Integration Architecture

### Option A: Map SwimActionState to AbilityIntData (Simple)

Modify `PlayerAnimationStateSystem` to set `AbilityIndex = 301` when swimming and map existing `SwimActionState` to Opsive's `AbilityIntData`:

```csharp
// Current SwimActionState → Opsive AbilityIntData mapping
// 0 (Not swimming) → AbilityIndex = 0
// 1 (Surface)      → AbilityIndex = 301, IntData = 1 (SurfaceSwim)
// 2 (Underwater)   → AbilityIndex = 301, IntData = 2 (UnderwaterSwim)
// 3 (Swimming down)→ AbilityIndex = 301, IntData = 2 (UnderwaterSwim)
// 4 (Swimming up)  → AbilityIndex = 301, IntData = 2 (UnderwaterSwim)
```

**Pros:** Minimal code change, uses existing ECS system
**Cons:** Loses down/up distinction in animation, no enter/exit animations

### Option B: Full Opsive IntData Support (Recommended)

Add new fields to track swim sub-states and map fully:

```csharp
// In SwimmingState or PlayerAnimationState:
public int OpsiveSwimState; // 0-4 matching Opsive's SwimStates enum

// In PlayerAnimationStateSystem:
if (ss.IsSwimming)
{
    anim.AbilityIndex = 301; // ABILITY_SWIM
    anim.AbilityIntData = ss.OpsiveSwimState;
    // Fire AbilityChange on state transitions
}
```

**Pros:** Full animation support including enter/exit
**Cons:** Need to add state tracking logic

---

## Implementation Plan

### Phase 0: Blocked by EPIC14.12
- [ ] Complete EPIC14.12 Phase 0 (AnimatorAbilityCopier tool)

### Phase 1: Copy Swimming States + Transitions
- [ ] Run AnimatorAbilityCopier for Swim (301) states
- [ ] Run AnimatorAbilityCopier for Dive (302) states
- [ ] Run AnimatorAbilityCopier for ClimbFromWater (303) states
- [ ] Run AnimatorAbilityCopier for Drown (304) states
- [ ] Verify parameters exist: AbilityIndex, AbilityChange, AbilityIntData, AbilityFloatData

### Phase 2: Add Swimming Constants
- [ ] Add to `OpsiveAnimatorConstants.cs`:
  ```csharp
  // Swimming Pack abilities (301-304)
  public const int ABILITY_SWIM = 301;
  public const int ABILITY_DIVE = 302;
  public const int ABILITY_CLIMB_FROM_WATER = 303;
  public const int ABILITY_DROWN = 304;

  // Swim IntData (SwimStates enum)
  public const int SWIM_ENTER_FROM_AIR = 0;
  public const int SWIM_SURFACE = 1;
  public const int SWIM_UNDERWATER = 2;
  public const int SWIM_EXIT_MOVING = 3;
  public const int SWIM_EXIT_IDLE = 4;

  // Dive IntData
  public const int DIVE_SHALLOW = 0;
  public const int DIVE_HIGH = 1;
  public const int DIVE_ENTER_WATER = 2;

  // ClimbFromWater IntData
  public const int CLIMB_WATER_NOT_IN_POSITION = 0;
  public const int CLIMB_WATER_IDLE = 1;
  public const int CLIMB_WATER_MOVING = 2;
  ```

### Phase 3: Update PlayerAnimationStateSystem
- [ ] Add swim state tracking (enter/exit transitions)
- [ ] Set `AbilityIndex = 301` when swimming
- [ ] Map to correct `AbilityIntData` based on swim state
- [ ] Fire `AbilityChange` on swim state transitions
- [ ] Add AbilityFloatData for pitch-based underwater movement

### Phase 4: Add Dive ECS Components (Optional)
- [ ] Create `DiveState` component
- [ ] Create `DiveAbilitySystem` to detect dive conditions
- [ ] Integrate with animation system

### Phase 5: Add ClimbFromWater ECS Components (Optional)
- [ ] Create `ClimbFromWaterState` component
- [ ] Create `ClimbFromWaterSystem` to detect climb conditions
- [ ] Integrate with animation system

### Phase 6: Animation Event Handlers
- [ ] Add to `ClimbAnimatorBridge.cs`:
  ```csharp
  public void OnAnimatorSwimEnteredWater() { /* Queue event */ }
  public void OnAnimatorSwimExitedWater() { /* Queue event */ }
  public void OnAnimatorDiveAddForce() { /* Queue event */ }
  public void OnAnimatorDiveComplete() { /* Queue event */ }
  public void OnAnimatorClimbComplete() { /* Queue event */ }
  public void OnAnimatorDrownComplete() { /* Queue event */ }
  ```

---

## Code Modifications Required

### OpsiveAnimatorConstants.cs
Add swimming ability indices and IntData values (see Phase 2).

### PlayerAnimationStateSystem.cs
Replace current swim handling with Opsive ability pattern:

```csharp
// Current (lines 210-260):
if (ss.IsSwimming)
{
    anim.SwimActionState = ...;  // Custom pattern
}

// NEW:
if (ss.IsSwimming)
{
    int prevAbilityIndex = anim.AbilityIndex;
    anim.AbilityIndex = OpsiveAnimatorConstants.ABILITY_SWIM; // 301

    // Map to Opsive IntData
    if (swimEvents.OnEnterWater && !wasSwimming)
        anim.AbilityIntData = OpsiveAnimatorConstants.SWIM_ENTER_FROM_AIR;
    else if (ss.IsSubmerged)
        anim.AbilityIntData = OpsiveAnimatorConstants.SWIM_UNDERWATER;
    else
        anim.AbilityIntData = OpsiveAnimatorConstants.SWIM_SURFACE;

    // AbilityFloatData = pitch for underwater movement
    anim.AbilityFloatData = cameraPitch * (input.Vertical >= 0 ? 1 : -1);

    if (prevAbilityIndex != OpsiveAnimatorConstants.ABILITY_SWIM)
        anim.AbilityChange = true;
}
```

### SwimmingComponents.cs
Add Opsive swim state tracking:

```csharp
public struct SwimmingState : IComponentData
{
    // ... existing fields ...

    /// <summary>Opsive swim state (0-4) for animation</summary>
    [GhostField] public int OpsiveSwimState;
}
```

### ClimbAnimatorBridge.cs
Add animation event handlers for swimming abilities.

---

## Animation Events Reference

From Opsive Swim.cs:177-178:
```csharp
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimEnteredWater", OnEnteredWater);
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimExitedWater", OnExitedWater);
```

From Dive.cs:78-79:
```csharp
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDiveAddForce", OnAddDiveForce);
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDiveComplete", OnDiveComplete);
```

From ClimbFromWater.cs:70:
```csharp
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbComplete", OnClimbComplete);
```

From Drown.cs:67:
```csharp
EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDrownComplete", OnDrownComplete);
```

---

## Verification Checklist

### Controller Setup
- [ ] ClimbingDemo.controller has all 18+ missing swim states
- [ ] All transitions with AbilityIndex conditions are in place
- [ ] Existing climbing/hang animations still work
- [ ] Locomotion not broken

### Swim (301)
- [ ] Fall into water plays EnterFromAir animation
- [ ] Surface swimming animation plays correctly
- [ ] Underwater swimming animation plays correctly
- [ ] Exit water animations play (moving vs idle)
- [ ] Pitch-based underwater movement blending works

### Dive (302) - If Implemented
- [ ] Shallow dive plays from low platform
- [ ] High dive plays from high platform
- [ ] Enter water transition plays

### ClimbFromWater (303) - If Implemented
- [ ] Character can climb out onto ledge
- [ ] Moving vs idle variants work

### Drown (304)
- [ ] Drown animation plays when breath depleted
- [ ] Character respawns after drown complete

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Missing transitions** | **Critical** | Blocked by EPIC14.12 Phase 0 |
| Swim state mapping | Medium | Test each transition thoroughly |
| Breath/Drown integration | Medium | Existing ECS system handles logic |
| AbilityFloatData pitch | Low | Mirror Opsive's calculation |
| Dive/ClimbFromWater new systems | High | Mark as optional, implement if time permits |

---

## Success Criteria

- [ ] Swim (301) animations play via AbilityIndex system
- [ ] Surface/Underwater transitions are smooth
- [ ] Enter/Exit water animations play
- [ ] Existing ECS swimming logic unchanged
- [ ] Drown animation triggers from BreathState
- [ ] No animation stuck states
- [ ] Climbing/Hang/Locomotion unaffected

---

## Dependencies on EPIC14.12

This EPIC is **BLOCKED** until:
1. EPIC14.12 Phase 0 completes (AnimatorAbilityCopier tool)
2. Tool is tested on Agility states first

Once Phase 0 is complete, swimming states can be copied using the same tool.
