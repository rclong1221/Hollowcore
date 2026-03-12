# SETUP GUIDE 13.28 - Weapon Animation States Setup

## Overview

This guide covers how weapon animations work in Opsive animator controllers and how to set up missing states.

### Layer Architecture

**Both Demo.controller and ClimbingDemo.controller use the same limb-based layers:**
- Base Layer
- Left Hand Layer / Right Hand Layer
- Arms Layer
- Upperbody Layer
- Left Arm Layer / Right Arm Layer  
- Left Upperbody Layer / Right Upperbody Layer
- Additive Layer / Additive Left Arm Layer / Additive Right Arm Layer
- Full Body Layer

Weapon animations are driven by **parameters** (Slot0ItemID, Slot0ItemStateIndex, etc.), not separate weapon layers.

---

## Understanding the ClimbingDemo Architecture

### How Weapon Animations Work in ClimbingDemo

Weapon animations are **parameter-driven** within limb layers, NOT on separate weapon layers.

The animator uses these parameters to select animations:
| Parameter | Type | Description |
|-----------|------|-------------|
| `Slot0ItemID` | int | Which weapon is equipped (23=Knife, 24=Katana, etc.) |
| `Slot0ItemStateIndex` | int | Current action (0=Idle, 1=Aim, 2=Use, 3=Reload) |
| `Slot0ItemSubstateIndex` | int | Variation/combo (1, 2, 3 for melee combos) |
| `MovementSetID` | int | Weapon category (0=Guns, 1=Melee, 2=Bow) |
| `Aiming` | bool | Currently aiming |

### Which Layers Handle Weapons

| Layer | Purpose |
|-------|---------|
| **Upperbody Layer** | Primary weapon animations (fire, reload, attack) |
| **Arms Layer** | Arm positioning while holding weapons |
| **Right Arm Layer** | One-handed weapon use |
| **Left Upperbody Layer** | Two-handed weapon positioning |
| **Full Body Layer** | Full-body attacks (heavy swings, jump attacks) |

---

## Part 1: Check Current Weapon States

### Step 1: Open Animator Window
1. **Window → Animation → Animator**
2. In Project window, navigate to: `Assets/Art/Animations/Opsive/AddOns/Climbing/`
3. Select **ClimbingDemo.controller**

### Step 2: Examine Upperbody Layer
1. In Layers panel, click **"Upperbody Layer"**
2. Look for sub-state machines like:
   - "Items" (contains weapon states)
   - "Use" (attack/fire states)
   - Blend trees for weapon types

### Step 3: Check Parameters
1. Click **"Parameters"** tab (next to Layers)
2. Verify these exist:
   - `Slot0ItemID` (int)
   - `Slot0ItemStateIndex` (int)
   - `Slot0ItemSubstateIndex` (int)
   - `MovementSetID` (int)
   - `Aiming` (bool)
   - `Slot0ItemStateIndexChange` (trigger)

---

## Part 2: Identify Missing States

### What States Should Exist

**In the Upperbody Layer → Items sub-state machine:**

For **Katana (ItemID=24)**:
- ✅ Idle
- ✅ Aim
- ✅ Equip From Idle / Unequip From Idle
- ❌ Attack 1 From Idle (MISSING)
- ❌ Attack 2 From Idle (MISSING)
- ❌ Attack 1 From Aim (MISSING)
- ❌ Attack 2 From Aim (MISSING)
- ❌ Jump Attack (MISSING)
- ❌ Recoil states (MISSING)

For **Guns (AssaultRifle, Pistol, etc.)**:
- ✅ Idle
- ✅ Aim
- ✅ Equip / Unequip
- ⚠️ Fire (may be missing or incomplete)
- ⚠️ Reload (may be missing or incomplete)

### Visual Check
In the Animator graph:
1. Navigate to the Items sub-state machine
2. Look for states with "Attack", "Fire", "Reload" in their names
3. States with missing motions show as "Missing!" or have no animation clip

---

## Part 3: Add Missing States Manually

Since ClimbingDemo uses limb layers instead of weapon layers, you need to add states within the existing structure.

### Step 1: Locate the Correct Sub-State Machine
1. In **Upperbody Layer**, find the "Items" sub-state machine
2. Double-click to enter it
3. Find weapon-specific states (organized by ItemID conditions)

### Step 2: Create a New Attack State
1. Right-click in empty area → **Create State → Empty**
2. Name it (e.g., "Katana Attack 1")
3. Select the state, in Inspector:
   - **Motion:** Drag an attack animation clip from:
     - `Assets/OPSIVE/.../Animations/Katana/` or similar
   - **Speed:** 1
   - **Write Defaults:** Match other states in this layer

### Step 3: Create Transition FROM Entry/Any State
1. Right-click on **"Any State"** → Make Transition
2. Click on your new attack state
3. Select the transition arrow
4. In Inspector, add conditions:

**For Katana Attack 1:**
```
Slot0ItemID = 24 (Katana)
Slot0ItemStateIndex = 2 (Use)
Slot0ItemSubstateIndex = 1 (Attack 1)
```

**For Katana Attack 2 (combo):**
```
Slot0ItemID = 24
Slot0ItemStateIndex = 2
Slot0ItemSubstateIndex = 2
```

