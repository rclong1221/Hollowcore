# Epic 20: Persistence (Save/Load)

**Priority**: CRITICAL
**Goal**: Persist player inventories and world containers to disk so progress is not lost on server restart.

## Architecture

### 1. Serialization Format (JSON vs Binary)
-   **Binary**: Faster, smaller. Recommended for buffers.
-   **Structure**:
    ```json
    {
       "player_uids": {
          "user_123": [
             { "id": 1, "count": 10, "durability": 100 },
             { "id": 5, "count": 1, "meta": 999 }
          ]
       },
       "containers": {
          "chest_coords_10_5_10": [ ... ]
       }
    }
    ```

### 2. Save System
-   **Trigger**: Auto-save (Timer) or Manual (Admin).
-   **System**: `InventorySaveSystem` (Server).
    -   Iterate all `InventorySlot` buffers.
    -   Map `NetCode.GhostOwner` -> `UserUID`.
    -   Serialize to disk (`world_save/inventory.dat`).

### 3. Load System
-   **Trigger**: Player Join.
-   **Logic**:
    -   On `NetworkConnection`, lookup UserUID.
    -   If Save exists, populate `InventorySlot` buffer.
    -   If New User, give Starter Kit (Epic 11.4).

## Tasks
- [ ] Define Save Data Models (C# Class).
- [ ] Implement `InventorySaveSystem`.
- [ ] Implement `InventoryLoadSystem` (On Player Spawn).
- [ ] Implement "Starter Kit" logic for new variables.

## Acceptance Criteria
- [ ] Player drops an item, leaves server, restarts server, joins -> Inventory is same.
- [ ] Player picks up item, restart -> Item still in inventory.
