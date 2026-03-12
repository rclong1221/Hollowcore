# EPIC 13.31 - Weapon Animation Fix Plan

## Status: � IN PROGRESS

---

## Problem Summary

| Weapon | Status | Symptom |
|--------|--------|---------|
| Assault Rifle | ✅ Works | Fire animation plays |
| Pistol | ✅ Works | Fire animation plays |
| Rocket Launcher | ✅ Works | Fire animation plays |
| Sniper Rifle | ✅ Works | Fire animation plays |
| Shotgun | ✅ Works | Fire animation plays |
| Katana | ❌ Broken | Triggers DROP animation instead of attack |
| Knife | ❌ Broken | Right-click shows aim, left-click does nothing |
| Sword | ❌ Broken | Right-click shows aim, left-click does nothing |
| Bow | ✅ FIXED | Code updated to match animator conditions |

**Pattern:** All guns work. Melee weapons don't work.

---

## Bow Fix (COMPLETED)

### ClimbingDemo.controller Bow Layer Transition Conditions

From animator inspection:

| State | Conditions |
|-------|-----------|
| **Idle** | ItemID=4, StateIndex < 2, SubstateIndex=0 |
| **Aim** | ItemID=4, SubstateIndex ≠ 11, Aiming=true |
| **Attack Pull Back** | ItemID=4, StateIndex=0, SubstateIndex ≠ 11 |
| **Attack Release** | SubstateIndex=12 |
| **Dry Fire** | ItemID=4, StateIndex=1, SubstateIndex=11 |

### Code Fix Applied

**File:** `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs`

- Right-click: Sets `Aiming=true` → Aim state
- Left-click: Sets `StateIndex=0, SubstateIndex=1` → Attack Pull Back (draw)
- Left-release: Sets `SubstateIndex=12` → Attack Release (fire arrow)

---

## Root Cause Analysis

### Issue 1: ClimbingDemo.controller Missing Melee Attack States

**Evidence:** From `Docs/WEAPON_ANIMATION_FIX.md` line 18-43:

```
### Current Katana States (ItemID=24)
Only has:
- Unequip From Idle
- Equip From Idle  
- Drop           ← This is why Katana triggers DROP
- Equip From Aim
- Unequip From Aim
- Idle
- Aim
- Ride Aim

### Missing Katana States (Exist in Demo.controller)
- ❌ Attack 1 From Idle
- ❌ Attack 2 From Idle
- ❌ Attack 1 From Aim
...etc
```

**Root Cause:** When melee attack is triggered with `Slot0ItemStateIndex=7`, no state exists in ClimbingDemo.controller with that condition. The animator falls through to DROP state (index 6), which DOES exist.

### Issue 2: Melee Input Handler Using Wrong State Index

**Evidence:** From `WeaponEquipVisualBridge.cs` lines 1803-1826:

```csharp
private void HandleMeleeInput(bool leftPressed, ...) 
{
    // Right click - Block
    if (rightPressed)
    {
        SetAnimatorState(stateIndex: 8, substateIndex: 0, ...);  // ← State 8 = Block
    }
    
    // Left click - Attack combo
    if (leftPressed)
    {
        SetAnimatorState(stateIndex: 2, substateIndex: _meleeComboIndex, ...);  // ← State 2 = Fire!
    }
}
```

**Problems:**
1. State 8 for block doesn't exist in ClimbingDemo.controller
2. State 2 is Use/Fire for guns - wrong for melee
3. Should use State 7 for melee attack (per Opsive standard)

### Issue 3: ECS MeleeState Not Propagating to Animation Bridge

**Evidence:** The bridge checks for `MeleeState.IsAttacking` but this requires the ECS `MeleeActionSystem` to set it. Let's trace the flow:

1. `HandleMeleeInput()` sets animator params directly (bypasses ECS)
2. `MeleeActionSystem` waits for `UseRequest.StartUse` from ECS
3. But `HandleMeleeInput()` never writes to ECS - it just drives animator

**Result:** Two competing input paths:
- **Path A (Guns):** Input → ECS → `WeaponFireState.IsFiring` → Bridge detects → Animator
- **Path B (Melee):** Input → `HandleMeleeInput()` → Direct animator (BYPASSES ECS)

Path B doesn't set the correct state index (uses 2 instead of 7).

### Issue 4: Bow Configuration Wrong

**Evidence:** From prefab inspection:

