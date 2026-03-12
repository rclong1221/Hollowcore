# EPIC 13.14: Fall System Parity

> **Status:** IMPLEMENTED
> **Priority:** HIGH
> **Dependencies:** EPIC 13.13 (Jump Parity)
> **Reference:** `OPSIVE/.../Runtime/Character/Abilities/Fall.cs`

## Overview

Bring the DIG `FallAbilitySystem` to feature parity with Opsive's Fall ability. ~~Current implementation covers ~30% of Opsive features.~~ **Now at 100% feature parity.**

---

## Implementation Summary

### New/Modified Files

| File | Purpose |
|------|---------|
| `MovementPolishComponents.cs` | Updated `FallAbility`, added `FallSettings`, `SurfaceImpactRequest`, `FallAnimationComplete` |
| `PlayerAnimationStateComponent.cs` | Added `IsFalling`, `FallVelocity`, `FallStateIndex` |
| `FallDetectionSystem.cs` | Complete rewrite with all 13.14 features |
| `FallAbilitySystem.cs` | Deprecated (disabled via `[DisableAutoCreation]`) |
| `SurfaceImpactPresentationSystem.cs` | New - handles VFX/audio on landing |
| `FallAnimatorBridge.cs` | New - animation state sync + event handling |
| `FallAnimationEventReceiverSystem.cs` | New - bridges animation events to DOTS |
| `TeleportEvent.cs` | New - teleportation signaling component |
| `TeleportSystem.cs` | New - processes teleports and signals fall system |
| `MovementPolishAuthoring.cs` | Updated to bake new components |

---

## Sub-Tasks

### 13.14.1 Minimum Fall Height Check
**Status:** COMPLETE
**Priority:** MEDIUM

Only trigger fall ability if character is high enough above ground.

#### Implementation
```csharp
// In FallDetectionSystem.cs
if (fallSettings.MinFallHeight > 0f)
{
    var rayInput = new RaycastInput
    {
        Start = position,
        End = position - up * fallSettings.MinFallHeight,
        Filter = new CollisionFilter { CollidesWith = fallSettings.SolidObjectLayerMask }
    };
    if (physicsWorld.CastRay(rayInput, out _))
    {
        canStartFall = false; // Too close to ground
    }
}
```

#### New Component Fields
```csharp
public struct FallSettings : IComponentData
{
    public float MinFallHeight; // 0.2f default, 0 = any height
    public uint SolidObjectLayerMask; // For ground raycast
}
```

#### Acceptance Criteria
- [x] Small drops don't trigger fall animation
- [x] Configurable threshold
- [x] Raycast uses solid object layers

---

### 13.14.2 Land Surface Impact
**Status:** COMPLETE
**Priority:** HIGH

Spawn VFX/audio when character lands.

#### Implementation
```csharp
// In FallDetectionSystem.cs - on landing
if (impactVelocity < fallSettings.MinSurfaceImpactVelocity)
{
    var impactRequest = new SurfaceImpactRequest
    {
        ContactPoint = groundPosition,
        ContactNormal = groundNormal,
        ImpactVelocity = impactVelocity,
        SurfaceMaterialId = materialId,
        SurfaceImpactId = fallSettings.LandSurfaceImpactId
    };
    ecb.SetComponent(entity, impactRequest);
    ecb.SetComponentEnabled<SurfaceImpactRequest>(entity, true);
}

// In SurfaceImpactPresentationSystem.cs
_audioManager.PlayImpact(materialId, contactPoint, intensity);
_audioManager.VFXManager.PlayVFX(surfaceMaterial.VFXPrefab, contactPoint);
```

#### New Component Fields
```csharp
public struct FallSettings : IComponentData
{
    public int LandSurfaceImpactId;
    public float MinSurfaceImpactVelocity; // -4f (negative = falling)
}

public struct SurfaceImpactRequest : IComponentData, IEnableableComponent
{
    public float3 ContactPoint;
    public float3 ContactNormal;
    public float ImpactVelocity;
    public int SurfaceMaterialId;
    public int SurfaceImpactId;
}
```

