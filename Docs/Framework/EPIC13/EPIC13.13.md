# EPIC 13.13: Jump System Parity

> **Status:** IN PROGRESS  
> **Priority:** HIGH  
> **Dependencies:** EPIC 13.5 (Locomotion Abilities)  
> **Reference:** `OPSIVE/.../Runtime/Character/Abilities/Jump.cs`  
> **Setup Guide:** [SETUP_GUIDE_13.13.md](SETUP_GUIDE_13.13.md)

## Overview

Bring the DIG `JumpSystem` to feature parity with Opsive's Jump ability. Current implementation covers ~20% of Opsive features.

---

## Sub-Tasks

### 13.13.1 Ceiling Check
**Status:** ✅ COMPLETE  
**Priority:** HIGH

Prevent jump if there's a flat surface above the character within a configurable distance.

#### Implementation
```csharp
// Before allowing jump, raycast upward
if (MinCeilingJumpHeight > 0) {
    if (Physics.Raycast(position, up, out hit, MinCeilingJumpHeight + skinWidth)) {
        if (Vector3.Angle(-up, hit.normal) < 5f) { // Flat ceiling
            return false; // Block jump
        }
    }
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public float MinCeilingJumpHeight; // 0.05f default, -1 to disable
}
```

#### Acceptance Criteria
- [x] Jump blocked when ceiling is too low
- [x] Angled ceilings allow jump (30° threshold)
- [x] Configurable via `MinCeilingJumpHeight` (0 = disabled)

---

### 13.13.2 Slope Limit Prevention
**Status:** ✅ COMPLETE  
**Priority:** MEDIUM

Prevent jumping on slopes steeper than the character's slope limit.

#### Implementation
```csharp
if (PreventSlopeLimitJump && isGrounded) {
    float slopeAngle = Vector3.Angle(groundNormal, up);
    if (slopeAngle > SlopeLimit) {
        return false; // Block jump on steep slopes
    }
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public bool PreventSlopeLimitJump; // true default
    public float SlopeLimit; // from CharacterController settings
}
```

#### Acceptance Criteria
- [x] Jump blocked on slopes > SlopeLimit
- [x] Configurable toggle via `PreventSlopeLimitJump`

---

### 13.13.3 Directional Force Multipliers
**Status:** ✅ COMPLETE  
**Priority:** MEDIUM

Reduce jump height when moving sideways or backwards.

#### Implementation
```csharp
float force = JumpForce;
if (inputVector.y < 0) { // Moving backwards
    force *= math.lerp(1f, BackwardsForceMultiplier, math.abs(inputVector.y));
} else {
    // Sideways movement reduces force
    float sidewaysInfluence = math.abs(inputVector.x) - math.abs(inputVector.y);
    force *= math.lerp(1f, SidewaysForceMultiplier, math.max(0, sidewaysInfluence));
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public float SidewaysForceMultiplier;  // 0.8f default
    public float BackwardsForceMultiplier; // 0.7f default
}
```

#### Acceptance Criteria
- [x] Backwards jump is shorter (via `BackwardsForceMultiplier`)
- [x] Sideways jump is slightly shorter (via `SidewaysForceMultiplier`)
- [x] Configurable multipliers

---

### 13.13.4 Multi-Frame Force Distribution
**Status:** ✅ COMPLETE  
**Priority:** LOW

Distribute jump force across multiple frames for smoother impulse.

#### Implementation
```csharp
if (jumpAbility.ValueRO.FramesRemaining > 0) {
    ApplyMultiFrameForce(ref velocity, ref jumpAbility, in jumpSettings);
    // Force per Frame = BaseForce / JumpFrames
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public int JumpFrames;       // 1 = instant
    public float ForceDamping;   // Damping
}

public struct JumpAbility : IComponentData
{
    public int FramesRemaining;
}
```

