# EPIC 14.24 - Ledge Hang System: Input Blocking & Controlled Transitions

## Overview

This epic implements a proper ledge-hang state machine with input blocking during transitions. When a player reaches a ledge:
1. Input is blocked while transitioning to the hang animation
2. Once in hang state: A/D to shimmy along ledge, W to vault up, S to climb back down
3. Vault-up transitions are smooth with blocked input until player reaches safe standing position

The design follows the **weapon reload pattern** where `IsReloading` blocks input during animation, cleared by animation event.

---

## Goals

- [x] Detect ledge and trigger transition to hang state (input blocked)
- [x] Wait for hang animation to complete (via animation event)
- [x] Enable limited input in hang state: A/D shimmy, W vault, S descend
- [x] W in hang state: Triggers vault animation with blocked input
- [x] Vault transition moves player smoothly to safe standing position
- [x] Animation event clears vault state, re-enables full input

---

## Animation Events Found in MemuAnim Controller

### From Opsive Hang FBX Files:

| Animation Clip | Event | Time | Purpose |
|----------------|-------|------|---------|
| `HangStart.fbx` | `OnAnimatorHangStartInPosition` | 0.92 | Hang entry complete |
| `FreeClimbtoHangVertical.fbx` | `OnAnimatorHangStartInPosition` | varies | Transition from climb complete |
| `FreeClimbtoHangHoriztonal.fbx` | `OnAnimatorHangStartInPosition` | varies | Transition from climb complete |
| `DropStart.fbx` | `OnAnimatorHangStartInPosition` | varies | Drop-to-hang complete |
| `PullUp.fbx` | `OnAnimatorHangComplete` | 0.97 | **Vault/pull-up complete** |
| `LadderClimbtoHang.fbx` | `OnAnimatorHangStartInPosition` | varies | Ladder→Hang transition |

### Animator Controller States (MemuAnim.controller):

| State Name | Purpose | AbilityIndex |
|------------|---------|--------------|
| `Hang Start` | Entry transition to hang | 104 |
| `Hang` | Idle hang state | 104 |
| `Hang Jump Start` | Jump from hang | 104 |
| `Hang to Free Climb Right/Left` | Exit hang back to climb | 503→104 |
| `Hang to Free Climb Vertical` | Exit hang downward | 503→104 |

### ClimbAnimatorBridge Methods (Already Implemented):

| Method | Called When | Current Implementation |
|--------|-------------|------------------------|
| `OnAnimatorHangStartInPosition()` | Animation event fires | Queues `HangStartInPosition` to ECS |
| `OnAnimatorHangComplete()` | Animation event fires | Queues `HangComplete` to ECS |


### Input Blocking Pattern (Weapon Reload Reference)

Location: `WeaponAmmoSystem.cs`, `ShootableActionSystem.cs`

```csharp
// WeaponAmmoSystem.cs - Start reload
if (request.ValueRO.Reload && !stateRef.IsReloading && ...)
{
    stateRef.IsReloading = true;  // Blocks fire in ShootableActionSystem
    stateRef.ReloadStartTime = currentTime;
}

// ShootableActionSystem.cs - Block during reload
if (stateRef.IsReloading)
{
    if (currentTime - stateRef.ReloadStartTime > stateRef.ReloadDuration)
        stateRef.IsReloading = false;  // Timeout safety
    return;  // Skip firing logic
}

// WeaponAnimationEventSystem.cs - Animation event clears state
case "ReloadComplete":
    ammo.IsReloading = false;
    break;
```

**Key Pattern:**
1. Set blocking flag → blocks input/actions
2. Animation plays → ECS waits
3. Animation event fires → clears blocking flag
4. Input/actions re-enabled

---

## Proposed State Machine

```
                    ┌─────────────────┐
                    │   Climbing      │
                    │ (IsClimbing=T)  │
                    └────────┬────────┘
                             │ Ledge Detected
                             │ (feet ray miss + hands ray hit)
                             ▼
         ┌──────────────────────────────────────┐
         │        Hang Transition               │
         │ IsHangTransitioning=T (INPUT BLOCKED)│
         │ Animator: Transition to Hang state   │
         └──────────────────┬───────────────────┘
                            │ OnAnimatorHangInPosition
                            ▼
         ┌──────────────────────────────────────┐
         │          Ledge Hang                  │
         │ IsFreeHanging=T, IsHangTransitioning=F│
         │ INPUT: A/D = Shimmy, W = Vault, S = Descend │
         └────┬───────────────┬─────────────────┘
              │ S pressed     │ W/Jump pressed
              ▼               ▼
    ┌─────────────────┐  ┌──────────────────────────────┐
    │ Descend Back    │  │         Vault Up             │
    │ to Climbing     │  │ IsClimbingUp=T (INPUT BLOCKED)│
    │ (reverse hang)  │  │ Position lerps to safe spot   │
    └─────────────────┘  └──────────────┬───────────────┘
                                        │ OnAnimatorHangComplete
                                        ▼
                         ┌──────────────────────────────┐
                         │       Standing on Ledge      │
                         │ IsClimbing=F, IsClimbingUp=F │
                         │ Full input restored          │
                         └──────────────────────────────┘
```