#### Acceptance Criteria
- [x] Dust/particles spawn on hard landing
- [x] Audio plays on landing
- [x] Surface-type aware (dirt vs metal vs water)
- [x] Velocity threshold prevents spam on soft landings

---

### 13.14.3 Animation Event for Land Complete
**Status:** COMPLETE
**Priority:** MEDIUM

Wait for animation event before ending fall ability.

#### Implementation
```csharp
// In FallDetectionSystem.cs
if (fallSettings.WaitForLandEvent)
{
    fallAbility.WaitingForAnimationEvent = true;
    fallAbility.AnimationEventTimer = 0f;
}

// Check for event each frame
if (SystemAPI.IsComponentEnabled<FallAnimationComplete>(entity))
{
    SystemAPI.SetComponentEnabled<FallAnimationComplete>(entity, false);
    fallAbility.WaitingForAnimationEvent = false;
    fallAbility.Landed = true;
}

// In FallAnimatorBridge.cs
public void OnAnimatorFallComplete()
{
    OnFallAnimationCompleteEvent?.Invoke(gameObject);
}

// FallAnimationEventReceiverSystem sets FallAnimationComplete enabled
```

#### New Components
```csharp
public struct FallAbility : IComponentData
{
    public bool WaitingForAnimationEvent;
    public float AnimationEventTimeout;
    public float AnimationEventTimer;
}

public struct FallAnimationComplete : IComponentData, IEnableableComponent { }
```

#### Acceptance Criteria
- [x] Fall ability waits for land animation
- [x] Timeout fallback
- [x] Clean transition to idle/walk

---

### 13.14.4 Blend Tree Float Data
**Status:** COMPLETE
**Priority:** MEDIUM

Send vertical velocity to animator for fall blend tree.

#### Implementation
```csharp
// In FallDetectionSystem.cs - every frame
animState.FallVelocity = verticalVelocity;
animState.FallStateIndex = fallAbility.StateIndex;
animState.IsFalling = fallAbility.IsFalling;

// In FallAnimatorBridge.cs
Animator.SetFloat(_fallVelocityHash, state.FallVelocity);
Animator.SetInteger(_fallStateIndexHash, state.FallStateIndex);
Animator.SetBool(_isFallingHash, state.IsFalling);
```

#### New Component Fields
```csharp
public struct PlayerAnimationState : IComponentData
{
    [GhostField] public bool IsFalling;
    [GhostField] public float FallVelocity;
    [GhostField] public int FallStateIndex;
}
```

#### Acceptance Criteria
- [x] Velocity sent to animator each frame
- [x] Blend tree smoothly transitions fall stages

---

### 13.14.5 Immediate Transform Change Handling
**Status:** COMPLETE
**Priority:** LOW

Handle teleportation during fall.

#### Implementation
```csharp
// In FallDetectionSystem.cs
if (fallAbility.PendingImmediateTransformChange)
{
    fallAbility.PendingImmediateTransformChange = false;
    if (grounded && fallAbility.IsFalling)
    {
        // Force end fall ability
        fallAbility.Landed = true;
        fallAbility.IsFalling = false;
        playerState.MovementState = PlayerMovementState.Idle;
    }
}

// In TeleportSystem.cs
fallAbility.PendingImmediateTransformChange = true;
transform.Position = teleport.TargetPosition;
```

#### New Components
```csharp
public struct FallAbility : IComponentData
{
    public bool PendingImmediateTransformChange;
}

public struct TeleportEvent : IComponentData, IEnableableComponent
{
    public float3 TargetPosition;
    public quaternion TargetRotation;
    public bool SnapAnimator;
}
```

#### Acceptance Criteria
- [x] Teleport to ground ends fall cleanly
- [x] No stuck-in-fall state

---

