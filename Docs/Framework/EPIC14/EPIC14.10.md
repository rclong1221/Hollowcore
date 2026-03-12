# EPIC 14.10 - Opsive Weapon Positioning Algorithm (ECS Adaptation)

**Goal:** Replicate Opsive's weapon positioning system ("specific positions for each weapon category") in our ECS framework, with character-defined offsets for scalability across multiple character models.

---

## Part 1: The Problem & Solution

### The Problem with Weapon-Stored Offsets

Originally, we considered storing position/rotation/scale offsets on weapon prefabs. But this doesn't scale:

- **N weapons × M characters = explosion of configurations**
- Weapon prefabs would need different offsets for each character model
- IK targets on weapons don't make sense for varying character sizes

### The Solution: Character Owns the Offsets

**Weapons only specify their category** (e.g., "I'm an Assault Rifle" = ID 50001001).
**Characters define how to hold each category** via `WeaponParentConfig` on each weapon parent transform.

```
Weapon: "I belong to category 50001001 (Assault Rifle)"
    ↓
Character: "Here's how I hold Assault Rifles" (position, rotation, IK targets)
```

This allows:
- Same weapon works on any character without modification
- Each character defines positioning once per category
- IK targets are character-specific, not weapon-specific

---

## Part 2: Architecture

### Character Hierarchy (Atlas_Client.prefab)

```
Atlas_Client (Character Root)
├── Armature
│   └── ORG-hand.R
│       └── Items (Container)
│           ├── AssaultRifleParent (ID: 50001001) + WeaponParentConfig
│           ├── PistolParent       (ID: 50002001) + WeaponParentConfig
│           ├── ShotgunParent      (ID: 50003001) + WeaponParentConfig
│           ├── SwordParent        (ID: 50022001) + WeaponParentConfig
│           └── ...
│   └── ORG-hand.L
│       └── Items (Container)
│           ├── ShieldParent       (ID: 50025001) + WeaponParentConfig
│           ├── BowParent          (ID: 50004001) + WeaponParentConfig
│           └── ...
```

Each weapon parent has:
1. `ObjectIdentifier` component (from Opsive) - provides the category ID
2. `WeaponParentConfig` component (new) - defines how weapons attach

### Data Flow

```
1. Weapon prefab has WeaponAttachmentConfig with WieldTargetID = 50001001
2. WeaponEquipVisualBridge finds parent by ID via ObjectIdentifier lookup
3. Parent transform has WeaponParentConfig with offsets and IK targets
4. Weapon is parented and positioned using character's config
```

---

## Part 3: Implementation

### Files Created

| File | Purpose |
|------|---------|
| `Assets/Scripts/Items/Bridges/WeaponParentConfig.cs` | MonoBehaviour on character weapon parents defining offsets and IK |
| `Assets/Scripts/Items/Components/ItemVisualComponents.cs` | ECS components: `WeaponCategory`, `WeaponParentElement` |
| `Assets/Scripts/Items/Authoring/WeaponCategoryAuthoring.cs` | Baker for weapon category |
| `Assets/Scripts/Player/Authoring/CharacterWeaponParentsAuthoring.cs` | Baker for character weapon parent buffer |

### Files Modified

| File | Changes |
|------|---------|
| `Assets/Scripts/Items/Bridges/WeaponAttachmentConfig.cs` | Simplified to only `WieldTargetID` |
| `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs` | Reads offsets from `WeaponParentConfig` on parent |

### Component: WeaponAttachmentConfig (Weapon Prefab)

```csharp
public class WeaponAttachmentConfig : MonoBehaviour
{
    // Only the category ID - nothing else
    public uint WieldTargetID = 0;
}
```

### Component: WeaponParentConfig (Character Prefab)

```csharp
public class WeaponParentConfig : MonoBehaviour
{
    // Equipped positioning
    public Vector3 WeaponLocalPosition = Vector3.zero;
    public Vector3 WeaponLocalRotation = Vector3.zero;
    public Vector3 WeaponLocalScale = Vector3.one;

    // IK targets (children of this transform)
    public Transform LeftHandIKTarget;
    public Transform RightHandIKTarget;

    // Holster configuration
    public Transform HolsterTarget;
    public Vector3 HolsterLocalPosition = Vector3.zero;
    public Vector3 HolsterLocalRotation = Vector3.zero;
}
```

### ECS Component: WeaponCategory

```csharp
public struct WeaponCategory : IComponentData
{
    public uint WieldTargetID;
}
```

---

## Part 4: ObjectIdentifier ID Reference

| Weapon Category | Right Hand ID | Left Hand ID |
|-----------------|---------------|--------------|
| Assault Rifle | 50001001 | - |
| Pistol | 50002001 | 50002002 |
| Shotgun | 50003001 | - |
| Bow | - | 50004001 |
| Sniper | 50005001 | - |
| Rocket Launcher | 50006001 | - |
| Sword | 50022001 | - |
| Knife | 50023001 | - |
| Katana | 50024001 | - |
| Shield | - | 50025001 |
| Frag Grenade | 50041001 | 50041002 |
| Flashlight | 50042001 | - |

---

## Part 5: Setup Checklist

### Character Prefab Setup (Client Only)

1. For each weapon parent (`AssaultRifleParent`, etc.):
   - Ensure it has `ObjectIdentifier` component with correct ID
   - Add `WeaponParentConfig` component
   - Set `WeaponLocalPosition`, `WeaponLocalRotation`, `WeaponLocalScale`
   - Create child transforms for IK targets if needed
   - Reference IK targets in the config

2. Optionally add `CharacterWeaponParentsAuthoring` for ECS access

### Weapon Prefab Setup

1. Add `WeaponAttachmentConfig` component
2. Set `WieldTargetID` to match the weapon category
3. That's it - no offsets needed on weapon

---

## Part 6: Benefits

| Aspect | Old Approach | New Approach |
|--------|--------------|--------------|
| Scalability | N weapons × M characters | N weapons + M characters |
| Weapon prefabs | Complex (offsets per character) | Simple (category ID only) |
| Character setup | None | One-time per category |
| IK targets | On weapon (wrong for varying sizes) | On character (correct) |
| Adding new character | Update all weapons | Configure once on character |
| Adding new weapon | Configure offsets | Just set category ID |
