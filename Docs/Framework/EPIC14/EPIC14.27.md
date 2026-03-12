# EPIC 14.27 - Climbing System Refinement & Edge Case Handling

## Overview
This epic focuses on hardening the **Object Gravity** climbing system (implemented in EPIC 14.26) by addressing subtle geometric, dynamic, and network edge cases. The goal is to move from a "working prototype" to a "production-ready" locomotion system that handles concave corners, moving platforms, and thin geometry robustly.

---

## Refinement Goals

### 1. Geometric Stability
- [ ] **Concave Corner Stabilization**: Implement normal smoothing to prevent jitter in tight V-shaped corners.
- [ ] **Thin Surface Validation**: Add thickness checks to prevent mounting "paper-thin" geometry where the character's volume would clip.
- [ ] **Curvature Optimization**: Improve whisker array density for smoother traversal over cylindrical voxels.

### 2. Dynamics & Interaction
- [ ] **Moving Platform Support**: Implement Local-Space grip tracking so players stay attached to moving/rotating objects (boats, elevators).
- [ ] **Conflict Resolution (Mount vs. Interact)**: Prioritize interaction logic when looking directly at stations/items to prevent accidental mounting.
- [ ] **Exit Debouncing**: Add a short cooldown between auto-dismount and re-mount to prevent the "stuttering" loop on bumpy terrain.

### 3. System Safety & Reliability
- [ ] **Transition Timeouts**: Add "Panic Fall" safety checks to break out of stuck lerps (e.g., if an animation event is missed).
- [ ] **Adhesion Hysteresis**: Implement a short grace period for raycast misses to prevent prediction flicker on jagged voxel edges.
- [ ] **NaN Protection**: Add rigorous validation for normals and positions across all climbing systems to prevent "physics explosions."

---

## Technical Approach

### 1. Stabilized Normals (Concave Corners)
We will introduce a `SmoothedNormal` in `FreeClimbLocalState`.
- Instead of snapping instantly to the raycast hit normal, we will slerp the normal used for rotation.
- This prevents the "vibrating" effect when a player is wedged between two blocks.

### 2. Local-Space Adhesion (Moving Platforms)
We will transition from absolute world-space tracking to relative tracking:
```csharp
// Store relative transform in FreeClimbState
climb.GripLocalPosition = math.transform(math.inverse(surfaceTransform), worldPos);
climb.GripLocalNormal = math.rotate(math.inverse(surfaceTransform), worldNormal);
```
In `FreeClimbMovementSystem`, we will re-calculate the world position based on the `SurfaceEntity`'s current `LocalTransform` before applying input.

### 3. Surface Validation (Thin Shells)
In `FreeClimbDetectionSystem`, a "Reverse Probe" will be added:
- After a hit is found, fire a ray from the *back* of the wall toward the player.
- If the distance between hits is `< 0.2m`, reject the mount as "too thin."

---

## Implementation Tasks

### Phase 1: Stability & Safety (Geometric Refinement)
1. **Normal Smoothing**: Update `FreeClimbMovementSystem` to use slerped normals for character alignment.
2. **Transition Timeouts**: Add `double LastTransitionStartTime` to `FreeClimbState` and enforce an exit if `TransitionProgress < 1.0` after 2 seconds.
3. **Adhesion Grace Period**: Add a "Stickiness" timer to maintain `IsAdhered` for 3-5 frames after a raycast miss.

### Phase 2: Dynamic Environments (Moving Platforms)
1. **Local-Space Logic**: Update `FreeClimbState` with `GripLocalPosition`/`Normal`.
2. **Transform Reconstitution**: Modify `FreeClimbMovementSystem` to project world-space input onto the *moving* surface plane.

### Phase 3: Interaction & Polish
1. **Mount Suppression**: Add a flag to `PlayerInput` to suppress climbing if an interaction prompt is active.
2. **Exit Debouncing**: Store `LastAutoDismountTime` and block re-mounting for 0.4s to stabilize ground-touching logic.

---

## Verification Plan

### Automated Tests
1. **Moving Platform Test**: Spawn a player on a rotating voxel block, verify they stick to the same face.
2. **Thin Wall Test**: Attempt to climb a 1-unit thick plane, verify rejection.
3. **Concave Corner Test**: Move into a 90-degree inner corner, verify no rotation jitter.

### Manual Verification
1. Climb up onto a ledge while a "Pilot" prompt is visible (verify interaction takes priority).
2. Move rapidly between different surface types on a complex voxel shape.
3. Verify character doesn't get stuck in "Transitioning" state if the animation is skipped in the Inspector.
