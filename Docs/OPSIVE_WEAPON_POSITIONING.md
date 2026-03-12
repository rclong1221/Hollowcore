# Opsive Weapon Positioning Research

## Overview

This document details how the Opsive Ultimate Character Controller handles weapon positioning, attachment, IK, and perspective-specific rendering.

---

## 1. Key Files and Their Purposes

### Core Item Classes

| File | Purpose |
|------|---------|
| [PerspectiveItem.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Items/PerspectiveItem.cs) | Abstract base class for perspective-specific item rendering. Defines spawn position, rotation, scale properties |
| [CharacterItem.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Items/CharacterItem.cs) | Main item component that manages both first and third person perspective items, equip/unequip logic |
| [CharacterItemSlot.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Items/CharacterItemSlot.cs) | Simple identifier marking where items attach on the character (hand bones) |
| [ItemPlacement.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Items/ItemPlacement.cs) | Identifier marking where item GameObjects are located (empty marker class) |

### Third Person Specific

| File | Purpose |
|------|---------|
| [ThirdPersonPerspectiveItem.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/ThirdPersonController/Items/ThirdPersonPerspectiveItem.cs) | Handles 3rd person weapon rendering, IK targets, holster positioning |

### First Person Specific

| File | Purpose |
|------|---------|
| [FirstPersonPerspectiveItem.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/FirstPersonController/Items/FirstPersonPerspectiveItem.cs) | Handles 1st person weapon with springs, bob, sway, procedural animation |
| [FirstPersonObjects.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/FirstPersonController/Character/FirstPersonObjects.cs) | Manages all first person objects positioning relative to camera |
| [FirstPersonBaseObject.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/FirstPersonController/Character/Identifiers/FirstPersonBaseObject.cs) | Identifier for first person arm/hand objects with pivot system |

### IK System

| File | Purpose |
|------|---------|
| [CharacterIKBase.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/CharacterIKBase.cs) | Abstract base for IK systems, defines IKGoal enum and interface |
| [CharacterIK.cs](../Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/CharacterIK.cs) | Unity Animator IK implementation for humanoid characters |

---

## 2. Main Classes/Components That Control Weapon Positioning

### `PerspectiveItem` (Abstract Base)
The foundation for all weapon positioning. Contains:

```csharp
[SerializeField] protected GameObject m_Object;                    // The visible weapon mesh
[SerializeField] protected IDObject<Transform> m_SpawnParent;      // Parent transform under ItemSlot
[SerializeField] protected Vector3 m_LocalSpawnPosition;           // Position offset when spawned
[SerializeField] protected Vector3 m_LocalSpawnRotation;           // Rotation offset when spawned
[SerializeField] protected Vector3 m_LocalSpawnScale = Vector3.one; // Scale when spawned
```

Key method:
```csharp
protected abstract Transform GetSpawnParent(GameObject character, int slotID, bool parentToItemSlotID);
```

### `ThirdPersonPerspectiveItem`
Extends PerspectiveItem with IK and holster support:

```csharp
[SerializeField] protected Transform m_NonDominantHandIKTarget;     // Where off-hand grips
[SerializeField] protected Transform m_NonDominantHandIKTargetHint; // Elbow hint for IK
[SerializeField] protected IDObject<Transform> m_HolsterTarget;     // Where weapon goes when unequipped
```

### `FirstPersonPerspectiveItem`
Extends PerspectiveItem with extensive procedural animation:

```csharp
// Base positioning
[SerializeField] protected int m_FirstPersonBaseObjectID;           // Which arm set to use
[SerializeField] protected GameObject m_VisibleItem;                // The actual weapon mesh
[SerializeField] protected Vector3 m_BasePositionOffset;            // Character-wide offset
[SerializeField] protected Vector3 m_PositionOffset;                // Item-specific offset
[SerializeField] protected Vector3 m_PositionExitOffset;            // Position when unequipping

// Spring systems for procedural animation
[SerializeField] protected Spring m_PositionSpring;                 // Position spring physics
[SerializeField] protected Spring m_RotationSpring;                 // Rotation spring physics
[SerializeField] protected Spring m_PivotPositionSpring;            // Pivot position spring
[SerializeField] protected Spring m_PivotRotationSpring;            // Pivot rotation spring

// Rotation offsets
[SerializeField] protected Vector3 m_RotationOffset;                // Default rotation
[SerializeField] protected Vector3 m_RotationExitOffset;            // Rotation when unequipping
[SerializeField] protected Vector3 m_PivotPositionOffset;           // Pivot position offset
[SerializeField] protected Vector3 m_PivotRotationOffset;           // Pivot rotation offset
```

