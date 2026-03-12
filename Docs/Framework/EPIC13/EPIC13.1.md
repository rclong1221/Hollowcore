# EPIC 13.1: Core Locomotion Enhancements

> **Status:** PARTIALLY IMPLEMENTED  
> **Priority:** HIGH  
> **Dependencies:** EPIC 1 (Character Controller)  
> **Reference:** `OPSIVE/.../Runtime/Character/CharacterLocomotion.cs`
> **Setup Guide:** [SETUP_GUIDE_13.1.md](./SETUP_GUIDE_13.1.md)

> [!IMPORTANT]
> **Architecture & Performance Requirements:**
> - **Server (Warrok_Server):** All locomotion systems run in `PredictedSimulationSystemGroup`
> - **Client (Warrok_Client):** Visual feedback via hybrid bridges (AnimatorRigBridge, etc.)
> - **Burst:** All systems must be `[BurstCompile]` with `ScheduleParallel()` where possible
> - **NetCode:** Movement state uses `[GhostField]` for replication; see [README](./README.md) for patterns
> - **No main-thread blocking:** Use `NativeArray`, avoid managed collections in hot paths

> [!NOTE]
> **Implementation Notes (Dec 2024):**
> - **Bool → Byte:** All `bool` fields converted to `byte` for Burst blittability (avoids `[MarshalAs]` overhead)
> - **Inlined Math:** Utility classes removed; calculations inlined in jobs to avoid function pointer issues
> - **Single-Pass Processing:** `ExternalForceSystem` uses single-pass buffer iteration (3x fewer iterations)
> - **Correct Components:** `MovementPolishSystem` uses `PlayerState.IsGrounded` (not `PlayerCollisionState`)

## Overview

Enhance the core character controller with foundational movement features that enable advanced gameplay mechanics. These features form the basis for all other controller improvements.

### Current Limitations

1. **No Moving Platform Support** - Player clips through or slides off moving objects
2. **No Root Motion** - Animations don't drive movement
3. **No External Forces** - No wind, conveyor belts, or push zones
4. **Fixed Gravity** - Only supports -Y gravity
5. **Fixed Collider Shape** - Only CapsuleCollider supported

---

## Sub-Tasks

### 13.1.1 Moving Platform Support
**Status:** ✅ IMPLEMENTED  
**Priority:** HIGH

Enable the character to properly stand on and move with moving/rotating platforms.

#### Algorithm (from Opsive `UpdateMovingPlatformMovement`)

```
1. OnGroundedHit: Check if ground has MovingPlatform component
2. If moving platform detected:
   - Store relative position: LocalPos = Platform.InverseTransformPoint(PlayerPos)
   - Store relative rotation: LocalRot = Inverse(Platform.Rot) * Player.Rot
3. Each tick while on platform:
   - Update player position: PlayerPos = Platform.TransformPoint(LocalPos)
   - Update player rotation: PlayerRot = Platform.Rot * LocalRot
4. On disconnect:
   - Inherit platform linear velocity
   - Inherit platform angular velocity (as tangential velocity)
   - Apply momentum decay over time
```

#### New Components

```csharp
// Tag for moving platforms
public struct MovingPlatform : IComponentData
{
    public float3 LastPosition;
    public quaternion LastRotation;
}

// Player state extension
public struct OnMovingPlatform : IComponentData
{
    public Entity PlatformEntity;
    public float3 LocalPosition;
    public quaternion LocalRotation;
    public float3 DisconnectVelocity;
    public float DisconnectDecayTimer;
}
```

#### New System

**File:** `Assets/Scripts/Player/Systems/MovingPlatformSystem.cs`

**Update Order:**
```
CharacterControllerSystem (ground detection)
    ↓
MovingPlatformSystem (attach/detach/update)
    ↓
PlayerMovementSystem (uses platform-adjusted position)
```

#### Acceptance Criteria

- [ ] Player stays on moving platform without sliding
- [ ] Player rotates with rotating platform
- [ ] Jumping off platform inherits platform velocity
- [ ] Momentum decays after disconnect
- [ ] Works with NetCode prediction

---

### 13.1.2 Root Motion Integration
**Status:** NOT STARTED  
**Priority:** HIGH