#### Acceptance Criteria
- [x] Setting frames > 1 distributes force
- [x] Configurable via `JumpFrames`
- [x] Damping support

#### New Components
```csharp
public struct SoftForceBuffer : IBufferElementData
{
    public float Force;
}

public struct JumpSettings : IComponentData
{
    public int JumpFrames;      // 1 default (instant)
    public float ForceDamping;  // 0.18f
}
```

#### Acceptance Criteria
- [ ] Force distributes over configurable frames
- [ ] Damping applied per frame
- [ ] Smoother jump arc

---

### 13.13.5 Animation Event Trigger
**Status:** ✅ COMPLETE  
**Priority:** HIGH

Wait for `OnAnimatorJump` event before applying jump force.

#### Implementation
```csharp
// In JumpSystem:
if (jumpSettings.WaitForAnimationEvent && !isAirborneJump) {
    jumpAbility.IsWaitingForEvent = true;
    // Enter wait state, trigger anim, hold force application
}

// In JumpAnimationBridge (MonoBehaviour):
public void OnAnimatorJump() {
    // Set JumpEventTrigger ECS component
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData 
{ 
    public bool WaitForAnimationEvent; 
    public float JumpEventTimeout; 
}

public struct JumpEventTrigger : IComponentData 
{ 
    public bool Triggered; 
}
```

#### Acceptance Criteria
- [x] `WaitForAnimationEvent` holds jump
- [x] `OnAnimatorJump` event releases jump
- [x] Timeout fallback prevents stuck state

---

### 13.13.6 Jump Surface Impact
**Status:** ✅ COMPLETE  
**Priority:** MEDIUM

Play audio/vfx based on surface type.

#### Implementation
```csharp
// JumpSystem:
if (SpawnSurfaceEffect) jumpAbility.JustJumped = true;

// JumpPresentationSystem:
if (jumpAbility.JustJumped) {
    int matId = SurfaceDetectionService.ResolveMaterialIdAt(pos);
    AudioManager.PlayFootstep(matId, pos, 0); // Plays jump sound
}
```

#### Acceptance Criteria
- [x] Dust/particles spawn on jump (via PresentationSystem)
- [x] Audio plays on jump
- [x] Surface-type aware (dirt vs metal)

---

### 13.13.7 Hold-For-Height
**Status:** ✅ COMPLETE  
**Priority:** MEDIUM

Add extra force while jump button is held.

#### Implementation
```csharp
if (jumpButtonHeld && isJumping && velocity.y > 0) {
    HoldForce += ForceHold;
    HoldForce /= (1 + ForceDampingHold);
    velocity.y += HoldForce;
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public float ForceHold;        // 0.003f
    public float ForceDampingHold; // 0.5f
}

public struct JumpAbility : IComponentData
{
    public float HoldForce; // Runtime accumulator
}
```

#### Acceptance Criteria
- [x] Holding jump = higher jump (via `ForceHold`)
- [x] Releasing early = shorter jump (existing gravity multiplier)
- [x] Damping prevents infinite height (via `ForceDampingHold`)

---

### 13.13.8 Airborne Jumps (Double/Triple Jump)
**Status:** ✅ COMPLETE
**Priority:** HIGH

Allow jumping again while airborne.

#### Implementation
```csharp
// Simple press detection - release and press again to trigger airborne jump
bool jumpJustPressed = jumpPressed && !jumpAbility.WasJumpPressed;

// AIRBORNE JUMP: in air, just pressed jump, have jumps remaining
if (jumpJustPressed && !isGrounded && jumpAbility.AirborneJumpsUsed < jumpSettings.MaxAirborneJumps)
{
    ApplyJumpForceImmediate(ref jumpAbility, ref velocity, in jumpSettings, in playerInput, true);
    // Inside ApplyJumpForceImmediate for airborne:
    // - Multiplies force by AirborneJumpForce (default 0.6)
    // - Increments AirborneJumpsUsed
    // - Resets velocity.y to 0 before applying force
}

// Track for next frame
jumpAbility.WasJumpPressed = jumpPressed;
```

