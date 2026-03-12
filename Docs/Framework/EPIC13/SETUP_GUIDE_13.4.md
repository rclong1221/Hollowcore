# Setup Guide: Epic 13.4 (IK System)

## Overview
The Epic 13.4 IK System uses a Hybrid ECS/MonoBehaviour approach.
*   **ECS:** Calculates Raycasts and Target positions (Burst compiled).
*   **MonoBehaviour (`IKBridge`):** Applies the calculated targets to the Animator via `OnAnimatorIK`.

## Step 1: Add Authoring Component (Logic)
> [!IMPORTANT]
> **Client/Server Setup:**
> Add `IKAuthoring` to **BOTH** prefabs. ECS needs the IK configuration on the Server to validate logic (aim direction) and on the Client for prediction and visual application.

1.  Open **`Warrok_Client`** Prefab -> Add `IKAuthoring`.
2.  Open **`Warrok_Server`** Prefab -> Add `IKAuthoring`.
3.  **Assignments:** Assign bones (Head, Hips, Feet) reference.
    *   *Note:* Ensure the Server prefab has the necessary bone hierarchy (even if valid meshes aren't present) or at least the references stay valid if they share a skeleton definition.
4.  **Sync Settings:** Ensure configuration (weights, speeds) matches on both.

## Step 2: Add IK Bridge (Visuals)
> [!NOTE]
> **Client Only:**
> The `IKBridge` component operates on the `Animator`, which only exists/runs on the Client representation.

1.  Open **`Warrok_Client`** Prefab (or specific Visual child).
2.  Add `IKBridge` component to the GameObject with the `Animator`.
3.  The Bridge will automatically find the ECS Entity and apply the data calculated by the systems.

## Step 3: Configure Settings (Inspector)

### Foot IK
*   **Raycast Offset:** `0.5` (Start ray above foot).
*   **Raycast Length:** `1.2`.
*   **Foot Offset:** `0.12` (Height of foot bone from ground).
*   **Pelvis Offset Speed:** `5.0` (How fast hips adjust to ground height).

### Look At IK
*   **Head Weight:** `0.7` (70% of rotation on head).
*   **Spine Weight:** `0.3` (30% on spine).
*   **Target Speed:** `15.0` (Smoothing speed for aim point).

## Step 4: Verification
1.  Enter Play Mode.
2.  Walk on uneven terrain (stairs/slopes).
3.  **Confirm:** Feet align to the slope angle.
4.  **Confirm:** Hips lower slightly when feet are on different elevations.
5.  Move the mouse/camera.
6.  **Confirm:** Character's head and spine rotate to look at the aim point.