Use animation root motion to drive character movement for more natural locomotion.

#### Algorithm

```
1. AnimatorBridge captures root motion delta each frame:
   - deltaPosition = animator.deltaPosition
   - deltaRotation = animator.deltaRotation
2. System reads root motion data via hybrid bridge
3. Blend root motion with input-driven movement:
   - FinalMove = Lerp(InputMove, RootMotionMove, RootMotionWeight)
4. Apply grounding adjustment (keep on ground)
```

#### New Components

```csharp
public struct RootMotionData : IComponentData
{
    public float3 DeltaPosition;
    public quaternion DeltaRotation;
    public float Weight; // 0 = full input, 1 = full root motion
}

public struct RootMotionSettings : IComponentData
{
    public float PositionWeight;
    public float RotationWeight;
    public bool UseRootMotionForMovement;
    public bool UseRootMotionForRotation;
}
```

#### Implementation

**File:** `Assets/Scripts/Player/Bridges/RootMotionBridge.cs` (MonoBehaviour)
**File:** `Assets/Scripts/Player/Systems/RootMotionApplySystem.cs`

#### Acceptance Criteria

- [ ] Animation root motion captured each frame
- [ ] Root motion blends with input movement
- [ ] Weight is configurable per-animation state
- [ ] Works with existing locomotion blend tree

---

### 13.1.3 External Force System
**Status:** ✅ IMPLEMENTED  
**Priority:** MEDIUM

Unified system for environmental forces (wind, conveyor belts, explosions, etc.).

#### Algorithm

```
1. ExternalForceZone triggers add force to player
2. Forces accumulate over frame
3. Apply damping based on force type
4. Decay forces over time when out of zone
```

#### New Components

```csharp
public struct ExternalForce : IBufferElementData
{
    public float3 Force;
    public float Decay; // Per-second decay rate
    public ForceMode Mode; // Continuous, Impulse
}

public struct ExternalForceZone : IComponentData
{
    public float3 Force;
    public float Damping;
    public bool IsDirectional; // vs radial
}
```

#### Acceptance Criteria

- [ ] Wind zones push player
- [ ] Conveyor belts move player
- [ ] Explosion impulses apply correctly
- [ ] Forces decay over time

---

### 13.1.4 Variable Gravity Direction
**Status:** NOT STARTED  
**Priority:** LOW

Support arbitrary gravity directions for special level sections.

#### Algorithm

```
1. GravityZone trigger overrides player's GravityDirection
2. Character controller uses GravityDirection instead of hardcoded -Y
3. Player rotation aligns to new "up" direction over time
4. AlignToGravityZone ability handles smooth rotation
```

#### New Components

```csharp
public struct GravityOverride : IComponentData
{
    public float3 Direction; // Normalized
    public float Strength; // Multiplier
    public float TransitionSpeed; // Rotation lerp
}

public struct SphericalGravityZone : IComponentData
{
    public float3 Center;
    public float Strength;
    public float InnerRadius;
    public float OuterRadius;
}
```

#### Acceptance Criteria

- [ ] Gravity zones change player gravity direction
- [ ] Player smoothly rotates to new up direction
- [ ] Spherical gravity pulls toward center
- [ ] Exit gravity zone restores default gravity

---

### 13.1.5 Variable Time Scale
**Status:** NOT STARTED  
**Priority:** LOW

Properly handle slow-motion and speed-up effects.

#### Implementation

- Use `Time.timeScale` aware delta time
- Animation speed scales with time
- Physics queries use scaled delta
- Audio pitch optionally scales

#### Acceptance Criteria

- [ ] Slow-motion affects player movement correctly
- [ ] Animations slow down proportionally
- [ ] Physics remains stable at low time scale

---

### 13.1.6 Multi-Collider Support
**Status:** NOT STARTED  
**Priority:** LOW

Support SphereCollider and BoxCollider in addition to CapsuleCollider.

#### Acceptance Criteria

- [ ] Sphere collider mode for ball-like characters
- [ ] Box collider mode for vehicle-like characters
- [ ] Collision detection works for all shapes

---

