# Setup Guide: EPIC 14.2 (Equipment Provider Interface)

## Overview

This epic introduces the `IEquipmentProvider` interface to abstract the equipment system from the animation bridge. This allows the entire inventory/equipment system to be swapped for an Asset Store solution (Inventory Pro, uMMORPG Remastered, Devion Games, etc.) without modifying animation code.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              IEquipmentProvider (Interface)                  │
│  + GetEquippedItem(slotIndex) → ItemInfo                     │
│  + SlotCount → int                                           │
│  + OnEquipmentChanged event                                  │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┴────────────────────┐
          ▼                                        ▼
┌─────────────────────────┐         ┌──────────────────────────────┐
│ DIGEquipmentProvider    │         │ YourAssetStoreAdapter        │
│ (Built-in ECS system)   │         │ (Implement interface)        │
└─────────────────────────┘         └──────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 WeaponEquipVisualBridge                      │
│  - Depends ONLY on IEquipmentProvider                        │
│  - No direct ECS component access                            │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IEquipmentProvider` | `Assets/Scripts/Items/Interfaces/` | Abstract interface for equipment access |
| `ItemInfo` | Same file | Data struct returned by provider |
| `DIGEquipmentProvider` | `Assets/Scripts/Items/Interfaces/` | Built-in ECS implementation |
| `EquipmentSlotConfig` | `Assets/Scripts/Items/Interfaces/` | ScriptableObject for slot configuration |
| `EquipmentProviderBindingSystem` | `Assets/Scripts/Items/Systems/` | Multiplayer entity binding |
| `EquipmentSlotConfigEditor` | `Assets/Scripts/Items/Editor/` | Custom inspector |
| `EquipmentSystemDebuggerWindow` | `Assets/Scripts/Items/Editor/` | Runtime debug tool |

---

## Setup Instructions

### 1. Add DIGEquipmentProvider to Player Prefab

1. Open your Player prefab (e.g., `Warrok_Client.prefab`)
2. Add the `DIGEquipmentProvider` component to the **root GameObject**
3. (Optional) Assign `EquipmentSlotConfig` assets to the `Slot Configs` array

### 2. Configure WeaponEquipVisualBridge

1. Find the `WeaponEquipVisualBridge` component on the player
2. Set `Equipment Provider Mono` field to reference the `DIGEquipmentProvider`
   - If left empty, it will auto-find via `GetComponent<IEquipmentProvider>()`

### 3. Create Slot Configuration Assets (Optional)

Use the Unity menu: `Assets > Create > DIG > Equipment > Main Hand Config`

Or use the Editor buttons:
1. Select any `EquipmentSlotConfig` asset
2. Click "Main Hand" or "Off Hand" buttons for quick presets

---

## Using the Equipment System

### Reading Equipment State

```csharp
// Get the provider (from inspector or GetComponent)
IEquipmentProvider provider = GetComponent<IEquipmentProvider>();

// Read main hand item
ItemInfo mainHand = provider.GetEquippedItem(0);
if (!mainHand.IsEmpty)
{
    Debug.Log($"Main hand: ID={mainHand.AnimatorItemID}, Type={mainHand.AnimationWeaponType}");
}

// Read off hand item
ItemInfo offHand = provider.GetEquippedItem(1);
```

### Subscribing to Equipment Changes

```csharp
void Start()
{
    var provider = GetComponent<IEquipmentProvider>();
    provider.OnEquipmentChanged += OnEquipmentChanged;
}

