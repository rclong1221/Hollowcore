# EPIC 12.1: Physics-Based Free Climbing System

> **Status:** COMPLETE  
> **Priority:** HIGH  
> **Dependencies:** Character Controller (EPIC 1), Animation System  
> **Reference:** `Assets/Invector-3rdPersonController/Add-ons/FreeClimb/Scripts/vFreeClimb.cs`

## Overview

Replace the current 1D rail-based climbing system with a physics-raycast-based free climbing system that works on **any surface** (concave, convex, irregular meshes). This Epic adapts the proven algorithms from Invector's `vFreeClimb` to our Unity ECS/DOTS architecture.

### Problem Statement

The current DIG climbing system has critical limitations:
1. **Rail-Based Movement** - Only moves along a predefined Bottom→Top line segment
2. **No Surface Detection** - Uses entity component lookup, not physics raycasts
3. **Capsule Collision Issues** - Standard capsule size causes wall clipping/rejection
4. **No IK** - Hands float in space instead of gripping the surface
5. **No Concave Support** - Cannot navigate around corners or inside caves

### Solution Architecture

Adopt Invector's proven approach:
- **Surface Detection via Raycasts** - Multi-phase linecasts to find valid climb surfaces
- **Local-Space Tracking** - Store grip position relative to the surface collider transform
- **Capsule Size Adjustment** - Reduce collider during climb to prevent wall rejection
- **Hand IK Raycasting** - Procedurally place hands on surface geometry
- **Surface Angle Validation** - Accept surfaces between 30-160° (vertical to slight overhang)

---

## Architecture

### Core Components

| Component | Description |
|-----------|-------------|
| `FreeClimbState` | Replaces `ClimbingState`. Tracks surface entity, local grip position, grip normal, IK weights |
| `FreeClimbCandidate` | Detection result: target surface, world position, normal, distance |
| `FreeClimbSettings` | Configuration: speed, stamina cost, angle limits, collider adjustments |

### System Pipeline

```mermaid
graph LR
    A[FreeClimbDetectionSystem] --> B[FreeClimbMountSystem]
    B --> C[FreeClimbMovementSystem]
    C --> D[FreeClimbIKSystem]
    D --> E[FreeClimbExitSystem]
```

---

### Technical Notes: Player Positioning

The grip position (`GripWorldPosition`) represents **where the player's hands contact the surface**. To position the player correctly:

```
PlayerCenter = GripPosition + NormalOffset - VerticalOffset
```

**Components:**
1. **GripPosition** - Where hands touch the surface (from raycast hit)
2. **NormalOffset** - Push player back from surface: `SurfaceNormal * DetectionDistance * 0.5`
3. **VerticalOffset** - Hands are above player center: `WorldUp * HandTargetOffset.y` (typically 1.5m)

**Why this matters:**
- `HandTargetOffset.y` is a **local offset** (hands are 1.5m above player center)
- This offset is in **world Y**, not relative to the surface normal
- Player center is always directly BELOW the grip point (gravity-based)
- Player is offset FROM the surface by the normal direction

**Common Bug:**
If player appears at grip position (not offset), the positioning logic is wrong. Debug dump should show:
- `GripWorldPosition.y` ≈ `PlayerPosition.y + 1.5` (hands above player)
- `GripWorldPosition.z` slightly different from `PlayerPosition.z` (player behind grip)

**Client/Server Entity Separation:**
- `SurfaceEntity` and `GripLocalPosition` are only valid on SERVER
- Client receives replicated `GripWorldPosition` and `GripWorldNormal`
- Entity IDs are world-local (server's Entity 4016 doesn't exist on client)
- Moving platform support works via server updating world position → replicating

---

## Sub-Tasks


### 12.1.1 Surface Detection System
**Status:** COMPLETE

Replace entity-distance detection with Invector-style multi-phase raycasting.

**Algorithm (from vFreeClimb.CheckCanMoveClimb):**
```
1. Cast from HandTarget position toward surface (forward)
2. If hit: validate surface angle (30-160°), validate tag/layer
3. If no hit: cast from HandTarget + input offset forward
4. If no hit: cast from previous endpoint diagonally back toward character
5. Track hit collider in local-space for moving platforms
```