### 13.1.7 Rotation Collision Detection
**Status:** NOT STARTED  
**Priority:** MEDIUM

Prevent the character from rotating into walls.

#### Algorithm (from Opsive `CheckRotation`)

```
1. Before applying rotation, check if new rotation would cause overlap
2. For each rotation collider (usually just the main capsule):
   - Calculate new collider bounds at rotated position
   - OverlapCapsule/Box at new orientation
   - If overlap detected: clamp rotation to avoid collision
3. Return valid rotation delta
```

#### Acceptance Criteria

- [ ] Rotation blocked when it would cause wall penetration
- [ ] Partial rotation allowed (slide along wall)
- [ ] Works for all collider shapes

---

### 13.1.8 Movement & Collision Polish
**Status:** ✅ IMPLEMENTED  
**Priority:** HIGH

Fine-grained movement and collision features that dramatically improve "game feel". These are the subtle mechanics from Opsive's CharacterLocomotion that make movement feel polished.

---

#### 13.1.8.1 Wall Glide Curve
**Status:** NOT STARTED

Control how much the character slides along walls based on approach angle.

##### Algorithm

```
1. When colliding with wall, calculate dot product:
   movementDot = Vector3.Dot(movementDir, wallNormal)
   // 0 = facing wall directly, 1 = parallel to wall
   
2. Evaluate curve to get slide amount:
   slideAmount = WallGlideCurve.Evaluate(1 - movementDot)
   
3. Apply slide:
   targetPosition = hitPosition + slideAmount * slideDirection
```

##### Components

```csharp
public struct WallSlideSettings : IComponentData
{
    // AnimationCurve baked to BlobAsset
    public BlobAssetReference<AnimationCurveBlob> WallGlideCurve;
    public float WallFrictionModifier;  // 1.0
    public float WallBounceModifier;    // 2.0 - bouncy walls!
}
```

##### Default Curve Values
```
Keyframe(0, 0)     // Facing wall = no slide
Keyframe(0.1, 0.5) // Slight angle = half slide
Keyframe(1, 0.5)   // Parallel = half slide
```

##### Acceptance Criteria

- [ ] AnimationCurve configurable in Inspector
- [ ] Curve baked to BlobAsset for Burst
- [ ] Wall bounce applies reflect velocity
- [ ] Wall friction uses PhysicMaterial

---

#### 13.1.8.2 Previous Acceleration Influence
**Status:** NOT STARTED

Control momentum preservation when changing direction.

##### Algorithm

```csharp
// Store previous motor rotation
var prevLocalMotorThrottle = InverseTransformDirection(MotorThrottle, PrevMotorRotation) 
                             * PreviousAccelerationInfluence;

// Blend previous momentum with new input
var rotation = Slerp(PrevMotorRotation, CurrentRotation, PreviousAccelerationInfluence);
MotorThrottle = TransformDirection(prevLocalThrottle + newInput, rotation);
```

##### Components

```csharp
public struct MotorSettings : IComponentData
{
    // 0 = instant direction change, 1 = full momentum carry-through
    public float PreviousAccelerationInfluence; // 1.0 default
    public quaternion PrevMotorRotation;
}
```

##### Acceptance Criteria

- [ ] Higher values = more momentum when turning
- [ ] 0 = instant direction changes (arcade)
- [ ] 1 = realistic momentum (simulation)

---

#### 13.1.8.3 Motor Backwards Multiplier
**Status:** NOT STARTED

Walking backwards is slower than forward.

##### Algorithm

```csharp
float backwardsMultiplier = 1f;
if (inputVector.y < 0) {
    backwardsMultiplier = Lerp(1, MotorBackwardsMultiplier, Abs(inputVector.y));
}
acceleration *= backwardsMultiplier;
```

##### Components

```csharp
public struct MotorSettings : IComponentData
{
    public float MotorBackwardsMultiplier; // 0.7 default (70% speed)
}
```

##### Acceptance Criteria

- [ ] Walking backwards is slower than forward
- [ ] Gradual blend based on input magnitude
- [ ] Configurable multiplier (0.5 - 1.0)

---

#### 13.1.8.4 Soft Force Frame Distribution
**Status:** NOT STARTED

