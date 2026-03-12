# EPIC 13.17: Interaction System Parity

> **Status:** NOT STARTED  
> **Priority:** MEDIUM  
> **Dependencies:** EPIC 13.8 (Interaction System), EPIC 13.9 (Resource Integration)  
> **Reference:** `OPSIVE/.../Runtime/Character/Abilities/Interact.cs`

## Overview

Bring the DIG Interaction system to feature parity with Opsive's Interact ability. Current implementation covers ~40% of features.

---

## Sub-Tasks

### 13.17.1 Animation Events for Interaction
**Status:** NOT STARTED  
**Priority:** HIGH

Wait for animation events before triggering interaction logic.

#### Implementation
- `OnAnimatorInteract` - Trigger the actual interaction
- `OnAnimatorInteractComplete` - End the ability

```csharp
public struct InteractionAnimationTrigger : IComponentData
{
    public bool WaitForInteractEvent;
    public float InteractTimeout;
    public bool WaitForCompleteEvent;
    public float CompleteTimeout;
    public bool InteractEventReceived;
    public bool CompleteEventReceived;
}
```

#### Acceptance Criteria
- [ ] Interaction waits for animation event
- [ ] Timeout fallback
- [ ] Clean ability exit after complete event

---

### 13.17.2 IK Targets for Hand Placement
**Status:** NOT STARTED  
**Priority:** MEDIUM

Position hands on interactable objects (buttons, levers).

#### Implementation
```csharp
public struct InteractableIKTarget : IBufferElementData
{
    public IKGoal Goal;              // LeftHand, RightHand
    public float3 TargetPosition;
    public quaternion TargetRotation;
    public float Delay;              // Seconds before IK activates
    public float Duration;           // How long IK stays active
    public float InterpolationSpeed;
}
```

#### Acceptance Criteria
- [ ] Hands move to button/lever
- [ ] Configurable delay and duration
- [ ] Smooth interpolation

---

### 13.17.3 MoveTowardsLocations
**Status:** NOT STARTED  
**Priority:** MEDIUM

Auto-position character at precise interaction point.

#### Implementation
```csharp
public struct MoveTowardsLocation : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
    public float MoveSpeed;
    public float RotateSpeed;
    public bool MustBeExact;
}

// On interaction start:
if (interactable has MoveTowardsLocation) {
    // Smoothly move character to position
    StartMoveTowards(location);
}
```

#### Acceptance Criteria
- [ ] Character auto-moves to interaction point
- [ ] Configurable speed
- [ ] Optional exact positioning

---

### 13.17.4 Interactable ID Filtering
**Status:** NOT STARTED  
**Priority:** LOW

Allow multiple Interact abilities that only work with specific IDs.

#### Implementation
```csharp
public struct InteractAbility : IComponentData
{
    public int InteractableID; // -1 = any, else must match
}

public struct Interactable : IComponentData
{
    public int ID;
}
```

#### Acceptance Criteria
- [ ] Interact(ID=1) only works with Interactable(ID=1)
- [ ] -1 matches any

---

### 13.17.5 Block Other Abilities During Interaction
**Status:** NOT STARTED  
**Priority:** LOW

Prevent item use, aiming, etc. during interaction.

#### Implementation
```csharp
public struct InteractAbility : IComponentData
{
    public bool AllowHeightChange;
    public bool AllowAim;
    public bool IsConcurrent;
}
```

#### Acceptance Criteria
- [ ] Can't aim while interacting (configurable)
- [ ] Can't use items while interacting
- [ ] Height change optionally allowed

---

## Files to Modify

| File | Changes |
|------|---------|
| `InteractionSystem.cs` | Animation events, ability blocking |
| `InteractableIKTarget` | New component |
| `MoveTowardsLocation` | New component |
| `InteractableAuthoring.cs` | IK targets, move location |
| `IK/HandIKSystem.cs` | Apply IK targets |

## Verification Plan

1. Press button → hand moves to button → animation plays → button activates
2. Use lever → character moves to lever position → pulls lever
3. Wrong ID interactable → cannot interact
4. Try to aim during interaction → blocked

