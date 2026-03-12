# SETUP GUIDE 14.3: Data-Driven Animation Configuration

## Overview
EPIC 14.3 transitions the project from hardcoded weapon logic in `WeaponEquipVisualBridge.cs` to a data-driven system using the new `ItemAnimationConfig` ECS component. This allows designers to configure weapon behavior (timings, aim requirements, combos) directly on prefabs.

## Key Components

### 1. ItemAnimationConfig (Component)
*   **Data:** Holds `WeaponType`, `ComboCount`, `UseDuration`, `IsChanneled`, etc.
*   **Source:** Baked from the `ItemAnimationConfigAuthoring` MonoBehaviour on weapon prefabs.

### 2. ItemAnimationConfigAuthoring (MonoBehaviour)
*   **Location:** Add this to any Weapon Prefab (e.g., Bow, Rifle, Sword).
*   **Usage:** Configure animation settings here.
    *   **AnimatorItemID:** Overrides the ID sent to the Animator (e.g., 61 for Magic).
    *   **WeaponType:** Defines input routing (Magic, Melee, Shield, etc.).
    *   **UseDuration:** Controls cast times or hold durations.
    *   **ComboCount:** Sets max combo steps for melee.

### 3. ItemConfigAutomator (Tool)
*   **Location:** `Tools > DIG > Update Item Animation Configs`
*   **Purpose:** Automatically adds and populates `ItemAnimationConfigAuthoring` on all prefabs based on their legacy `WeaponAuthoring` settings. Run this if you add new weapons or reset prefabs.

## Verification Checklist

Use this checklist to verify that weapons behave correctly under the new system.

### 1. Magic Casting (IDs 61-65)
*   **Setup:** Equip a magic staff.
*   **Verify:**
    *   [ ] Left Click trigger "Use" animation.
    *   [ ] Cast duration matches `UseDuration` on the prefab.
    *   [ ] Movement is locked during cast (if `LockMovementDuringUse` is checked).
    *   [ ] Movement cancels cast (if `CancelUseOnMove` is checked).

### 2. Melee Combo (Sword: 25, Knife: 23)
*   **Setup:** Equip a sword.
*   **Verify:**
    *   [ ] Spamming trigger performs a combo sequence (1 -> 2 -> 3).
    *   [ ] Changing `ComboCount` on the prefab changes the actual max combo length in-game.

### 3. Bow (ID 4)
*   **Setup:** Equip the Bow.
*   **Verify:**
    *   [ ] Right Click (Hold) enters "Aim" (Draw) state.
    *   [ ] Character holds draw indefinitely (Channeled behavior).
    *   [ ] Release Right Click fires.

### 4. Dual Pistols (ID 2)
*   **Setup:** Equip Pistols in both Slot 0 and Slot 1.
*   **Verify:**
    *   [ ] Verify proper idling and firing behavior.
    *   [ ] `WeaponEquipVisualBridge` now checks both hands for `ItemAnimationConfig` to determine Dual Wield state.

### 5. Shield (ID 26)
*   **Setup:** Equip Shield in Slot 1.
*   **Verify:**
    *   [ ] Right Click blocks.
    *   [ ] Verify Block animation plays.

## Troubleshooting
*   **Animation not playing?** Check if `ItemAnimationConfigAuthoring` is present on the prefab and `AnimatorItemID` is correct.
*   **Wrong behavior?** Enable `DebugLogging` on `WeaponEquipVisualBridge` to see runtime config values in the Console.
