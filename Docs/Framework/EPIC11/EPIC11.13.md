# Epic 11.13: Consumables & Item Usage

**Priority**: HIGH
**Status**: **IMPLEMENTED**
**Goal**: Logic for "Using" an item (Eat, Heal, Reload) directly from inventory.

## Design Notes
1.  **Usage Pipeline**:
    *   **Input**: User performs "Use" action (Inventory Context Menu / Hotkey).
    *   **Networking**: `UseItemRequest` sent to Server.
    *   **Logic**: Server validates item properties, applies changes, consumes item.
2.  **Stat Application**:
    *   Directly modifies `Health` (Player) or `OxygenTank` (Survival) components.
    *   Uses `ItemDatabaseBlob` data (RestoreHealth, etc) for fast lookup.

## Implemented Components

### InventoryComponents.cs
Location: `Assets/Scripts/Items/Components/InventoryComponents.cs`

| Component | Description |
|-----------|-------------|
| `UseItemRequest` | RPC { SlotIndex }. |

### ItemBlobData (Modified)
Location: `Assets/Scripts/Items/Components/ItemDatabaseBlob.cs`
- Added: `RestoreHealth`, `RestoreOxygen`, `RestoreHunger`.

## Implemented Systems

### ItemUsageSystem
Location: `Assets/Scripts/Player/Systems/ItemUsageSystem.cs`
- **Server**: Consumes `UseItemRequest`.
- **Validation**: Checks if Slot has valid item index.
- **Application**:
  - IF `RestoreHealth > 0`: `Health.Current += RestoreHealth` (Clamped).
  - IF `RestoreOxygen > 0`: `OxygenTank.Current += RestoreOxygen` (Clamped).
- **Consumption**: Decrements stack count. Updates `InventoryVersion`.

## Authoring Data

### ConsumableItemDef.cs
Location: `Assets/Scripts/Items/Definitions/ConsumableItemDef.cs`
- ScriptableObject for defining consumables.
- Fields: `RestoreHealth`, `RestoreOxygen`, etc.

## Integration Guide

### 1. Create Consumable
1.  Project > `Create > DIG > Items > Consumable Item`.
2.  Name: "Medkit".
3.  Set `RestoreHealth` = 50.
4.  ID: `item.medkit`.
5.  Assign Icon/Prefab.

### 2. UI Hookup (Future/Pending)
- Currently `UseItemRequest` must be sent via code or debug tool.
- **Epic 11.14** will add the Context Menu to trigger this.

## Testing
1.  **Setup**: Give player a "Medkit" (via initial inventory or Pickup).
2.  **Damage**: Manually reduce Player Health in Entity Debugger (e.g. set to 50).
3.  **Trigger**:
    - *Debug*: Should add a temporary key in `HotbarInputSystem` (e.g., Right Click) or a Test System to trigger usage.
    - *Verification*: Health increases to 100. Item count decreases.