---

## Implementation Plan

### Phase 1: New State Flag - `IsHangTransitioning`

**File: `FreeClimbComponents.cs`**

Add a new flag to block input specifically during hang entry:

```csharp
public struct FreeClimbState : IComponentData
{
    // ... existing fields ...
    
    // NEW: Blocks input during hang entry transition
    [GhostField] public bool IsHangTransitioning;
    [GhostField] public double HangTransitionStartTime;
}
```

**Rationale:** `IsTransitioning` is used for mount/dismount. A dedicated flag keeps hang logic isolated.

---

### Phase 2: Modify FreeHangDetectionSystem - Trigger Transition

**File: `FreeHangDetectionSystem.cs`**

Currently sets `IsFreeHanging = true` immediately. Change to:

```csharp
if (shouldBeFreeHanging && !climb.IsFreeHanging && !climb.IsHangTransitioning)
{
    // NEW: Start hang transition instead of immediate hang
    climb.IsHangTransitioning = true;
    climb.HangTransitionStartTime = CurrentTime;
    
    // Animation system will set IsFreeHanging when animation completes
}
```

**Key Changes:**
- Entry becomes two-phase: `IsHangTransitioning` → animation → `IsFreeHanging`
- Adds 2-second timeout safety (same pattern as reload)

---

### Phase 3: Block Input During Hang Transition

**File: `FreeClimbMovementSystem.cs`**

Add input blocking check:

```csharp
private void Execute(...)
{
    // Skip if not climbing, in wall jump, transitioning, OR hang transitioning
    if (!climb.IsClimbing || climb.IsWallJumping || climb.IsTransitioning || climb.IsHangTransitioning)
        return;
    
    // ... existing logic ...
}
```

**Also in `FreeClimbInputDismountSystem.cs`:**
```csharp
if (!climb.IsClimbing || climb.IsTransitioning || climb.IsClimbingUp || 
    climb.IsWallJumping || climb.IsHangTransitioning)  // ADD THIS
    return;
```

---

### Phase 4: Animation Event Completes Hang Entry

**File: `FreeClimbAnimationEventSystem.cs`**

Modify `HangStartInPosition` handler:

```csharp
case FreeClimbAnimationEvents.EventType.HangStartInPosition:
    if (climb.IsHangTransitioning)
    {
        // Hang transition complete - now in active hang state
        climb.IsHangTransitioning = false;
        climb.IsFreeHanging = true;
        Debug.Log($"[FreeClimb] HangStartInPosition - Hang active, input enabled for shimmy/vault");
    }
    break;
```

---

### Phase 5: Hang State Input Handling

**File: `FreeClimbMovementSystem.cs`**

When `IsFreeHanging == true`:

```csharp
// In Execute(), after blocking checks pass:
if (climb.IsFreeHanging)
{
    // A/D: Shimmy along ledge (horizontal only)
    float inputX = playerInput.Horizontal;
    // (existing horizontal movement logic applies)
    
    // W/S blocked here - handled by FreeClimbLedgeSystem (vault) and a new descend system
    inputY = 0;  // Vertical movement disabled in hang
    
    // ... apply horizontal shimmy ...
    return;
}
```

**Note:** W-to-vault is already handled in `FreeClimbLedgeSystem`:
```csharp
bool isValidTrigger = playerInput.Jump.IsSet || (climb.IsFreeHanging && playerInput.Vertical > 0.5f);
```

---

### Phase 6: Vault-Up Input Blocking

**File: `FreeClimbLedgeSystem.cs`**

Already sets `IsClimbingUp = true` when vault triggers. This already blocks movement via:
```csharp
// FreeClimbMovementSystem checks:
if (!climb.IsClimbing || climb.IsWallJumping || climb.IsTransitioning)
```

