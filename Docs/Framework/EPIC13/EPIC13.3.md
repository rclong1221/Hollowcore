# EPIC 13.3: Movement Polish

> **Status:** NOT STARTED  
> **Priority:** HIGH  
> **Dependencies:** EPIC 13.2 (Ability System Architecture)  
> **Reference:** `OPSIVE/.../Runtime/Character/Abilities/QuickStart.cs`, `QuickStop.cs`, `Fall.cs`

> [!IMPORTANT]
> **Architecture & Performance Requirements:**
> - **Server (Warrok_Server):** QuickStart/Stop/Turn logic runs server-side with prediction
> - **Client (Warrok_Client):** Landing animations/VFX triggered via `LandingAnimatorBridge`
> - **Burst:** Movement polish systems fully Burst-compatible, `ScheduleParallel` for all
> - **NetCode:** Speed modifiers replicated via `[GhostField]`, client interpolates
> - **Main thread:** Never block - use ECB for entity creation (e.g., landing effects)

## Overview

Implement the "game feel" abilities that make movement responsive and satisfying. These are the subtle mechanics that differentiate a good controller from a great one.

---

## Sub-Tasks

### 13.3.1 QuickStart Ability
**Status:** NOT STARTED  
**Priority:** HIGH

Acceleration boost when starting to move from standstill.

#### Algorithm (from Opsive)

```
1. Detect transition from stationary to moving (velocity near zero → input detected)
2. Apply acceleration multiplier for first N seconds
3. Play "quick start" animation (lean forward)
4. Transition to normal locomotion blend tree
```

#### Components

```csharp
public struct QuickStartAbility : IComponentData
{
    public bool IsActive;
    public float Duration; // 0.3s typical
    public float AccelerationMultiplier; // 2.0 typical
    public float MinInputMagnitude; // 0.3 to trigger
    public float ElapsedTime;
}

public struct QuickStartSettings : IComponentData
{
    public float Duration;
    public float AccelerationMultiplier;
    public float MinInputMagnitude;
    public float VelocityThreshold; // Max velocity to trigger
}
```

#### Animation Integration

- Trigger: `QuickStart` (bool or trigger)
- Blend to: Normal locomotion blend tree
- Duration: Match animation length

#### Acceptance Criteria

- [ ] Starting to move feels snappy
- [ ] Acceleration boost applies correctly
- [ ] Animation plays during quick start
- [ ] Smooth transition to normal movement

---

### 13.3.2 QuickStop Ability
**Status:** NOT STARTED  
**Priority:** HIGH

Rapid deceleration when stopping movement.

#### Algorithm

```
1. Detect transition from moving to stopping (input released while moving)
2. Apply deceleration multiplier
3. Play "quick stop" animation (lean back, skid)
4. Optional: Apply stopping distance based on current speed
```

#### Components

```csharp
public struct QuickStopAbility : IComponentData
{
    public bool IsActive;
    public float Duration;
    public float DecelerationMultiplier; // 3.0 typical
    public float MinVelocityToTrigger;
    public float3 StopDirection;
    public float ElapsedTime;
}
```

#### Acceptance Criteria

- [ ] Stopping feels crisp, not floaty
- [ ] Skid animation plays
- [ ] Higher speeds = longer stop time
- [ ] Works in any direction

---

### 13.3.3 QuickTurn Ability
**Status:** NOT STARTED  
**Priority:** MEDIUM

180-degree turn when reversing direction.

#### Algorithm

```
1. Detect sharp direction change (dot product of current velocity and input < -0.5)
2. Trigger turn animation
3. Rotate character over animation duration
4. Apply momentum preservation or loss as configured
```

#### Components

```csharp
public struct QuickTurnAbility : IComponentData
{
    public bool IsActive;
    public float TurnSpeed; // Degrees per second
    public float Duration;
    public float3 TargetDirection;
    public float MomentumRetention; // 0-1, how much speed preserved
}
```

#### Acceptance Criteria

- [ ] 180° turns trigger turn animation
- [ ] Character rotates smoothly
- [ ] Can configure momentum loss

---

### 13.3.4 SpeedChange Ability
**Status:** NOT STARTED  
**Priority:** MEDIUM

Runtime speed multiplier for buffs/debuffs.

#### Algorithm

```
1. Stack speed modifiers from various sources
2. Calculate final multiplier: Base * Σ(Modifiers)
3. Apply to movement speed calculation
4. Handle min/max speed limits
```

#### Components

```csharp
public struct SpeedModifier : IBufferElementData
{
    public int SourceId; // Unique per modifier source
    public float Multiplier; // 0.5 = half speed, 2.0 = double
    public float Duration; // -1 = permanent
    public float ElapsedTime;
}

public struct SpeedModifierState : IComponentData
{
    public float CombinedMultiplier;
    public float MinSpeed;
    public float MaxSpeed;
}
```

#### Acceptance Criteria

- [ ] Speed modifiers stack correctly
- [ ] Timed modifiers expire
- [ ] Min/max speed enforced

---

### 13.3.5 Fall Ability
**Status:** NOT STARTED  
**Priority:** HIGH

Dedicated fall state with landing effects.

#### Algorithm

