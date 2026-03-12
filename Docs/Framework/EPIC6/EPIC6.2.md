# EPIC 6.2: Physics Interaction (Pushing & Pulling)

**Status**: Implemented  
**Priority**: LOW (Future)  
**Goal**: Allow players to manipulate heavy physical objects (Crates, Debris) using physics-based coupling.
**Dependencies**: Epic 1.5 (Movement), Unity Physics (Joints)

## Implementation Details

The pushing system creates a physical linkage between the player and target objects, modifying the player's movement capabilities based on the object's mass.

### Core Systems

| System | Namespace | Description |
|---|---|---|
| **`PushInteractionSystem`** | `Player.Systems.Physics` | Handles the "Grab" input. Raycasts forward to find `PushableObject` entities and creates a Fixed `PhysicsJoint` connecting the player and the object. |
| **`PushMovementSystem`** | `Player.Systems.Physics` | Modifies player movement while pushing. Clamps velocity based on object mass (heavier = slower) and locks player rotation to face the object. |

### Data Components (DIG.Survival.Physics)

| Component | Description |
|---|---|
| **`PushableObject`** | Tag component containing `Mass` and `Friction`. Added to crates/debris. |
| **`ActivePushConstraint`** | Runtime component on the Player tracking the current push state (connected Entity, Joint Entity). |
| **`PushSettings`** | Defines push limits and speed modifiers (e.g., break force). |

### Authoring

| Script | Usage |
|---|---|
| **`PushableObjectAuthoring`** | Add to any crate/object. Configures Mass (kg). Requires Rigidbody/Collider. |
| **`PushInteractionAuthoring`** | Add to Player Prefab. Enables pushing mechanics. |

## Integration Guide

### 1. Player Setup
1.  Open the **Player Prefab**.
2.  Add the `PushInteractionAuthoring` component.
3.  Ensure the player has `PlayerInput` configured (handled by `PlayerInputSystem`).
4.  Input Mapping: **G Key** is mapped to Grab (Note: Replaces 'Tackle' input).

### 2. Creating Pushable Objects
1.  Create a GameObject (Crate, debris).
2.  Add a `PhysicsShape` (Box/Mesh Collider).
3.  Add a `PhysicsBody` (Rigidbody). Set Mass logic here or via `PushableObject`.
4.  Add `PushableObjectAuthoring`. Set **Mass** (e.g., 50 for light, 200 for heavy).
    *   *Note*: Pushing speed logic scales: 100kg = 1.0 (clamped max), 200kg = 0.5 speed.

## Technical Notes

*   **Joint Logic**: Uses `PhysicsJoint.CreateFixed`. This rigidly locks the player to the object. The player's Kinematic Controller drives the Dynamic object.
*   **Force Feedback**: The system does NOT apply forces to the player (Kinematic). Instead, it "simulates" weight by clamping the player's max velocity in `PushMovementSystem`.
*   **Assembly Structure**: Systems reside in the Player assembly (`Player.Systems.Physics`) to resolve circular dependencies between `DIG.Survival` (Components) and `DIG.Player` (Movement Logic).

## Validation / Testing

- [x] **Grab**: Pressing 'G' near a crate attaches the player.
- [x] **Release**: Pressing 'G' again or moving away (force break logic pending) detach.
- [x] **Movement**: Moving while attached drags the crate.
- [x] **Weight**: Heavier crates significantly slow down walking speed.
- [x] **Rotation**: Player automatically faces the crate while pushing.