**Files to Modify:**
- `[REPLACE] ClimbDetectionSystem.cs` → `FreeClimbDetectionSystem.cs`

**Acceptance Criteria:**
- [x] Raycast detects any mesh with "Climbable" layer
- [x] Surface angle 30-160° from vertical accepted
- [x] Works on concave geometry (caves)

**Implementation Completed:**
- [x] Created `FreeClimbDetectionSystem.cs` with multi-phase raycast
- [x] Phase 1: Direct raycast from hand target toward surface
- [x] Phase 2: Forward cast catches inside corners  
- [x] Phase 3: Diagonal fallback for curves
- [x] Phase 4: PointDistanceInput for sphere detection
- [x] Surface angle validation (30-160°)
- [x] `FreeClimbCandidate` component for detected surfaces

---

### 12.1.2 Collider Adjustment During Climb
**Status:** COMPLETE

Reduce capsule collider size during climb to prevent wall rejection.

**Invector Reference (vFreeClimb line 291):**
```csharp
Physics.SphereCast(handTargetPosition + transform.forward * -capsuleRadius * 0.5f, 
                   capsuleRadius * 0.5f, transform.forward, ...)
```

**Implementation:**
1. Store original collider dimensions in `FreeClimbState`
2. On mount: Set `PhysicsCollider.Value.Geometry` to reduced size
3. On dismount: Restore original dimensions

**Files to Modify:**
- `[MODIFY] CharacterControllerSystem.cs` - Add climb collider mode
- `[NEW] FreeClimbColliderSystem.cs` - Manage collider transitions

**Acceptance Criteria:**
- [x] Capsule radius reduced to 50% during climb
- [x] No wall penetration or rejection during climb
- [x] Original size restored on dismount

**Implementation Completed:**
- [x] Created `FreeClimbColliderSystem.cs`
- [x] Added `ClimbColliderRadiusMultiplier` (0.5) to settings
- [x] Added `ClimbColliderHeightMultiplier` (0.8) to settings
- [x] Store `OriginalRadius`/`OriginalHeight` in `FreeClimbState`
- [x] `ColliderAdjusted` flag prevents duplicate adjustments
- [x] Restore dimensions on dismount

---

### 12.1.3 Local-Space Grip Tracking
**Status:** COMPLETE

Store grip position relative to the surface collider for moving platform support.

**Invector Reference (vDragInfo struct):**
```csharp
public Vector3 position {
    get { return collider.transform.TransformPoint(localPosition); }
    set { localPosition = collider.transform.InverseTransformPoint(value); }
}
```

**Implementation:**
1. On mount: `LocalGripPosition = Surface.InverseTransformPoint(WorldHitPoint)`
2. Each frame: `WorldGripPosition = Surface.TransformPoint(LocalGripPosition)`
3. Network: Replicate `LocalGripPosition` (smaller bandwidth than world pos)

**Files to Modify:**
- `[MODIFY] ClimbingState.cs` → Add `LocalGripPosition`, `GripSurfaceEntity`

**Acceptance Criteria:**
- [x] Grip position updates when surface entity moves
- [x] Works on rotating platforms

**Implementation Completed:**
- [x] Added `GripLocalPosition` to `FreeClimbState`
- [x] `InverseTransformPoint()` on mount converts world → local
- [x] `TransformPoint()` each frame converts local → world
- [x] Handles scale, rotation, and position changes
- [x] Auto-dismount if surface entity destroyed

---

### 12.1.4 Movement Input Projection
**Status:** COMPLETE

Project WASD input onto the climb surface plane.

**Invector Reference (vFreeClimb.ApplyClimbMovement):**
```csharp
var root = new Vector3(input.x, input.z, 0) * climbSpeed * dt;
position = (dragPosition - transform.rotation * handTarget.localPosition) 
           + (transform.right * root.x + transform.up * root.y);
```