```yaml
# BowWeapon_ECS.prefab
Type: 1              # ← WeaponType.Shootable (should be Bow-specific or at least treated differently)
AnimatorItemID: 4    # ← Correct
```

The bow is configured as `Shootable`, so it gets `WeaponFireComponent` but the `HandleBowInput()` is trying to use bow-specific states that may not exist.

### Issue 5: MovementSetID Correctly Set But Animation States Missing

**Evidence:** From `WeaponEquipVisualBridge.cs`:
- Melee weapons correctly get `MovementSetID = 1`
- Bow correctly gets `MovementSetID = 2`

But even with correct MovementSetID, the underlying animation STATES don't exist in ClimbingDemo.controller.

---

## Why Guns Work

1. **Complete animation states exist** - ClimbingDemo.controller has full Fire/Reload states for all guns
2. **Correct ECS flow** - `WeaponFireSystem` sets `WeaponFireState.IsFiring`, bridge detects it, sets `Slot0ItemStateIndex=2`
3. **`HandleGunInput()` ALSO works** - Sets StateIndex=2 directly (redundant but works)
4. **Correct SubstateIndex=2** - ClimbingDemo Fire transitions require SubstateIndex=2

---

## Fix Strategy

### CRITICAL: Don't Break Guns!

The gun animation flow works. We must NOT:
- Change how `Slot0ItemStateIndex=2` triggers Fire animations
- Modify `HandleGunInput()` behavior
- Alter the ECS→Bridge→Animator flow for shootable weapons

### Fix Approach: Isolated Melee/Bow Fixes

#### Fix A: Add Missing Animation States to ClimbingDemo.controller (UNITY EDITOR REQUIRED)

This is the **primary fix**. Use the existing `AnimatorStateCopier` tool:

1. Open Unity Editor
2. Go to **DIG > Animation > Animator State Copier**
3. Load Demo.controller as source
4. Load ClimbingDemo.controller as target
5. For each weapon layer (Katana, Knife, Sword, Bow):
   - Select missing Attack states
   - Copy to ClimbingDemo.controller
6. Save the animator controller

**States to copy for MELEE weapons (Katana/Knife/Sword):**
- Attack 1 From Idle (transition: Slot0ItemStateIndex == 2, Slot0ItemSubstateIndex == 1, Aiming == false)
- Attack 2 From Idle (transition: Slot0ItemStateIndex == 2, Slot0ItemSubstateIndex == 2, Aiming == false)
- Attack 1 From Aim (transition: Slot0ItemStateIndex == 2, Slot0ItemSubstateIndex == 1, Aiming == true)
- Attack Recoil states

**States to copy for BOW:**
- Draw (transition: Aiming == true)
- Fire (transition: Slot0ItemStateIndex == 2)
- Release animation

#### Fix B: Correct HandleMeleeInput() State Indices (CODE CHANGE)

**File:** `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs`

**Current (broken):**
```csharp
private void HandleMeleeInput(...)
{
    // Right click - Block
    if (rightPressed)
    {
        SetAnimatorState(stateIndex: 8, ...);  // ❌ Wrong
    }
    
    // Left click - Attack
    if (leftPressed)
    {
        SetAnimatorState(stateIndex: 2, substateIndex: _meleeComboIndex, ...);  // ❌ Wrong
    }
}
```

**Fixed:**
```csharp
private void HandleMeleeInput(...)
{
    // Right click - Aim/Guard stance (same as guns, enables "From Aim" attack variants)
    if (rightPressed)
    {
        _isAiming = true;
        PlayerAnimator.SetBool(_hashAiming, true);
        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
    }
    else if (rightReleased)
    {
        _isAiming = false;
        PlayerAnimator.SetBool(_hashAiming, false);
        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
    }
    
    // Left click - Attack (uses State 2 = Use with melee substate)
    // Opsive melee attacks use the same "Use" state as guns (StateIndex=2)
    // but with different SubstateIndex values (1, 2, 3 for combo hits)
    if (leftPressed)
    {
        // Advance combo if within window
        if (_meleeComboTimer > 0)
        {
            _meleeComboIndex = (_meleeComboIndex % 3) + 1;
        }
        else
        {
            _meleeComboIndex = 1;
        }
        
        _meleeComboTimer = COMBO_WINDOW;
        
        // Use StateIndex=2 (Use/Attack) with combo substates
        // This matches Opsive's approach where melee and ranged both use "Use" state
        SetAnimatorState(stateIndex: 2, substateIndex: _meleeComboIndex, triggerChange: true);
        if (DebugLogging) Debug.Log($"[WEAPON_INPUT] MELEE: Attack combo {_meleeComboIndex} (State=2, Substate={_meleeComboIndex})");
    }
}
```

