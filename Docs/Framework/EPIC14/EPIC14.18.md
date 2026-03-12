# EPIC 14.18 - Advanced Character IK System

## Overview
Extend the PlayerIKBridge to include all IK features from Opsive's CharacterIK system. This provides professional-quality IK for weapons, aiming, and character animation polish.

## Current State
- ✅ Look At IK (head follows camera)
- ✅ Foot IK (ground placement on slopes)
- ✅ Hand IK (weapon grip)
- ✅ Upper Arm IK (torso aiming)
- ✅ Knee Hint IK (leg bending direction)
- ✅ IK Target Interpolation (smooth transitions)
- ✅ Multi-Layer IK (base/upper body layers)
- ✅ Event Integration (death/respawn/equip)

## Features to Implement

### 1. Hand IK (High Priority)
**Purpose**: Position and rotate hands for weapon holding, aiming, and recoil.

**Components**:
- `HandIKSettings` - Configuration for hand IK weights and speeds
- `HandIKState` - Runtime state for hand positions/rotations

**Algorithm (from Opsive)**:
```csharp
// Hand Rotation - face the look direction
Quaternion GetTargetHandRotation(bool leftHand, AvatarIKGoal ikGoal)
{
    // Use the distant hand so hands always point same direction
    var distantHand = GetDistantHand(); // Hand further from camera
    var lookDirection = (LookDirection(distantHand.position) + LookAtOffset).normalized;
    return Quaternion.LookRotation(lookDirection, Up) 
           * Quaternion.Inverse(transform.rotation)
           * Quaternion.Euler(SpringValue)  // Recoil spring
           * Animator.GetIKRotation(ikGoal);
}

// Hand Position - follow upper arm rotation + spring offset
Vector3 GetTargetHandPosition(Transform hand, bool leftHand)
{
    Vector3 handPosition;
    if (UpperArmWeight > 0) {
        handPosition = (hand == DominantHand) ? DominantHandPosition : NonDominantHandPosition;
    } else {
        handPosition = hand.position;
    }
    return handPosition + SpringValue + HandPositionOffset;
}
```

**Spring System**:
- `LeftHandPositionSpring` / `RightHandPositionSpring` - Recoil position
- `LeftHandRotationSpring` / `RightHandRotationSpring` - Recoil rotation
- Springs receive forces from weapon fire events

### 2. Upper Arm IK (High Priority)
**Purpose**: Rotate upper arms toward look target when holding weapons.

**Algorithm (from Opsive)**:
```csharp
void RotateUpperArms()
{
    if (DominantUpperArm == null || UpperArmWeight <= 0) return;
    
    // Get look direction in local space
    var localLookDir = transform.InverseTransformDirection(LookDirection(DominantUpperArm.position));
    var lookDir = transform.InverseTransformDirection(transform.forward);
    lookDir.y = localLookDir.y;
    lookDir = transform.TransformDirection(lookDir).normalized;
    
    // Prevent arm from rotating too far behind
    if (localLookDir.y < 0) {
        lookDir = Vector3.Lerp(transform.forward, lookDir, 1 - Mathf.Abs(localLookDir.y));
    }
    
    // Calculate target rotation
    var targetRotation = Quaternion.FromToRotation(transform.forward, lookDir) * DominantUpperArm.rotation;
    targetRotation = Quaternion.Slerp(DominantUpperArm.rotation, targetRotation, UpperArmWeight);
    
    // Calculate hand positions based on upper arm rotation
    var offset = Vector3.Scale(DominantUpperArm.InverseTransformPoint(DominantHand.position), DominantHand.lossyScale);
    DominantHandPosition = MathUtility.TransformPoint(DominantUpperArm.position, targetRotation, offset);
    
    // Non-dominant hand follows dominant hand rotation
    NonDominantHandPosition = MathUtility.TransformPoint(DominantHandPosition, 
        Animator.GetIKRotation(DominantHandGoal), NonDominantHandOffset);
}
```

### 3. Knee Hint IK (Medium Priority)
**Purpose**: Control knee bend direction for better leg poses on stairs/slopes.

**Algorithm (from Opsive)**:
```csharp
// In PositionLowerBody, after foot IK:
if (IKTarget[IKGoal.LeftKnee] != null) {
    Animator.SetIKHintPosition(AvatarIKHint.LeftKnee, IKTarget[IKGoal.LeftKnee].position);
    Animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, FootIKWeight[0]);
}
if (IKTarget[IKGoal.RightKnee] != null) {
    Animator.SetIKHintPosition(AvatarIKHint.RightKnee, IKTarget[IKGoal.RightKnee].position);
    Animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, FootIKWeight[1]);
}
```

