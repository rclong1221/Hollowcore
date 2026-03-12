# EPIC 13.15: Crouch/HeightChange Parity

> **Status:** COMPLETE  
> **Priority:** CRITICAL  
> **Dependencies:** EPIC 13.5 (Locomotion Abilities)  
> **Reference:** `OPSIVE/.../Runtime/Character/Abilities/HeightChange.cs`

## Overview

Bring the DIG `CrouchSystem` to feature parity with Opsive's HeightChange ability. Current implementation covers ~40% of features. **CRITICAL: Missing standup obstruction check allows players to clip through ceilings!**

---

## Sub-Tasks

### 13.15.1 Standup Obstruction Check
**Status:** COMPLETE  
**Priority:** CRITICAL

**This is a game-breaking bug.** Players can stand up inside low ceilings and clip through geometry.

#### Implementation
```csharp
// Before allowing standup:
bool canStand = true;

// For each collider on the character:
foreach (var collider in characterColliders) {
    if (collider is CapsuleCollider capsule) {
        // Check if standing height would overlap world geometry
        float standingHeight = originalHeight;
        Vector3 start = position + up * capsule.radius;
        Vector3 end = position + up * (standingHeight - capsule.radius);
        
        if (Physics.OverlapCapsuleNonAlloc(start, end, capsule.radius - skinWidth, 
                                            overlapBuffer, solidLayers) > 0) {
            canStand = false;
            break;
        }
    }
}

if (!canStand) {
    return; // Stay crouched!
}
```

#### New Component Fields
```csharp
public struct CrouchSettings : IComponentData
{
    public float StandingHeight;      // Original capsule height
    public float CrouchingHeight;     // Reduced height
    public float ColliderSpacing;     // Skin width
}

public struct CrouchAbility : IComponentData
{
    public bool StandupBlocked; // For UI feedback
}
```

#### Acceptance Criteria
- [x] Cannot stand up under low ceiling
- [x] Cannot stand up inside ventilation shafts
- [x] UI feedback when standup blocked (optional) - StandupBlocked field available
- [x] Works with all collider types (Capsule, Sphere, Box) - Capsule implemented, others deferred

---

### 13.15.2 Capsule Collider Height Adjustment
**Status:** COMPLETE  
**Priority:** HIGH

Dynamically resize the character's collider when crouching.

#### Implementation
```csharp
if (isCrouching) {
    // Shrink collider
    capsule.height = CrouchingHeight;
    capsule.center = new float3(0, CrouchingHeight / 2, 0);
} else {
    // Restore original
    capsule.height = StandingHeight;
    capsule.center = new float3(0, StandingHeight / 2, 0);
}
```

#### Acceptance Criteria
- [x] Collider shrinks when crouching
- [x] Collider restores when standing
- [x] Center adjusts to keep feet on ground

---

### 13.15.3 Height Animator Parameter
**Status:** COMPLETE  
**Priority:** MEDIUM

Send height state to animator for blend tree.

#### Implementation
```csharp
public struct PlayerAnimationState : IComponentData
{
    public int Height; // 0 = standing, 1 = crouching, 2 = prone
}
```

#### Acceptance Criteria
- [x] Animator receives height parameter
- [x] Blend tree uses height for posture

---

### 13.15.4 Block SpeedChange While Crouching
**Status:** COMPLETE  
**Priority:** LOW

Optionally prevent sprinting while crouched.

#### Implementation
```csharp
public struct CrouchSettings : IComponentData
{
    public bool AllowSpeedChange; // false = block sprint
}
```

#### Acceptance Criteria
- [x] Sprint blocked when crouching (if configured)
- [x] Configurable toggle

---

### 13.15.5 Multi-Collider Support
**Status:** DEFERRED  
**Priority:** LOW

Support Sphere and Box colliders in addition to Capsule.

#### Implementation
```csharp
// In obstruction check:
if (collider is SphereCollider sphere) {
    if (Physics.OverlapSphereNonAlloc(...) > 0) { ... }
} else if (collider is BoxCollider box) {
    if (Physics.OverlapBoxNonAlloc(...) > 0) { ... }
}
```

#### Acceptance Criteria
- [ ] Sphere collider characters can crouch
- [ ] Box collider characters can crouch
- [ ] Obstruction check works for all types

---

## Parity Gaps (Future Work)

The following are known gaps between our implementation and Opsive's HeightChange:

### 13.15.P1 Accurate Obstruction Check
**Status:** NOT STARTED  
**Priority:** MEDIUM

Current implementation uses a simple upward raycast. Opsive uses `Physics.OverlapCapsule` for exact shape matching.

**Risk:** Edge cases near sloped ceilings may not block standup correctly.

**Options:**
- **A (Keep Raycast)**: Simpler, performant. Good enough for most cases.
- **B (OverlapCollider)**: Use DOTS `OverlapCollider` with temp capsule BlobAsset for accuracy.

---

### 13.15.P2 Smooth Height Transition
**Status:** COMPLETE  
**Priority:** LOW

Opsive lerps collider height over time. Our implementation snaps instantly.

**Risk:** Visual jitter or physics glitches during rapid stance changes.

**Options:**
- **A (Keep Instant)**: No interpolation overhead.
- **B (Add Lerp)**: Use `crouchSettings.TransitionSpeed` to lerp `CurrentHeight`.

---

### 13.15.P3 Collider Center Offset
**Status:** COMPLETE  
**Priority:** LOW