### 13.14.6 State Index for Animation
**Status:** COMPLETE
**Priority:** LOW

Track fall state (0 = falling, 1 = landed) for animator.

#### Implementation
```csharp
public struct FallAbility : IComponentData
{
    [GhostField] public int StateIndex; // 0 = falling, 1 = landed
}

// In FallDetectionSystem.cs
// On fall start:
fallAbility.StateIndex = 0;
// On landing:
fallAbility.StateIndex = 1;
```

#### Acceptance Criteria
- [x] State transitions correctly
- [x] Animator receives state index

---

## Files Modified

| File | Changes |
|------|---------|
| `FallDetectionSystem.cs` | Complete rewrite with all features |
| `MovementPolishComponents.cs` | Updated `FallAbility`, added `FallSettings`, `SurfaceImpactRequest`, `FallAnimationComplete` |
| `PlayerAnimationStateComponent.cs` | Added fall animation fields |
| `MovementPolishAuthoring.cs` | Updated to bake new components |
| `FallAbilitySystem.cs` | Deprecated |
| `PlayerGroundCheckSystem.cs` | 13.14.P1-P4: Parallel check, removed hybrid physics |
| `JumpSystem.cs` | 13.14.P5-P7, P10: Lazy raycasts, math optimizations |
| `LocomotionComponents.cs` | 13.14.P8-P9: IEnableableComponent, cleanup |
| `DeathTransitionSystem.cs` | 13.14.P9: Disable abilities on death |
| `RespawnSystem.cs` | 13.14.P9: Re-enable abilities on respawn |

## New Files Created

| File | Purpose |
|------|---------|
| `SurfaceImpactPresentationSystem.cs` | VFX/audio spawning on landing |
| `FallAnimatorBridge.cs` | Animator sync + event handling |
| `FallAnimationEventReceiverSystem.cs` | Bridge events to DOTS |
| `TeleportEvent.cs` | Teleportation signal component |
| `TeleportSystem.cs` | Process teleports |

## Verification Plan

1. Walk off ledge at edge → no fall animation (height too small) ✓
2. Fall from height → fall animation plays ✓
3. Land hard → dust VFX + thud audio ✓
4. Land soft → no VFX ✓
5. Teleport while falling → ability ends cleanly ✓

---

## Test Environment Tasks

> **Status:** ALL COMPLETE
> **Location:** `GameObject > DIG - Test Objects > Traversal > Complete Test Course` (Section 15: Fall Tests)
> **Implementation:** `Assets/Editor/TraversalObjectCreator.cs`

All test objects are auto-generated as part of the Complete Test Course at position (30, 0, 110).

### 13.14.T1 Fall Height Test Tower
**Status:** COMPLETE

Tower with platforms at various heights to test minimum fall height.

#### Specifications
- Ground level + platforms at 0.5m, 1m, 2m, 3m, 5m, 7m, 10m, 15m, 20m
- Step-off ledges (no jump required)
- Height labels on each platform
- Color-coded by danger level (green→yellow→orange→red)
- Telepads for heights above 3m
- Kill volume at bottom

#### Hierarchy
```
Section_Fall_Tests/
  FallHeightTower/
    Platform_Ground
    Platform_0.5m (green)
    Platform_1m
    Platform_2m
    Platform_3m (yellow)
    Platform_5m
    Platform_7m (orange)
    Platform_10m
    Platform_15m (red)
    Platform_20m
    TelePad_To_5m ... TelePad_To_20m
    KillVolume
```

#### Implementation
Added to `TraversalObjectCreator.cs` via `CreateFallHeightTower()` method.

---

### 13.14.T2 Landing Surface Test Pads
**Status:** COMPLETE

Different floor materials to test landing VFX/audio.

#### Specifications
- Elevated platform (5m) with telepad access
- 5 landing pads with different SurfaceMaterialId values:
  - Concrete (ID 0) - gray
  - Grass (ID 1) - green
  - Metal (ID 2) - silver
  - Water (ID 3) - blue
  - Mud (ID 4) - brown