**Rationale:** Opsive's Demo.controller uses `Slot0ItemStateIndex == 2` for BOTH gun fire AND melee attack. The difference is:
- Guns: SubstateIndex varies (1 for single shot, etc.)
- Melee: SubstateIndex = combo number (1, 2, 3)
- The animator uses ItemID to route to correct weapon layer

**WAIT - This is what it already does!** Let me re-check...

Actually, the current code DOES use `stateIndex: 2`. The issue is that:
1. ClimbingDemo.controller's melee weapon layers don't have states that respond to StateIndex=2
2. Only the "Drop" state exists, and something causes it to trigger

Let me trace further...

#### Fix C: Investigate Why DROP Triggers for Katana

The Katana triggers DROP animation. Looking at what conditions trigger DROP:
- `Slot0ItemStateIndex == 6` → Drop state

But we're setting `stateIndex: 2`. How does it become 6?

Possibilities:
1. **AnyState transition** in ClimbingDemo.controller routes to Drop when conditions not met
2. **Fallback behavior** when no matching state exists
3. **State machine default** when entering layer

**Investigation needed in Unity:**
1. Open ClimbingDemo.controller
2. Go to Katana layer
3. Check for AnyState → Drop transition
4. Check transition conditions

Most likely, there's an AnyState→Drop transition with weak/default conditions that catches all unhandled cases.

#### Fix D: Ensure Katana Layer Weight = 1

The bridge sets layer weights, but let's verify Katana layer exists and gets weight=1:

**Evidence from code:**
```csharp
private static readonly Dictionary<int, string> ItemIDToLayerName = new Dictionary<int, string>
{
    { 24, "Katana" },  // ← This should work
    { 23, "Knife" },
    ...
};
```

This looks correct. But does ClimbingDemo.controller have layers with these exact names?

---

## Implementation Plan

### Phase 1: Unity Editor Work (REQUIRED FIRST)

1. **Open AnimatorStateCopier tool**
   - DIG > Animation > Animator State Copier
   
2. **Copy Katana attack states from Demo.controller to ClimbingDemo.controller**
   - Attack 1 From Idle
   - Attack 2 From Idle
   - Attack 1 From Aim
   - Attack 2 From Aim
   - Recoil states

3. **Copy Knife attack states**
   - Same pattern as Katana

4. **Copy Sword attack states**
   - Note: Sword might not exist in Demo.controller - may need to create manually or copy from Katana

5. **Copy Bow states**
   - Draw
   - Fire/Release
   - Aim states

6. **Verify transition conditions**
   - Each attack state needs: `Slot0ItemStateIndex == 2`, `Slot0ItemSubstateIndex == N`
   - Entry transitions from Idle/Aim based on `Aiming` bool

7. **Test in Editor**
   - Play mode
   - Equip each weapon
   - Left-click should trigger attack animation

### Phase 2: Code Fixes (IF NEEDED AFTER PHASE 1)

If animations still don't work after Phase 1, then:

1. **Add debug logging** to trace exact animator state
2. **Check ItemID mapping** - ensure prefab AnimatorItemID matches controller
3. **Verify layer names** match between code and controller

### Phase 3: Bow-Specific Handling

The Bow is configured as Shootable (Type=1) which is technically correct for the firing mechanism. But the animator needs Bow-specific states:

1. **Right-click = Draw** (Aim + charging)
2. **Left-click while drawing = Release** (Fire)
3. States should use: StateIndex=2 (Use), with Bow ItemID

---

## Prefab Verification

Current prefab settings (VERIFIED):

| Weapon | Type | AnimatorItemID | Correct? |
|--------|------|----------------|----------|
| Assault Rifle | 1 (Shootable) | 1 | ✅ |
| Katana | 2 (Melee) | 24 | ✅ |
| Knife | 2 (Melee) | 23 | ✅ |
| Sword | 2 (Melee) | 22 | ✅ |
| Bow | 1 (Shootable) | 4 | ⚠️ Type=1 is fine, but needs animator states |

---

## Animator Controller State Reference

