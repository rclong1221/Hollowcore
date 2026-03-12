# Setup Guide: EPIC 14.22 - Climbing Fixes

## Overview
Fixes specific edge cases in the climbing system: Hang-to-Vault transitions and Horizontal Loops around cylinders.

## For Level Designers

### 1. Ledge Geometry
- **Vaulting:** Ensure ledges where you want the player to vault up have clear standing space above them.
- **Cylinders/Pillars:** Players can now climb 360 degrees around pillars.
  - **Constraint:** Ensure pillars have smooth collision meshes. Jagged 90-degree internal corners on pillars might still be tricky without the refinements in 14.27.

## For Developers/Debuggers

### Logging Toggles
If climbing behaves strangely, enable logging on the Player entity components:
1. Select Player in Hierarchy (Runtime).
2. Locate these Authoring components:
   - `FreeClimbDetectionSystem` -> Check `Enable Logging`.
   - `FreeClimbMountSystem` -> Check `Enable Logging`.
   - `FreeClimbDiagnosticSystem` -> Check `Enable Logging`.

### Key Parameters (FreeClimbMovementSystem)
These hardcoded constants in `FreeClimbMovementSystem.cs` control the fixes:
- `MAX_ROTATION_PER_FRAME`: 30f. (Controls how fast player rotates to match cylinder curve).
- `inputY *= 0.3f`: (Internal) Downscales input when hanging to allow vault intent without sliding up.
