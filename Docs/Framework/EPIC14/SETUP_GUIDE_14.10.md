# EPIC 14.10 - Opsive Weapon Positioning Setup Guide

## Overview

The Weapon Positioning system implements Opsive's "Category + Specific Offset" algorithm with a key improvement: **character prefabs define weapon offsets, not weapon prefabs**. This allows the same weapons to work across different character models without modification.

---

## Key Concept

**Weapons specify their category. Characters define how to hold each category.**

```
Weapon Prefab:
  - WeaponAttachmentConfig.WieldTargetID = 50001001 (Assault Rifle)

Character Prefab (on AssaultRifleParent transform):
  - ObjectIdentifier.ID = 50001001
  - WeaponParentConfig with position, rotation, IK targets
```

This is **client-side only** - weapon visual parenting is purely presentation logic.

---

## Files Reference

| File | Location | Purpose |
|------|----------|---------|
| WeaponParentConfig | `Assets/Scripts/Items/Bridges/` | Component on character weapon parents |
| WeaponAttachmentConfig | `Assets/Scripts/Items/Bridges/` | Component on weapon prefabs (category ID only) |
| WeaponCategoryAuthoring | `Assets/Scripts/Items/Authoring/` | ECS baker for weapon category |
| CharacterWeaponParentsAuthoring | `Assets/Scripts/Player/Authoring/` | ECS baker for character (optional) |
| ItemVisualComponents | `Assets/Scripts/Items/Components/` | ECS components |

---

## Quick Setup

### 1. Character Prefab Setup

For each character model (`Atlas_Client.prefab`, etc.):

#### Step 1: Verify Weapon Parent Hierarchy

Navigate to the Items container under each hand:
```
ORG-hand.R > Items > AssaultRifleParent, PistolParent, etc.
ORG-hand.L > Items > ShieldParent, BowParent, etc.
```

Each weapon parent should have an `ObjectIdentifier` component with the correct ID.

#### Step 2: Add WeaponParentConfig to Each Parent

1. Select a weapon parent (e.g., `AssaultRifleParent`)
2. Add Component → `WeaponParentConfig`
3. Configure the fields:

| Field | Description |
|-------|-------------|
| **Weapon Local Position** | Position offset for weapons in this category |
| **Weapon Local Rotation** | Rotation offset (euler angles) |
| **Weapon Local Scale** | Scale (usually 1,1,1) |
| **Left Hand IK Target** | Transform for left hand IK (create as child) |
| **Right Hand IK Target** | Transform for right hand IK (optional) |
| **Holster Target** | Where to holster (or null for default back) |
| **Holster Local Position** | Position when holstered |
| **Holster Local Rotation** | Rotation when holstered |

#### Step 3: Create IK Target Transforms (Optional)

For two-handed weapons (rifles, bows):
1. Create empty child under the weapon parent: `AssaultRifleParent > LeftHandIK`
2. Position it where the left hand should grip
3. Reference it in `WeaponParentConfig.LeftHandIKTarget`

### 2. Weapon Prefab Setup

For each weapon prefab:

1. Add Component → `WeaponAttachmentConfig`
2. Set **Wield Target ID** to the category:

| Weapon Type | Wield Target ID |
|-------------|-----------------|
| Assault Rifle | 50001001 |
| Pistol (Right) | 50002001 |
| Pistol (Left) | 50002002 |
| Shotgun | 50003001 |
| Bow | 50004001 |
| Sniper | 50005001 |
| Rocket Launcher | 50006001 |
| Sword | 50022001 |
| Knife | 50023001 |
| Katana | 50024001 |
| Shield | 50025001 |
| Frag Grenade (Right) | 50041001 |
| Frag Grenade (Left) | 50041002 |
| Flashlight | 50042001 |

That's all you need on the weapon - no offsets, no IK references.

---

## Component Reference

### WeaponAttachmentConfig (Weapon Prefab)

```csharp
public class WeaponAttachmentConfig : MonoBehaviour
{
    public uint WieldTargetID = 0;  // Category ID only
}
```

Weapons with `WieldTargetID = 0` use the default `HandAttachPoint`.

### WeaponParentConfig (Character Prefab)

```csharp
public class WeaponParentConfig : MonoBehaviour
{
    // Equipped positioning
    public Vector3 WeaponLocalPosition = Vector3.zero;
    public Vector3 WeaponLocalRotation = Vector3.zero;
    public Vector3 WeaponLocalScale = Vector3.one;

    // IK targets
    public Transform LeftHandIKTarget;
    public Transform RightHandIKTarget;

    // Holster
    public Transform HolsterTarget;
    public Vector3 HolsterLocalPosition = Vector3.zero;
    public Vector3 HolsterLocalRotation = Vector3.zero;
}
```

