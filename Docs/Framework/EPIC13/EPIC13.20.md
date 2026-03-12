# EPIC 13.20: Advanced Free Climb & Parity

## Overview
This epic focuses on bringing the `FreeClimb` system to feature parity with standard climbing solutions (like Opsive) while maintaining the voxel-compatible, physics-based architecture suitable for DIG. 

Current implementation suffers from "sticky" climbing (climbs everything), lack of crucial states (free hang, gap crossing), and input conflicts (cannot jump off).

## Objectives
1.  **Selective Surface Detection**: Stop climbing on "everything". Implement specific component/tag requirements.
2.  **State Machine Refinement**: Distinct handling for Wall Jump vs. Jump Off vs. Drop.
3.  **Advanced Traversal**: Gap crossing (chain climbing) and Corner Rounding.
4.  **Free Hang Support**: Allow climbing without foot support (dangling).
5.  **Smoother Entry/Exit**: Fix "sticky" behavior and allow fluid detachment.

## 1. Selective Surface Detection
**Problem**: `FreeClimbDetectionSystem` currently raycasts against a layer mask (likely set to Default), causing the player to climb players, dynamic objects, and random geometry.
**Solution**:
-   Introduce `ClimbableSurface` component (or reliable Tag system).
-   Update `FreeClimbDetectionSystem` to require this marker OR a specific `Climbable` physics layer.
-   **Auto-Tagging**: Voxel chunks should automatically apply this tag to vertical surfaces if needed, or we rely on specific block types.

## 2. Input Handling & Detachment
**Problem**: Player "starts climbing and can't detach".
-   `FreeClimbMountSystem` has a cooldown check that might be failing or resetting too fast.
-   Input conflict: Jump is used for both "Wall Jump" (with direction) and "Jump Off" (with/without direction?).
**Refinement**:
-   **Jump Off (Wall Kick)**: `Input.Jump` + `Input.Back` (or away from wall).
-   **Wall Jump**: `Input.Jump` + `Input.Left/Right/Up`.
-   **Drop**: `Input.Crouch` (detach and fall).
-   **Hard Detach**: Prevent immediate re-attachment to the *same* surface for `X` seconds after voluntary detachment.

## 3. Advanced Movement Features

### 3.1 Gap Crossing (Chain Climb)
**Requirement**: "With climbable objects next to each other, we should able to chain climb."
**Algorithm**:
1.  **Trigger**: `MovementRay` fails (edge of surface reached).
2.  **Gap Cast**:
    -   Calculate `GapOrigin = CurrentGripPos + (MoveDirection * GapDistance)` (e.g., 1.0m).
    -   Cast Ray from `GapOrigin + (SurfaceNormal * 0.5f)` direction `-SurfaceNormal`.
    -   *Logic*: Look for a parallel surface across the gap.
3.  **Transition**:
    -   If Hit: Trigger `MountTransition`.
    -   Target Pos: `Hit.Point`.
    -   Target Rot: `LookRotation(-Hit.Normal)`.

### 3.2 Corner Rounding (The "Spider" Logic)
**Requirement**: Smoothly traverse inner and outer corners.
**Algorithm**:

**A. Outer Corner (Convex)**
1.  **Trigger**: `MovementRay` fails (edge) AND `GapCast` fails (no wall across).
2.  **Side Cast**:
    -   We are effectively wrapping 90 degrees around the block.
    -   Calculate `CornerOrigin = CurrentGripPos + (MoveDirection * Offset) - (SurfaceNormal * Offset)`.
    -   Cast Ray from `CornerOrigin` direction `-MoveDirection`.
    -   *Logic*: Look back at the "side" of the block we just passed.
3.  **Action**:
    -   If Hit: Rotate player 90 degrees around the corner axis.
    -   New Normal = `Hit.Normal`.
    -   New Forward = Old Normal.

**B. Inner Corner (Concave)**
1.  **Trigger**: Movement is physically blocked (Physics Body collision), but `MovementRay` (surface check) is still valid or invalid.
2.  **Wall Cast**:
    -   Cast Ray from `GripPos` direction `MoveDirection` (Forward along surface).
    -   *Logic*: Is there a climbable wall blocking our path?
