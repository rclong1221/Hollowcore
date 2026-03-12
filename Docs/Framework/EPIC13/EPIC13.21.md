# EPIC 13.21: Camera & Gameplay Parity (Opsive Standard)

## Overview
This epic aims to achieve true parity with the Opsive Ultimate Character Controller (UCC) Camera and Gameplay systems. Our current monolithic implementation lacks the modularity ("View Types"), physics integration ("Spring System"), and state management ("State System") that defines the Opsive standard.

## Opsive Feature Gap Analysis
| Feature | Opsive Implementation | Current DIG Implementation | Gap |
| :--- | :--- | :--- | :--- |
| **Architecture** | **View Types** (Modular, Swappable at Runtime) | Single `PlayerCameraControlSystem` | **Critical** |
| **Configuration** | **State System** (Preset-based property overrides) | Hardcoded fields / simple logic | **Critical** |
| **Physics** | **Spring System** (Shake, Bob, Sway, Recoil) | Basic Lerner | **High** |
| **Occlusion** | **Object Fader** & Collision | None | **High** |
| **Gameplay** | **Movement Types** (Combat, Adventure, Point&Click) | Single WASD Logic | **High** |
| **Input** | **Input Integration** (Abstracted, Unity/Legacy) | Hybrid/Direct check | Medium |

## Opsive Reference Architecture (For Guidance)

This section provides technical notes on *how* Opsive implements features, to guide our own ECS implementation.

### 1. The Camera ViewType Pattern
Opsive uses a Strategy Pattern where `CameraController` is the context and `ViewType` is the strategy.
*   **Structure**: `ViewType` is an abstract base class. Concrete classes (`Combat`, `Adventure`, `FirstPerson`) override `Rotate` and `Move`.
*   **Key Methods**:
    *   `Rotate(horizontalInput, verticalInput, ref targetRotation)`: Calculates the new rotation.
    *   `Move(ref targetPosition)`: Calculates the new position (after rotation).
    *   `LookDirection(distance)`: Returns the forward vector for character IK/Aiming.
*   **Composition**: The Controller maintains a list of *possible* ViewTypes. Only one is `Active` at a time.
*   **Transitioning**: Switching ViewTypes often involves a `TransitionViewType` that lerps values between the old and new view over time.

### 2. The State System (Configuration Layering)
Opsive uses a hierarchical "preset" system called the State System.
*   **Concept**: Instead of hardcoding logic like `if (aiming) fov = 40`, they treat "Aim" as a holistic state that can affect *any* scalar property on *any* component.
*   **Mechanism**:
    *   **Base State**: The values set in the Inspector.
    *   **State Presets**: ScriptableObjects or serialized lists of `(PropertyName, OverrideValue)`.
    *   **Active State List**: A priority-sorted list of currently active states (e.g., `["Airborne", "Aim", "Crouch"]`).
*   **Resolution Loop**:
    1.  Start with Base values.
    2.  Iterate Active State list from bottom (lowest priority) to top.
    3.  Apply overrides.
    4.  Result is the value used this frame.
*   **ECS Adaptation**: We can simulate this by having a `DynamicBuffer<ActiveStateTag>` on the generic entity, and a system that reads `CameraStateConfig` blobs to resolve final values (FOV, Offset, etc.) into `PlayerCameraSettings`.

### 3. The Spring System (Procedural Physics)
Used for all procedural motion (Bob, Sway, Shake, Recoil).
*   **Math Model**: Damped Harmonic Oscillator.
    *   `Force = -Stiffness * Displacement - Damping * Velocity`
    *   Integrate acceleration -> velocity -> position.
*   **Parameters**:
    *   `Stiffness` (k): Return strength (Higher = snaps back faster).
    *   `Damping` (c): Resistance (Higher = less oscillation/jiggle).
*   **Application**:
    *   `ViewType` does its logic first (base position).
    *   `Spring` modifiers are added on top (additive offset).
    *   Recoil adds an impulsive force (velocity change) to the spring.