**Verify:** `IsClimbingUp` should also block input. Add to movement check:
```csharp
if (!climb.IsClimbing || climb.IsWallJumping || climb.IsTransitioning || 
    climb.IsHangTransitioning || climb.IsClimbingUp)  // ADD IsClimbingUp
    return;
```

---

### Phase 7: Smooth Position Transition During Vault

**File: `FreeClimbMountSystem.cs`**

Already handles position lerping when `IsTransitioning`:
```csharp
if (climb.IsTransitioning)
{
    float t = EaseOutQuad(climb.TransitionProgress);
    lt.Position = math.lerp(climb.TransitionStartPos, climb.TransitionTargetPos, t);
    lt.Rotation = math.slerp(climb.TransitionStartRot, climb.TransitionTargetRot, t);
}
```

**Verify:** Vault (`IsClimbingUp`) uses same transition system. `StartClimbUp()` already sets:
```csharp
climb.IsTransitioning = true;
climb.TransitionProgress = 0f;
climb.TransitionStartPos = lt.Position;
climb.TransitionTargetPos = targetPos;  // Safe standing position
```

---

### Phase 8: Vault Complete Clears State

**File: `FreeClimbAnimationEventSystem.cs`**

Modify `HangComplete` handler:

```csharp
case FreeClimbAnimationEvents.EventType.HangComplete:
    // Vault animation complete - player is now standing on ledge
    climb.IsClimbingUp = false;
    climb.IsTransitioning = false;
    climb.IsFreeHanging = false;
    climb.IsClimbing = false;  // Exit climb mode entirely
    Debug.Log($"[FreeClimb] HangComplete - Vault finished, full input restored");
    break;
```

---

### Phase 9: Descend from Hang (S input)

**NEW File: `FreeClimbHangDescendSystem.cs`**

When in hang state and S is pressed:

```csharp
[UpdateAfter(typeof(FreeClimbMovementSystem))]
public partial struct FreeClimbHangDescendSystem : ISystem
{
    private void Execute(ref FreeClimbState climb, RefRO<PlayerInput> input, ...)
    {
        // Only process when in active hang (not transitioning)
        if (!climb.IsFreeHanging || climb.IsHangTransitioning || climb.IsClimbingUp)
            return;
        
        // S input = descend back to climb
        if (input.ValueRO.Vertical < -0.5f)
        {
            // Simply exit hang state - will return to climb
            climb.IsFreeHanging = false;
            Debug.Log($"[FreeClimb] Descending from hang, returning to climb");
        }
    }
}
```

---

## Files to Modify

| File | Changes |
|------|---------|
| `FreeClimbComponents.cs` | Add `IsHangTransitioning`, `HangTransitionStartTime` |
| `FreeHangDetectionSystem.cs` | Two-phase entry: set `IsHangTransitioning` first |
| `FreeClimbMovementSystem.cs` | Block on `IsHangTransitioning` and `IsClimbingUp` |
| `FreeClimbInputDismountSystem.cs` | Block on `IsHangTransitioning` |
| `FreeClimbAnimationEventSystem.cs` | Handle `HangStartInPosition` → enable hang; `HangComplete` → exit |
| **NEW** `FreeClimbHangDescendSystem.cs` | Handle S input to exit hang |

---

## Verification Plan

### Manual Testing

1. **Hang Entry Transition:**
   - Climb to ledge top until feet lose surface contact
   - Verify input is blocked during hang entry animation
   - Verify player cannot move or dismount during transition

2. **Hang State Input:**
   - Once in hang state, verify A/D moves along ledge
   - Verify W is blocked (vault handled separately)
   - Verify S returns to climbing state

3. **Vault Transition:**
   - Press W/Jump while hanging at ledge
   - Verify input blocked during vault animation
   - Verify player position smoothly transitions to safe standing spot
   - Verify full input restored after animation complete

4. **Edge Cases:**
   - Rapid input during transitions (should be ignored)
   - Timeout safety (hang transition completes after 2s if animation fails)
   - NetCode prediction (transitions should survive rollback)

---

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Animation event never fires | 2-second timeout safety on all transitions |
| Desync during rollback | `IsHangTransitioning` is GhostField, replicates |
| Existing vault logic conflicts | Verify `IsClimbingUp` already used correctly |
| Input feels unresponsive | Tune animation lengths; consider reduced movement instead of full block |

---

## Open Questions

1. **Shimmy Animation:** Does the Opsive Hang ability have shimmy animations, or is this new?
2. **Ledge Detection Origin:** Should hang only trigger on true vertical ledges (90° corners), or also on curved surfaces?
3. **Camera Behavior:** Should camera adjust during hang state to better show the ledge?
