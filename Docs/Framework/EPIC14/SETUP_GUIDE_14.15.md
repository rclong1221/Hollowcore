# SETUP_GUIDE_14.15.md - First Person Arms Animation Integration

## Overview
This guide covers the setup and verification steps for integrating First Person Arms animations from Opsive demos into the `ClimbingDemo.controller`. This enables proper weapon handling animations (Equip, Fire, Reload, Aim) for both standard weapons and swimming-specific weapons.

## Prerequisites
- [ ] **Opsive First Person Controller** imported
- [ ] **Opsive Swimming Pack** imported
- [ ] **EPIC 14.12** (Agility Pack) completed
- [ ] **AnimatorArmsCopier** tool available (`Assets/Editor/AnimatorArmsCopier.cs`)

## Step 1: Copy Weapon States

The `ClimbingDemo.controller` lacks first-person arm animations. We use the `AnimatorArmsCopier` tool to copy them from `SwimmingFirstPersonArmsDemo.controller` (which contains both standard and swimming weapons).

1.  Open **DIG > Animation > Copy First Person Arms States**.
2.  **Source Controller**: Assign `SwimmingFirstPersonArmsDemo.controller` (search in Project view).
    *   *Path:* `Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/SwimmingFirstPersonArmsDemo.controller`
3.  **Target Controller**: Assign `ClimbingDemo.controller`.
    *   *Path:* `Assets/Art/Animations/Opsive/AddOns/Climbing/Animator/ClimbingDemo.controller`
4.  **Checkboxes**: Ensure all are checked (Copy Parameters, Copy Layers, Copy State Machines).
5.  Click **Copy States**.
6.  Wait for "Copy Complete!" in the console.

## Step 2: Configuration Verification

The code integration has been completed automatically, but verify the following files have the correct modifications:

1.  **OpsiveAnimatorConstants.cs**:
    *   Should define `ITEM_TRIDENT = 20` and `ITEM_UNDERWATER_GUN = 21`.
2.  **WeaponEquipVisualBridge.cs**:
    *   `ItemIDToLayerName` should include mappings for Trident and Underwater Gun.
    *   `ItemIDToSubStateMachine` should include mappings for Trident and Underwater Gun.

## Step 3: Item Database Setup

You must configure the Item Database (ScriptableObject) to include the new swimming weapons.

1.  Locate your Item Database / Item Definitions.
2.  **Add New Item: Trident**
    *   **ID**: 20
    *   **Name**: Trident
    *   **Category**: Swimming Melee (or similar)
3.  **Add New Item: Underwater Gun**
    *   **ID**: 21
    *   **Name**: Underwater Gun
    *   **Category**: Swimming Ranged
4.  **Assign Prefabs**: Ensure you have weapon prefabs for these items (even placeholders) so the visual bridge has something to spawn.

## Step 4: Verification

### Standard Weapons
1.  **Enter Play Mode**.
2.  Equip **Assault Rifle** (Slot 1).
    *   Verify arms appear.
    *   Verify Idle, Fire, Reload animations play.
3.  Equip **Pistol** (Slot 6).
    *   Verify smooth unequip/equip transition.

### Swimming Weapons
1.  **Enter Water** (Trigger Swimming state).
2.  Equip **Trident** (Item ID 20).
    *   Verify arms switch to Trident hold animations.
    *   Test Attack (Left Click).
3.  Equip **Underwater Gun** (Item ID 21).
    *   Verify arms switch to Underwater Gun hold animations.
    *   Test Fire.

## Troubleshooting

- **Arms not visible?**
    *   Check `WeaponEquipVisualBridge` inspector. Ensure `HandAttachPoint` is assigned.
    *   Check Console for "[WEAPON_DEBUG] UPPERBODY_LAYER enabled". If not found, the layer weight might be 0.
- **Animations stuck/wrong?**
    *   Enable `DebugLogging` on `WeaponEquipVisualBridge`.
    *   Look for "SET Slot0ItemID: X". Ensure X matches your Item ID.
    *   Ensure the Animator Controller actually contains the states (Step 1).
- **Swimming weapons working on land?**
    *   This is currently allowed by the animation system but should be restricted by gameplay logic (`ItemEquipSystem`). This guide only covers *animation* support.
