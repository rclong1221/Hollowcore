# EPIC 14.18 - Advanced Character IK Setup Guide

## Overview

This guide covers how to set up and configure the PlayerIKBridge system for character IK including:
- Foot IK (ground placement)
- Look At IK (head tracking)
- Hand IK (weapon aiming)
- Upper Arm IK (torso rotation)
- Recoil springs
- Ability-driven IK (climbing, vaulting)

---

## Prerequisites

1. **Humanoid Rig**: Character must use a Humanoid avatar with properly configured bones
2. **Animator Controller**: Must have IK Pass enabled on relevant layers
3. **ECS Entity**: Character must have an ECS entity with IK components

---

## Step 1: Enable IK Pass on Animator Layers

In your Animator Controller, enable **IK Pass** on:
- **Base Layer (0)**: For Foot IK and Look At IK
- **Upper Body Layer (4)**: For Hand IK (if using layer-based upper body)

```
Animator Controller → Layer Settings → IK Pass ✓
```

> **Note**: The default layer indices are `BaseLayerIndex = 0` and `UpperBodyLayerIndex = 4`. Modify in `PlayerIKBridge.cs` if your setup differs.

---

## Step 2: Add IK Components to Player Prefab

### Required Components

Add these to your player's **IKAuthoring** component in the prefab:

#### Foot IK Settings
| Field | Default | Description |
|-------|---------|-------------|
| `FootRayLength` | 0.5 | How far down to raycast for ground |
| `FootOffset` | 0.1 | Height offset from ground to foot sole |
| `BodyHeightAdjustment` | 0.3 | Max amount hips can lower on slopes |
| `FootIKWeight` | 1.0 | IK blend weight when foot needs adjustment |
| `BodyIKWeight` | 1.0 | Body lowering blend weight |
| `BlendSpeed` | 5.0 | How fast IK blends in/out |

#### Look At IK Settings
| Field | Default | Description |
|-------|---------|-------------|
| `Mode` | MouseAim | LookAt behavior mode |
| `MaxHeadAngle` | 60 | Max head rotation in degrees |
| `HeadWeight` | 1.0 | Head IK weight |
| `BodyWeight` | 0.3 | Body follow weight |
| `BlendSpeed` | 10.0 | Blend speed |

#### Hand IK Settings (EPIC 14.18)
| Field | Default | Description |
|-------|---------|-------------|
| `HandWeight` | 1.0 | Hand IK weight when aiming |
| `HandAdjustmentSpeed` | 10.0 | How fast hand IK blends |
| `HandPositionOffset` | (0,0,0) | Local offset for hand position |
| `UpperArmWeight` | 0.5 | Upper arm rotation weight |
| `UpperArmAdjustmentSpeed` | 10.0 | Upper arm blend speed |
| `SpringStiffness` | 0.2 | Recoil spring stiffness |
| `SpringDamping` | 0.25 | Recoil spring damping |

---

## Step 3: Add PlayerIKBridge to Character

The `PlayerIKBridge` MonoBehaviour should be on the same GameObject as your Animator:

```
Character (GameObject)
├── Animator
├── PlayerIKBridge  ← Add this
└── LinkedEntityGroup (or similar ECS linking)
```

The bridge is automatically linked via `PlayerIKBridgeLinkSystem` when the entity is created.

---

## Step 4: Configure Weapon Handedness

Call `SetDominantHand()` when equipping weapons:

```csharp
// In your weapon equip system/code:
playerIKBridge.OnEquipmentChanged(isRightHanded: true);  // Right-handed weapon
playerIKBridge.OnEquipmentChanged(isRightHanded: false); // Left-handed weapon
```

---

## Step 5: Enable Aiming IK

Set the `IsAiming` flag on `HandIKState` to enable hand/arm IK:

```csharp
// Option 1: Via ECS
var handState = entityManager.GetComponentData<HandIKState>(entity);
handState.IsAiming = true;
entityManager.SetComponentData(entity, handState);

// Option 2: Via PlayerIKBridge event
playerIKBridge.OnAimStateChanged(isAiming: true);
```

---

## Step 6: Add Weapon Recoil

Call `AddRecoilForce()` when weapon fires:

```csharp
// In weapon fire code:
Vector3 positionalRecoil = new Vector3(0, 0.02f, -0.05f);  // Kick up and back
Vector3 rotationalRecoil = new Vector3(-5f, 0, 0);         // Rotate up
playerIKBridge.AddRecoilForce(positionalRecoil, rotationalRecoil);
```

