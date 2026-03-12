# EPIC 13.29 - Melee Weapon ECS/DOTS/NfE Migration

## Status: 🟢 KNIFE WORKING - Katana/Sword Ready

---

## Overview

This document outlines the migration plan for melee weapons (Knife, Katana, Sword) from Opsive's MonoBehaviour-based system to our ECS/DOTS/NfE architecture. The goal is to maintain animation compatibility with the existing ClimbingDemo.controller while implementing server-authoritative melee combat logic in ECS.

---

## 🚨 CRITICAL LESSONS LEARNED (Read First!)

### Key Gotcha #1: AnyState Transitions Don't Work Reliably

**Problem**: Adding AnyState transitions to melee sub-state machines (like we did for Bow) doesn't work for melee weapons.

**Reason**: The transition conditions fire, but the state immediately exits. The melee states have internal exit conditions that aren't met when entering via AnyState.

**Solution**: Use `Animator.Play()` to FORCE the state directly. This bypasses transition logic entirely.

### Key Gotcha #2: State Name Ambiguity

**Problem**: State names like `Attack 1 Light From Idle` exist in MULTIPLE sub-state machines (Knife, Katana, Sword). Using just the state name causes Unity to play the WRONG weapon's animation!

**Symptom**: Calling `Play("Attack 1 Light From Idle")` while holding knife plays the SWORD animation (first alphabetically).

**Solution**: Always use full path: `Play("Knife.Attack 1 Light From Idle")`

### Key Gotcha #3: HasState() Validation Required

**Problem**: Unity's `Animator.StringToHash()` generates hashes for ANY string - even invalid ones. Using an invalid hash with `Play()` fails silently.

**Solution**: Always verify state exists before playing:
```csharp
int hash = Animator.StringToHash("Knife.Attack 1 Light From Idle");
if (PlayerAnimator.HasState(upperbodyLayerIndex, hash))
{
    PlayerAnimator.Play(hash, upperbodyLayerIndex, 0f);
}
```

### Key Gotcha #4: Layer Weight Must Be 1

**Problem**: Upperbody Layer animations aren't visible if layer weight is 0.

**Solution**: Ensure layer weight is set:
```csharp
PlayerAnimator.SetLayerWeight(upperbodyLayerIndex, 1f);
```

### Key Gotcha #5: CurrentStateHash Returns Short Name

**Problem**: `GetCurrentAnimatorStateInfo().shortNameHash` returns the hash of JUST the state name, not the full path. This makes debugging confusing.

**Example**:
- You call `Play("Knife.Attack 1")` with hash `-952135142`
- But `currentState.shortNameHash` shows `464624387` (hash of just "Attack 1")

---

## Working Implementation Pattern

Based on knife implementation, here's the working pattern for all melee weapons:

```csharp
private void HandleMeleeInput(bool leftPressed, ...)
{
    if (leftPressed)
    {
        // 1. Update combo index
        _meleeComboIndex = (_meleeComboIndex % weaponComboCount) + 1; // 1→2→1 for knife
        _meleeComboTimer = COMBO_WINDOW;
        
        // 2. Set animator parameters (for debugging/other systems)
        SetAnimatorState(stateIndex: 2, substateIndex: _meleeComboIndex, triggerChange: true);
        
        // 3. FORCE PLAY the state directly - bypasses unreliable AnyState transitions
        int upperbodyLayer = GetUpperbodyLayerIndex();
        string stateName = $"{weaponPrefix}.Attack {_meleeComboIndex} Light From Idle";
        
        // 4. Try multiple path formats (Unity is picky)
        string[] pathFormats = new string[]
        {
            $"{weaponPrefix}.{baseStateName}",  // "Knife.Attack 1 Light From Idle"
            $"{weaponPrefix} {baseStateName}",   // "Knife Attack 1 Light From Idle"
            baseStateName,                        // "Attack 1 Light From Idle"
        };
        
        foreach (string path in pathFormats)
        {
            int hash = Animator.StringToHash(path);
            if (PlayerAnimator.HasState(upperbodyLayer, hash))
            {
                PlayerAnimator.Play(hash, upperbodyLayer, 0f);
                break;
            }
        }
    }
}
```