### 4. Object Fader (Occlusion)
*   **Logic**:
    1.  **Obstruction Check**: SphereCast or Raycast from Camera to Character Head.
    2.  **Proximity Check**: Distance check to Character Center.
*   **Targeting**:
    *   If `Hit.collider != Player`, it's an obstruction.
    *   Cache `Material` of hit object.
    *   Set `_Color.a` or `Mode` to Transparent.
*   **Optimization**: Support "fading" multiple objects by maintaining a `List<FadedObject>` struct to restore opacity when they are no longer hit.

## Objectives

### 1. The View Type System (Architecture)
Refactor the monolithic camera system into a modular strategy pattern.
-   **`IViewType` Interface**:
    -   `UpdateView(deltaTime, input)`
    -   `GetTargetTransform()`
-   **Required Implementations**:
    -   `CombatView` (Third Person over-shoulder)
    -   `AdventureView` (Third Person free orbit)
    -   `FirstPersonView` (Locked to head, handles self-mesh visibility)
    -   `TopDownView` (Fixed angle, follows position)
    -   `PointAndClickView` (RTS style)

### 2. The Spring System (Physics)
Implement a generic Spring/Damper system for camera modifiers.
-   **Usage**: Weapon recoil, land shake, run bob, explosion impact.
-   **Logic**: `TargetValue`, `CurrentValue`, `Velocity`, `Stiffness`, `Damping`.
-   **Integration**: Each `ViewType` applies spring offsets to its final calculation.

### 3. The State System (Configuration)
A data-driven override system.
-   **Concept**: Entities have "States" (e.g., "Zoom", "Run", "Crouch").
-   **Presets**: `CameraProfile` assets define values for base state.
-   **Overrides**: Profiles contain override sets: "If State == Zoom, set FOV = 40".
-   **Resolution**: System creates a composite config frame every tick based on active states.

### 4. Advanced Gameplay (Movement & Occlusion)
-   **Point & Click**:
    -   Requires `NavMeshAgent` (or equivalent) authoring.
    -   New `PointAndClickMovementType` that ignores WASD and follows NavMesh path.
-   **Object Fader**:
    -   Raycast from Camera to Character.
    -   If hit object is not Player, apply "Dither/Fade" material property (requires Shader support or Material swapper).

## Technical Implementation Plan

### Phase 1: Core Architecture (View Types & Springs)
1.  Define `CameraViewType` component and enum.
2.  Create `CameraSpringState` component (ghosted/local).
3.  Implement `SpringSolverSystem` (updates physics).
4.  Refactor `PlayerCameraControlSystem` to delegate logic to specific View methods based on enum.

### Phase 2: Gameplay & Occlusion
5.  Implement `PointAndClick` View & Movement logic (NavMesh).
6.  Implement `ObjectFaderSystem` (detects obstruction, fades materials).

### Phase 3: The State System (Stretch Goal)
7.  Define `CameraStateProfile` ScriptableObject.
8.  Implement `CameraStateResolverSystem` to apply overrides.

## Success Criteria
- [ ] Camera logic is modular (View Types).
- [ ] Camera has "juicy" physics (Springs) for shakes/recoil.
- [ ] Objects fade out when blocking view (Object Fader).
- [ ] "Point & Click" gameplay exists and works (NavMesh).
- [ ] "Point & Click" gameplay exists and works (NavMesh).
- [ ] Configuration is driven by profiles/states, not hardcoded if-statements.

## Test Environment
To verify these features, the following test objects should be added to the `TraversalObjectCreator`:

### 13.21.T1: Camera Occlusion Course
- **Goal**: Verify Object Fader system.
- **Setup**:
    - A narrow corridor with pillars between the camera and player.
    - "See-through" walls that should fade when blocking the view.
- **Success**: Objects turn transparent/dither when obstructing the character.

### 13.21.T2: Point & Click Arena
- **Goal**: Verify NavMesh integration and top-down control.
- **Setup**:
    - A flat arena with small maze-like obstacles.
    - NavMesh baked on the surface.
- **Success**: Player moves to clicked point, pathfinding around walls.
