# Setup Guide: EPIC 13.14 (Fall System Parity)

## Overview

This epic brings the Fall system to feature parity with Opsive UCC, adding:
- **Minimum Fall Height** - Only trigger fall animation above threshold
- **Surface Impact** - Audio/VFX on landing (surface-aware)
- **Animation Events** - Wait for land animation to complete
- **Blend Tree Data** - Send velocity to animator for smooth transitions
- **Teleport Handling** - Clean fall state on immediate transform changes
- **State Index** - Track fall stage for animator state machine

---

## Quick Start (For Designers)

### Step 1: Locate the Player Prefab
1. Open Unity Editor
2. Navigate to: `Assets/Prefabs/Player/` (or your player prefab location)
3. Select the **Player prefab** and open it in the Inspector

### Step 2: Find Fall Settings
1. In the Inspector, locate the **Movement Polish Authoring** component
2. Expand the "Fall - ..." headers for specific settings

### Step 3: Configure Features

#### Minimum Fall Height
1. Set `MinFallHeight` = **0.2** (default) to ignore tiny drops
2. Set to **0** to detect any height as a fall
3. Set `MinFallHeightForAnimation` = **1.5** for when fall anim plays

#### Landing Effects
1. Set `MinSurfaceImpactVelocity` = **-4** (negative = falling down)
2. Softer landings (slower than -4 m/s) won't trigger VFX
3. Configure `LandSurfaceImpactId` for specific impact preset

#### Fall Damage Thresholds
1. `HardLandingHeight` = **3.0** - Extra stumble animation
2. `MaxSafeFallHeight` = **6.0** - Falls beyond this deal damage

---

## Animation Hookup Guide (For Artists/Engineers)

To synchronize the fall ability with landing animations:

1. **Add Bridge Component**:
   - Add `FallAnimatorBridge.cs` to the GameObject that has the `Animator`.

2. **Configure Animation Clip**:
   - Open the Land animation clip in the Animation window.
   - Add an **Animation Event** at the frame where recovery completes.
   - **Function Name:** `OnAnimatorFallComplete`
   - **Object:** (Leave empty/default)

3. **Configure Authoring**:
   - In `MovementPolishAuthoring`:
   - Set `WaitForLandEvent` = **true**
   - Set `LandEventTimeout` = **1.0** (failsafe in seconds)

4. **Animator Parameters** (set automatically by bridge):
   - `IsFalling` (bool) - Currently in fall state
   - `FallVelocity` (float) - Vertical speed for blend tree
   - `FallStateIndex` (int) - 0 = falling, 1 = landed

---

## Teleportation Integration

When teleporting a player, you must signal the fall system to prevent stuck states:

### Option A: Use TeleportEvent Component
```csharp
// Enable component and set destination
var teleport = new TeleportEvent
{
    TargetPosition = destination,
    TargetRotation = rotation,
    SnapAnimator = true
};
ecb.SetComponent(playerEntity, teleport);
ecb.SetComponentEnabled<TeleportEvent>(playerEntity, true);
// TeleportSystem will handle the rest
```

### Option B: Direct Transform Move
```csharp
// If moving transform directly, set the flag first
fallAbility.PendingImmediateTransformChange = true;
transform.Position = newPosition;
// FallDetectionSystem will clean up fall state
```

---

## Component Fields Reference

### FallSettings (Configurable)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `MinFallHeight` | float | 0.2 | Min height to trigger fall ability (0 = any) |
| `MinFallHeightForAnimation` | float | 1.5 | Min height for fall animation |
| `HardLandingHeight` | float | 3.0 | Height for hard landing effects |
| `MaxSafeFallHeight` | float | 6.0 | Max height without damage |
| `LandSurfaceImpactId` | int | 0 | Surface impact preset ID |
| `MinSurfaceImpactVelocity` | float | -4 | Min velocity for impact VFX (negative) |
| `WaitForLandEvent` | bool | true | Wait for animation event |
| `LandEventTimeout` | float | 1.0 | Failsafe timeout (seconds) |
| `SolidObjectLayerMask` | uint | 1 | Layers for ground raycast |

### FallAbility (Runtime State - Don't Edit)

| Field | Type | Description |
|-------|------|-------------|
| `IsFalling` | bool | Currently in fall state |
| `FallStartHeight` | float | Y position when fall began |
| `FallDuration` | float | Time spent falling |
| `StateIndex` | int | 0 = falling, 1 = landed |
| `Landed` | bool | Just touched ground |
| `WaitingForAnimationEvent` | bool | Waiting for OnAnimatorFallComplete |
| `AnimationEventTimer` | float | Timeout counter |
| `PendingImmediateTransformChange` | bool | Teleport flag |

### SurfaceImpactRequest (Event Component)

