# EPIC 13.30 - Weapon Positioning System

## Status: 📋 PLANNED

---

## Problem Statement

Weapons are not positioned correctly when equipped. Currently all weapons get `localPosition = Vector3.zero` when parented to the hand, but each weapon needs **per-weapon position and rotation offsets** to grip correctly.

---

## Current Implementation (DIG)

### How Weapons Are Currently Attached

**File:** [WeaponEquipVisualBridge.cs](../../Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs#L757-L759)

```csharp
// When equipping:
newWeapon.transform.SetParent(HandAttachPoint, false);
newWeapon.transform.localPosition = Vector3.zero;      // ❌ WRONG - All weapons at origin
newWeapon.transform.localRotation = Quaternion.identity; // ❌ WRONG - No rotation offset

// When holstering:
prevWeapon.transform.SetParent(BackAttachPoint, false);
prevWeapon.transform.localPosition = Vector3.zero;      // ❌ WRONG - All holstered weapons stack
prevWeapon.transform.localRotation = Quaternion.identity;
```

### Current Architecture

| Aspect | Current State | Problem |
|--------|---------------|---------|
| **HandAttachPoint** | Found by bone name search ("RightHand", "mixamorig:RightHand") | ✅ Works |
| **BackAttachPoint** | Found by bone name search ("Spine2", "Spine1") | ❌ All holstered weapons stack at same point |
| **Weapon Position** | `localPosition = Vector3.zero` for all | ❌ No per-weapon grip offsets |
| **Weapon Rotation** | `localRotation = Quaternion.identity` for all | ❌ No per-weapon rotation offsets |
| **IK Target** | Searches child for "LeftHandAttach" or "LeftHandGrip" | ⚠️ Works but relies on hardcoded names |
| **Holster Positions** | One shared BackAttachPoint | ❌ Rifle/pistol/melee should holster differently |

---

## Opsive Reference Implementation

### ThirdPersonPerspectiveItem.cs

**File:** `Assets/OPSIVE/.../ThirdPersonController/Items/ThirdPersonPerspectiveItem.cs`

Opsive stores **per-weapon offsets** on a component attached to each weapon prefab:

```csharp
public class ThirdPersonPerspectiveItem : PerspectiveItem
{
    // IK targets - stored on the component, not searched by name
    [SerializeField] protected Transform m_NonDominantHandIKTarget;
    [SerializeField] protected Transform m_NonDominantHandIKTargetHint;
    
    // Per-weapon holster position
    [SerializeField] protected IDObject<Transform> m_HolsterTarget;
}
```

### PerspectiveItem.cs (Base Class)

**File:** `Assets/OPSIVE/.../Items/PerspectiveItem.cs`

```csharp
public abstract class PerspectiveItem : StateBehavior
{
    // The actual weapon model GameObject
    [SerializeField] protected GameObject m_Object;
    
    // PER-WEAPON spawn offsets (what we're missing!)
    [SerializeField] protected Vector3 m_LocalSpawnPosition;   // ✅ Position offset
    [SerializeField] protected Vector3 m_LocalSpawnRotation;   // ✅ Rotation offset
    [SerializeField] protected Vector3 m_LocalSpawnScale = Vector3.one;  // ✅ Scale
    
    // Parent transform (can be child of slot)
    [SerializeField] protected IDObject<Transform> m_SpawnParent;
}
```

### How Opsive Applies Offsets

```csharp
// From PerspectiveItem.Initialize():
m_Object.transform.parent = parent;
m_Object.transform.localPosition = m_LocalSpawnPosition;     // Uses stored offset
m_Object.transform.localRotation = Quaternion.Euler(m_LocalSpawnRotation);
m_Object.transform.localScale = m_LocalSpawnScale;
```

### CharacterItemSlot.cs

**File:** `Assets/OPSIVE/.../Items/CharacterItemSlot.cs`

Opsive uses **multiple slots** attached to different bones, identified by ID:

```csharp
public class CharacterItemSlot : MonoBehaviour
{
    [SerializeField] protected int m_ID;  // 0 = right hand, 1 = left hand, etc.
}
```

This allows:
- Right hand weapons → Slot 0 (on RightHand bone)
- Left hand weapons → Slot 1 (on LeftHand bone)
- Two-handed → Both slots

### Opsive Holster System

When unequipping, Opsive parents to a per-weapon `HolsterTarget`:

```csharp
// From ThirdPersonPerspectiveItem.SetActive():
if (!active)  // Unequipping
{
    m_ObjectTransform.parent = HolsterTarget;  // Per-weapon holster!
    m_ObjectTransform.localPosition = Vector3.zero;
    m_ObjectTransform.localRotation = Quaternion.identity;
}
```

Each weapon defines its own holster transform (back, hip, thigh, etc.).

---

## Gap Analysis

| Feature | Opsive Has | DIG Has | Fix Required |
|---------|------------|---------|--------------|
| **Per-weapon position offset** | `m_LocalSpawnPosition` | ❌ None | Use Opsive component |
| **Per-weapon rotation offset** | `m_LocalSpawnRotation` | ❌ None | Use Opsive component |
| **Per-weapon scale** | `m_LocalSpawnScale` | ❌ None | Use Opsive component |
| **Per-weapon IK target** | Serialized `m_NonDominantHandIKTarget` | Searched by name | Use Opsive component |
| **Per-weapon holster** | `m_HolsterTarget` per weapon | One shared `BackAttachPoint` | Use Opsive component |
| **Multiple item slots** | `CharacterItemSlot` with ID | One `HandAttachPoint` | Optional (dual wield) |

---

## Key Insight: Opsive Demo Weapons Use Zero Offsets!

After investigation, Opsive's demo weapons **also** use `m_LocalSpawnPosition = (0,0,0)`.

**How they make it work:**
1. **Weapon models are authored with the grip at the pivot point (0,0,0)**
2. When parented to hand with `localPosition = 0`, it grips correctly
3. `m_LocalSpawnPosition` is only needed if the model pivot is wrong

**This means:**
- If weapon models have correct pivots → No offsets needed
- If weapon models have wrong pivots → Use offsets to compensate

---

## Proposed Solution: Use Opsive's Existing Component

### Why NOT Create a New Component

Opsive already has `ThirdPersonPerspectiveItem` with all the fields we need:
- `m_LocalSpawnPosition` - Position offset
- `m_LocalSpawnRotation` - Rotation offset  
- `m_LocalSpawnScale` - Scale
- `m_NonDominantHandIKTarget` - Left hand IK target
- `m_NonDominantHandIKTargetHint` - Elbow hint
- `m_HolsterTarget` - Per-weapon holster location

**Creating a duplicate `WeaponAttachmentConfig` would be redundant.**

### Recommended Approach

| Option | Effort | Pros | Cons |
|--------|--------|------|------|
| **A. Use ThirdPersonPerspectiveItem** | Low | Already exists, has inspector, full-featured | Adds Opsive dependency |
| ~~B. Create WeaponAttachmentConfig~~ | Medium | Custom, no dependency | Duplicates existing work |
| **C. Fix weapon model pivots** | High | No runtime code needed | Requires re-exporting models |

### ✅ RECOMMENDED: Option A - Use ThirdPersonPerspectiveItem

1. Add `ThirdPersonPerspectiveItem` component to each weapon prefab
2. Update `WeaponEquipVisualBridge` to read offsets from this component
3. Use Opsive's built-in inspector to configure values
4. Optionally add an "Items" child object to hand bones (like Opsive does)

---

## Implementation Plan

### Task 13.30.1 - Add ThirdPersonPerspectiveItem to Weapon Prefabs
- [ ] For each weapon in `Assets/Prefabs/Items/Converted/`:
  - Add `ThirdPersonPerspectiveItem` component
  - Set `m_Object` to the weapon's visual mesh
  - Configure `m_NonDominantHandIKTarget` (assign LeftHandAttach child)
  - Leave position/rotation at zero unless needed

**Weapons to update:**
- [ ] AssaultRifleWeapon_ECS.prefab
- [ ] KatanaWeapon_ECS.prefab
- [ ] KnifeWeapon_ECS.prefab
- [ ] ShotgunWeapon_ECS.prefab
- [ ] SniperRifleWeapon_ECS.prefab
- [ ] PistolWeaponBase_ECS.prefab
- [ ] RocketLauncherWeapon_ECS.prefab
- [ ] BowWeapon_ECS.prefab

### Task 13.30.2 - Add "Items" GameObject to Character Hands
- [ ] On player prefab, add child "Items" to RightHand bone
- [ ] Update WeaponEquipVisualBridge.HandAttachPoint to reference "Items"
- [ ] This matches Opsive's architecture exactly

### Task 13.30.3 - Update WeaponEquipVisualBridge
- [ ] Add using statement for Opsive namespace
- [ ] In `UpdateWeaponVisuals()`, read from ThirdPersonPerspectiveItem:
  ```csharp
  using Opsive.UltimateCharacterController.ThirdPersonController.Items;
  
  // In UpdateWeaponVisuals():
  var perspectiveItem = newWeapon.GetComponent<ThirdPersonPerspectiveItem>();
  if (perspectiveItem != null)
  {
      newWeapon.transform.localPosition = perspectiveItem.LocalSpawnPosition;
      newWeapon.transform.localRotation = Quaternion.Euler(perspectiveItem.LocalSpawnRotation);
      newWeapon.transform.localScale = perspectiveItem.LocalSpawnScale;
      
      // Use direct IK reference instead of name search
      if (HandIK != null && perspectiveItem.NonDominantHandIKTarget != null)
      {
          HandIK.LeftHandIKTarget = perspectiveItem.NonDominantHandIKTarget;
      }
  }
  else
  {
      // Fallback to current behavior
      newWeapon.transform.localPosition = Vector3.zero;
      newWeapon.transform.localRotation = Quaternion.identity;
  }
  ```

### Task 13.30.4 - Add Holster Support (Optional)
- [ ] Create holster transforms on character (BackHolster, HipHolster)
- [ ] Read `perspectiveItem.HolsterTarget` for per-weapon holstering
- [ ] Parent unequipped weapons to their designated holster

### Task 13.30.5 - Test and Adjust Offsets
- [ ] Test each weapon in Play mode
- [ ] If grip is wrong, adjust `m_LocalSpawnPosition` and `m_LocalSpawnRotation`
- [ ] Verify IK targets work correctly

---

## Editor Workflow

Since we're using Opsive's component, the inspector already exists:

1. **Select weapon prefab** in Project window
2. **Find ThirdPersonPerspectiveItem** in Inspector
3. **Configure fields:**
   - `Object` → The weapon mesh GameObject
   - `Local Spawn Position` → Position offset (usually 0,0,0)
   - `Local Spawn Rotation` → Rotation offset (usually 0,0,0)
   - `Non Dominant Hand IK Target` → Drag LeftHandAttach transform
   - `Holster Target` → Drag holster transform (optional)
4. **Apply prefab changes**

**No custom editor tool needed** - Opsive's inspector handles it!

---

## Weapon Offset Reference (To Be Filled During Configuration)

| Weapon | Position Offset | Rotation Offset | Holster Slot | Notes |
|--------|-----------------|-----------------|--------------|-------|
| Assault Rifle | TBD | TBD | 0 (Back) | Two-handed, grip lower |
| Katana | TBD | TBD | 2 (Back) | Angled on back |
| Knife | TBD | TBD | 1 (Hip) | Small, hip holster |
| Shotgun | TBD | TBD | 0 (Back) | Similar to rifle |
| Sniper | TBD | TBD | 0 (Back) | Longer than rifle |
| Pistol | TBD | TBD | 1 (Hip) | Hip holster |
| Rocket Launcher | TBD | TBD | 0 (Back) | Large, on back |
| Grenade | TBD | TBD | 1 (Hip) | Small, hip/vest |

---

## Files to Modify

| File | Changes |
|------|---------|
| `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs` | Read from ThirdPersonPerspectiveItem, apply offsets |
| `Assets/Scripts/Items/Bridges/DigOpsiveIK.cs` | Use NonDominantHandIKTarget from component |
| Each weapon prefab in `Assets/Prefabs/Items/Converted/` | Add ThirdPersonPerspectiveItem component |
| Player prefab | Add "Items" child to RightHand bone |

---

## Testing Checklist

### Positioning
- [ ] Rifle grips correctly (hands align with grip/foregrip)
- [ ] Pistol grips correctly (one-handed)
- [ ] Melee weapon grips correctly
- [ ] Each weapon has unique position (not stacked)

### Holstering
- [ ] Rifles go to back holster
- [ ] Pistols go to hip holster
- [ ] Melee goes to appropriate holster
- [ ] Holstered weapons don't clip through body

### IK
- [ ] Left hand reaches correct position on two-handed weapons
- [ ] IK works when weapon has ThirdPersonPerspectiveItem
- [ ] IK fallback works when no component (name search)

---

## Dependencies

- WeaponEquipVisualBridge (already exists)
- DigOpsiveIK (already exists)
- Weapon prefabs (already exist in WeaponModels array)
- **Opsive.UltimateCharacterController.ThirdPersonController.Items** namespace

---

## Designer/Developer Workflow: Adding a New Weapon

### Step 1: Create Weapon Prefab

1. **Model Setup**
   - Import weapon model (FBX/OBJ)
   - Create prefab in `Assets/Prefabs/Items/Converted/`
   - Add child transforms:
     - `LeftHandAttach` or `LeftHandGrip` (for off-hand IK on two-handed weapons)
     - `Muzzle` or `FirePoint` (for projectile/VFX spawn point)

2. **Add ThirdPersonPerspectiveItem** (Opsive component)
   - Add `ThirdPersonPerspectiveItem` component to weapon root
   - Configure `Object` → Point to the weapon mesh child
   - Configure `NonDominantHandIKTarget` → Point to LeftHandAttach transform
   - Set `LocalSpawnPosition/Rotation` only if grip is wrong (usually 0,0,0)
   - Optionally set `HolsterTarget` for per-weapon holster location

### Step 2: Create ECS Weapon Entity

1. **Create Weapon Authoring Prefab**
   - Create `[WeaponName]Weapon_ECS.prefab`
   - Add `WeaponAuthoring` component
   - Configure:
     - `AnimatorItemID` (must match Opsive animator - see ClimbingDemo.controller)
     - `ActionType` (Shootable, Melee, Throwable)
     - Damage, fire rate, ammo capacity

2. **Create Related Prefabs** (if needed)
   - `[WeaponName]Projectile_ECS.prefab` (for guns)
   - `[WeaponName]MuzzleFlash_ECS.prefab`
   - `[WeaponName]Drop_ECS.prefab` (dropped version)
   - `[WeaponName]Pickup_ECS.prefab` (world pickup)

### Step 3: Register in WeaponEquipVisualBridge

1. **Assign QuickSlot**
   - Open player prefab
   - Find `WeaponEquipVisualBridge` component
   - Add weapon visual prefab to `WeaponModels[]` at desired slot index

2. **Configure Mappings**
   - `SlotItemIDs[slot]` = Opsive AnimatorItemID
   - `SlotMovementSetIDs[slot]` = 0 (Gun), 1 (Melee), or 2 (Bow)

### Step 4: Add to Starting Inventory (Optional)

- Open `PlayerInventoryAuthoring` on player prefab
- Add weapon to `StartingItems` list with QuickSlot assignment

### Step 5: Animator Setup (If New Weapon Type)

If the weapon doesn't share animations with existing weapons:
1. Open `ClimbingDemo.controller`
2. Add states for new weapon under `Items > [WeaponName]`
3. Use `AnimatorControllerAnalyzer` (DIG → Animation) to compare with Demo.controller
4. Use `AnimatorStateCopier` to copy missing states

---

## Quick Reference Table

| Step | File/Location | What to Configure |
|------|---------------|-------------------|
| 1 | Weapon Prefab | Model, LeftHandAttach, Muzzle transforms |
| 2 | ThirdPersonPerspectiveItem | Object, LocalSpawnPosition/Rotation, NonDominantHandIKTarget |
| 3 | WeaponAuthoring (ECS) | AnimatorItemID, ActionType, damage, ammo |
| 4 | WeaponEquipVisualBridge | WeaponModels[slot], SlotItemIDs, SlotMovementSetIDs |
| 5 | PlayerInventoryAuthoring | StartingItems (if default weapon) |
| 6 | ClimbingDemo.controller | Animator states (only if new weapon type) |

---

## Example: Adding a "Submachine Gun"

```
1. Model: Import SMG.fbx → Create SMGWeapon.prefab
   - Add child "LeftHandAttach" at foregrip
   - Add child "Muzzle" at barrel end

2. Add ThirdPersonPerspectiveItem component:
   - Object: → SMG mesh child
   - LocalSpawnPosition: (0, 0, 0)  ← Usually zero if model pivot is correct
   - LocalSpawnRotation: (0, 0, 0)
   - NonDominantHandIKTarget: → LeftHandAttach transform
   - HolsterTarget: → BackHolster (optional)

3. Create SMGWeapon_ECS.prefab with WeaponAuthoring:
   - AnimatorItemID: 22 (reuse Rifle animations)
   - ActionType: Shootable
   - Damage: 15, FireRate: 0.08, MagSize: 30

4. On Player's WeaponEquipVisualBridge:
   - WeaponModels[9] = SMGWeapon.prefab
   - SlotItemIDs[9] = 22
   - SlotMovementSetIDs[9] = 0 (Gun)

5. Test: Press 9 in game → SMG equips with proper grip
```

---

## Notes

- **Why zero offsets usually work:** If the weapon model's pivot is at the grip point, `localPosition = (0,0,0)` puts the grip exactly at the hand bone.
- **When offsets are needed:** If the model pivot is at the center of the mesh (common for downloaded assets), you'll need to adjust `LocalSpawnPosition` to move the grip to the hand.
- Opsive uses `m_StartLocalPosition` / `m_StartLocalRotation` to cache the original offset, then restores it when re-equipping. We should do the same.
- For multiplayer, the visual positioning is client-only (MonoBehaviour). ECS only needs to know which weapon is equipped, not the visual offsets.
