# Epic 11.2: Inventory Backend (ECS)

**Priority**: CRITICAL
**Status**: **IMPLEMENTED**
**Goal**: Core backend for managing inventory buffers, replication, and standard operations (Add, Remove, Swap, Drop).

## Design Notes
1.  **ECS Architecture**:
    *   **Data**: `DynamicBuffer<InventorySlot>` stores items.
    *   **Capacity**: `InventoryCapacity` tracks weight/slots.
    *   **Versioning**: `InventoryVersion` tracks state changes for event-driven UI updates.
2.  **Network Architecture**:
    *   **Client**: Sends RPCs (`SwapItemRequest`, `DropItemRequest`).
    *   **Server**: Validates and executes logic. Updates Buffers and Version.
    *   **Replication**: Buffers and Components replicated via NetCode ghosts.

## Implemented Components

### InventoryComponents.cs
Location: `Assets/Scripts/Items/Components/InventoryComponents.cs`

| Component | Description |
|-----------|-------------|
| `InventorySlot` | Buffer Element `{ DefaultID, Count, Durability }`. |
| `InventoryCapacity` | `{ MaxSlots, MaxWeight, CurrentWeight }`. |
| `InventoryVersion` | `{ Value }` - Incremented on any change. |
| `DroppedItem` | Tag for world entities. |
| `GlobalItemConfig` | Singleton for Drop Prefab ref. |

## Implemented Systems

### Server Systems
Location: `Assets/Scripts/Items/Systems/`

1.  **ItemPickupSystem**:
    - Trigger logic. Adds `DroppedItem` to `InventorySlot` buffer.
    - Increments `InventoryVersion`.
2.  **DropItemSystem**:
    - Processes `DropItemRequest`. Removes from buffer. Spawns entity.
    - Increments `InventoryVersion`.
3.  **SwapItemSystem**:
    - Processes `SwapItemRequest`. Reorders buffer.
    - Increments `InventoryVersion`.

## Integration Guide

### 1. Player Setup
1.  Add `InventoryAuthoring` to Player Prefab.
    - **Slots**: 30.
    - **Max Weight**: 100.
2.  System automatically adds `InventoryVersion`.

### 2. Dropped Item Config
1.  Create `GlobalConfig` object in SubScene.
2.  Add `GlobalItemConfigAuthoring`. Assign a crate/box prefab.

## Testing
- **Pickup**: Walk over item. Buffer adds entry. Version increments.
- **Drop**: Press Key (UI required). Buffer removes entry. Entity spawns.
- **Stacking**: Pickup same item. Count increases.