---

## ObjectIdentifier ID Reference

### Right Hand Parents

| Parent Name | ID | Used By |
|-------------|----|---------|
| AssaultRifleParent | 50001001 | Rifles, SMGs |
| PistolParent | 50002001 | Pistols (main hand) |
| ShotgunParent | 50003001 | Shotguns |
| SniperParent | 50005001 | Sniper rifles |
| RocketLauncherParent | 50006001 | Rocket launchers |
| SwordParent | 50022001 | Swords |
| KnifeParent | 50023001 | Knives, daggers |
| KatanaParent | 50024001 | Katanas |
| FragGrenadeParent | 50041001 | Throwables |
| FlashlightParent | 50042001 | Flashlights |

### Left Hand Parents

| Parent Name | ID | Used By |
|-------------|----|---------|
| PistolParent | 50002002 | Dual-wield pistols |
| BowParent | 50004001 | Bows |
| ShieldParent | 50025001 | Shields |
| GrenadeParent | 50041002 | Off-hand throwables |

---

## Runtime Behavior

### Equip Flow

```
1. Player equips weapon with WieldTargetID = 50001001
2. WeaponEquipVisualBridge looks up ObjectIdentifier with ID 50001001
3. Finds AssaultRifleParent transform
4. Gets WeaponParentConfig from that transform
5. Parents weapon to AssaultRifleParent
6. Applies WeaponLocalPosition/Rotation/Scale from config
7. Sets IK targets from config
```

### Holster Flow

```
1. Player switches weapons
2. Previous weapon needs holstering
3. Get WeaponParentConfig from current parent
4. Use HolsterTarget (or default BackAttachPoint)
5. Apply HolsterLocalPosition/Rotation
```

---

## Adding New Characters

When adding a new character model:

1. Create the weapon parent hierarchy under each hand
2. Add `ObjectIdentifier` components with matching IDs
3. Add `WeaponParentConfig` to each parent
4. Configure offsets appropriate for this character's size/proportions
5. Create and reference IK targets

**No weapon prefabs need to be modified.**

---

## Adding New Weapon Categories

To add a new weapon category (e.g., "GreatSword"):

1. Choose a unique ID (e.g., 50026001)
2. On each character prefab:
   - Create `GreatSwordParent` under Items
   - Add `ObjectIdentifier` with ID 50026001
   - Add `WeaponParentConfig` with appropriate offsets
3. On weapon prefabs:
   - Set `WieldTargetID = 50026001`

---

## Debugging

Enable **Debug Logging** on `WeaponEquipVisualBridge` to see:

```
[WeaponEquipVisualBridge] Weapon parent cache initialized with 14 entries
[WeaponEquipVisualBridge] Using specific parent: AssaultRifleParent (ID: 50001001, HasConfig: True)
[WeaponEquipVisualBridge] Applied WeaponParentConfig: parent=AssaultRifleParent pos=(0.0, 0.1, 0.0) rot=(0.0, 0.0, 0.0)
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Weapon at wrong position | Check `WeaponParentConfig` offsets on the parent transform |
| Weapon parent not found | Verify `ObjectIdentifier` component and ID on parent |
| IK not working | Ensure `LeftHandIKTarget` is set in `WeaponParentConfig` |
| Holster position wrong | Configure `HolsterLocalPosition/Rotation` in `WeaponParentConfig` |
| New character doesn't hold weapons right | Add `WeaponParentConfig` to each weapon parent on that character |

---

## ECS Integration (Optional)

For ECS-level access to weapon categories:

### On Weapon Prefabs
Add `WeaponCategoryAuthoring` component (reads from `WeaponAttachmentConfig`)

### On Character Prefabs (Client Only)
Add `CharacterWeaponParentsAuthoring` with references to Items containers

### ECS Components

```csharp
// On weapon entities
public struct WeaponCategory : IComponentData
{
    public uint WieldTargetID;
}

// On character entities (buffer)
public struct WeaponParentElement : IBufferElementData
{
    public uint ObjectIdentifierID;
    public int TransformInstanceID;
}
```

---

## Summary

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `WeaponAttachmentConfig` | Weapon prefab | Declares category ID |
| `WeaponParentConfig` | Character prefab (each parent) | Defines positioning for category |
| `ObjectIdentifier` | Character prefab (each parent) | Maps ID to transform |
| `WeaponEquipVisualBridge` | Character prefab | Runtime parent lookup and positioning |
