# EPIC 14.4 Setup Guide - Off-Hand Shield & Blocking

## Overview
This update enables fully functional off-hand shields with blocking mechanics.

## 1. Setup
**No manual setup required** if you have already set up the `LeftHandAttachPoint` from the previous step.
- Ensure `Atlas_Client.prefab` has `WeaponEquipVisualBridge` with `Left Hand Attach Point` assigned.
- Ensure the player entity has been re-baked (Enter Play Mode).

## 2. Verification Steps

### Test Shield Blocking
1. Enter Play Mode.
2. Equip a **One-Handed Weapon** in Main Hand (e.g. Sword, Pistol).
   - Press `1` (Sword) or `2` (Pistol).
3. Equip a **Shield** in Off-Hand.
   - Press `Option+2` (Mac) or `Alt+2` (Windows) if Shield is in slot 2.
   - Or use Debugger Window: Click **Off Hand: [2]**.
4. Verify Shield is visible on left arm.
5. **Press and Hold Right Mouse Button**.
   - Character should enter **Block Stance** (Shield raised).
   - Release RMB to return to Idle.

### Test Two-Handed Suppression
1. Equip **Shield** in Off-Hand.
2. Equip a **Two-Handed Weapon** (e.g. Rifle/AK-47).
   - Press `3` (Rifle).
3. Verify Shield **disappears** (visuals suppressed).
4. Switch back to Sword (`1`).
5. Verify Shield **reappears**.

## Troubleshooting
- **Shield doesn't block?** Check Console for `[DIGEquipmentProvider] Off-Hand Use Input (RMB) Pressed`.
- **Shield visible but animation doesn't play?** `WeaponEquipVisualBridge` connects input to Animator parameter `Slot1ItemStateIndex`. Ensure Animator has transitions for `Slot1ItemStateIndex = 3`.

## Known Issues
- **Shield Off→Main Swap Animation Stuck:** When moving Shield from Off-Hand to Main-Hand, the left arm may remain stuck in "shield holding" pose. 
  - **Workaround:** Use the Equipment System Debugger's **Clear** button on Off-Hand **before** equipping Shield to Main-Hand.
  - **Root Cause:** The Animator Controller's state machine doesn't properly transition through "Unequip" when items swap slots. Requires Animator Controller investigation.
