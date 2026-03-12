# EPIC 14.23: Robust Surface Projection Climbing

## Overview
We are replacing the legacy "Discrete Raycast" climbing logic with a "Continuous Surface Projection" system inspired by *Breath of the Wild*. This ensures robust traversal on procedural geometry, cylinders, and imperfect meshes where standard raycasts often fail.

## Core Pillars

### 1. Organic "Surface Projection"
Instead of asking "Is there a wall exactly X meters away?", we continuously project the character's intention against the environment.
*   **SphereCast Sweep:** We use a `0.3f` radius SphereCast for movement. This "glides" over cracks, seams, and low-poly terrain noise that would stop a single ray.
*   **Virtual Normal:** We smooth the surface normal input to create a "Virtual Surface" that the player model aligns to, filtering out jitter from jagged rocks.

### 2. The "Sensor Array" (Edge Recovery)
When the primary sweep fails (e.g., walking off a ledge or around a cylinder), we don't immediately stop. We fire a calibrated array of "Whiskers" to find where the surface went.

*   **Whisker A (90° Inward):** Detects standard corners and box geometry.
*   **Whisker B (135° Back):** Detects acute angles (sharp pillars, thin fences).
*   **Whisker C (45° Forward):** Detects chamfered or beveled corners.

If any whisker hits, we interpret it as a continuous surface and "orbit" onto it.

### 3. Depenetration (Inside Corners)
Instead of stopping movement when hitting a perpendicular wall (inside corner), we:
1.  Calculate the "Crease Vector" (Cross product of Current Normal x New Normal).
2.  Project movement onto this crease.
3.  Result: The player slides deep into the corner rather than getting stuck.

## Implementation Details

### `FreeClimbMovementSystem.cs`

We will completely rewrite `TryResolveSurface`.

#### New Flow:
1.  **Project:** Calculate `TargetPosition = CurrentPos + MoveDelta`.
2.  **Sweep:** `Physics.SphereCast(TargetPosition, -CurrentNormal, ...)`
3.  **If Sweep Hits:**
    *   Update `GripWorldPosition` / `Normal`.
    *   Apply "Virtual Normal" smoothing.
4.  **If Sweep Misses (The Void):**
    *   Trigger `TryFindSurfaceFromVoid(TargetPosition)`.
    *   Fire Whiskers.
    *   If Whisker hits -> Interpolate to new surface (Orbit).
    *   If all miss -> Stop (Ledge/Cliff found).

### Deprecated Logic
*   `TryOuterCorner` (The old explicit checking logic)
*   `CornerCooldown` / `CornerState`
*   Manual `TryCast` phases 2, 3, 4.

## Verification
*   **Cylinders:** Movement should be smooth with no "stutter" or "snap".
*   **Procedural Rocks:** Player should not jitter violently on uneven surfaces.
*   **Inside Corners:** Player should slide into the corner, not stop 1m away.