Distribute impulses across multiple frames to prevent jerky movement.

##### Algorithm

```csharp
// Instead of applying force instantly:
AddForce(bigImpulse);

// Distribute across N frames:
for (int i = 0; i < numFrames; i++) {
    SoftForceFrames[i] = impulse / numFrames;
}

// Each tick, apply first frame and shift buffer:
DesiredMovement += SoftForceFrames[0];
ShiftArrayLeft(SoftForceFrames);
```

##### Components

```csharp
public struct SoftForceBuffer : IComponentData
{
    public BlobAssetReference<SoftForceFrames> Frames;
    public int MaxFrames; // 100 default
}

// Or use DynamicBuffer
public struct SoftForceFrame : IBufferElementData
{
    public float3 Force;
}
```

##### Acceptance Criteria

- [ ] Explosions don't cause instant jerky movement
- [ ] Knockback spreads across configurable frames
- [ ] Buffer shifts each tick

---

#### 13.1.8.5 Slope Force Adjustment
**Status:** NOT STARTED

Different movement speed when moving uphill vs downhill.

##### Algorithm

```csharp
if (OnSlope && AdjustMotorForceOnSlope) {
    float slopeFactor = (movingUphill) 
        ? MotorSlopeForceUp   // 1.0 = no change
        : MotorSlopeForceDown; // 1.25 = 25% faster downhill
    
    acceleration *= slopeFactor;
}
```

##### Components

```csharp
public struct SlopeSettings : IComponentData
{
    public bool AdjustMotorForceOnSlope;
    public float MotorSlopeForceUp;   // 1.0
    public float MotorSlopeForceDown; // 1.25
}
```

##### Acceptance Criteria

- [ ] Moving uphill is configurable (can be slower)
- [ ] Moving downhill is configurable (can be faster)
- [ ] Works with existing slope detection

---

#### 13.1.8.6 PhysicMaterial Integration
**Status:** NOT STARTED

Use Unity PhysicMaterial properties for wall/ground behavior.

##### Algorithm

```csharp
// On wall collision:
float friction = FrictionValue(playerMaterial, wallMaterial) * WallFrictionModifier;
float bounce = BouncinessValue(playerMaterial, wallMaterial) * WallBounceModifier;

if (bounce > 0) {
    float magnitude = desiredMovement.magnitude * bounce;
    AddForce(Vector3.Reflect(moveDir, wallNormal) * magnitude);
}

// On ground collision:
float groundFriction = FrictionValue(playerMaterial, groundMaterial) * GroundFrictionModifier;
```

##### Components

```csharp
public struct PhysicMaterialSettings : IComponentData
{
    public float WallFrictionModifier;   // 1.0
    public float WallBounceModifier;     // 2.0
    public float GroundFrictionModifier; // 10.0
    public float GroundBounceModifier;   // 1.0
}
```

##### Acceptance Criteria

- [ ] Bouncy walls reflect velocity
- [ ] Icy surfaces reduce friction
- [ ] Configurable per-material behavior

---

#### 13.1.8.7 Continuous Collision Detection
**Status:** NOT STARTED

Check for penetrations even when player isn't moving.

##### Algorithm

```csharp
if (desiredMovement.sqrMagnitude <= 0.0001f) {
    if (ContinuousCollisionDetection) {
        // Still check for overlaps
        if (OverlapColliders(collider, position, rotation) > 0) {
            ResolvePenetrations(collider, out offset);
            DesiredMovement += offset;
        }
    }
}
```

##### Components

```csharp
public struct CollisionSettings : IComponentData
{
    public bool ContinuousCollisionDetection; // true
}
```

##### Acceptance Criteria

- [ ] Objects can push player when player is stationary
- [ ] Prevents clipping through moving doors
- [ ] Minimal performance impact when disabled

---

#### 13.1.8.8 Penetration Resolution Iterations
**Status:** NOT STARTED

Multi-pass penetration solving for complex geometry.

##### Algorithm