---

## Weapon Specifications

### Knife (ItemID: 23) ✅ WORKING

| Property | Value |
|----------|-------|
| Prefab | `KnifeWeapon_ECS.prefab` |
| ItemID | 23 |
| MovementSetID | 1 (Melee) |
| Combo Count | 2 (cycles 1→2→1) |
| Animation Prefix | `Knife` |

**Knife Animation States:**
| State | Full Path |
|-------|-----------|
| Attack 1 | `Knife.Attack 1 Light From Idle` |
| Attack 2 | `Knife.Attack 2 Light From Idle` |
| Idle | `Knife.Idle` |
| Equip | `Knife.Equip From Idle` |

---

### Katana (ItemID: 24) ⏳ READY

| Property | Value |
|----------|-------|
| Prefab | `KatanaWeapon_ECS.prefab` |
| ItemID | 24 |
| MovementSetID | 1 (Melee) |
| Combo Count | 3 (cycles 1→2→3→1) |
| Animation Prefix | `Katana` |

**Expected Animation States:**
| State | Full Path |
|-------|-----------|
| Attack 1 | `Katana.Attack 1 Light From Idle` |
| Attack 2 | `Katana.Attack 2 Light From Idle` |
| Attack 3 | `Katana.Attack 3 Light From Idle` |

---

### Sword (ItemID: TBD) ⏳ READY

| Property | Value |
|----------|-------|
| Prefab | `SwordWeapon_ECS.prefab` (needs creation) |
| ItemID | TBD (verify in animator) |
| MovementSetID | 1 (Melee) |
| Combo Count | 2 |
| Animation Prefix | `Sword` |

**Note**: Sword states likely use same naming pattern as Knife/Katana.

---

## Implementation Approach

### For Katana/Sword: No Editor Tools Needed!

The knife implementation proved that **editor tools for adding transitions are unnecessary**. The force-play approach works without any animator modifications.

### Steps to Enable Katana:

1. **Update HandleMeleeInput** to detect Katana (ItemID=24):
   ```csharp
   // Determine weapon prefix based on ItemID
   string weaponPrefix = currentItemID switch
   {
       23 => "Knife",
       24 => "Katana",
       25 => "Sword",
       _ => "Knife"
   };
   
   // Adjust combo count per weapon
   int comboCount = currentItemID switch
   {
       23 => 2,  // Knife: 2-hit combo
       24 => 3,  // Katana: 3-hit combo  
       25 => 2,  // Sword: 2-hit combo
       _ => 2
   };
   ```

2. **Test** - Equip katana and verify 3-hit combo works

### Steps to Enable Sword:

1. Create `SwordWeapon_ECS.prefab` with correct ItemID
2. Add Sword to the prefix/comboCount switch
3. Test

---

## Updated Status Tracking

| Task | Status |
|------|--------|
| Knife Animation | ✅ Working (force-play approach) |
| Knife Transition Adder | ⚠️ Created but NOT needed |
| Katana Animation | ⏳ Ready to implement (same pattern) |
| Sword Animation | ⏳ Ready to implement (same pattern) |
| MeleeSystem.cs (ECS combat logic) | ⏳ Deferred |
| Block/Guard System | ⏳ Deferred |

---

## Key Files

| File | Purpose |
|------|---------|
| [WeaponEquipVisualBridge.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs) | Melee input handling and force-play implementation |
| [KnifeTransitionAdder.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Editor/KnifeTransitionAdder.cs) | Editor tool (created but not needed due to force-play) |
| [KnifeWeapon_ECS.prefab](file:///Users/dollerinho/Desktop/DIG/Assets/Prefabs/Items/Converted/KnifeWeapon_ECS.prefab) | Working knife prefab |

---

## References

- [EPIC13.28](file:///Users/dollerinho/Desktop/DIG/Docs/EPIC13/EPIC13.28.md) - Weapon Animation Input System
- [BowTransitionAdder.cs](file:///Users/dollerinho/Desktop/DIG/Assets/Editor/BowTransitionAdder.cs) - Similar (but different) approach for bow
