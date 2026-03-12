# Setup Guide: EPIC 14.24 - Ledge Hang System

## Overview
Implements a specific "Hanging" state when at the top of a ledge, blocking input during transitions.

## For Animators

### Animation Events (Critical)
The system relies on Animation Events to unlock input.
**Ensure these events are present in your Animation Clips (FBX import settings):**

1. **`OnAnimatorHangStartInPosition`**
   - **Where:** At the end of `HangStart`, `DropStart`, `ClimbToHang`.
   - **Effect:** Transition from "Entering Hang" (Input Blocked) to "Hanging" (Input Allowed: Shimmy/Vault).

2. **`OnAnimatorHangComplete`**
   - **Where:** At the end of `PullUp` (Vault) animation.
   - **Effect:** Transition from "Vaulting" (Input Blocked) to "Standing".
   - **Safety:** If missed, the system has a 2-second timeout, but it will feel laggy.

## For Designers

### Valid Ledges
- The system automatically detects a "Ledge" when:
  1. Hands have wall capability.
  2. Feet have **NO** wall capability (dangling).
- **Setup:** Ensure climbable walls have a flat top or ending. If the wall continues up, it's just a wall, not a ledge.

### Controls
- **W / Jump:** Vault Up.
- **S:** Climb Down.
- **A / D:** Shimmy sideways.
