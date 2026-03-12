# Epic 13.1 Core Locomotion - Setup Guide

> **Version:** 1.0.0  
> **Last Updated:** 2025-12-28  
> **Status:** IMPLEMENTED

This guide explains how to set up and use the Epic 13.1 Core Locomotion Enhancements, including Movement Polish, Moving Platforms, and External Forces.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Movement Polish Setup](#movement-polish-setup)
3. [Moving Platform Setup](#moving-platform-setup)
4. [External Force Zone Setup](#external-force-zone-setup)
5. [Developer Reference](#developer-reference)
6. [Tuning Guide](#tuning-guide)
7. [Troubleshooting](#troubleshooting)

---

## Quick Start

### Minimum Setup (Player)

1. Open the player prefab: `Assets/Prefabs/Warrok_Server.prefab`
2. Add the following components:
   - **Locomotion Polish Authoring** - Movement polish (backwards speed, slopes)
   - **Moving Platform Player Authoring** - Platform riding support
3. Save prefab

### Create a Moving Platform

1. Create any GameObject with movement (animation, script, etc.)
2. Add **Moving Platform Authoring** component
3. Ensure it has a `Collider` (not trigger)
4. Done - players landing on it will ride it

### Create a Wind Zone

1. Create an empty GameObject
2. Add a `Box Collider` (or any collider) as **Trigger**
3. Add **External Force Zone Authoring**
4. Set Force direction and magnitude
5. Done - players entering will be pushed

---

## Movement Polish Setup

### Adding to Player Prefab

1. **Select** `Assets/Prefabs/Warrok_Server.prefab`
2. **Add Component** → `Player/Authoring/Locomotion Polish Authoring`
3. **Choose Preset:**
   - `Default` - Balanced feel
   - `Arcade` - Snappy, responsive (good for action games)
   - `Realistic` - Weighty, momentum-based (good for simulation)
   - `Custom` - Manual configuration

### Inspector Settings

| Setting | Description | Default |
|---------|-------------|---------|
| **Motor Backwards Multiplier** | Speed when walking backwards (0.7 = 70%) | 0.7 |
| **Previous Acceleration Influence** | Momentum preservation (0 = instant, 1 = heavy) | 0.5 |
| **Adjust Motor Force On Slope** | Enable slope speed changes | ✓ |
| **Motor Slope Force Up** | Speed multiplier going uphill | 1.0 |
| **Motor Slope Force Down** | Speed multiplier going downhill | 1.15 |
| **Ground Stick Distance** | Prevents bouncing on slopes | 0.1 |
| **Wall Friction Modifier** | Wall sliding friction | 1.0 |
| **Wall Bounce Modifier** | Bounce off walls (0 = disabled) | 0 |
| **Ground Friction Modifier** | Ground friction multiplier | 1.0 |
| **Continuous Collision Detection** | Check collisions when stationary | ✓ |
| **Max Penetration Checks** | Collision resolution iterations | 5 |
| **Cancel Force On Ceiling** | Stop upward force on ceiling hit | ✓ |
| **Max Soft Force Frames** | Frames to distribute impulses | 30 |

### Preset Comparison

| Setting | Arcade | Default | Realistic |
|---------|--------|---------|-----------|
| Backwards Multiplier | 0.9 | 0.7 | 0.6 |
| Acceleration Influence | 0.3 | 0.5 | 1.0 |
| Slope Force Up | 1.0 | 1.0 | 0.8 |
| Slope Force Down | 1.0 | 1.15 | 1.3 |
| Ground Stick | 0.05 | 0.1 | 0.15 |

---

## Moving Platform Setup

### Creating a Moving Platform

1. **Create Platform Object:**
   - Create or import your platform mesh
   - Add a `Collider` (Box, Mesh, or Capsule - NOT trigger)
   - Put on appropriate layer (e.g., "Default" or "Ground")

2. **Add Platform Component:**
   - Add Component → `Player/Authoring/Moving Platform Authoring`

3. **Configure:**

| Setting | Description | Default |
|---------|-------------|---------|
| **Inherit Momentum On Disconnect** | Player gets platform velocity when jumping off | ✓ |
| **Sudden Stop Threshold** | Threshold for detecting sudden platform stops (m/s) | 20 |
| **Show Debug Gizmos** | Show platform bounds in editor | ✓ |

4. **Animate Platform:**
   - Use Animation, DOTween, or any movement system
   - The system auto-calculates velocity from position changes

### Adding Platform Support to Player

1. **Select** `Assets/Prefabs/Warrok_Server.prefab`
2. **Add Component** → `Player/Authoring/Moving Platform Player Authoring`
3. **Configure:**

| Setting | Description | Default |
|---------|-------------|---------|
| **Momentum Decay Duration** | How long momentum lasts after leaving (seconds) | 0.5 |
| **Min Velocity For Momentum** | Ignore tiny platform movements (m/s) | 0.5 |
| **Rotate With Platform** | Rotate player with platform | ✓ |

### Platform Types

**Linear Platforms:**
- Elevators, moving floors
- Automatic velocity inheritance

**Rotating Platforms:**
- Turntables, spinning platforms
- Player rotates with platform if enabled

**Path-Following Platforms:**
- Ferris wheels, conveyor loops
- Works with any animation system

---

## External Force Zone Setup

### Creating a Wind Zone

1. **Create Zone Object:**
   - Create Empty GameObject
   - Add `Box Collider` (or other collider shape)
   - **Enable "Is Trigger"** checkbox

2. **Add Force Zone:**
   - Add Component → `Player/Authoring/External Force Zone Authoring`

3. **Configure:**

| Setting | Description | Default |
|---------|-------------|---------|
| **Force** | Direction and magnitude (X, Y, Z) | (0, 0, 10) |
| **Is Directional** | Force in fixed direction vs radial | ✓ |
| **Radial Center** | Center for radial forces (optional) | - |
| **Mode** | Continuous (every frame) or Impulse (once) | Continuous |
| **Exit Damping** | How quickly force stops when leaving | 5 |
| **Priority** | Override priority for overlapping zones | 0 |

### Force Zone Types

**Wind Tunnels:**
```
Force: (15, 0, 0)  // Push right
Mode: Continuous
Exit Damping: 3
```

**Updrafts:**
```
Force: (0, 20, 0)  // Push up
Mode: Continuous
Exit Damping: 5
```

**Explosion Zones:**
```
Force: (50, 10, 0)  // Explosion strength
Is Directional: false
Mode: Impulse
Exit Damping: 10
```

**Conveyor Belts:**
```
Force: (5, 0, 0)  // Gentle push
Mode: Continuous
Exit Damping: 8
```

### Gizmo Visualization

- **Blue box:** Zone bounds
- **Yellow arrow:** Force direction (directional)
- **Red rays:** Radial force directions

---

## Developer Reference

### Component Locations

| Component | File | Namespace |
|-----------|------|-----------|
| MotorPolishSettings | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| MotorPolishState | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| WallSlideSettings | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| CollisionPolishSettings | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| SoftForceFrame | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| SoftForceSettings | `Components/LocomotionPolishComponents.cs` | `Player.Components` |
| MovingPlatform | `Components/MovingPlatformComponents.cs` | `Player.Components` |
| OnMovingPlatform | `Components/MovingPlatformComponents.cs` | `Player.Components` |
| PlatformMomentum | `Components/MovingPlatformComponents.cs` | `Player.Components` |
| MovingPlatformSettings | `Components/MovingPlatformComponents.cs` | `Player.Components` |
| ExternalForceZone | `Components/ExternalForceComponents.cs` | `Player.Components` |
| ExternalForceElement | `Components/ExternalForceComponents.cs` | `Player.Components` |
| ExternalForceState | `Components/ExternalForceComponents.cs` | `Player.Components` |
| ExternalForceSettings | `Components/ExternalForceComponents.cs` | `Player.Components` |
| AddExternalForceRequest | `Components/ExternalForceComponents.cs` | `Player.Components` |

### System Update Order

```
PredictedFixedStepSimulationSystemGroup
├── PlayerStateSystem (existing)
├── MovementPolishSystem       [NEW] ← Prepares polish data
├── ExternalForceSystem        [NEW] ← Accumulates forces
├── PlayerMovementSystem       [MODIFIED] ← Uses polish + forces
├── CharacterControllerSystem  (existing)
└── MovingPlatformSystem       [NEW] ← Platform attachment
```

### Applying Forces Programmatically

```csharp
// Add explosion force to a player
var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);

// Method 1: Direct force request
ecb.SetComponent(playerEntity, new AddExternalForceRequest
{
    Force = new float3(10, 5, 0),
    SoftFrames = 15,  // Distribute over 15 frames
    Decay = 8f,
    SourceId = myExplosionId
});
ecb.SetComponentEnabled<AddExternalForceRequest>(playerEntity, true);

// Method 2: Use utility
var request = ExternalForceUtility.CreateExplosionForce(
    explosionCenter: explosionPos,
    targetPosition: playerPos,
    forceMagnitude: 50f,
    radius: 10f,
    softFrames: 10
);
ecb.SetComponent(playerEntity, request);
ecb.SetComponentEnabled<AddExternalForceRequest>(playerEntity, true);
```

### Using Motor Polish Utilities

```csharp
using Player.Systems;
using Player.Components;

// In your system:
var settings = SystemAPI.GetComponent<MotorPolishSettings>(entity);

// Get backwards multiplier
float2 input = new float2(horizontal, vertical);
float backMult = MotorPolishUtility.GetBackwardsMultiplier(input, in settings);

// Get slope multiplier
var polishState = SystemAPI.GetComponent<MotorPolishState>(entity);
float slopeMult = MotorPolishUtility.GetSlopeMultiplier(
    polishState.IsMovingUphill, 
    polishState.CurrentSlopeAngle, 
    in settings
);

// Apply to speed
float finalSpeed = baseSpeed * backMult * slopeMult;
```

---

## Tuning Guide

### Game Feel Presets

**Fast-Paced Action (Arena Shooter):**
```
Backwards Multiplier: 0.9
Acceleration Influence: 0.2
Slope Forces: Disabled
Soft Force Frames: 5
```

**Adventure/Exploration:**
```
Backwards Multiplier: 0.75
Acceleration Influence: 0.5
Slope Up: 0.9, Down: 1.1
Soft Force Frames: 20
```

**Simulation/Tactical:**
```
Backwards Multiplier: 0.5
Acceleration Influence: 0.8
Slope Up: 0.7, Down: 1.3
Soft Force Frames: 40
```

### Platform Momentum Tuning

| Game Style | Decay Duration | Min Velocity |
|------------|---------------|--------------|
| Precision Platformer | 0.2s | 0.2 |
| Action Adventure | 0.5s | 0.5 |
| Physics-Based | 1.0s | 0.1 |

### Force Zone Tuning

| Effect | Force Magnitude | Exit Damping |
|--------|-----------------|--------------|
| Light Breeze | 2-5 | 8 |
| Strong Wind | 8-15 | 5 |
| Hurricane | 20-40 | 3 |
| Explosion | 30-80 | 10 |
| Conveyor | 3-8 | 10 |

---

## Troubleshooting

### Player Not Sticking to Platform

1. **Check collider:** Platform must have non-trigger collider
2. **Check layer:** Platform should be on raycast-visible layer
3. **Check component:** Ensure `MovingPlatformAuthoring` is present
4. **Check player:** Ensure `MovingPlatformPlayerAuthoring` is on player

### Platform Momentum Not Working

1. **Check velocity threshold:** Platform may be moving too slowly
2. **Check settings:** `InheritMomentumOnDisconnect` must be enabled
3. **Check decay:** `MomentumDecayDuration` > 0

### External Force Not Pushing

1. **Check trigger:** Collider must be set as trigger
2. **Check force magnitude:** Increase Force values
3. **Check damping:** Lower Exit Damping for longer effect
4. **Check mode:** Use Continuous for constant push

### Backwards Speed Not Working

1. **Check component:** `LocomotionPolishAuthoring` needed on player
2. **Check value:** `MotorBackwardsMultiplier` < 1.0
3. **Check integration:** System runs before `PlayerMovementSystem`

### Slope Speed Not Working

1. **Check enabled:** `AdjustMotorForceOnSlope` must be true
2. **Check values:** Up/Down multipliers should differ from 1.0
3. **Check detection:** May need steeper slopes to trigger

---

## Files Reference

### New Files Created

**Components:**
- `Assets/Scripts/Player/Components/LocomotionPolishComponents.cs`
- `Assets/Scripts/Player/Components/MovingPlatformComponents.cs`
- `Assets/Scripts/Player/Components/ExternalForceComponents.cs`

**Systems:**
- `Assets/Scripts/Player/Systems/MovementPolishSystem.cs`
- `Assets/Scripts/Player/Systems/MovingPlatformSystem.cs`
- `Assets/Scripts/Player/Systems/ExternalForceSystem.cs`

**Authoring:**
- `Assets/Scripts/Player/Authoring/LocomotionPolishAuthoring.cs`
- `Assets/Scripts/Player/Authoring/MovingPlatformAuthoring.cs`
- `Assets/Scripts/Player/Authoring/ExternalForceZoneAuthoring.cs`

### Modified Files

- `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs`
  - Added polish multiplier integration
  - Added external force application

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial implementation |