```
1. Detect not grounded + downward velocity
2. Track fall distance and duration
3. At min fall height: enter fall state, play animation
4. On land: play landing animation, apply screen shake
5. High falls: landing recovery, damage
```

#### Components

```csharp
public struct FallAbility : IComponentData
{
    public bool IsFalling;
    public float FallStartHeight;
    public float FallDuration;
    public float MinFallHeightForAnimation; // 1.5m
    public float HardLandingHeight; // 3m
    public float MaxSafeFallHeight; // 10m
}

public struct LandingEffect : IComponentData
{
    public float RecoveryDuration;
    public float CameraShakeIntensity;
    public bool TriggerHardLanding;
}
```

#### Acceptance Criteria

- [ ] Fall animation plays when falling far enough
- [ ] Landing animation scales with fall distance
- [ ] Hard landing has recovery time
- [ ] Camera shake on impact

---

### 13.3.6 Idle Ability
**Status:** NOT STARTED  
**Priority:** LOW

Proper idle state machine with random variations.

#### Algorithm

```
1. When player stationary for IdleDelay seconds:
   - Enable idle randomization
2. After RandomInterval seconds:
   - Play random idle variation
3. On any input: exit idle state

Optional: "Impatient" idles after long waits
```

#### Components

```csharp
public struct IdleAbility : IComponentData
{
    public float IdleTime;
    public float NextVariationTime;
    public float MinVariationInterval;
    public float MaxVariationInterval;
    public int CurrentVariation;
    public int VariationCount;
}
```

#### Acceptance Criteria

- [ ] Idle variations play randomly
- [ ] Immediate exit on input
- [ ] Configurable timing

---

### 13.3.7 RestrictPosition/Rotation
**Status:** NOT STARTED  
**Priority:** LOW

Clamp character to bounds or angles.

#### Use Cases

- Prevent falling off edges (invisible walls)
- Restrict rotation during cutscenes
- Confine to play area

#### Components

```csharp
public struct RestrictPosition : IComponentData
{
    public float3 Min;
    public float3 Max;
    public bool3 AxesEnabled; // Which axes to restrict
}

public struct RestrictRotation : IComponentData
{
    public float MinYaw, MaxYaw;
    public float MinPitch, MaxPitch;
    public bool RestrictYaw;
    public bool RestrictPitch;
}
```

#### Acceptance Criteria

- [ ] Position clamped to bounds
- [ ] Rotation clamped to angles
- [ ] Can enable/disable per-axis

---

### 13.3.8 MoveTowards Ability
**Status:** NOT STARTED  
**Priority:** LOW

Pathfinding-assisted movement to a target.

#### Algorithm

```
1. Set target position
2. Player auto-navigates toward target
3. Optional: Use NavMesh for pathfinding
4. Stop when within threshold distance
5. Optional: Face target on arrival
```

#### Use Cases

- Walk to interaction points
- Cutscene positioning
- AI-controlled player movement

#### Components

```csharp
public struct MoveTowardsAbility : IComponentData
{
    public float3 TargetPosition;
    public float StopDistance;
    public float MoveSpeed;
    public bool FaceTargetOnArrival;
    public bool UseNavMesh;
    public bool IsMoving;
}
```

#### Acceptance Criteria

- [ ] Player auto-moves to target
- [ ] Stops at correct distance
- [ ] Works with/without NavMesh

---

## Files to Create

| File | Purpose |
|------|---------|
| `QuickStartSystem.cs` | Quick start ability logic |
| `QuickStopSystem.cs` | Quick stop ability logic |
| `QuickTurnSystem.cs` | Quick turn ability logic |
| `SpeedModifierSystem.cs` | Speed modifier stacking |
| `FallAbilitySystem.cs` | Fall detection and landing |
| `IdleAbilitySystem.cs` | Idle variation logic |
| `RestrictPositionSystem.cs` | Position clamping |
| `RestrictRotationSystem.cs` | Rotation clamping |
| `MoveTowardsSystem.cs` | Auto-navigation |
| `MovementPolishComponents.cs` | All components |
| `MovementPolishAuthoring.cs` | Inspector configuration |

## Verification Plan

### Manual Verification

1. **QuickStart:** Start moving, feel initial boost
2. **QuickStop:** Release input while running, observe skid
3. **QuickTurn:** Run forward, press back, observe turn animation
4. **Fall:** Jump off ledge, observe falling and landing
5. **Idle:** Stand still for 10+ seconds, observe variations

## Designer Setup Guide

### Tuning Quick Movement

| Setting | Conservative | Responsive | Arcade |
|---------|-------------|------------|--------|
| QuickStart Duration | 0.2s | 0.3s | 0.5s |
| QuickStart Multiplier | 1.5x | 2.0x | 3.0x |
| QuickStop Duration | 0.3s | 0.2s | 0.1s |
| QuickStop Multiplier | 2.0x | 3.0x | 5.0x |
| QuickTurn Speed | 360°/s | 540°/s | 720°/s |

### Fall Damage Thresholds

| Fall Height | Effect |
|-------------|--------|
| < 1.5m | No landing animation |
| 1.5m - 3m | Light landing animation |
| 3m - 10m | Hard landing + recovery |
| > 10m | Damage + hard landing |