- Each pad 3.5m x 3.5m with height labels

#### Hierarchy
```
Section_Fall_Tests/
  LandingSurfacePads/
    JumpPlatform (5m)
    TelePad_ToJump
    Pad_Concrete (SurfaceMaterialId=0)
    Pad_Grass (SurfaceMaterialId=1)
    Pad_Metal (SurfaceMaterialId=2)
    Pad_Water (SurfaceMaterialId=3)
    Pad_Mud (SurfaceMaterialId=4)
```

#### Implementation
Added to `TraversalObjectCreator.cs` via `CreateLandingSurfacePads()` method.

---

### 13.14.T3 Velocity Threshold Test
**Status:** COMPLETE

Ramps and falls designed to test velocity thresholds.

#### Specifications
- **Slide Ramp**: 45° ramp for gradual velocity buildup (slide off, don't jump)
- **Drop Hole**: 5m straight drop through a hole for instant fall detection
- **Stairwell Tower**: Multi-story with 2m drops per floor (4 floors, 8m total)
  - Tests repeated fall detection and landing
  - Telepad at base to return to top

#### Hierarchy
```
Section_Fall_Tests/
  VelocityThresholdTest/
    SlideRamp/
      RampSurface (45°)
      RampBase
      LandingPad
    DropHole/
      SurroundWall
      HolePlatform
      LandingPad
    Stairwell/
      StairwellTower (4 floors)
      TelePad_ToTop
```

#### Implementation
Added to `TraversalObjectCreator.cs` via `CreateVelocityThresholdTest()` method.

---

### 13.14.T4 Teleport Mid-Fall Test
**Status:** COMPLETE

Trigger volume that teleports player mid-fall.

#### Specifications
- **Start Tower**: 20m high platform with telepad access from ground
- **Mid-Air Telepad**: Positioned 10m up, teleports to ground (tests `PendingImmediateTransformChange`)
- **Ground Landing Pad**: Destination with height label
- Verifies fall ability ends cleanly after teleport

#### Hierarchy
```
Section_Fall_Tests/
  TeleportMidFallTest/
    StartTower (20m)
    TelePad_ToTop
    MidAirTelepad (10m, semi-transparent)
    GroundLandingPad
```

#### Implementation
Added to `TraversalObjectCreator.cs` via `CreateTeleportMidFallTest()` method.
Uses `CreateTelepad()` helper which creates trigger volumes with `TeleportTriggerAuthoring`.

---

## Architecture Notes

### DOTS vs Opsive Comparison

| Opsive Pattern | DIG DOTS Pattern |
|----------------|------------------|
| `SurfaceImpact` ScriptableObject | `SurfaceImpactRequest` component + `SurfaceMaterial` SO |
| `AnimationEventTrigger` class | `FallAnimationComplete` enableable component |
| `EventHandler.RegisterEvent` | Static C# event + receiver system |
| `m_CharacterLocomotion.Velocity` | `PhysicsVelocity.Linear` |
| `Physics.Raycast` | `PhysicsWorld.CastRay` |

### Burst Compatibility

All simulation systems are `[BurstCompile]` compatible:
- Uses `ComponentLookup<T>` instead of `SystemAPI.HasComponent` in inner loops
- Uses `EntityCommandBuffer` for structural changes
- Uses native collections (`Allocator.Temp`)

### Network Replication

Key fields are marked with `[GhostField]` for NetCode replication:
- `FallAbility.IsFalling`, `StateIndex`, `Landed`
- `PlayerAnimationState.FallVelocity`, `FallStateIndex`, `IsFalling`

---

## Performance Optimization Tasks

> **Status:** NOT STARTED
> **Priority:** MEDIUM
> **Impact:** Improved frame times, especially with many players

### 13.14.P1 Parallelize PlayerGroundCheckSystem
**Status:** COMPLETE
**Priority:** 🔴 HIGH
**Impact:** HIGH

Convert `PlayerGroundCheckSystem` from main-thread foreach loop to parallel `IJobEntity`.

#### Current Issue
```csharp
// BAD: Main thread, blocking EntityManager calls
foreach (var (...) in SystemAPI.Query<...>()) {
    state.EntityManager.HasComponent<SurfaceMaterialId>(entity) // Main thread blocking
}
```

#### Solution
```csharp
[BurstCompile]
partial struct GroundCheckJob : IJobEntity
{
    [ReadOnly] public PhysicsWorld PhysicsWorld;
    [ReadOnly] public ComponentLookup<SurfaceMaterialId> SurfaceMaterialLookup;
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ...) { ... }
}
job.ScheduleParallel();
```

#### Files
- `PlayerGroundCheckSystem.cs`

---

### 13.14.P2 Remove UnityEngine.Physics Fallback
**Status:** COMPLETE
**Priority:** 🔴 HIGH
**Impact:** HIGH

Remove fallback to managed `Physics.Raycast` in ground check.

#### Current Issue
```csharp
// BAD: Managed physics on main thread as fallback
if (Physics.Raycast(start, direction, out hit, rayLength)) { ... }
```

#### Solution
- Ensure all ground colliders are baked to DOTS Physics
- Remove the fallback entirely, or make it a debug-only option
- Log warning if DOTS raycast misses but fallback hits (indicates missing bake)

#### Files
- `PlayerGroundCheckSystem.cs:167-177`

---

### 13.14.P3 Deferred ECB Playback
**Status:** COMPLETE
**Priority:** 🔴 HIGH
**Impact:** HIGH

Use system group ECB instead of per-frame allocation.

#### Current Issue
```csharp
// BAD: Allocate and playback ECB every frame on main thread
var ecb = new EntityCommandBuffer(Allocator.Temp);
// ... loop ...
ecb.Playback(state.EntityManager);
ecb.Dispose();
```

#### Solution
```csharp
// GOOD: Use EndSimulationEntityCommandBufferSystem
var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
// Playback happens automatically at end of frame
```

#### Files
- `PlayerGroundCheckSystem.cs`

---

### 13.14.P4 Optimize Ground Check Raycast Allocation
**Status:** COMPLETE
**Priority:** 🔴 HIGH
**Impact:** HIGH

Avoid `NativeList` allocation per entity in ground check loop.

#### Current Issue
```csharp
// BAD: Allocation inside loop, per entity
var hits = new NativeList<RaycastHit>(Allocator.Temp);
physicsWorld.CollisionWorld.CastRay(raycastInput, ref hits);
hits.Dispose();
```

#### Solution
```csharp
// GOOD: Use single-hit raycast (most common case)
if (physicsWorld.CastRay(raycastInput, out RaycastHit closestHit)) { ... }

// Or if multi-hit needed, allocate once outside loop
var hits = new NativeList<RaycastHit>(16, Allocator.TempJob);
// ... use in job ...
hits.Dispose();
```

#### Files
- `PlayerGroundCheckSystem.cs:139`

---

### 13.14.P5 Lazy Ceiling Raycast in JumpSystem
**Status:** COMPLETE
**Priority:** 🟡 MEDIUM
**Impact:** MEDIUM

Only perform ceiling raycast when player is actually trying to jump.

#### Current Issue
```csharp
// BAD: Raycast every frame even when not jumping
if (canJump && !CheckCeilingClearance(...)) { canJump = false; }
```

#### Solution
```csharp
// GOOD: Only raycast when jump is requested
if (jumpJustPressed && canJump)
{
    if (!CheckCeilingClearance(...)) { canJump = false; }
    if (canJump) { StartJumpForce(...); }
}
```

#### Files
- `JumpSystem.cs:92`

---

### 13.14.P6 Replace math.acos with Dot Product Comparison
**Status:** COMPLETE
**Priority:** 🟡 MEDIUM
**Impact:** MEDIUM

Avoid expensive `math.degrees(math.acos(...))` calls.

#### Current Issue
```csharp
// BAD: Expensive trig functions
float angle = math.degrees(math.acos(math.dot(groundNormal, up)));
return slopeAngle <= slopeLimit;
```

#### Solution
```csharp
// GOOD: Compare dot products directly
// Precompute: cosThreshold = math.cos(math.radians(slopeLimit))
float dot = math.dot(groundNormal, up);
return dot >= cosThreshold; // Higher dot = smaller angle
```

#### Files
- `JumpSystem.cs:228,238`

---

### 13.14.P7 Lazy GetRigidBodyIndex
**Status:** COMPLETE
**Priority:** 🟡 MEDIUM
**Impact:** MEDIUM

Only call `GetRigidBodyIndex` when ceiling check is needed.

#### Current Issue
```csharp
// BAD: Called every frame for every entity
int rigidBodyIndex = PhysicsWorld.GetRigidBodyIndex(entity);
```

#### Solution
Move inside the ceiling check branch, only called when `jumpJustPressed && canJump`.

#### Files
- `JumpSystem.cs:83`

---

### 13.14.P8 Remove Unused LastAirborneJumpFrame Field
**Status:** COMPLETE
**Priority:** 🟡 MEDIUM
**Impact:** LOW (reduces component size + network bandwidth)

Field was added for frame-based detection but simplified approach uses `WasJumpPressed` instead.

#### Solution
Remove from `JumpAbility` component:
```csharp
// REMOVE:
[GhostField] public uint LastAirborneJumpFrame;
```

#### Files
- `LocomotionComponents.cs:43`

---

### 13.14.P9 IEnableableComponent for JumpAbility
**Status:** COMPLETE
**Priority:** 🟢 LOW
**Impact:** LOW

Skip jump processing for entities in invalid states (dead, downed).

#### Solution
```csharp
public struct JumpAbility : IComponentData, IEnableableComponent { ... }

// Disable when entering invalid state:
ecb.SetComponentEnabled<JumpAbility>(entity, false);
```

#### Files
- `LocomotionComponents.cs`
- Various state transition systems

---

### 13.14.P10 Precompute Default Multipliers
**Status:** COMPLETE
**Priority:** 🟢 LOW
**Impact:** LOW

Avoid runtime ternary checks for default values.

#### Current Issue
```csharp
// BAD: Runtime check every frame
float mult = settings.SidewaysForceMultiplier > 0 ? settings.SidewaysForceMultiplier : 0.8f;
```

#### Solution
Set proper defaults in authoring baker, remove runtime fallbacks.

#### Files
- `JumpSystem.cs:159,246-247`
- `LocomotionAbilityAuthoring.cs`

---

## Optimization Summary

| Task | System | Issue | Priority | Impact |
|------|--------|-------|----------|--------|
| P1 | PlayerGroundCheckSystem | Not parallelized | ✅ COMPLETE | HIGH |
| P2 | PlayerGroundCheckSystem | UnityEngine.Physics fallback | ✅ COMPLETE | HIGH |
| P3 | PlayerGroundCheckSystem | ECB on main thread | ✅ COMPLETE | HIGH |
| P4 | PlayerGroundCheckSystem | NativeList per entity | ✅ COMPLETE | HIGH |
| P5 | JumpSystem | Ceiling raycast every frame | ✅ COMPLETE | MEDIUM |
| P6 | JumpSystem | math.acos expensive | ✅ COMPLETE | MEDIUM |
| P7 | JumpSystem | GetRigidBodyIndex every frame | ✅ COMPLETE | MEDIUM |
| P8 | LocomotionComponents | Unused field | ✅ COMPLETE | LOW |
| P9 | JumpAbility | IEnableableComponent | ✅ COMPLETE | LOW |
| P10 | JumpSystem | Runtime default checks | ✅ COMPLETE | LOW |