> **Note:** Uses simple `WasJumpPressed` state tracking instead of frame count detection.
> Player must release and re-press jump to trigger each airborne jump.

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public int MaxAirborneJumpCount;   // 0 = no double jump, -1 = infinite
    public float AirborneJumpForce;    // 0.6f
    public int AirborneJumpFrames;     // 10
}

public struct JumpAbility : IComponentData
{
    public int AirborneJumpCount; // Reset on ground
}
```

#### Acceptance Criteria
- [x] Double/triple jump works (via `MaxAirborneJumps`)
- [ ] Configurable count (-1 for infinite) — *only positive counts supported*
- [ ] Distinct audio for airborne jumps — *deferred to audio system*
- [x] Resets on landing (`AirborneJumpsUsed` cleared)

---

### 13.13.9 Recurrence Delay
**Status:** ✅ COMPLETE  
**Priority:** LOW

Prevent rapid jump spam after landing.

#### Implementation
```csharp
if (justLanded) {
    LandTime = currentTime;
}
if (currentTime < LandTime + RecurrenceDelay) {
    return false; // Block jump
}
```

#### New Component Fields
```csharp
public struct JumpSettings : IComponentData
{
    public float RecurrenceDelay; // 0.2f
}

public struct JumpAbility : IComponentData
{
    public double LandTime;
}
```

#### Acceptance Criteria
- [x] Can't jump again for X seconds after landing
- [x] Configurable delay via `RecurrenceDelay` (0 = disabled)

---

### 13.13.10 Gravity Reset on Stop
**Status:** ✅ COMPLETE  
**Priority:** LOW

Reset gravity accumulation/vertical velocity on jump start/land.

#### Implementation
```csharp
// On Jump Start (Airborne):
velocity.ValueRW.Linear.y = 0; // Reset before impulse

// On Land:
jumpAbility.ValueRW.HoldForce = 0f;
// Physics engine handles velocity stop
```

#### Acceptance Criteria
- [x] Clean gravity state after jump ends
- [x] Configurable toggle (Internal logic handles it)

---

## Performance Optimizations

### 13.13.11 Parallelize Jump Logic
**Status:** ✅ COMPLETE
**Priority:** HIGH
**Impact:** High

Refactor `JumpSystem` to use `IJobEntity` and `.ScheduleParallel()`.

### 13.13.12 Remove Managed Physics from Presentation
**Status:** NOT STARTED
**Priority:** HIGH
**Impact:** High

Use DOTS Physics (`PhysicsWorldSingleton`) in a background job or reuse simulation data instead of `UnityEngine.Physics.Raycast` on main thread.

### 13.13.13 Cache Surface Material
**Status:** NOT STARTED
**Priority:** MEDIUM
**Impact:** Medium

Reuse `SurfaceMaterialId` from `PlayerState` or Ground Detection instead of raycasting on jump.

### 13.13.14 Tag-Based Filtering
**Status:** ✅ COMPLETE
**Priority:** MEDIUM
**Impact:** Medium

Use `IEnableableComponent` to disable `JumpAbility` when in invalid states (Sitting, Dead), removing them from iteration. (See EPIC 13.14.P9)

### 13.13.15 Audio Event Queue
**Status:** NOT STARTED
**Priority:** MEDIUM
**Impact:** Medium

Decouple simulation from presentation using `NativeQueue<JumpAudioEvent>`. Simulation writes, Presentation reads.

### 13.13.16 Micro-Optimizations (Math)
**Status:** ✅ COMPLETE
**Priority:** LOW
**Impact:** Low

Use dot product checks instead of `acos` for angles. Use `ref readonly` for constants. (See EPIC 13.14.P6)

---

## Files to Modify

| File | Changes |
|------|---------|
| `JumpSystem.cs` | Add all checks and force logic |
| `JumpSettings` component | Add all new fields |
| `JumpAbility` component | Add runtime state fields |
| `JumpAuthoring.cs` | Expose new settings |
| `SurfaceManager` | Surface impact integration |

## Verification Plan

1. Jump under low ceiling → blocked
2. Jump on steep slope → blocked
3. Jump backwards → shorter arc
4. Hold jump → higher arc
5. Double jump in air → works
6. Spam jump after landing → blocked by delay
7. Jump VFX/audio plays on takeoff

---

## Test Environment Tasks

Create the following test objects under: `GameObject > DIG - Test Objects > Traversal > Jump Tests`

### 13.13.T1 Low Ceiling Test Chamber
**Status:** NOT STARTED

Create a tunnel with progressively lower ceilings to test ceiling jump blocking.

#### Specifications
- Entrance: 3m ceiling (can jump)
- Middle: 1.5m ceiling (marginal)
- End: 1m ceiling (should block jump)
- Floor markers showing ceiling heights
- Visual indicator when jump blocked

#### Hierarchy
```
Jump Tests/
  Low Ceiling Chamber/
    Floor
    Ceiling_3m
    Ceiling_1.5m
    Ceiling_1m
    Height Markers (UI)
