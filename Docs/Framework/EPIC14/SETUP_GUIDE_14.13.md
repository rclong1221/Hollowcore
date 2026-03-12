# SETUP_GUIDE_14.13.md - Swimming Pack Animation Integration

## Overview
This guide covers the verification and setup steps for the Opsive Swimming Pack animation integration (AbilityIndex 301).

## Prerequisites
- [ ] **Opsive Swimming Pack** imported into the project
- [ ] **EPIC 14.12** (Agility Pack) completed
- [ ] **AnimatorAbilityCopier** tool available (from EPIC 14.12 Phase 0)

## Step 1: Copy Swimming States

The `ClimbingDemo.controller` needs the swimming states from `SwimmingDemo.controller`.

1.  Open **Window > Analysis > Animator Ability Copier**.
2.  **Source Animator**: Assign `SwimmingDemo.controller` (Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/SwimmingDemo.controller).
3.  **Target Animator**: Assign `ClimbingDemo.controller` (Assets/Art/Animations/Opsive/AddOns/Climbing/Animator/ClimbingDemo.controller).
4.  **Ability Config**:
    - **Ability ID**: 301 (Swim)
    - **IntData Values**: 0, 1, 2, 3, 4
5.  Click **Copy Ability States**.
6.  Repeat for **Dive** (Ability 302) if needed.
7.  Repeat for **ClimbFromWater** (Ability 303) if needed.

> **Note:** If the copier tool is not yet available, you may need to manually copy the "Swim" sub-state machine from `SwimmingDemo` to `ClimbingDemo` Base Layer, and ensure transitions use `AbilityIndex = 301`.

## Step 2: Verification

1.  **Enter Play Mode**.
2.  Locate a body of water (or create a WaterZone).
3.  **Jump into water**:
    - Verify `AbilityIndex` changes to **301**.
    - Verify animation plays **Swim Enter** (IntData 0) then **Swim Surface** (IntData 1).
4.  **Swim around**:
    - Move WASD - verify swimming locomotion.
5.  **Dive underwater** (Look down + move):
    - Verify `AbilityIndex` stays **301**.
    - Verify `AbilityIntData` changes to **2** (Underwater).
    - Verify animation changes to underwater swim.
6.  **Exit water**:
    - Swim to shore.
    - Verify `AbilityIndex` resets to **0** (Locomotion) when exiting.

## Troubleshooting

- **Character T-poses in water**: The Swim states are missing from the Animator. Perform Step 1 again.
- **Character walks on water bottom**: `SwimmingState.IsSwimming` is not being set. check `WaterDetectionSystem`.
- **Animation stuck in Swim**: `SwimmingState.IsSwimming` is not clearing upon exit. Check `SwimExitThreshold` in `SwimmingComponents.cs`.

## Parameters Reference

| Parameter | Type | Value | Description |
|-----------|------|-------|-------------|
| AbilityIndex | Int | 301 | Active when swimming |
| AbilityIntData | Int | 0 | Enter from air |
| AbilityIntData | Int | 1 | Surface swim |
| AbilityIntData | Int | 2 | Underwater swim |
| AbilityIntData | Int | 3 | Exit water (moving) |
| AbilityIntData | Int | 4 | Exit water (idle) |