| Field | Type | Description |
|-------|------|-------------|
| `ContactPoint` | float3 | World position of impact |
| `ContactNormal` | float3 | Ground normal at impact |
| `ImpactVelocity` | float | Downward velocity at impact |
| `SurfaceMaterialId` | int | Material ID for audio/VFX |
| `SurfaceImpactId` | int | Impact preset ID |

### TeleportEvent (Event Component)

| Field | Type | Description |
|-------|------|-------------|
| `TargetPosition` | float3 | Destination position |
| `TargetRotation` | quaternion | Destination rotation |
| `SnapAnimator` | bool | Snap animator state |

---

## Configuration Presets

### Preset: Realistic Shooter
```
MinFallHeight = 0.3
MinFallHeightForAnimation = 2.0
HardLandingHeight = 4.0
MaxSafeFallHeight = 8.0
MinSurfaceImpactVelocity = -5
WaitForLandEvent = true
```

### Preset: Platformer (Forgiving)
```
MinFallHeight = 0
MinFallHeightForAnimation = 3.0
HardLandingHeight = 10.0
MaxSafeFallHeight = 999 (no fall damage)
MinSurfaceImpactVelocity = -8
WaitForLandEvent = false
```

### Preset: Survival Horror (Punishing)
```
MinFallHeight = 0.1
MinFallHeightForAnimation = 1.0
HardLandingHeight = 2.0
MaxSafeFallHeight = 4.0
MinSurfaceImpactVelocity = -3
WaitForLandEvent = true
```

---

## Test Environment

Test objects are available via: `GameObject > DIG - Test Objects > Traversal > Complete Test Course`

The Fall Tests section (Section 15) includes:

### Height Tower (13.14.T1)
- Platforms from 0.5m to 20m
- Color-coded danger levels
- Telepads for high platforms

### Landing Surface Pads (13.14.T2)
- 5 material types (Concrete, Grass, Metal, Water, Mud)
- Tests surface-aware audio/VFX

### Velocity Threshold Test (13.14.T3)
- Slide ramp (gradual velocity)
- Drop hole (instant fall)
- Multi-story stairwell

### Teleport Mid-Fall Test (13.14.T4)
- 20m tower with mid-air telepad
- Tests PendingImmediateTransformChange

---

## Verification

### Basic Fall Detection
1. Enter Play Mode
2. Walk off a ledge (>0.2m)
3. ✅ Fall animation plays
4. ✅ Land animation plays on ground contact

### Minimum Height Check
1. Set `MinFallHeight = 1.0`
2. Step off a 0.5m ledge
3. ✅ No fall animation (too short)
4. Step off a 2m ledge
5. ✅ Fall animation plays

### Surface Impact VFX
1. Jump from 5m height onto different surfaces
2. ✅ Dust/particles spawn on landing
3. ✅ Audio plays (different per surface)
4. Soft land from 1m
5. ✅ No VFX (velocity below threshold)

### Animation Event Wait
1. Set `WaitForLandEvent = true`
2. Fall from height
3. ✅ Character plays full land animation before moving

### Teleport During Fall
1. Jump off the 20m tower in Teleport Test
2. Pass through the mid-air telepad
3. ✅ Teleported to ground instantly
4. ✅ No stuck-in-fall state
5. ✅ Can move immediately

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No fall animation | Check `MinFallHeightForAnimation` threshold |
| Fall triggers on tiny drops | Increase `MinFallHeight` (try 0.3) |
| No landing VFX | Check `MinSurfaceImpactVelocity` or verify AudioManager exists |
| Stuck in fall state | Check `WaitForLandEvent` timeout or animation event setup |
| Wrong surface audio | Verify `SurfaceMaterialId` component on floor objects |
| Teleport leaves character falling | Set `PendingImmediateTransformChange = true` before moving |
| Fall damage not working | This is handled by a separate damage system, not 13.14 |

---

## Files Reference

### Core Systems
| File | Description |
|------|-------------|
| `FallDetectionSystem.cs` | Main fall logic (Burst-compiled) |
| `TeleportSystem.cs` | Processes teleport events |
| `SurfaceImpactPresentationSystem.cs` | Spawns VFX/audio |
| `FallAnimationEventReceiverSystem.cs` | Bridges animation events |

### Components
| File | Description |
|------|-------------|
| `MovementPolishComponents.cs` | FallAbility, FallSettings, SurfaceImpactRequest |
| `TeleportEvent.cs` | Teleportation signal component |
| `PlayerAnimationStateComponent.cs` | IsFalling, FallVelocity, FallStateIndex |

### Bridges
| File | Description |
|------|-------------|
| `FallAnimatorBridge.cs` | Animator sync + event handling |

### Authoring
| File | Description |
|------|-------------|
| `MovementPolishAuthoring.cs` | Inspector configuration |

