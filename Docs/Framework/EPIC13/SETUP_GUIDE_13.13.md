# Setup Guide: EPIC 13.13 (Jump System Parity)

## Overview

This epic brings the Jump system to feature parity with Opsive UCC, adding:
- **Ceiling Check** - Block jump if ceiling too low
- **Slope Limit** - Block jump on steep slopes
- **Directional Multipliers** - Reduce height for backwards/sideways jumps
- **Airborne Jumps** - Double/triple jump support
- **Hold-For-Height** - Higher jumps when holding button
- **Recurrence Delay** - Prevent jump spam after landing
- **Multi-Frame Force** - Smoother jump impulse over 2+ frames
- **Animation Events** - Sync jump force with character animation
- **Surface Impact** - Audio/VFX on jump start

---

## Quick Start (For Designers)

### Step 1: Locate the Player Prefab
1. Open Unity Editor
2. Navigate to: `Assets/Prefabs/Player/` (or your player prefab location)
3. Select the **Player prefab** and open it in the Inspector

### Step 2: Find Jump Settings
1. In the Inspector, locate the **Locomotion Ability Authoring** component
2. Locate the headers "Jump - ..." for specific settings

### Step 3: Configure Advanced Features

#### Enable Animation Sync (Requires Animator Hookup)
1. Check `WaitForAnimationEvent` = **true**
2. Ensure your Jump Animation has an event `OnAnimatorJump`
3. Add `JumpAnimationBridge` component to the GameObject with the Animator
4. Set `JumpEventTimeout` = **0.15** (failsafe in seconds)

#### Enable Multi-Frame Force
1. Set `JumpFrames` = **2** or **3** (distributes force)
2. Useful for smoothing out physics interactions

#### Enable Double Jump
1. Set `Max Airborne Jumps` = **1**
2. Set `Airborne Jump Force` = **0.6**

---

## Animation Hookup Guide (For Artists/Engineers)

To synchronize the physical jump with the animation (e.g., jump force applies when feet leave ground):

1. **Add Bridge Component**:
   - Add `JumpAnimationBridge.cs` to the GameObject that has the `Animator`.
   
2. **Configure Animation Clip**:
   - Open the Jump animation clip in the Animation window.
   - Add an **Animation Event** at the frame where the character leaves the ground.
   - **Function Name:** `OnAnimatorJump`
   - **Object:** (Leave empty/default)

3. **Configure Authoring**:
   - In `LocomotionAbilityAuthoring`:
   - Set `WaitForAnimationEvent` = **true**
   - Set `JumpEventTimeout` to slightly longer than your blend-out time (e.g. 0.2s).

---

## Component Fields Reference

### JumpSettings (Configurable)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `JumpForce` | float | 8 | Base jump velocity |
| `MaxJumps` | int | 1 | Max grounded jumps |
| `GravityMultiplier` | float | 0.5 | Variable jump cut multiplier |
| `MinCeilingJumpHeight` | float | 0 | Raycast distance, 0 = disabled |
| `PreventSlopeLimitJump` | bool | false | Block jump on steep slopes |
| `SlopeLimit` | float | 45 | Max slope angle (degrees) |
| `SidewaysForceMultiplier` | float | 0.8 | Height multiplier when moving sideways |
| `BackwardsForceMultiplier` | float | 0.7 | Height multiplier when moving backwards |
| `CoyoteTime` | float | 0.15 | Grace period after leaving ground |
| `ForceHold` | float | 0.003 | Extra force per frame while held |
| `ForceDampingHold` | float | 0.5 | Damping for hold force |
| `MaxAirborneJumps` | int | 0 | 0=disabled, 1=double, 2=triple |
| `AirborneJumpForce` | float | 0.6 | Multiplier for air jump force |
| `RecurrenceDelay` | float | 0 | Seconds after landing before jump, 0 = disabled |
| `JumpFrames` | int | 1 | Distribute force over N frames |
| `ForceDamping` | float | 0 | Dampening for multi-frame force |
| `WaitForAnimationEvent` | bool | false | Wait for OnAnimatorJump event |
| `JumpEventTimeout` | float | 0.15 | Failsafe timeout for event |
| `SpawnSurfaceEffect` | bool | true | Play Audio/VFX on jump |

### JumpAbility (Runtime State - Don't Edit)

| Field | Type | Description |
|-------|------|-------------|
| `IsActive` | bool | Jump ability active |
| `JumpPressed` | bool | Input buffer |
| `IsJumping` | bool | Currently in jump |
| `HoldForce` | float | Accumulated hold force |
| `AirborneJumpsUsed` | int | Air jumps used this airtime |
| `LastLandTime` | double | For recurrence delay |
| `LastGroundedTime` | double | For coyote time |

---

## Configuration Presets

### Preset: Realistic Shooter
```
JumpForce = 6
MinCeilingJumpHeight = 0.1
PreventSlopeLimitJump = true
SlopeLimit = 40
SidewaysForceMultiplier = 0.85
BackwardsForceMultiplier = 0.75
MaxAirborneJumps = 0
RecurrenceDelay = 0.15
```

### Preset: Platformer
```
JumpForce = 10
ForceHold = 0.015
ForceDampingHold = 0.3
CoyoteTime = 0.2
MaxAirborneJumps = 1
AirborneJumpForce = 0.8
```

### Preset: Arena Shooter
```
JumpForce = 8
MaxAirborneJumps = 1
CoyoteTime = 0.1
RecurrenceDelay = 0
```

---

## Verification

### Ground Jumping
1. Enter Play Mode
2. Press **Space** to jump
3. ✅ Character jumps normally

### Ceiling Check
1. Set `MinCeilingJumpHeight = 0.1`
2. Stand under low ceiling (<10cm above head)
3. Press **Space**
4. ✅ Jump blocked

### Slope Limit
1. Set `PreventSlopeLimitJump = true`, `SlopeLimit = 45`
2. Stand on steep slope (>45°)
3. Press **Space**
4. ✅ Jump blocked

### Double Jump
1. Set `MaxAirborneJumps = 1`
2. Jump, then press **Space** mid-air
3. ✅ Second jump executes

### Hold-For-Height
1. Tap **Space** quickly
2. Hold **Space** for full duration
3. ✅ Held jump reaches higher

### Directional Multipliers
1. Set `BackwardsForceMultiplier = 0.5`
2. Jump while moving forward vs backward
3. ✅ Backward jump is noticeably shorter

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Can't jump at all | Verify `JumpForce > 0` and entity has both `JumpSettings` and `JumpAbility` |
| Ceiling blocks when nothing above | Set `MinCeilingJumpHeight = 0` to disable |
| Double jump not working | Set `MaxAirborneJumps >= 1` |
| Jump feels floaty | Reduce `ForceHold` or increase gravity |
| Can't jump on slight slopes | Increase `SlopeLimit` or set `PreventSlopeLimitJump = false` |
| Jump spamming too fast | Set `RecurrenceDelay = 0.1` or higher |
| Falling off ledges can't jump | Increase `CoyoteTime` (try 0.2) |

