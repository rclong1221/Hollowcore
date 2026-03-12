# EPIC 6.3: Ragdoll & Physics Reactions

**Status**: Implemented
**Priority**: Medium
**Goal**: Replace static death animations with dynamic ragdoll physics for realistic body reactions to damage and gravity.
**Dependencies**: Epic 4.1 (Death), Unity Physics

## Implementation Details

The ragdoll system transitions a character from a Kinematic (Animated) state to a Dynamic (Physics-driven) state upon death. This interaction ensures visual fidelity during eliminations without the cost of full-time physics simulation.

### Core Systems

| System | Namespace | Description |
|---|---|---|
| **`RagdollTransitionSystem`** | `Player.Systems` | Monitors `DeathState`. When `Phase` becomes `Dead` or `Downed`, it triggers the ragdoll transition: disabling the root collider, unparenting bones, enabling dynamic physics mass, and inheriting velocity. |
| **`KillZoneSystem`** | `Player.Systems.Hazards` | Handles environmental hazards (like the "Kill Zone" in testing) that apply damage over time using Physics Trigger Events. |

### Data Components (DIG.Survival.Physics)

| Component | Description |
|---|---|
| **`RagdollController`** | Added to the character Root. Tracks `IsRagdolled` state and references the `Pelvis` entity. |
| **`RagdollBone`** | Added to each limb entity (Pelvis, Spine, Head, Limbs). Flags the entity as part of the ragdoll chain. |
| **`KillZone`** | Helper component for hazards, defining `DamagePerSecond`. |

### Authoring

| Script | Usage |
|---|---|
| **`RagdollAuthoring`** | Add to Character Root. Assign the **Pelvis** GameObject. Recursively bakes `RagdollBone` onto all children with `Rigidbody`. |
| **`RagdollTestAuthoring`** | Helper for testing. Adds `Health` and `DeathState` to an entity (useful for Dummies). |
| **`KillZoneAuthoring`** | Creates a hazardous volume that damages entities triggering it. |

## Integration Guide

### 1. Character Setup
1.  **Skeleton Hierarchy**: Ensure the character has a GameObject hierarchy for bones (Pelvis -> Spine -> Head etc.).
2.  **Physics Setup**: Add `Rigidbody` (Kinematic) and `Collider` (Capsule/Box) to each bone.
3.  **Joints**: Connect bones using `CharacterJoint` or `ConfigurableJoint`.
4.  **Authoring**: Add `RagdollAuthoring` to the root GameObject and link the `Pelvis`.
5.  **Baking**: The system automatically configures the entities for transition.

### 2. Testing (Traversal Course)
A "Ragdoll Test" section (Section 12) has been added to the **Complete Test Course**:
-   **Kill Zone**: A red transparent volume that deals massive damage.
-   **Ragdoll Dummy**: A kinematic "Stick Figure" suspended in the zone. Upon play, it takes damage, dies, transitions to ragdoll, and falls dynamically.

## Technical Notes

*   **Kinematic to Dynamic**: Bones are baked as Kinematic entities (InverseMass = 0) to follow animations cheaply. On death, `RagdollTransitionSystem` uses `PhysicsMass.CreateDynamic` to calculate and assign real mass properties (based on Colliders), enabling gravity and collision response.
*   **Velocity Inheritance**: The system copies the `PhysicsVelocity` from the player root to all bones at the moment of death, preserving momentum.
*   **Dependency Management**: `RagdollTransitionSystem` resides in `Player` assembly to check `DeathState`, while Components reside in `DIG.Survival`, ensuring proper clean architecture.

## Validation / Testing

- [x] **Death Trigger**: Entities transition to ragdoll state immediately upon Health depletion.
- [x] **Physics Activation**: Bones become dynamic and react to gravity/collisions.
- [x] **Separation**: Bones detach from the Root parent (transform hierarchy) to simulate independently while maintained by Joints.
- [x] **Hazard Interaction**: `KillZoneSystem` correctly detects overlaps and applies damage.
