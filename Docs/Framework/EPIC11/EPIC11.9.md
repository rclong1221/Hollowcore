# Epic 11.9: Equipment & Armor

**Priority**: MEDIUM
**Status**: **IMPLEMENTED** (Backend & Visual Logic)
**Goal**: Allow players to equip items to specific body slots.

## Implementation Details

### 1. Definitions
-   **Asset**: `EquipmentItemDef` (Inherits from `ItemDef`).
    -   `EquipSlot`: Enum (Head, Chest, Legs, Feet).
    -   `ArmorValue`: int.
    -   `EquippedVisualPrefab`: GameObject.

### 2. Backend
-   **Component**: `Equipment` (Synced Ghost).
    -   Stores `ItemID` for each slot.
-   **System**: `EquipmentSystem` (Server).
    -   Handles `EquipItemRequest` (Swap Inventory <-> Equipment).
    -   Handles `UnequipItemRequest`.
    -   Validates Slot Types using `ItemDatabaseBlob`.

### 3. Visuals
-   **System**: `EquipmentVisualSystem` (Client/Managed).
    -   Detects changes in `Equipment`.
    -   Instantiates `EquippedVisualPrefab`.
    -   *Note*: Bone attachment logic requires Avatar reference (currently logs).

## Tasks
- [x] Define `EquipmentItemDef` and Enum.
- [x] Update `ItemDatabaseBlob` to include Equip data.
- [x] Implement `EquipmentSystem` (Swap logic).
- [x] Implement `EquipmentVisualSystem`.

## Integration Guide
1.  **Create Equipment**: Project -> Create -> DIG -> Items -> Equipment.
2.  **Assign Slot**: Set `EquipSlot` to Head/Chest/etc.
3.  **UI**: Drag and drop should send `EquipItemRequest`.
