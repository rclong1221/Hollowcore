# Setup Guide: Epic 13.3 (Movement Polish)

## Overview
This epic adds "game feel" abilities to the player, such as snappy acceleration, quick stops, and 180-degree turns. These are configured via the `MovementPolishAuthoring` component.

## Step 1: Add Component
> [!IMPORTANT]
> **Client/Server Setup:**
> Because this system affects physical movement (Prediction & Authority), you must add the Authoring component to **BOTH** the Client and Server prefabs.

1.  Open **`Warrok_Client`** Prefab -> Add `MovementPolishAuthoring`.
2.  Open **`Warrok_Server`** Prefab -> Add `MovementPolishAuthoring`.
3.  **Ensure Settings Match:** The configuration values must be identical on both prefabs to prevent prediction errors (rollback/jitter).

## Step 2: Configure Settings

### Quick Start
*   **Accel Multiplier:** `2.0` (Double acceleration when starting from stop).
*   **Duration:** `0.2` (Boost lasts for 0.2s).
*   **Min Input Threshold:** `0.1` (Requires significant input).

### Quick Stop
*   **Decel Multiplier:** `3.0` (Triples friction when input stops).
*   **Min Speed:** `2.0` (Only triggers if moving fast enough).

### Quick Turn
*   **Turn Threshold:** `150` degrees (Triggers 180 turn if input reverses).
*   **Speed Boost:** `1.5` (Maintains momentum during turn).

### Air / Fall
*   **Fall Gravity Multiplier:** `1.5` (Falls faster than rising).
*   **Max Fall Speed:** `20.0`.
*   **Landing Drag:** `5.0` (Briefly slows down on impact).

### Idle
*   **Time To Idle:** `0.1` (Time before switching to idle state).

## Step 3: Verification
1.  Enter Play Mode.
2.  Move the character.
3.  **Confirm:** Character accelerates quickly ("snappy").
4.  **Confirm:** Releasing keys stops character quickly (no "sliding on ice").
5.  **Confirm:** Pressing 'S' while running 'W' performs a quick turn without losing speed.