3.  **Action**:
    -   If Hit (and Angle is ~90 deg to current normal):
    -   Trigger Transition to the *blocking* wall.
    -   New Normal = `Hit.Normal`.
    -   Player rotates -90 degrees.

### 3.3 Free Hang (Dangling)
**Requirement**: "Features like hanging... feels flawed."
**Logic**:
-   If `FeetRaycast` fails (no wall below feet) but `HandRaycast` holds:
-   Enter `FreeHang` state.
-   Change animation (dangle).
-   Disable `FeetIK`.
-   Allow "Pull Up" (vault) from this state if `Input.Up` is pressed at the top.

## 4. Technical Implementation Plan

### Phase 1: Filtering & Input Fixes
1.  **Refactor `FreeClimbDetectionSystem`**: Add `CheckForClimbableComponent` logic.
2.  **Update `FreeClimbMountSystem`**:
    -   Fix `Dismount` logic (verify cooldown persistence).
    -   Bind `Crouch` to Drop.
    -   Tune `Jump` detachment forces.

### Phase 2: Traversal Logic
3.  **Enhance `FreeClimbMovementSystem`**:
    -   Add `GapSearch` logic when primary movement rays fail.
    -   Add `CornerRecovery` for outer corners.
4.  **Implement `FreeHangSystem`**:
    -   New state in `FreeClimbState` enum/bools.
    -   Logic to detect "feet loss".

### Phase 3: Animation & Polish
5.  **Bridge Update**: Ensure `ClimbAnimatorBridge` handles `Hang` vs `Climb` blend trees.
6.  **IK Polish**: Ensure hands stick correctly during gap transit.

## Success Criteria
- [x] **Selective Surface Detection**:
    - [x] Implement `ClimbableSurface` component / tag.
    - [x] **Angle Validation (Opsive Parity)**: Ensure player is facing the surface within +/- 60 degrees.
    - [ ] **Obstructed Check (Opsive Parity)**: Raycast ahead of movement to prevent climbing into ceilings or solid objects.

- [x] **Advanced Entry/Exit (Opsive Parity)**:
    - [ ] **Top Mount (Climb Down)**: Detect ledges below feet when walking off an edge to auto-transition to climb.
    - [ ] **Air Mount**: Allow mounting mid-air (jumping onto a wall).
    - [x] **Dismount Force**: Apply usage push-away force on detach to prevent sticky re-climbing.

- [x] **Advanced Traversal**:
    - [x] **Gap Crossing**: Chain climb between separated objects.
    - [x] **Inner/Outer Corner Logic**:
        - [x] Implement "Spider" wrapping for outer corners.
        - [ ] **Corner Offsets**: Ensure player maintains distance from inner corners to avoid clipping (Opsive `InnerCornerOffset`).
    - [ ] **Ledge Strafe Mechanics**:
        - [ ] Detect "narrow ledge" situations where only sideways movement is allowed.
        - [ ] (Future) Separate `LedgeStrafe` state if different from normal Climb.

- [x] **Free Hang**:
    - [x] Detect "No Foothold" state.
    - [ ] Transition animations to "Dangle".

- [ ] **Polish**:
    - [ ] **Configurable Offsets**: Expose `HandIKOffset`, `FootIKOffset`, and `BodyWallOffset` in authoring for precise visual alignment.

## Test Environment
To verify these features, the following test objects should be added to the `TraversalObjectCreator`:

### 13.20.T1: Gap Crossing (Chain Climb)
- **Goal**: Verify ability to jump between parallel walls.
- **Setup**:
    - Two parallel walls facing each other.
    - Test Gaps: 0.5m, 1.0m, 1.5m, 2.0m.
- **Success**: Player can "chain jump" back and forth without falling.

### 13.20.T2: Corner Rounding
- **Goal**: Verify inner and outer corner traversal.
- **Setup**:
    - **Convex (Outer)**: A simple column or block to climb around.
    - **Concave (Inner)**: An L-shaped wall corner.
- **Success**: Player smoothly transitions around corners without losing grip.

