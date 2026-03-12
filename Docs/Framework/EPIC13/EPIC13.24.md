# EPIC 13.24: Locomotion & IK Parity

## Overview
This epic focuses on "grounding" the character. Opsive uses a sophisticated IK system (Foot IK, Look At IK) and Kinematic Root Motion to make the character feel connected to the world. Our current system handles basic movement but lacks the procedural adaptation to terrain (slopes, stairs) and focus targets.

## 1. Grounding & Terrain Adaptation (Foot IK)
**Current Status**: `FootIKSystem` exists but might be basic or specialized for specific states.
**Opsive Standard**:
-   **Predictive Foot Placement**: Raycasts from hip/knee to find ground.
-   **Pelvis Adjustment**: Lowers the hips if feet need to reach lower ground (or raises them).
-   **Slope Alignment**: Rotates feet to match surface normal.
-   **Stair Handling**: Ignores "invisible ramps" to place feet on visual steps if needed.

### Implementation Plan
-   **`FootIKSystem` Refactor**:
    -   Implement generic 2-Bone IK solver (CCD or Analytical) in ECS.
    -   **Raycast Logic**:
        -   Cast from `KneePos + UpOffset` downwards.
        -   Calculate `FootOffset` and `HipOffset`.
    -   **Smoothing**: Lerp weights to prevent popping when walking over small bumps.

## 2. Look-At & Aiming IK
**Current Status**: Basic head rotation in some states.
**Opsive Standard**:
-   **Body Weight**: Spines rotate partially.
-   **Head Weight**: Head rotates fully.
-   **Eyes Weight**: Eyes tracking point of interest.
-   **Clamp**: Limits to prevent "Exorcist" head spinning.

### Implementation Plan
-   **`LookAtIKSystem`**:
    -   Target: `CameraTarget.Position` (or specific `LookTarget` entity).
    -   Weighted distribution over `Spine`, `Chest`, `Neck`, `Head` bones.
    -   **Contexts**: different weights for "Combat" (stiff body) vs "Idle" (loose head).

## 3. Root Motion & Determinism
**Current Status**: Manual velocity calculations in `CharacterController`.
**Opsive Standard**:
-   **Root Motion Support**: Option to use Animation Root Motion for complex moves (Vaulting, Rolling) while keeping physics consistent.
-   **Interpolation**: Smooths visual root motion on top of physics velocity.

### Implementation Plan
-   **`RootMotionBridge`**:
    -   Extract delta position/rotation from Animator.
    -   Apply to `CharacterController` velocity during "RootMotion" states (e.g., Dodge, Climb).
    -   **Server-Side**: If possible, bake Root Motion curves into Blob Assets for server-side prediction without a full Animator.

## 4. Technical Roadmap

### Phase 1: The Unified IK Solver
1.  Implement `IKSolverJob`: A generic burst-compatible solver (FABRIK or CCD).
2.  Deploy `FootIKSystem` checking ground normals.

### Phase 2: Look-At Logic
3.  Implement `LookAtSystem` reading `CameraTarget`.
4.  Add weighting profiles (Combat/Peaceful).

### Phase 3: Root Motion Integration
5.  Refactor `LocomotionSystem` to accept "Animator Delta" overrides.

## Success Criteria
- [ ] Character feet align with 45-degree slopes.
- [ ] Character hips drop when straddling a peak/stair.
- [ ] Character looks at the camera aim point naturally.
- [ ] Special moves (Rolls) use exact animation distances (Root Motion) instead of estimated forces.

## Test Environment
To verify these features, the following test objects should be added to the `TraversalObjectCreator`:

### 13.24.T1: IK Terrain & Stairs
- **Goal**: Verify predictive foot placement and hip adjustment.
- **Setup**:
    - **Stairs**: Standard staircase (0.3m height increments).
    - **Uneven Ground**: Grid of blocks with varying heights (+/- 0.2m noise).
    - **Slopes**: Ramps at 15°, 30°, 45°.
- **Success**: visible foot contact with ground, hips lower to accommodate reach.
