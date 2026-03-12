# SETUP GUIDE 14.16: Asset Pipeline Migration

## Overview
This guide covers the setup and migration process for the new DIG Asset Pipeline (Phase 3), including the Equipment Workstation, Universal Sockets, and Automated Baking.

## Walkthrough: Migrating to the New Asset Pipeline

This guide will help you migrate your first weapon (e.g., Assault Rifle) to the new **Socket System**.

### Prerequisites
*   Open the **Equipment Workstation** (`Tools > DIG > Equipment Workstation`).
*   Ensure you have a Character Prefab (e.g., your Soldier) and a Weapon Prefab (e.g., `AssaultRifleWeapon`).

### Step 1: Rig the Character
1.  Go to the **Rigger** tab in the Equipment Workstation.
2.  Assign your character prefab to the **Character Prefab** field.
    *   **Note**: Use your **Visual/Authoring Prefab** (the one with the Mesh and Animator). This is typically what you see on the Client.
3.  Observe the status. It should show `[MISSING]` for MainHand, OffHand, etc.
4.  Click **Auto-Generate Sockets**.
5.  **Verify**: Hierarchy should now have `Socket_MainHand` under the Right Hand bone.

### Step 2: Align the Weapon (The "Bench")
1.  Go to the **Align** tab.
2.  Assign `AssaultRifleWeapon` (or your target weapon) to the **Weapon Prefab** field.
3.  Click **Start Alignment Session**.
4.  **Scene View**: You will see a "Ghost Hand" (Cube) and your weapon at `(0,0,0)`.
5.  **Action**: Move/Rotate the weapon so the handle fits perfectly into the Ghost Hand.
    *   *Tip: Visualize the Ghost Hand as the character's closed fist.*
6.  Click **SAVE TO PREFAB**.
7.  **Verify**: Select the weapon prefab in Project view. It should now have an `ItemGripAuthoring` component with non-zero values.

### Step 3: Define Logic (WeaponConfig)
1.  In Project View, right-click `Create > DIG > Items > Weapon Config`.
2.  Name it (e.g., `AssaultRifle_Config`).
3.  **Inspect it**:
    *   Set `Weapon Name`: "Assault Rifle"
    *   Set `Item ID`: 1 (Must match your system ID)
    *   **Combos**: If it's a melee weapon, add entries to `Combo Chain`. If gun, just ensure `Base Damage` etc are set.
4.  **Assign to Prefab**:
    *   Select your `AssaultRifleWeapon` prefab.
    *   Find the `WeaponAuthoring` component.
    *   Drag your `AssaultRifle_Config` asset into the **Config** slot.

### Step 4: Validation
1.  Open **Equipment Workstation** > **Board** (Pipeline Dashboard).
2.  Click **Refresh Matrix**.
3.  **Verify**: Your weapon row should show:
    *   Grip: **YES** (Green)
    *   Config: **YES** (Green)
    *   Status: **READY** (Green)

### Step 5: Test In-Game
1.  Play the game.
2.  Equip the Assault Rifle.
3.  **Result**:
    *   **Visual**: It snaps to the new socket.
    *   **Logic**: If you set custom stats in Config, they should apply (verify via Logs/Debug).
4.  **Check Console**: Look for `[WeaponEquipVisualBridge] Using Universal Socket...`.

### Step 6: Cleanup (Later)
*   Once ALL weapons are verified, you can delete `AssaultRifleParent`, `PistolParent`, etc. from the Character Prefab.
*   *Do NOT do this until you have migrated everything.*