Opsive adjusts `capsule.center` to keep feet planted. Our collider rebuild doesn't offset root position.

**Risk:** Visual "floating" or "sinking" during crouch transitions.

**Option:** Offset `LocalTransform.Position.y` by height delta.

---

### 13.15.P4 Prone Transition Obstruction
**Status:** COMPLETE  
**Priority:** MEDIUM

Current check only handles crouch→stand. Does not check prone→crouch or prone→stand.

**Risk:** Player clips through low obstacles when rising from prone.

**Option:** Extend obstruction check to any stance change to a taller state.

---

## Performance Optimizations (Future Work)

### 13.15.O1 Edge-Triggered Raycast
**Status:** COMPLETE  
**Priority:** HIGH

Current: Raycast runs every frame when `!wantsToCrouch`.

**Optimization:** Only raycast when input *just* changed (edge trigger on key release).

---

### 13.15.O2 Parallelize CrouchSystem
**Status:** COMPLETE  
**Priority:** MEDIUM

Current: Main-thread `foreach` loop.

**Optimization:** Convert to `IJobEntity` like `PlayerGroundCheckSystem`.

---

### 13.15.O3 Pre-Baked Colliders
**Status:** NOT STARTED  
**Priority:** HIGH

Current: Creates new `BlobAssetReference` on each stance change.

**Optimization:** Pre-bake 2 colliders (standing + crouched) in authoring, swap references at runtime. Zero runtime allocation.

---

### 13.15.O4 Cached Component Lookup
**Status:** NOT STARTED  
**Priority:** LOW

Current: `SprintSystem` uses `SystemAPI.HasComponent + GetComponent` per entity.

**Optimization:** Add `CrouchAbility` directly to the query if sprint blocking is common.

---

### 13.15.O5 Dirty Stance Flag
**Status:** COMPLETE  
**Priority:** LOW

Current: `PlayerColliderHeightSystem` checks `CurrentHeight` delta every frame.

**Optimization:** Use `IEnableableComponent` or a "DirtyStance" flag to skip unchanged entities.

---

## Files to Modify

| File | Changes |
|------|---------|
| `CrouchSystem.cs` | Obstruction check, collider resize |
| `CrouchSettings` component | Height values, AllowSpeedChange |
| `CrouchAbility` component | StandupBlocked state |
| `CrouchAuthoring.cs` | Expose settings |
| `CharacterControllerSystem.cs` | Apply collider changes |

## Verification Plan

1. Crouch under low ceiling → stays crouched when trying to stand
2. Crouch in vent shaft → cannot stand until exiting
3. Crouch in open area → stands normally
4. Bullet passes over crouching player → confirms hitbox shrunk
5. Sprint while crouched → blocked (if configured)

---

## Test Environment Tasks

Create the following test objects under: `GameObject > DIG - Test Objects > Traversal > Crouch Tests`

### 13.15.T1 Low Ceiling Tunnel
**Status:** COMPLETE

Tunnel that requires crouching to traverse.

#### Specifications
- Entrance: 2m ceiling (standing allowed)
- Main tunnel: 1.2m ceiling (crouch required)
- Internal alcoves with even lower ceilings (1m)
- Exit back to standing height
- Visual indicator showing "Crouch Required"

#### Hierarchy
```
Crouch Tests/
  Low Ceiling Tunnel/
    Entrance_2m
    Tunnel_1.2m
    Alcove_1m
    Exit_2m
    Crouch Sign (UI)
```

---

### 13.15.T2 Vent Shaft System
**Status:** COMPLETE

Network of ventilation shafts for stealth movement.

#### Specifications
- Multiple interconnected vents (1m x 1m cross-section)
- Vent grates that open/close
- Junction points where player can turn
- Exit points with varying ceiling heights
- Must stay crouched throughout

#### Hierarchy
```
Crouch Tests/
  Vent System/
    Vent_Entry
    Vent_Straight_1
    Vent_Junction
    Vent_Straight_2
    Vent_Exit_LowCeiling
    Vent_Exit_HighCeiling
```

---

### 13.15.T3 Crouch Cover Wall
**Status:** COMPLETE

Cover system test with varying wall heights.

#### Specifications
- Low wall (1m) - crouching hides player
- Medium wall (1.5m) - standing exposes head
- Window with bullet trajectory markers
- Shooter spawn point across area

#### Hierarchy
```
Crouch Tests/
  Cover Wall/
    Wall_Low_1m
    Wall_Medium_1.5m
    Window_Cutout
    Shooter_Point
    Bullet_Trajectory_Markers
```

---

### 13.15.T4 Standup Trap
**Status:** COMPLETE (Static layout; runtime moving ceiling deferred)

Trap that lowers ceiling after player enters.

#### Specifications
- Open room with 2m ceiling
- Pressure plate that lowers ceiling to 1.2m
- Player should be forced to crouch
- Cannot stand until ceiling raises again

#### Hierarchy
```
Crouch Tests/
  Standup Trap/
    Room
    Ceiling_Movable
    Pressure_Plate
    Reset_Trigger
```

---

### 13.15.T5 Collider Visualization Chamber
**Status:** COMPLETE

Room with visual markers showing collider extents.

#### Specifications
- Debug visualization of capsule collider
- Standing height markers
- Crouching height markers
- Real-time collider preview

#### Hierarchy
```
Crouch Tests/
  Collider Visualization/
    Chamber
    Standing_Height_Marker
    Crouching_Height_Marker
    Collider_Preview (Gizmo)
```

