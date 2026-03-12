# Setup Guide: EPIC 13.20 (Advanced Free Climb & Parity)

## Overview
This epic introduces advanced free-climb mechanics including **Gap Crossing** (jumping between parallel walls), **Corner Rounding** (moving around convex corners), and **Free Hang** (hanging without footholds).

## Setup Instructions

### 1. Gap Crossing Setup
To create a gap crossing challenge:
1.  Place two parallel walls with a gap between **1.0m and 2.5m**.
2.  Add the `ClimbableSurface` authoring component to both walls.
3.  **Crucial**: Ensure the normals of the walls face each other.
4.  Test: While climbing one wall, hold input towards the other wall and press **JUMP**.

### 2. Corner Rounding Setup
To allow players to traverse around corners:
1.  Ensure the corner geometry is convex (outer corner).
2.  Both surfaces meeting at the corner must be `ClimbableSurface`.
3.  The system automatically detects the corner when the player reaches the edge.
4.  **Tuning**: If the player clips through the corner, adjust the `CornerOffset` in `ClimbableObject` (if manually added) or `FreeClimbSystem`.

### 3. Free Hang Setup
Free hang occurs automatically when:
1.  The player is climbing.
2.  Feet raycasts fail to find a surface.
3.  Hands still have a valid hold.
4.  **Verification**: You will see the character enter the "Free Hang" state in the debugger, and foot IK will be disabled.

### 4. Ledge Strafe & Obstructed Check
- **Ledge Strafe**: Movement along ledges is now automatically handled. If you try to move diagonally but one axis is blocked (e.g., at the top of a wall), the system will slide you along the valid axis.
- **Obstructed Check**: The system casts a ray from the character's head in the direction of movement. If a ceiling or overhang is detected, movement stops to prevent clipping.
    - **Config**: Adjust `ClimbColliderHeightMultiplier` in `FreeClimbSettings` to tune the clearance height.

### 5. Advanced Mounting (Top & Air Mount)
- **Top Mount (Climb Down)**: Walk off a ledge slowly. If you are facing away from the ledge (or turn quickly), the system detects the surface behind/below you and snaps to a climb.
- **Air Mount**: If `AutoClimbLedge` is enabled in settings, jumping towards a wall will automatically grab it without needing a second Jump press.
    - **Requirements**: Player must be falling (`Velocity.y < -0.5`) and facing the wall.

## Test Objects
New automated test objects are available to verify these features easily.
1.  Open **DIG > Test Objects > Traversal > Complete Test Course**.
2.  This generates a course including:
    -   **Gap Crossing Section**: Parallel walls at 0.5m, 1.0m, 1.5m, and 2.0m gaps.
    -   **Vertical Gap**: To test vertical reach.
    -   **Corner Test**: A block setups for inner/outer corner transitions.

## Debugging
-   **Can't Gap Jump?**
    -   Check if `GapCrossingEnabled` is true in `FreeClimbSettings`.
    -   Ensure distance is < `MaxGapJumpDistance` (default 3.0m).
    -   Ensure player is facing the gap (input direction).
-   **Getting Stuck on Corners?**
    -   Check collision geometry. Mesh Colliders are recommended for complex shapes.