**Implementation:**
1. Get surface normal from current grip
2. Calculate surface-relative Right = Cross(Normal, WorldUp)
3. Calculate surface-relative Up = Cross(Right, Normal)
4. Project input: `MoveDir = Right * input.x + Up * input.y`
5. Raycast from new position back to surface to snap

**Files to Modify:**
- `[REPLACE] ClimbingMovementSystem.cs` → `FreeClimbMovementSystem.cs`

**Acceptance Criteria:**
- [x] W moves up the surface
- [x] A/D moves laterally along the surface
- [x] S moves down the surface
- [x] Movement follows concave geometry (wraps around corners)

**Implementation Completed:**
- [x] Created `FreeClimbMovementSystem.cs`
- [x] Calculate `surfaceRight = Cross(Normal, WorldUp)` for horizontal
- [x] Calculate `surfaceUp = Cross(Right, Normal)` for vertical
- [x] Project WASD: `MoveDir = surfaceRight * input.x + surfaceUp * input.y`
- [x] Handle horizontal surfaces (floor/ceiling climbing)
- [x] Zero physics velocity during climb

---

### 12.1.5 Surface Snapping (Multi-Phase Raycast)
**Status:** COMPLETE

Re-acquire surface after each movement step to handle irregular geometry.

**Invector Reference (vFreeClimb.CheckCanMoveClimb, lines 354-391):**
```csharp
// Phase 1: Direct linecast to target
if (Physics.Linecast(centerCharacter, targetPos, climbLayers)) { valid }
// Phase 2: Forward from target
if (Physics.Linecast(target, target + forward * radius * 2, climbLayers)) { valid }
// Phase 3: Diagonal fallback
if (Physics.Linecast(p1, p2 + diagonal, climbLayers)) { valid }
```

**Implementation:**
1. After movement, raycast from player toward surface
2. If miss: raycast from projected position forward
3. If miss: raycast diagonally toward last known surface
4. If all miss: block movement (edge case)

**Acceptance Criteria:**
- [x] Player snaps to curved surfaces (spheres, cylinders)
- [x] Player wraps around inside corners (concave)
- [x] Player stops at outside corners (convex edge)

**Implementation Completed:**
- [x] 4-phase raycast in `FreeClimbMovementSystem.cs`
- [x] Phase 1: Normal toward surface (standard snap)
- [x] Phase 2: Forward from target (inside corners)
- [x] Phase 3: Diagonal fallback (curves)
- [x] Phase 4: Reverse cast from grip (wraparound)
- [x] `TryCast()` helper method
- [x] Uses new surface normal for player positioning

---

### 12.1.6 Hand IK Raycasting
**Status:** COMPLETE

Procedurally place hands on surface using raycasts from bone positions.

**Invector Reference (vFreeClimb.OnAnimatorIK, lines 845-906):**
```csharp
if (Physics.Raycast(leftHandBone + forward * -0.5f + up * -0.2f, forward, climbLayers)) {
    targetPositionL = hit.point;
}
animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandPosition);
animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
```

**Implementation:**
1. Get hand bone world position from Animator
2. Raycast from hand toward surface
3. If hit: Set IK position to hit point + offset
4. Lerp `ikWeight` for smooth transitions

**Files to Modify:**
- `[NEW] FreeClimbIKSystem.cs`
- `[MODIFY] ClimbAnimatorBridge.cs`

**Acceptance Criteria:**
- [x] Both hands contact surface geometry
- [x] IK weight fades in/out during mount/dismount
- [x] Hands follow irregular surface contours

**Implementation Completed:**
- [x] Created `FreeClimbIKController.cs` (MonoBehaviour)
- [x] Reads `FreeClimbState` from ECS each frame
- [x] Raycasts for left/right hand positions
- [x] Raycasts for left/right foot positions
- [x] Smooth lerp to new IK targets (`ikTargetSpeed`)
- [x] Updates `ClimbAnimatorBridge.SetIKTargets()`
- [x] Debug gizmos for visualization

---

### 12.1.7 Climb Mount/Dismount Transitions
**Status:** COMPLETE

