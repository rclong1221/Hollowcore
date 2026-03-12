# Setup Guide: EPIC 14.23 - Robust Surface Projection

## Overview
Replaces raycast climbing with "Surface Projection" (SphereCast) to handle irregular geometry like rocks and cliffs.

## For Environment Artists

### Collision Setup
- **Mesh Colliders:** "Convex" is recommended for moving objects. Concave (Non-Convex) Mesh Colliders are supported for static terrain.
- **Smoothness:** The system uses a **0.3m SphereCast**.
  - **Benefit:** Small cracks/seams in geometry are ignored.
  - **Note:** Very tight crevices (< 0.3m) might not be climbable as the sphere won't fit.

## For Designers (Tuning)

### FreeClimbSettings Component
Located on the Player Prefab/Entity.

| Parameter | Recommended | Description |
| :--- | :--- | :--- |
| **Surface Detection Radius** | `0.3` | Size of the "feeler" sphere. Increase for stickier climbing on rough terrain. Decrease for more precision on small props. |
| **Whiskers**| *(Internal)* | The system fires 3 secondary rays (Whiskers) if the main sphere misses. This handles 90-degree outer corners automatically. |

## Verification
- **Test:** Climb a bumpy rock face. The camera/character should not jitter; they should glide over the bumps using the "Virtual Normal".