### Opsive Standard State Indices

| Index | State | Gun Behavior | Melee Behavior |
|-------|-------|--------------|----------------|
| 0 | Idle | Default | Default |
| 1 | Aim | ADS | Guard stance |
| 2 | Use | Fire | Attack |
| 3 | Reload | Reload mag | N/A |
| 4 | Equip | Equip | Equip |
| 5 | Unequip | Unequip | Unequip |
| 6 | Drop | Drop weapon | Drop weapon |
| 7 | Melee | N/A | (Legacy) |

**Key insight:** Both guns and melee use `StateIndex=2` for their primary action (Fire/Attack).

---

## Files to Modify

### Unity Assets (Editor Work)
| File | Change |
|------|--------|
| `Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller` | Add missing melee/bow attack states |

### Code Files (Only if needed after animator fix)
| File | Change |
|------|--------|
| `Scripts/Items/Bridges/WeaponEquipVisualBridge.cs` | Debug logging, potential HandleMeleeInput fix |

---

## Testing Checklist

After implementing fixes, verify:

- [ ] Assault Rifle still fires (don't regress!)
- [ ] Pistol still fires
- [ ] Sniper still fires
- [ ] Shotgun still fires
- [ ] Rocket Launcher still fires
- [ ] Katana plays attack animation on left-click
- [ ] Katana right-click enters aim/guard stance
- [ ] Knife plays attack animation on left-click
- [ ] Sword plays attack animation on left-click
- [ ] Bow right-click starts draw animation
- [ ] Bow left-click (while drawn) releases arrow

---

## Quick Reference: AnimatorStateCopier Tool

Location: `DIG > Animation > Animator State Copier`

Steps:
1. Click "Load Demo.controller" 
2. Click "Load ClimbingDemo.controller"
3. Click "Find Missing States"
4. Check boxes for states to copy
5. Click "Copy Selected States"
6. Save the animator controller (Ctrl+S)

---

## Summary

**The fix is primarily an ANIMATOR CONTROLLER issue, not a code issue.**

ClimbingDemo.controller is missing the melee attack states that exist in Demo.controller. The code is correctly sending `Slot0ItemStateIndex=2` for attacks, but the animator has no states configured to respond to those conditions for melee weapons.

**Action:** Use the AnimatorStateCopier tool to copy missing states from Demo.controller to ClimbingDemo.controller.

---

## Addendum: Bow ECS Implementation (COMPLETED)

### Problem Discovered
The original bow fix (HandleBowInput) was being overridden by the ECS WeaponFireSystem because:
1. Bow prefab had `Type=1` (Shootable)
2. This caused gun components (`WeaponFireComponent`, `WeaponFireState`) to be baked
3. `WeaponFireSystem` fired the bow on left-click, setting `IsFiring=true`
4. `UpdateWeaponState()` read `IsFiring` and set `StateIndex=2`, overriding HandleBowInput

### Solution Implemented

Created proper Bow ECS infrastructure:

#### Files Modified:
| File | Change |
|------|--------|
| `Weapons/Authoring/WeaponAuthoring.cs` | Added `WeaponType.Bow`, Bow settings fields, Bow baking case |
| `Weapons/Components/WeaponActionComponents.cs` | Added `UsableActionType.Bow=6`, `BowAction`, `BowState` structs |
| `Weapons/Systems/BowActionSystem.cs` | NEW - Handles bow draw/charge/release ECS logic |
| `Items/Bridges/WeaponEquipVisualBridge.cs` | Skip WeaponFireState for bows, use ActionType.Bow for MovementSetID |

#### New Components:
```csharp
public struct BowAction : IComponentData
{
    public float DrawTime, BaseDamage, MaxDamage, ProjectileSpeed;
    public int ProjectilePrefabIndex;
}

public struct BowState : IComponentData
{
    public bool IsDrawing, IsFullyDrawn, IsAiming, JustReleased;
    public float CurrentDrawTime, DrawProgress, TimeSinceRelease;
}
```

### USER ACTION REQUIRED

**Update Bow Prefab:**
1. Open `Assets/Prefabs/Items/Converted/BowWeapon_ECS.prefab`
2. Change **Type** from `Shootable` to `Bow`
3. Configure Bow Settings (DrawTime, BaseDamage, MaxDamage, etc.)
4. Save the prefab

This will make the Baker add `BowAction`/`BowState` instead of gun components.