### `CharacterItem`
The master item component that:
- References both `FirstPersonPerspectiveItem` and `ThirdPersonPerspectiveItem`
- Manages perspective switching
- Controls equip/unequip state
- Has `SlotID` property that determines which hand/slot the item uses

---

## 3. Important Properties/Fields for Position Offsets

### Third Person Offsets

| Property | Type | Purpose |
|----------|------|---------|
| `m_LocalSpawnPosition` | Vector3 | Position relative to spawn parent |
| `m_LocalSpawnRotation` | Vector3 | Rotation relative to spawn parent |
| `m_LocalSpawnScale` | Vector3 | Scale of the weapon |
| `m_NonDominantHandIKTarget` | Transform | Where off-hand should grip |
| `m_HolsterTarget` | Transform | Where weapon goes when holstered |

### First Person Offsets

| Property | Type | Purpose |
|----------|------|---------|
| `m_BasePositionOffset` | Vector3 | Character-wide position adjustment |
| `m_PositionOffset` | Vector3 | Item-specific position (where weapon "wants to be") |
| `m_PositionExitOffset` | Vector3 | Position when unequipping/off-screen |
| `m_RotationOffset` | Vector3 | Default rotation offset |
| `m_RotationExitOffset` | Vector3 | Rotation when unequipping |
| `m_PivotPositionOffset` | Vector3 | Pivot transform position |
| `m_PivotRotationOffset` | Vector3 | Pivot transform rotation |

### First Person Procedural Animation

| Property | Type | Purpose |
|----------|------|---------|
| `m_PositionFallImpact` | float | Push down when landing |
| `m_PositionMoveSlide` | Vector3 | Slide based on movement direction |
| `m_RotationLookSway` | Vector3 | Sway from mouse/look movement |
| `m_RotationStrafeSway` | Vector3 | Sway from strafing |
| `m_BobPositionalRate/Amplitude` | Vector3 | Walking bob rate and strength |
| `m_ShakeSpeed/Amplitude` | float/Vector3 | Idle shake animation |

---

## 4. How Weapon Placement Works

### Hierarchy Structure

```
Character (UltimateCharacterLocomotion)
├── Model (with Animator)
│   ├── Armature
│   │   ├── RightHand
│   │   │   └── CharacterItemSlot (ID: 0)
│   │   │       └── Items
│   │   │           └── [ThirdPersonPerspectiveItem Object]
│   │   │               └── NonDominantHandIKTarget
│   │   └── LeftHand
│   │       └── CharacterItemSlot (ID: 1)
│   ├── HolsterTargets
│   │   └── BackHolster (for unequipped weapons)
│   └── CharacterIK
│
└── FirstPersonObjects
    └── FirstPersonBaseObject (arms model)
        └── Pivot
            └── CharacterItemSlot
                └── [FirstPersonPerspectiveItem VisibleItem]
```

### Spawn Parent Resolution

The `GetSpawnParent()` method finds where to attach weapons:

1. **Third Person:**
   - Searches for `CharacterItemSlot` components matching the `SlotID`
   - Excludes slots under FirstPersonBaseObject
   - Optionally uses `m_SpawnParent` to find a child of the slot

2. **First Person:**
   - Finds `FirstPersonBaseObject` matching `m_FirstPersonBaseObjectID`
   - Searches for `CharacterItemSlot` within the base object
   - Parents `m_VisibleItem` under the slot

### Perspective Switching

When switching between first and third person:
- `CharacterItem.OnChangePerspectives(bool firstPersonPerspective)` is called
- Sets `m_ActivePerspectiveItem` to either first or third person item
- Activates/deactivates the appropriate visible objects
- First person objects are managed by `FirstPersonObjects` component

---

## 5. Equip/Unequip Positioning

### Third Person Equip/Unequip

```csharp
// In ThirdPersonPerspectiveItem.SetActive()
if (active) {
    // Move from holster to hand
    m_ObjectTransform.parent = m_StartParentTransform;  // ItemSlot parent
    m_ObjectTransform.localPosition = m_StartLocalPosition;
    m_ObjectTransform.localRotation = m_StartLocalRotation;
} else {
    // Move to holster
    m_ObjectTransform.parent = HolsterTarget;
    m_ObjectTransform.localPosition = Vector3.zero;
    m_ObjectTransform.localRotation = Quaternion.identity;
}
```

### First Person Equip/Unequip

Uses spring system with exit offsets:
- `m_PositionExitOffset` - target position when moving off-screen
- `m_RotationExitOffset` - target rotation when moving off-screen
- Springs interpolate smoothly between states

---

## 6. IK System for Hand Positioning

### CharacterIKBase Interface

