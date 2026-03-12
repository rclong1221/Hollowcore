# Setup Guide: Epic 13.24 - Locomotion & IK Parity

## Overview
This guide validates the setup for the new Unified IK Solver (Foot IK), Look-At systems, and Root Motion integration.

## Step 1: Foot IK Setup
1.  **Authoring**:
    -   Select your Player Prefab (e.g., `Warrok_Client`).
    -   Ensure `FootIKSettings` component is present (added via `IKAuthoring` or `LocomotionPolishAuthoring`).
    -   Set `FootRayLength` (approx 1.2m) and `FootOffset` (height from ground to ankle pivot).
2.  **Layers**:
    -   Ensure your terrain/floors are on the Physics Layer included in the `Default` collision filter (or configure specific mask in `FootIKSystem`).
3.  **Validation**:
    -   Enter Play Mode. Walk on stairs. Hips should lower (`BodyOffset` becomes negative) when straddling steps.

## Step 2: Look-At Setup
1.  **Camera**:
    -   Ensure the `AimDirection` system is running and updating `AimPoint`.
2.  **Settings**:
    -   In `LookAtIKSettings` (Authoring):
        -   Set `MaxHeadAngle` (e.g., 60 degrees).
        -   Set `HeadWeight` (1.0).
        -   Set `Mode` to `MouseAim`.
3.  **Animator**:
    -   Ensure `IKBridge` is on the GameObject with the `Animator`.
    -   The Animator Controller must have "IK Pass" enabled on the Base Layer.

## Step 3: Root Motion
1.  **Configuration**:
    -   In `LocomotionAbilityAuthoring`, enable `RootMotion` support (if toggle exists) or verify `RootMotionDelta` component is baked.
2.  **Usage**:
    -   Root Motion is automatically captured by `IKBridge`.
    -   To *use* it, a specific Ability/State must set `UseRootMotion = true` on the `RootMotionDelta` component.
    -   *Note*: Standard locomotion (WASD) typically overrides this. Use it for climb mounting or precise interactions.

## Troubleshooting
-   **"Feet floating above ground"**: Increase `FootRayLength` or check `FootOffset` (too high?).
-   **"Head spinning"**: Decrease `MaxTotalAngle` in `LookAtIKSettings`.
-   **"No IK Effect"**: Verify "IK Pass" is checked in the Animator Controller layer settings.