---

## Test Environment Tasks

Create the following test objects under: `GameObject > DIG - Test Objects > Interaction > Interaction Tests`

### 13.17.T1 Button Panel
**Status:** NOT STARTED

Wall-mounted buttons with IK hand targets.

#### Specifications
- Multiple buttons at different heights
- IK target markers visible in editor
- Button press animation
- Visual/audio feedback on press
- Different button types (momentary, toggle)

#### Hierarchy
```
Interaction Tests/
  Button Panel/
    Wall Mount
    Button_Low (knee height)
    Button_Mid (chest height)
    Button_High (above head - requires reach)
    IK Target Markers (Gizmo)
```

---

### 13.17.T2 Lever Station
**Status:** NOT STARTED

Lever with MoveTowardsLocation for precise positioning.

#### Specifications
- Lever with two positions (up/down)
- MoveTowards target position marker
- Player auto-moves to position before pulling
- Rotation alignment (face the lever)

#### Hierarchy
```
Interaction Tests/
  Lever Station/
    Lever
    MoveTowards Target
    Position Marker (Gizmo)
    Rotation Guide
```

---

### 13.17.T3 ID-Filtered Interactables
**Status:** NOT STARTED

Test interactable ID filtering.

#### Specifications
- Door with ID=1 (only Key1 opens)
- Door with ID=2 (only Key2 opens)
- Keycard pickups that grant interact capability
- UI showing current keys

#### Hierarchy
```
Interaction Tests/
  ID Filter Test/
    Door_ID1
    Door_ID2
    Keycard_ID1
    Keycard_ID2
    Key Status (UI)
```

---

### 13.17.T4 Terminal Station
**Status:** NOT STARTED

Computer terminal with extended interaction.

#### Specifications
- Sit-down animation to terminal
- Blocking during interaction (no aiming)
- Exit animation on complete
- Timer showing interaction duration

#### Hierarchy
```
Interaction Tests/
  Terminal Station/
    Terminal
    Chair
    Seat Position Marker
    Interaction Timer (UI)
```

---

### 13.17.T5 Valve Wheel
**Status:** NOT STARTED

Two-handed valve requiring IK targets for both hands.

#### Specifications
- Large valve wheel
- Left hand IK target
- Right hand IK target
- Rotation animation during turn
- Progress indicator

#### Hierarchy
```
Interaction Tests/
  Valve Wheel/
    Valve
    LeftHand_IK_Target
    RightHand_IK_Target
    Progress Bar (UI)
```

## 7. Algorithmic Implementation Details (Opsive -> ECS Port)

### 7.1 Move Towards Logic
Derived from **`MoveTowardsLocation.cs`**.
*   **Validity Check**:
    ```csharp
    // Calculate local offset
    float3 direction = targetPos - playerPos;
    float3 localDir = math.rotate(math.inverse(targetRot), direction);
    
    // Check bounds (using 'Size' box)
    if (math.abs(localDir.x) <= Size.x/2 && 
        math.abs(localDir.z) <= Size.z/2) {
        // Valid Position
    }
    ```
*   **Rotation Check**:
    *   Dot product of `PlayerForward` vs `TargetForward` must be within `Angle` threshold (e.g., 0.5 degrees).

### 7.2 IK Target Scheduling
Derived from **`Interact.cs`**.
*   **Sequence**:
    1.  **Start**: `OnAbilityStarted` -> Schedule `SetIKTarget` after `Delay`.
    2.  **Update**: IK System interpolates Hand to Goal.
    3.  **End**: Schedule `StopIKTarget` after `Duration`.
*   **ECS Implementation**:
    *   Use a dynamic buffer `ScheduledEvent` on the entity.
    *   System decrements timers.
    *   When timer < 0, apply `IKTarget` component (Add/Remove).

### 7.3 ID Filtering
Derived from **`Interact.cs`** -> `ValidateObject`.
*   **Logic**:
    *   `AbilityID == -1`: Accepts ANY Interactable.
    *   `AbilityID != -1`: Accepts ONLY `Interactable.ID == AbilityID`.