```

---

### 13.13.T2 Slope Jump Test Ramps
**Status:** NOT STARTED

Create ramps at various angles to test slope limit prevention.

#### Specifications
- Flat ground (0°) - can jump
- Gentle slope (20°) - can jump
- Moderate slope (35°) - near limit
- Steep slope (50°) - should block
- Very steep slope (70°) - definitely block
- Angle markers on each ramp

#### Hierarchy
```
Jump Tests/
  Slope Ramps/
    Ramp_00deg
    Ramp_20deg
    Ramp_35deg
    Ramp_50deg
    Ramp_70deg
    Angle Labels (UI)
```

---

### 13.13.T3 Jump Height Measurement Wall
**Status:** NOT STARTED

Vertical wall with height markers to measure jump height.

#### Specifications
- Graduated markers every 0.5m up to 5m
- Different colored zones (green/yellow/red)
- Platform at 2m for double jump testing
- Ledge at 3m for triple jump testing

#### Hierarchy
```
Jump Tests/
  Height Measurement/
    Measurement Wall
    Height Markers
    Platform_2m
    Ledge_3m
```

---

### 13.13.T4 Directional Jump Test Arena
**Status:** NOT STARTED

Open area with distance markers for testing directional jump force.

#### Specifications
- Center starting platform
- Radial distance markers (every 1m)
- Cardinal direction labels (N/S/E/W)
- Target landing zones for each direction
- Note: Backwards jump should land shorter

#### Hierarchy
```
Jump Tests/
  Directional Arena/
    Start Platform
    Distance Rings
    Direction Labels
    Target Zones (N/S/E/W)
```

---

### 13.13.T5 Multi-Jump Pit
**Status:** NOT STARTED

Deep pit to test double/triple jump without landing.

#### Specifications
- 20m deep pit
- Platforms at 5m intervals
- Respawn trigger at bottom
- Counter display showing jump count

#### Hierarchy
```
Jump Tests/
  Multi-Jump Pit/
    Pit Walls
    Platform_5m
    Platform_10m
    Platform_15m
    Kill Volume
    Jump Counter (UI)
```

---

### 13.13.T6 Surface Type Jump Pads
**Status:** NOT STARTED

Different floor materials to test jump VFX/audio.

#### Specifications
- Dirt floor section
- Metal grate section
- Wood plank section
- Stone/concrete section
- Water/puddle section
- Each should spawn different VFX on jump

#### Hierarchy
```
Jump Tests/
  Surface Pads/
    Pad_Dirt
    Pad_Metal
    Pad_Wood
    Pad_Stone
    Pad_Water
```

