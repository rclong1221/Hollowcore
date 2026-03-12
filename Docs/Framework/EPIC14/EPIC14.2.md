# EPIC14.2 - Equipment System Off-Hand Support

**Status:** Complete
**Dependencies:** EPIC14.1 (Dual Pistol animations require off-hand support)
**Blocks:** EPIC14.3 (Data-driven refactor benefits from working equipment system)

---

## Design Principles

### Replaceability
This epic uses an **interface abstraction** so the entire equipment system can be swapped for an Asset Store solution (Inventory Pro, uMMORPG, Devion Games, etc.) with minimal code changes.

### Data-Driven
Equipment slots are configured via **ScriptableObject**, not hardcoded. Adding new slots or changing input bindings requires no code changes.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              IEquipmentProvider (Interface)                  │
│  + GetEquippedItem(slotIndex) → ItemInfo                     │
│  + GetSlotCount() → int                                      │
│  + IsSlotOccupied(slotIndex) → bool                         │
│  + EquipItem(slotIndex, itemEntity) → void                  │
│  + UnequipItem(slotIndex) → void                             │
│  + event OnEquipmentChanged(slotIndex, oldItem, newItem)    │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┴────────────────────┐
          ▼                                        ▼
┌─────────────────────────┐         ┌──────────────────────────────┐
│ DIGEquipmentProvider    │         │ AssetStoreEquipmentAdapter   │
│ (Your ECS-based system) │         │ (Wrapper for 3rd party APIs) │
│                         │         │                              │
│ - Reads from ECS        │         │ - InventoryPro adapter      │
│ - ActiveEquipmentSlot   │         │ - uMMORPG adapter           │
│ - ItemSetSwitchSystem   │         │ - Custom implementations    │
└─────────────────────────┘         └──────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 WeaponEquipVisualBridge                      │
│  - Depends ONLY on IEquipmentProvider                       │
│  - Never directly references ECS components                 │
│  - Listens to OnEquipmentChanged event                      │
└─────────────────────────────────────────────────────────────┘
```

---

## Data Structures

### `EquipmentSlotConfig` (ScriptableObject)

| Field | Type | Description |
|:------|:-----|:------------|
| `SlotName` | string | "MainHand", "OffHand", "Armor", etc. |
| `SlotIndex` | int | 0 = main, 1 = off-hand |
| `InputBinding` | string | "1-9" for main, "Alt+1-9" for off-hand |
| `AllowedCategories` | enum[] | Weapon, Shield, Consumable |
| `AnimatorSlotParam` | string | "Slot0ItemID" or "Slot1ItemID" |

### `ItemInfo` (Returned by Interface)

| Field | Type | Description |
|:------|:-----|:------------|
| `ItemEntity` | Entity | ECS entity reference |
| `AnimatorItemID` | int | ID for Animator (e.g., 26 for Shield) |
| `Category` | enum | Weapon, Shield, Magic, etc. |
| `DisplayName` | string | For UI |

---

## Unity Editor Tools

To support the data-driven approach and improve developer workflow, we will implement the following custom Editor tools.

### 1. Equipment Slot Configurator
**Goal:** Streamline the creation and management of equipment slots.
**Features:**
- **Dashboard View:** Visual list of all configured slots in the project.
- **One-Click Creation:** Button to create a new `EquipmentSlotConfig` asset with safe defaults.
- **Validation:** Automatically checks for conflicts (e.g., duplicate Slot Index or Input Bindings) and displays warnings.
- **Assignment:** Drag-and-drop area to quickly assign slot configs to the global `DIGEquipmentProvider` or player prefab.

### 2. Runtime Equipment Debugger
**Goal:** visualize the state of the equipment system in real-time.
**Features:**
- **Provider State:** Read-only view of `IEquipmentProvider.GetEquippedItem(i)` for all slots.
- **ECS Comparison:** Side-by-side view of IEquipmentProvider state vs. actual ECS component state to spot desyncs.
- **Force Equip:** Testing buttons to manually inject `EquipItem()` calls without needing gameplay triggers.
- **Event Log:** Real-time log of `OnEquipmentChanged` events firing.

---

## Implementation Checklist

### Phase 1: Interface Definition
- [x] Create `IEquipmentProvider` interface in `Assets/Scripts/Items/Interfaces/`
- [x] Define `ItemInfo` struct with equipment data
- [x] Create `EquipmentSlotConfig` ScriptableObject

### Phase 2: DIG Implementation
- [x] Create `DIGEquipmentProvider` ECS adapter
- [x] Create `EquipmentSlotConfigEditor` (Custom Inspector)
- [x] Implement DIGEquipmentProvider adapter logic (ECS-based)
- [x] Add off-hand input binding (Alt+Number)

### Phase 3: Bridge Refactor
- [x] Create `EquipmentProviderBindingSystem` to link Ghosts to Entities (Multiplayer Fix)
- [x] Modify `WeaponEquipVisualBridge.cs` to use `IEquipmentProvider`
- [x] Update `DIGEquipmentProvider` with robust entity finding logic (from Bridge)
- [x] Remove direct ECS dependencies (`ActiveEquipmentSlot`, `UsableAction`) from Bridge

### Phase 4: Multiplayer Support & Verification
- [x] Create `EquipmentProviderBindingSystem` to link Ghosts to Entities
- [x] Expose `PlayerEntity` in `DIGEquipmentProvider` for binding
- [x] Verify `ActiveEquipmentSlot` is a Replicated Ghost Component
- [x] Verify `ItemSetEntry` buffer replication (if used)
- [x] Test with Multi-Client (ParrelSync/Building)

### Phase 5: Editor Tools
- [x] Create `EquipmentSlotConfigEditor.cs`
    - [x] Custom Inspector with preview
    - [x] Validation (duplicate slot indices, missing params)
    - [x] Quick-create menu for MainHand/OffHand presets
- [x] Create `EquipmentSystemDebuggerWindow.cs`
    - [x] Runtime state visualization (per-slot ItemInfo)
    - [x] ECS vs Provider state comparison
    - [x] "Force Equip" testing button
    - [x] Event log for OnEquipmentChanged

---

## Migration Path for Asset Store Replacement

If developers want to use an Asset Store inventory system:

1. **Create adapter** that implements `IEquipmentProvider`
2. **Map** Asset Store item IDs to `AnimatorItemID`
3. **Replace** DI binding: `EquipmentProvider = new AssetStoreAdapter()`
4. **Done** - No changes to animation code needed!

---

## Notes
- `ActiveEquipmentSlot` fields still exist but are implementation detail of `DIGEquipmentProvider`
- Third-party systems can ignore ECS entirely if they provide the interface
- `WeaponEquipVisualBridge.IsPistolItemID()` etc. remain unchanged unless modified by EPIC 14.3