Smooth enter/exit animations with position alignment.

**Invector Reference (EnterClimbAlignment coroutine, lines 720-760):**
```csharp
while (transition <= 1f) {
    transform.rotation = Lerp(_rotation, targetRotation, transition);
    transform.position = Lerp(_position, targetPosition, transition);
    transition += dt * climbEnterSpeed;
}
```

**Implementation:**
1. On mount input: Start "EnterClimb" animation
2. Lerp player position/rotation to surface over ~0.4s
3. Disable physics velocity during transition
4. On dismount: Play exit animation, restore physics

**Files to Modify:**
- `[MODIFY] FreeClimbMountSystem.cs`

**Acceptance Criteria:**
- [x] Smooth transition from ground to wall
- [x] No jittering during mount
- [x] Exit animation plays when jumping off

**Implementation Completed:**
- [x] Added transition state to `FreeClimbState`:
  - [x] `IsTransitioning`, `TransitionProgress`
  - [x] `TransitionStartPos/Rot`, `TransitionTargetPos/Rot`
- [x] Added `MountTransitionSpeed` (2.5) and `DismountTransitionSpeed` (3.0)
- [x] `EaseOutQuad()` easing for smooth deceleration
- [x] Zero physics velocity during transition
- [x] Jump-off impulse: `surfaceNormal * 5f + (0, 3, 0)`

---

### 12.1.8 Climb Up (Ledge Grab)
**Status:** COMPLETE

Automatically climb over edges when reaching the top.

**Invector Reference (CheckClimbUp, lines 531-582):**
```csharp
// Check for obstruction above
if (!Physics.Linecast(startPoint, endPoint, obstacleLayers)) {
    // Check for thickness
    if (Physics.Linecast(thicknessPoint, climbPoint, groundLayer)) {
        ClimbUp(); // Trigger MatchTarget animation
    }
}
```

**Implementation:**
1. When player reaches top of surface, check for ledge
2. Raycast upward (obstruction check)
3. Raycast forward-down (thickness check)
4. If valid: Play ClimbUp animation with MatchTarget

**Files to Modify:**
- `[NEW] FreeClimbLedgeSystem.cs`

**Acceptance Criteria:**
- [x] Auto-vault over ledges when climbing up
- [x] MatchTarget aligns hands to ledge point
- [x] Works with varying ledge heights

**Implementation Completed:**
- [x] Created `FreeClimbLedgeSystem.cs`
- [x] Trigger: Moving upward (W key) while climbing
- [x] Phase 1: Upward raycast (headroom check)
- [x] Phase 2: Forward raycast (wall continues?)
- [x] Phase 3: Downward raycast (find ledge surface)
- [x] Validate ledge is horizontal (<45° from up)
- [x] Thickness check for solid ledge
- [x] Transition to position on top of ledge
- [x] **Polish & Bug Fixes:**
  - [x] Implemented `PlayerChildLayerEnforcementSystem` to fix self-collision
  - [x] Refined obstruction detection to ignore player hierarchy
  - [x] Fixed "launch" bug by using dynamic radius/height for landing position

---

### 12.1.9 Ground-Based Auto-Dismount
**Status:** COMPLETE

Automatically dismount when player's feet touch ground while climbing.

**Problem:**
Players had to manually jump off (Space) or climb all the way down to exit climbing state. No automatic detection when reaching the ground.

**Solution:**
Added `FreeClimbExitSystem` that detects ground contact using raycasts with surface normal + proximity checks.

**Detection Logic:**
1. **Trigger condition**: Player pressing down (S key) OR fast-falling
2. **Surface normal check**: Only floor-like surfaces (angle < 45° from up)
3. **Proximity check**: Feet must be within 0.5m of ground
4. **Mount cooldown**: Prevents instant dismount on short walls

**Voxel Terrain Compatibility:**
- Casts against ALL collision layers (no layer filtering)
- Uses surface normal angle to distinguish walls from floors
- Runs on ServerSimulation where physics colliders exist