### 4. IK Target Interpolation (Medium Priority)
**Purpose**: Smooth transitions when abilities set IK targets (climbing, vaulting, etc).

**Algorithm (from Opsive)**:
```csharp
// When ability sets an IK target:
void SetAbilityIKTarget(IKGoal goal, Transform target, float duration)
{
    AbilityIKTarget[goal] = target;
    if (target != null) {
        StartInterpolation[goal] = Time.time;
        InterpolationDuration[goal] = duration;
        InterpolationTarget[goal].SetPositionAndRotation(GetCurrentIKPosition(goal), GetCurrentIKRotation(goal));
    }
}

// Each frame, interpolate:
void UpdateTargetInterpolations()
{
    for (int i = 0; i < IKGoal.Last; i++) {
        if (StartInterpolation[i] == -1) continue;
        
        var time = Mathf.Clamp01((Time.time - StartInterpolation[i]) / InterpolationDuration[i]);
        
        if (AbilityIKTarget[i] == null) {
            // Interpolating back to animation
            InterpolationTarget[i].position = Vector3.Lerp(InterpolationTarget[i].position, AnimationIKPosition, time);
        } else {
            // Interpolating to target
            InterpolationTarget[i].position = Vector3.Lerp(AnimationIKPosition, AbilityIKTarget[i].position, time);
        }
    }
}
```

### 5. Multi-Layer IK (Medium Priority)
**Purpose**: Apply different IK to different animation layers.

**Layers**:
- **Base Layer (0)**: Foot IK + Look At IK
- **Upper Body Layer (4)**: Hand rotation + Upper arm rotation + Hand positioning
- **Full Body Layer**: Secondary hand positioning pass (for two-handed weapons)

```csharp
void OnAnimatorIK(int layerIndex)
{
    if (layerIndex == BaseLayerIndex) {
        PositionLowerBody();  // Foot IK
        LookAtTarget();       // Head IK
    } 
    else if (layerIndex == UpperBodyLayerIndex) {
        RotateHands();        // Hand rotation toward aim
        RotateUpperArms();    // Upper arm toward aim
        PositionHands();      // Hand positions
    } 
    else if (RequireSecondHandPositioning) {
        PositionHands();      // Secondary pass for non-dominant hand
    }
}
```

### 6. Event Integration (Low Priority)
**Purpose**: Respond to game events for proper IK behavior.

**Events**:
- `OnDeath` - Disable IK when character dies
- `OnRespawn` - Re-enable IK with immediate positioning
- `OnEquipItem` / `OnUnequipItem` - Recalculate dominant hand
- `OnAim` - Activate hand/arm IK when aiming
- `OnUseStart` - Activate during item use (attacks)
- `OnAddForce` - Apply recoil springs

## ECS Components

### HandIKSettings (IComponentData)
```csharp
public struct HandIKSettings : IComponentData
{
    public float HandWeight;           // 0-1
    public float HandAdjustmentSpeed;  // Default: 10
    public float3 HandPositionOffset;  // Local offset
    public float UpperArmWeight;       // 0-1
    public float UpperArmAdjustmentSpeed; // Default: 10
}
```

### HandIKState (IComponentData)
```csharp
public struct HandIKState : IComponentData
{
    public float3 LeftHandTarget;
    public float3 RightHandTarget;
    public quaternion LeftHandRotation;
    public quaternion RightHandRotation;
    public float LeftHandWeight;
    public float RightHandWeight;
    
    // Spring state
    public float3 LeftPositionSpringValue;
    public float3 LeftPositionSpringVelocity;
    public float3 RightPositionSpringValue;
    public float3 RightPositionSpringVelocity;
    public float3 LeftRotationSpringValue;
    public float3 LeftRotationSpringVelocity;
    public float3 RightRotationSpringValue;
    public float3 RightRotationSpringVelocity;
}
```

### IKTargetOverride (IBufferElementData)
```csharp
public struct IKTargetOverride : IBufferElementData
{
    public IKGoal Goal;
    public float3 Position;
    public quaternion Rotation;
    public float StartTime;
    public float Duration;
    public bool Active;
}
```

## Implementation Order

1. ✅ **Hand IK Components + Basic Implementation**
   - Added HandIKSettings/HandIKState to IKComponents.cs
   - Added to IKAuthoring.cs
   - Implemented in PlayerIKBridge.cs

2. ✅ **Upper Arm IK**
   - Added to HandIKSettings (UpperArmWeight, UpperArmAdjustmentSpeed)
   - Implemented RotateUpperArms() in PlayerIKBridge.cs

3. ✅ **Dominant Hand Tracking**
   - Track which hand holds the weapon (_isRightHandDominant)
   - Wire up via OnEquipmentChanged() event handler

