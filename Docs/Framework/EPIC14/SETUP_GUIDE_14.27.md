# Setup Guide: EPIC 14.27 - Climbing Refinement

## Overview
Polish for Moving Platforms, Concave Corners, and Transitions.

## For Level Designers

### Moving Platforms
- **Requirement:** Any moving climbable object (elevator, spaceship, floating rock) **MUST** have a `PhysicsVelocity` component (if ECS) or `Rigidbody` (if GameObject).
  - **Why:** The scaling system calculates "Relative Velocity". If the platform moves but has no velocity data, the player will slide off.

### Concave Corners
- **Setup:** Standard 90-degree internal corners work out of the box.
- **Tuning:** If the player jitters in a corner, check `Corner Transition Time`.

## For Designers (Tuning)

### FreeClimbSettings Component

| Parameter | Recommended | Description |
| :--- | :--- | :--- |
| **Corner Transition Time** | `0.15` | Time (seconds) to interpolate between two walls. Increase to `0.2` or `0.25` if corner traversal feels jerky. |
| **Debounce Time** | `0.4` | Cooldown after dismounting before re-mounting. Prevents "Mount spam" on uneven floors. |

### Thin Geometry Protection
- The system now rejects walls thinner than **0.2m** to prevent camera clipping.
- **Fix:** If a wall isn't climbable, thicken the collider.