```csharp
int iterations = 0;
bool resolved = false;

while (iterations < MaxPenetrationChecks && !resolved) {
    resolved = true;
    int hitCount = OverlapColliders(collider, position + offset, rotation);
    
    for (int i = 0; i < hitCount; i++) {
        if (Physics.ComputePenetration(collider, ..., out direction, out distance)) {
            offset += direction * (distance + ColliderSpacing);
            resolved = false;
        }
    }
    iterations++;
}
```

##### Components

```csharp
public struct CollisionSettings : IComponentData
{
    public int MaxPenetrationChecks;        // 5
    public int MaxMovementCollisionChecks;  // 5
    public float ColliderSpacing;           // 0.01
}
```

##### Acceptance Criteria

- [ ] Handles complex corner cases
- [ ] Prevents tunneling through thin walls
- [ ] Iteration count is configurable

---

#### 13.1.8.9 Ceiling Collision Handling
**Status:** NOT STARTED

Cancel vertical forces when hitting ceiling.

##### Algorithm

```csharp
if (Vector3.Dot(hitNormal, -upDirection) > 0.5f) {
    // Hit ceiling - cancel vertical external force
    var localForce = InverseTransformDirection(ExternalForce);
    if (localForce.y > 0) {
        localForce.y = 0;
        ExternalForce = TransformDirection(localForce);
    }
}
```

##### Acceptance Criteria

- [ ] Jump force cancels when hitting ceiling
- [ ] No "sticky ceiling" effect
- [ ] Momentum redirects horizontally (optional)

---

## Files to Create

| File | Purpose |
|------|---------|
| `MovingPlatformSystem.cs` | Moving platform attachment/inheritance |
| `MovingPlatformAuthoring.cs` | Platform tag authoring |
| `RootMotionBridge.cs` | MonoBehaviour capturing animator root motion |
| `RootMotionApplySystem.cs` | Apply root motion to ECS position |
| `ExternalForceSystem.cs` | Accumulate and apply external forces |
| `ExternalForceZoneAuthoring.cs` | Zone trigger authoring |
| `GravityOverrideSystem.cs` | Variable gravity handling |
| `GravityZoneAuthoring.cs` | Zone authoring |
| `MovementPolishSystem.cs` | Wall glide, slope adjustment, backwards multiplier |
| `CollisionPolishSystem.cs` | Continuous detection, penetration resolution |
| `SoftForceSystem.cs` | Multi-frame force distribution |
| `LocomotionPolishComponents.cs` | All polish-related components |
| `LocomotionPolishAuthoring.cs` | Inspector configuration |

## Verification Plan

### Automated Tests
- Unit test: Platform velocity inheritance calculation
- Unit test: Root motion blending math
- Integration test: Gravity zone transitions
- Unit test: Wall glide curve evaluation
- Unit test: Penetration resolution convergence

### Manual Verification
1. Stand on moving platform, verify no sliding
2. Jump off moving platform, verify momentum
3. Enable root motion, verify animation drives movement
4. Enter wind zone, verify push effect
5. Enter gravity zone, verify orientation change
6. Run into wall at angle, verify smooth slide
7. Walk backwards, verify slower movement
8. Take explosion hit, verify smooth knockback
9. Walk up slope, verify speed change
10. Stand still while door closes, verify push-out

---

## Designer Setup Guide

### Moving Platforms

1. Add `MovingPlatformAuthoring` to any moving object
2. Ensure object has `PhysicsBody` with velocity
3. Player will automatically attach when grounded on it

### External Force Zones

1. Create trigger volume with `ExternalForceZoneAuthoring`
2. Set Force direction and magnitude
3. Set Damping for resistance feel (higher = more resistance)

### Gravity Zones

1. Create trigger volume with `GravityZoneAuthoring`
2. Set Override Direction (unit vector)
3. Set Transition Speed for smooth rotation

### Movement Polish Tuning

| Setting | Arcade | Realistic |
|---------|--------|-----------|
| PreviousAccelerationInfluence | 0.3 | 1.0 |
| MotorBackwardsMultiplier | 0.9 | 0.6 |
| MotorSlopeForceUp | 1.0 | 0.8 |
| MotorSlopeForceDown | 1.0 | 1.3 |
| WallBounceModifier | 0 | 2.0 |
| SoftForceFrames | 10 | 30 |