### Step 4: Create Exit Transition
1. Right-click on attack state → Make Transition
2. Click on "Exit" or the Idle state
3. Select the transition, in Inspector:
   - **Has Exit Time:** ✅ Checked
   - **Exit Time:** 0.85 (near end of animation)
   - **Transition Duration:** 0.15
   - **Conditions:** `Slot0ItemStateIndex = 0` (optional)

### Step 5: Set Transition Properties
For the entry transition (Any State → Attack):
| Setting | Value |
|---------|-------|
| Has Exit Time | ❌ Unchecked |
| Transition Duration | 0.1 |
| Can Transition To Self | ❌ Unchecked |

---

## Part 4: Animation State Index Reference

### State Index Values (Slot0ItemStateIndex)
| Value | State | Description |
|-------|-------|-------------|
| 0 | Idle | Default, not doing anything |
| 1 | Aim | Aiming (right mouse held) |
| 2 | Use | Firing/Attacking (left mouse) |
| 3 | Reload | Reloading (R key) |
| 4 | Equip | Equipping weapon |
| 5 | Unequip | Unequipping weapon |
| 6 | Drop | Dropping weapon |
| 7 | Melee (legacy) | Some setups use 7 for melee |
| 8 | Block | Blocking (melee) |
| 9 | Parry | Parrying (melee) |

### Substate Values (Slot0ItemSubstateIndex)
| Value | Use |
|-------|-----|
| 0 | Default/None |
| 1 | Attack 1 / Fire variation 1 |
| 2 | Attack 2 / Combo hit 2 |
| 3 | Attack 3 / Combo hit 3 |
| 4+ | Additional combos |

### Movement Set IDs
| Value | Category |
|-------|----------|
| 0 | Guns (ranged) |
| 1 | Melee |
| 2 | Bow |

---

## Part 5: Weapon-Specific Conditions

### Katana (ItemID = 24)
| State Name | Conditions |
|------------|------------|
| Attack 1 | ItemID=24, StateIndex=2, SubstateIndex=1 |
| Attack 2 | ItemID=24, StateIndex=2, SubstateIndex=2 |
| Attack 3 | ItemID=24, StateIndex=2, SubstateIndex=3 |
| Block | ItemID=24, StateIndex=8 |

### Knife (ItemID = 23)
| State Name | Conditions |
|------------|------------|
| Attack 1 | ItemID=23, StateIndex=2, SubstateIndex=1 |
| Attack 2 | ItemID=23, StateIndex=2, SubstateIndex=2 |

### Assault Rifle (ItemID = 1)
| State Name | Conditions |
|------------|------------|
| Fire | ItemID=1, StateIndex=2 |
| Reload | ItemID=1, StateIndex=3 |
| Aim | ItemID=1, StateIndex=1 |

---

## Part 6: Verify Setup

### Test in Play Mode
1. Enter Play Mode
2. Equip weapon (number keys)
3. Watch the Animator window bottom bar:
   ```
   [ANIMATOR_PARAM] STATE SUMMARY | QuickSlot=3 | SlotOItemID=23 StateIdx=0 SubstateIdx=0 | MovementSetID=1 ...
   ```
4. Press attack/fire and verify:
   - `StateIdx` changes to 2
   - `SubstateIdx` shows combo (1, 2, 3)
   - Animation plays in Animator preview

### Debug Console
Enable `DebugLogging` on WeaponEquipVisualBridge:
```
[WEAPON_INPUT] MELEE: Attack combo 1
[WEAPON_INPUT] MELEE: Attack combo 2
```

---

## Troubleshooting

### Animation Not Playing

**Check 1:** Verify the state exists in the correct layer (Upperbody Layer)

**Check 2:** Verify transition conditions match exactly:
- `Slot0ItemID` = correct weapon ID
- `Slot0ItemStateIndex` = 2 (for attack/fire)

**Check 3:** Verify the state has a Motion assigned (animation clip)

### Wrong Animation Playing

**Cause:** Another transition is matching first

**Fix:** 
1. Check transition order (higher = checked first)
2. Add more specific conditions (include ItemID AND StateIndex AND SubstateIndex)

### Animation Loops Forever

**Cause:** No exit transition

**Fix:** Add transition to Exit or Idle with "Has Exit Time" enabled

---

## File Locations

### Controllers
| Controller | Path |
|------------|------|
| ClimbingDemo.controller | `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller` |
| Demo.controller (reference only) | `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Animator/Characters/Demo.controller` |

### Animation Clips
Opsive weapon animations are in:
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Animator/Animations/`
- Look in subfolders for specific weapons (Katana, AssaultRifle, etc.)

### Character Prefabs
| Prefab | Controller Used |
|--------|-----------------|
| Warrok_Client | ClimbingDemo.controller |
| Atlas_Client | ClimbingDemo.controller |

---

## Summary Checklist

- [ ] Open ClimbingDemo.controller in Animator window
- [ ] Navigate to Upperbody Layer → Items sub-state machine
- [ ] Identify missing attack/fire states
- [ ] Create new states with correct animation clips
- [ ] Add transitions from Any State with proper conditions:
  - `Slot0ItemID` = weapon ID
  - `Slot0ItemStateIndex` = 2 (Use/Attack)
  - `Slot0ItemSubstateIndex` = combo number
- [ ] Add exit transitions with "Has Exit Time" enabled
- [ ] Test in Play Mode
- [ ] Verify animator parameters change during gameplay
- [ ] Verify animation plays for each weapon action
