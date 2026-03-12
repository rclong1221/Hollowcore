# Setup Guide: EPIC 14.26 - Object Gravity Climbing

## Overview
The core physics change. Gravity is now relative to the surface being climbed.

## For Level Designers

### Ceiling Climbing
- **How to:** Simply place a climbable surface upside down.
- **Requirement:** `Adhesion Strength` must be high enough to counteract World Gravity if you want the player to stick indefinitely without stamina drain (though stamina usually limits this).

### Voxel/Destruction
- **Setup:** No special interaction needed. The system listens for chunk modification events.
- **Constraint:** If a voxel is destroyed under the player, they will fall unless another valid surface is within **Surface Detection Radius** (0.3m).

## For Designers (Tuning)

### FreeClimbSettings Component

| Parameter | Recommended | Description |
| :--- | :--- | :--- |
| **Adhesion Strength** | `0.8 - 1.0` | `1.0` = Full gravity replacement (Spider-man). `< 1.0` = Some downward drag. |
| **Min Surface Angle** | `30` | Surfaces flatter than this trigger Auto-Dismount (walking). |

## Environment Requirements
- **Thickness:** Avoid single-plane walls (0 thickness). Use walls with at least **0.1m - 0.2m** thickness to ensure the gravity vector calculation remains stable.