```csharp
public abstract void SetItemIKTargets(
    CharacterItem characterItem, 
    Transform itemTransform,           // The weapon transform
    Transform itemHand,                // Which hand holds it (dominant)
    Transform nonDominantHandTarget,   // Where off-hand should go
    Transform nonDominantHandElbowTarget  // Elbow hint
);
```

### How IK Targets Are Set

In `ThirdPersonPerspectiveItem.SetActive()`:

```csharp
if (active) {
    // Schedule IK target update
    m_CharacterIK.SetItemIKTargets(m_CharacterItem, m_ObjectTransform, 
                                    NonDominantHandIKTarget, NonDominantHandIKTargetHint);
} else {
    // Clear IK targets
    SetItemIKTargets(null, null, null);
}
```

### IKGoal Enum

```csharp
public enum IKGoal {
    LeftHand,   // Character's left hand
    LeftElbow,  // Character's left elbow hint
    RightHand,  // Character's right hand
    RightElbow, // Character's right elbow hint
    LeftFoot,   // Character's left foot
    LeftKnee,   // Character's left knee hint
    RightFoot,  // Character's right foot
    RightKnee,  // Character's right knee hint
    Last
}
```

### CharacterIK Hand Assignment

The IK system automatically determines:
- If weapon is in right hand → Left hand uses IK target
- If weapon is in left hand → Right hand uses IK target

```csharp
// In CharacterIK.SetItemIKTargets()
if (itemHand.IsChildOf(m_RightHand) || itemHand == m_RightHand) {
    m_LeftHandItemIKTarget = nonDominantHandTarget;
    m_LeftHandItemIKHintTarget = nonDominantHandElbowTarget;
} else {
    m_RightHandItemIKTarget = nonDominantHandTarget;
    m_RightHandItemIKHintTarget = nonDominantHandElbowTarget;
}
```

---

## 7. Summary: The Position Flow

### Third Person
1. `CharacterItem` spawns with a `SlotID` (e.g., 0 = right hand)
2. `ThirdPersonPerspectiveItem` finds `CharacterItemSlot` with matching ID under character's armature
3. Weapon mesh is parented to slot with `m_LocalSpawnPosition/Rotation/Scale`
4. IK system positions non-dominant hand using `m_NonDominantHandIKTarget`
5. When unequipped, weapon moves to `m_HolsterTarget` position

### First Person
1. `FirstPersonPerspectiveItem` finds `FirstPersonBaseObject` by `m_FirstPersonBaseObjectID`
2. Gets the pivot transform from the base object
3. Parents `m_VisibleItem` under `CharacterItemSlot` inside the base object
4. Applies `m_PositionOffset` and `m_RotationOffset` as rest position
5. Spring systems apply procedural animation (bob, sway, recoil)
6. `FirstPersonObjects` component manages overall first person object positioning relative to camera

---

## 8. Key Takeaways for Custom Weapon Positioning

1. **Slot System**: Items use `SlotID` to determine attachment point. Each slot has a `CharacterItemSlot` component.

2. **Perspective-Specific**: Different components handle first vs third person - don't try to share logic.

3. **Local Transforms**: All position/rotation values are local to parent. The spawn parent is the key reference.

4. **IK Integration**: Third person weapons should define `NonDominantHandIKTarget` for proper two-handed poses.

5. **Holster System**: Third person supports automatic holstering via `HolsterTarget`.

6. **Spring Animation**: First person uses physics springs for all procedural animation - modify spring properties, not raw transforms.

7. **Model Switching**: The system supports multiple character models - weapons reparent correctly via `OnCharacterSwitchModels`.

---

## 9. Quick Reference: Inspector Properties

### On ThirdPersonPerspectiveItem Component
- **Object**: The weapon mesh GameObject
- **Local Spawn Position/Rotation/Scale**: Transform offsets from slot
- **Non Dominant Hand IK Target**: Transform for off-hand placement
- **Non Dominant Hand IK Target Hint**: Elbow bend direction
- **Holster Target**: Transform for holstered weapon position

### On FirstPersonPerspectiveItem Component
- **First Person Base Object ID**: Which arm model to use (0 = default)
- **Visible Item**: The weapon mesh GameObject
- **Base Position Offset**: Global position adjustment
- **Position Offset**: Weapon's rest position
- **Position Exit Offset**: Where weapon goes when unequipping
- **Rotation Offset/Exit Offset**: Same for rotations
- **Spring properties**: Stiffness, damping for procedural animation
- **Bob/Sway/Shake properties**: Procedural movement settings

### On CharacterItem Component
- **Slot ID**: Which hand slot (0 = right hand typically)
- **Equip/Unequip Events**: Animation timing