**Configuration (FreeClimbSettings):**
| Setting | Default | Description |
|---------|---------|-------------|
| GroundCheckDistance | 0.4m | Base ray length for ground detection |
| DismountCooldown | 0.5s | Time after mount before ground check activates |
| FastFallThreshold | 2.0 m/s | Downward velocity that triggers ground check |

**Files Created/Modified:**
- `[NEW] FreeClimbExitSystem.cs` - Ground dismount detection
- `[MODIFY] FreeClimbComponents.cs` - Added ground check settings
- `[MODIFY] FreeClimbSettingsAuthoring.cs` - Added Inspector fields

**Acceptance Criteria:**
- [x] Dismounts when pressing S and feet touch floor
- [x] Dismounts on fast-fall impact with ground
- [x] Does NOT dismount when far from ground
- [x] Does NOT dismount on wall surfaces (angle ≥ 45°)
- [x] Works with voxel terrain chunks
- [x] No velocity ejection on dismount

---


---

### 12.1.10 Performance Optimization (Burst/Jobs)
**Status:** COMPLETE

Refactored the entire Free Climbing system to use Unity's Job System and Burst Compiler for maximum performance and scalability.

**Problem:**
Original implementation used main-thread `EntityManager` access and `Physics.Raycast` (or `PhysicsWorld.CastRay` on main thread), which incurs overhead and blocks the main thread.

**Solution:**
Converted all systems to `IJobEntity` scheduled with `ScheduleParallel()`, enabling multi-threaded execution and Burst compilation.

**Implementation Details:**
1.  **Parallel Execution**: All systems now run on worker threads.
2.  **Burst Compilation**: Math-heavy logic (projecting movement, lerping transitions) is Burst-compiled.
3.  **Thread Safety**: Replaced `EntityManager` with `ComponentLookup<T>` and `EntityCommandBuffer`.
4.  **Zero GC**: Removed all managed allocations (Debug.Log, reference types) from hot paths.

**Refactored Systems:**
- `FreeClimbDetectionSystem`: Parallelized surface detection raycasts.
- `FreeClimbMovementSystem`: Optimized 4-phase raycast surface snapping.
- `FreeClimbLedgeSystem`: Replaced recursive `EntityManager` hierarchy checks with flat `ComponentLookup` loops.
- `FreeClimbMountSystem`: Burst-compiled transition lerping.
- `FreeClimbExitSystem`: Parallelized ground detection.

**Acceptance Criteria:**
- [x] All FreeClimb systems use `[BurstCompile]`
- [x] No `EntityManager` usage in `OnUpdate` (except for singleton access)
- [x] No GC allocation during climbing
- [x] Performance scales with number of players

---


## Designer/Developer Setup Guide


### Step 1: Create Climbable Surfaces

1. Assign surfaces to `Climbable` layer (configure in FreeClimbSettingsAuthoring)
2. Ensure surfaces have valid `MeshCollider` or primitive collider
3. Surface angle must be 30-160° from vertical (walls, overhangs, caves)

### Step 2: Configure Player Prefab

1. Add `FreeClimbSettingsAuthoring` component to player prefab
2. Configure detection settings:
   - `HandTargetOffset`: (0, 1.5, 0) - offset from player center to hand position
   - `DetectionDistance`: 1.0 - how far to raycast for surfaces
   - `ClimbSpeed`: 2.0 - movement speed while climbing
   - `MinSurfaceAngle`: 30 - minimum wall angle (vertical = 90°)
   - `MaxSurfaceAngle`: 160 - maximum wall angle (overhang)
3. Configure collider settings:
   - `ClimbColliderRadiusMultiplier`: 0.5 - reduce capsule during climb
   - `ClimbColliderHeightMultiplier`: 0.8 - reduce height during climb
4. Configure transition settings:
   - `MountTransitionSpeed`: 2.5 (~0.4 seconds to mount)
   - `DismountTransitionSpeed`: 3.0 (~0.33 seconds)
5. Set layer masks:
   - `ClimbableLayers`: Layers to detect as climbable
   - `ObstacleLayers`: Layers to check for obstruction

