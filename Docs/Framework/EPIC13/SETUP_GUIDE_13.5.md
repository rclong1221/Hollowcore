# Setup Guide: Epic 13.5 (Locomotion Abilities)

## Overview
Epic 13.5 replaced the legacy hardcoded movement logic with a modular Ability System.
**Jump, Crouch, and Sprint** are now separate Abilities configurable via `LocomotionAbilityAuthoring`.

## Step 1: Add Authoring Component
> [!IMPORTANT]
> **Client/Server Setup:**
> Locomotion physics MUST be identical on Client and Server for prediction to work. Add this component to **BOTH** prefabs.

1.  Open **`Warrok_Client`** Prefab -> Add `LocomotionAbilityAuthoring`.
2.  Open **`Warrok_Server`** Prefab -> Add `LocomotionAbilityAuthoring`.
3.  **Ensure Settings Match:** Verify that `JumpSettings`, `CrouchSettings`, etc., are identical.

## Step 2: Configure Ghost Mode (CRITICAL)
> [!CAUTION]
> **Network Replication:**
> The player ghost prefab MUST use `Owner Predicted` mode for crouch/sprint animations to replicate correctly to remote clients.

1.  Open **`Warrok_Server`** Prefab.
2.  Find the **`GhostAuthoringComponent`**.
3.  Set **Supported Ghost Modes** to `Owner Predicted`.
4.  Save the prefab.

**Why:** If set to `All` or `Predicted`, all clients will predict all player ghosts. This causes local systems to overwrite replicated values, breaking animation replication for remote players.

## Step 3: Configure Settings

### Jump Ability
*   **Jump Force:** `5.0` (Instant vertical velocity).
*   **Max Jumps:** `1` (Set to 2 for double jump).
*   **Gravity Multiplier:** `0.5` (Determines how "floaty" the jump feels when holding the button).

### Crouch Ability
*   **Crouch Height:** `1.0` (Collider height when crouching).
*   **Transition Speed:** `10.0` (Visual smoothing speed).
*   **Speed Multiplier:** `0.5` (Reduces movement speed by 50% when engaged).

### Sprint Ability
*   **Speed Multiplier:** `1.5` (Increases movement speed by 50% when engaged).

## Step 4: Verification
1.  Enter Play Mode.
2.  **Jump:** Press Space. Ensure player jumps.
3.  **Crouch:** Hold 'C' (or Ctrl). Ensure player moves slower.
4.  **Sprint:** Hold Shift. Ensure player moves faster.

### Multiplayer Verification
1.  Host a game and join with a second client.
2.  Have the remote player crouch.
3.  **Confirm:** The host sees the remote player's crouch animation.
4.  **Confirm:** Remote player's `Stance` changes are visible to all clients.

## Important Notes
*   **Legacy Code:** The methods `PerformJump` and stance-based speed switches were DELETED from `PlayerMovementSystem.cs`. If the new abilities are not added to the prefab, the player **will not be able to jump, crouch, or sprint**.
*   **Animation Sync:** `PlayerAnimationStateSyncSystem` (server-side) syncs `PlayerState.Stance` to `PlayerAnimationState.IsCrouching` for network replication.