void OnEquipmentChanged(object sender, EquipmentChangedEventArgs args)
{
    Debug.Log($"Slot {args.SlotIndex}: {args.OldItem.AnimatorItemID} → {args.NewItem.AnimatorItemID}");
}
```

### Input Bindings

| Input | Action |
|-------|--------|
| `1-9` | Equip to main hand (slot 0) |
| `Alt + 1-9` | Equip to off hand (slot 1) |

---

## ItemInfo Structure

| Field | Type | Description |
|-------|------|-------------|
| `ItemEntity` | Entity | ECS entity reference (may be `Entity.Null` for non-ECS systems) |
| `AnimatorItemID` | int | Animator parameter value (e.g., 2=Pistol, 26=Shield) |
| `AnimationWeaponType` | enum | Category for input routing (Gun, Melee, Bow, Magic, Shield, Throwable) |
| `MovementSetID` | int | Animator movement set (0=Guns, 1=Melee, 2=Bow) |
| `DisplayName` | string | For UI display |
| `IsEmpty` | bool | Returns true if slot is empty |

---

## AnimationWeaponType Enum

| Value | Meaning | Example Items |
|-------|---------|---------------|
| None | Empty slot | - |
| Gun | Ranged firearms | Rifle, Pistol, Shotgun |
| Melee | Close combat | Sword, Knife, Katana |
| Bow | Draw/release mechanics | Bow |
| Magic | Spell casting | Staff (ID 61-65) |
| Shield | Blocking/parrying | Shield (ID 26) |
| Throwable | Thrown items | Grenade |

---

## Editor Tools

### Equipment Slot Config Editor

When you select an `EquipmentSlotConfig` asset:

- **Quick Setup Buttons**: Click "Main Hand" or "Off Hand" for instant preset configuration
- **Validation**: Warnings appear for:
  - Empty animator parameter names
  - Duplicate slot indices across configs

### Equipment System Debugger Window

Open via menu: `DIG > Equipment > System Debugger`

**Features:**
- **Slot States**: Live view of all equipped items
- **ECS Comparison**: Side-by-side of Provider state vs actual ECS components
- **Force Equip**: Testing buttons to manually trigger equips
- **Event Log**: Real-time log of `OnEquipmentChanged` events

---

## Multiplayer Support

### Automatic Entity Binding

The `EquipmentProviderBindingSystem` automatically links each player's `DIGEquipmentProvider` to the correct ECS Entity:

- Works for local player (predicted ghost)
- Works for remote players (interpolated ghosts)
- Runs every frame in `PresentationSystemGroup`

### Ghost Component Configuration

Equipment state is replicated via:

| Component | PrefabType | Description |
|-----------|------------|-------------|
| `ActiveEquipmentSlot` | All | Replicated to all clients |
| `ItemSetEntry` (buffer) | All | Item bindings replicated |

**No manual setup required** - components already have correct `[GhostComponent]` attributes.

---

## Replacing with Asset Store System

To swap the equipment system for a third-party solution:

1. **Create an adapter** that implements `IEquipmentProvider`:

```csharp
public class InventoryProAdapter : MonoBehaviour, IEquipmentProvider
{
    // Map Inventory Pro items to AnimatorItemID
    public ItemInfo GetEquippedItem(int slotIndex)
    {
        var item = InventoryProAPI.GetEquippedItem(slotIndex);
        return new ItemInfo
        {
            AnimatorItemID = MapToAnimatorID(item),
            AnimationWeaponType = MapToWeaponType(item),
            // ...
        };
    }
    
    // Implement other interface members...
}
```

2. **Replace the provider** on the player prefab:
   - Remove `DIGEquipmentProvider`
   - Add your `InventoryProAdapter`
   - Assign it to `WeaponEquipVisualBridge.EquipmentProviderMono`

3. **Done** - No changes to animation code needed!

---

## Verification Checklist

### Basic Functionality
- [ ] Player prefab has `DIGEquipmentProvider` component
- [ ] `WeaponEquipVisualBridge` finds the provider (no error logs)
- [ ] Press 1-9 to equip weapons → visuals update

### Off-Hand Support
- [ ] Press Alt+2 to equip pistol off-hand
- [ ] Dual pistol animations play when both hands have pistols
- [ ] Press Alt+# to equip shield → shield visible

### Debugger Window
- [ ] Open `DIG > Equipment > System Debugger`
- [ ] "Find" button locates the provider
- [ ] Slot states update in real-time
- [ ] Force equip buttons work

### Multiplayer
- [ ] Connect 2 clients
- [ ] Each client sees correct weapons on remote players
- [ ] Equipment changes replicate to all clients

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "MISSING IEquipmentProvider!" error | Add `DIGEquipmentProvider` to player prefab |
| Provider not finding player entity | Check debug logs for world/entity discovery |
| Remote players have wrong weapons | Verify `ActiveEquipmentSlot` has `[GhostComponent]` |
| Off-hand not working | Verify `OffHandQuickSlot` field is set in ECS |
| Events not firing | Subscribe before equipment changes occur |
| Debugger shows "Not available" | Enter Play Mode first |