The spring system will animate the hands back to their target position.

---

## Step 7: Handle Death/Respawn

Disable IK when character dies, re-enable on respawn:

```csharp
// On death:
playerIKBridge.OnDeath();

// On respawn:
playerIKBridge.OnRespawn();
```

---

## Step 8: Ability-Driven IK (Optional)

For abilities like climbing that need to override hand/foot positions:

### Add IKTargetOverride to Entity

```csharp
// In ability start:
var buffer = entityManager.GetBuffer<IKTargetOverride>(entity);
buffer.Add(new IKTargetOverride
{
    Goal = IKGoal.LeftHand,
    Position = climbHandholdPosition,
    Rotation = climbHandholdRotation,
    StartTime = Time.time,
    Duration = 0.2f,  // Blend duration
    Active = true
});
```

### Clear on Ability End

```csharp
// In ability end:
var buffer = entityManager.GetBuffer<IKTargetOverride>(entity);
for (int i = 0; i < buffer.Length; i++)
{
    var data = buffer[i];
    data.Active = false;  // Will interpolate back to animation
    buffer[i] = data;
}
```

---

## File Structure

```
Assets/Scripts/Player/View/
├── PlayerIKBridge.cs           # Core: fields, dispatch, linking
├── PlayerIKBridge.FootIK.cs    # Foot ground placement
├── PlayerIKBridge.LookAtIK.cs  # Head tracking
├── PlayerIKBridge.HandIK.cs    # Hand/arm positioning, springs
├── PlayerIKBridge.Interpolation.cs  # Ability IK transitions
└── PlayerIKBridge.Events.cs    # Death/respawn/equipment events

Assets/Scripts/Player/IK/
└── IKComponents.cs             # ECS components (FootIKSettings, HandIKSettings, etc.)

Assets/Scripts/Player/Authoring/IK/
└── IKAuthoring.cs              # Inspector configuration
```

---

## Troubleshooting

### Character limbs stretched to origin (0,0,0)
- **Cause**: IK interpolation arrays not initialized
- **Fix**: Ensure `_ikInterpolationStart[i] = -1f` in Awake()

### Foot IK not working
- **Check**: IK Pass enabled on Base Layer (0)
- **Check**: `FootIKSettings` component on entity
- **Check**: Character is grounded (`PlayerState.IsGrounded`)

### Hand IK not activating
- **Check**: `HandIKState.IsAiming` or `HasLeftTarget`/`HasRightTarget` is true
- **Check**: IK Pass enabled on Upper Body Layer (4)
- **Check**: `HandIKSettings.HandWeight > 0`

### Recoil not visible
- **Check**: `SpringStiffness` and `SpringDamping` are non-zero
- **Check**: `AddRecoilForce()` is being called with non-zero values

### Look At not tracking
- **Check**: `LookAtIKState.HasTarget` is true
- **Check**: `AimDirection.AimPoint` is being updated by camera system

---

## Performance Notes

- Foot IK raycasts are per-foot per-frame (~2 raycasts)
- IK calculations run in `OnAnimatorIK` callback (after animation)
- Spring updates are O(1) per hand
- Consider disabling IK for distant characters via `_ikEnabled = false`

---

## API Reference

### PlayerIKBridge Public Methods

| Method | Description |
|--------|-------------|
| `LinkState(Entity, EntityManager)` | Called automatically by linking system |
| `SetDominantHand(bool rightHanded)` | Set which hand holds weapon |
| `AddRecoilForce(Vector3 pos, Vector3 rot, bool global)` | Apply recoil to springs |
| `OnDeath()` | Disable all IK |
| `OnRespawn()` | Re-enable and recalibrate IK |
| `OnEquipmentChanged(bool isRightHanded)` | Update dominant hand |
| `OnAimStateChanged(bool isAiming)` | Toggle hand IK |
| `OnItemUseStateChanged(bool isUsing)` | Toggle item use IK |

### IKGoal Enum

```csharp
public enum IKGoal : byte
{
    LeftHand = 0,
    LeftElbow = 1,
    RightHand = 2,
    RightElbow = 3,
    LeftFoot = 4,
    LeftKnee = 5,
    RightFoot = 6,
    RightKnee = 7
}
```