### Step 3: Set Up IK (Optional)

1. Add `FreeClimbIKController` component to player UI prefab
2. Ensure `ClimbAnimatorBridge` has `EnableIK = true`
3. Create 4 empty child GameObjects as IK targets:
   - LeftHandIKTarget, RightHandIKTarget
   - LeftFootIKTarget, RightFootIKTarget
4. Assign targets to `ClimbAnimatorBridge`
5. Configure raycast settings on `FreeClimbIKController`:
   - `climbableLayers`: Match FreeClimbSettings
   - `handSpread`: 0.3 (horizontal distance between hands)
   - `footVerticalOffset`: 1.2 (how far below grip for feet)

### Step 4: Set Up Animator

1. Create climbing states in Animator Controller:
   - `ClimbIdle`, `ClimbUp`, `ClimbDown`, `ClimbLeft`, `ClimbRight`
   - `ClimbEnter`, `ClimbExit`, `ClimbJumpOff`, `ClimbUp` (ledge vault)
2. Add the following parameters (used by `ClimbAnimatorBridge`):

| Parameter | Type | Default Name | Description |
|-----------|------|--------------|-------------|
| IsClimbing | Bool | `IsClimbing` | True while player is in climbing state |
| ClimbProgress | Float | `ClimbProgress` | 0 = bottom of surface, 1 = top |
| ClimbSpeed | Float | *(empty)* | Vertical climb speed for animation blending (optional) |
| ClimbHorizontal | Float | *(empty)* | Horizontal movement for traversal blending (optional) |
| GrabTrigger | Trigger | *(empty)* | Fired when grabbing a new anchor (optional) |
| ReleaseTrigger | Trigger | `ReleaseTrigger` | Fired when releasing/dismounting (optional) |
| ClimbUpTrigger | Trigger | `ClimbUpTrigger` | Fired when vaulting over a ledge (optional) |
| Speed | Float | `Speed` | Overall movement speed (shared with locomotion) |

3. Configure transitions:
   - `Any State → ClimbEnter`: When `IsClimbing` becomes true
   - `ClimbEnter → ClimbIdle`: After mount animation completes
   - `ClimbIdle ↔ ClimbUp/Down`: Based on `ClimbProgress` delta or `ClimbSpeed`
   - `Climb* → ClimbExit`: When `IsClimbing` becomes false
4. Enable **IK Pass** on climbing layer if using procedural IK

### Step 5: Test

1. Menu: **GameObject > DIG - Test Objects > Traversal > Free Climb Test Course**
2. Run game, approach wall, press **Space** to mount
3. Use **WASD** to move on surface
4. Press **Space** while climbing to dismount (jump off)
5. Hold **S** while climbing near ground - should auto-dismount
6. Climb to top of wall - should auto-vault over ledge

---

## Verification Plan

### Automated Tests
- Unit test: Surface angle validation (30-160° range)
- Integration test: Mount/dismount state transitions

### Manual Verification
1. Climb flat vertical wall (basic case)
2. Climb curved surface (cylinder/sphere)
3. Climb inside corner (concave geometry)
4. Climb to outside corner (should stop at edge)
5. Climb moving platform (position follows surface)
6. Verify hand IK contacts surface
7. Verify ledge climb-up works
8. Verify ground auto-dismount (hold S near floor)

---

## References

- **Invector Source:** `Assets/Invector-3rdPersonController/Add-ons/FreeClimb/Scripts/vFreeClimb.cs`
- **Animation Controller:** `Assets/Invector-3rdPersonController/Add-ons/FreeClimb/Invector@FreeClimb.controller`
- **Demo Scene:** `Assets/Invector-3rdPersonController/Add-ons/FreeClimb/Invector_Addon_FreeClimb.unity`
- **Copied Animations:** `Art/Animations/FreeClimb/` (sourced from Invector add-on)

---

## Next Steps

For advanced climbing features (animation, wall jump, hand IK, stamina, moving platforms), see:
- **[EPIC 12.2: Advanced Free Climbing Features](./EPIC12.2.md)**


---