4. ✅ **Spring System for Recoil**
   - Implemented UpdateSprings() with stiffness/damping
   - Springs stored in HandIKState (SpringStiffness, SpringDamping)

5. ✅ **Knee Hints**
   - Knee hints project forward from lower leg
   - Applied when foot IK weight > 0

6. ✅ **IK Target Interpolation**
   - Added IKTargetOverride buffer element
   - Implemented UpdateIKTargetInterpolations() and ApplyIKTargetInterpolations()

7. ✅ **Multi-Layer Support**
   - BaseLayerIndex (0): Foot IK + Look At IK
   - UpperBodyLayerIndex (4): Hand IK + Upper Arm IK
   - PositionHandsSecondPass() for two-handed weapons

8. ✅ **Event Integration**
   - OnDeath() - Disables all IK
   - OnRespawn() - Re-enables and recalibrates
   - OnEquipmentChanged(bool isRightHandDominant) - Updates dominant hand
   - OnAimStateChanged(bool isAiming) - Enables/disables hand IK
   - OnItemUseStateChanged(bool isUsing) - For non-weapon items

## Key Files

### PlayerIKBridge (Partial Class - Split for Maintainability)

| File | Lines | Purpose |
|------|-------|---------|
| `PlayerIKBridge.cs` | ~250 | Core fields, Awake, LinkState, OnAnimatorIK dispatch, OnAnimatorMove |
| `PlayerIKBridge.FootIK.cs` | ~215 | Foot ground placement, calibration, knee hints |
| `PlayerIKBridge.HandIK.cs` | ~300 | Hand/arm positioning, springs, upper arm rotation |
| `PlayerIKBridge.Interpolation.cs` | ~180 | Ability-driven IK smooth transitions |
| `PlayerIKBridge.Events.cs` | ~105 | Death/respawn/equipment/aim events |
| `PlayerIKBridge.LookAtIK.cs` | ~50 | Head/body tracking |

**Key Methods by File:**

**PlayerIKBridge.cs** (Core)
- `LinkState()` - ECS entity binding
- `SetDominantHand()` - Weapon hand configuration
- `AddRecoilForce()` - Apply recoil to springs
- `OnAnimatorIK()` - Layer-based IK dispatch
- `OnAnimatorMove()` - Root motion capture

**PlayerIKBridge.FootIK.cs**
- `CalibrateFootIK()` - T-pose calibration
- `GetFootRaycastPosition()` - Raycast origin calculation
- `PositionLowerBody()` - Foot IK + knee hints

**PlayerIKBridge.HandIK.cs**
- `UpdateSprings()` - Recoil spring physics
- `GetDistantHand()` - Consistent aim direction
- `GetTargetHandRotation()` - Hand rotation toward aim
- `GetTargetHandPosition()` - Hand position with spring offset
- `RotateUpperArms()` - Upper arm rotation toward look direction
- `PositionUpperBody()` - Hand/arm IK on upper body layer
- `PositionHandsSecondPass()` - Two-handed weapon support

**PlayerIKBridge.LookAtIK.cs**
- `LookAtTarget()` - Head tracking on base layer

**PlayerIKBridge.Interpolation.cs**
- `UpdateIKTargetInterpolations()` - Process IKTargetOverride buffer
- `GetCurrentIKPosition()` / `GetCurrentIKRotation()` - Current pose getters
- `ApplyIKTargetInterpolations()` - Apply interpolated IK

**PlayerIKBridge.Events.cs**
- `OnDeath()` - Disable all IK
- `OnRespawn()` - Re-enable and recalibrate
- `OnEquipmentChanged()` - Update dominant hand
- `OnAimStateChanged()` - Toggle hand IK
- `OnItemUseStateChanged()` - Toggle item use IK

### IKComponents.cs
- Added `IKGoal` enum (8 goals for hands/feet/elbows/knees)
- Expanded `HandIKSettings` with weights and spring parameters
- Expanded `HandIKState` with targets, rotations, and aiming flags
- Added `IKTargetOverride` buffer for ability-driven IK

### IKAuthoring.cs
- Added Hand IK section (HandWeight, HandAdjustmentSpeed, HandPositionOffset)
- Added Upper Arm section (UpperArmWeight, UpperArmAdjustmentSpeed)
- Added Spring section (SpringStiffness, SpringDamping)

## Testing Checklist
- [ ] Hand IK positions correctly when holding rifle
- [ ] Non-dominant hand grips weapon foregrip
- [ ] Upper arms rotate toward aim direction
- [ ] Recoil springs work on weapon fire
- [ ] Knees bend correctly on stairs
- [ ] IK transitions smoothly during climb
- [ ] IK disables on death, re-enables on respawn
- [ ] Weapon equip/unequip transitions smoothly
