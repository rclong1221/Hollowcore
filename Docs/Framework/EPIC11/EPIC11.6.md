# Epic 11.6: Storage Containers

**Priority**: HIGH
**Status**: **IMPLEMENTED** (Backend Logic + Interaction)
**Goal**: Enable players to store items in world objects (Chests, Crates, Lockers).

## Implementation Details

### 1. Storage Entity
-   **Structure**: `StorageContainer` (Tag) + `InventorySlot` (Buffer) + `InventoryCapacity`.
-   **Authoring**: `StorageContainerAuthoring` (MonoBehaviour). Use this to create Prefabs.

### 2. Interaction (Server Authoritative)
-   **System**: `ContainerInteractionSystem` (Server).
-   **Logic**:
    -   Detects `PlayerInput.Interact` events when looking at `StorageContainer`.
    -   Adds `OpenedContainer` component to the Player Entity.
    -   Auto-closes if distance > 5m or target invalid.
    -   Client observes `OpenedContainer` (Ghost Config) and triggers UI.

### 3. Transaction Logic
-   **RPC**: `MoveItemRequest` { SourceEntity, SourceSlot, TargetEntity, TargetSlot, Count }.
-   **System**: `InventoryTransactionSystem` (Server).
-   **Validation**:
    -   Ensures Player initiates the request.
    -   Verifies Player has access to Source Entity (Self or OpenedContainer).
    -   Performs swap/move operation if valid.
    -   **Versioning**: Increments `InventoryVersion` on *both* Source and Target entities to refresh UI.

## Integration Guide: Connecting UI

### 1. Detecting Open State (InventoryPresenter)
To show/hide the External Container panel:
```csharp
void Update() {
    var player = GetLocalPlayer();
    if (EntityManager.HasComponent<OpenedContainer>(player)) {
        var opened = EntityManager.GetComponent<OpenedContainer>(player);
        // Show UI for opened.ContainerEntity
        ContainerPanel.Show(opened.ContainerEntity); 
    } else {
        ContainerPanel.Hide();
    }
}
```

### 2. Moving Items (PointerDrag)
When Drag & Drop finishes:
```csharp
// Example: Moving from Player (Left) to Chest (Right)
var req = new MoveItemRequest {
    SourceEntity = LocalPlayerEntity,
    SourceSlot = draggedIdx,
    TargetEntity = isOpenContainer ? containerEntity : LocalPlayerEntity, // Dynamic target
    TargetSlot = dropIdx,
    Count = 0 // 0 Implies Swap/MoveAll
};
SendRpc(req);
```

### 3. Visuals
-   The UI simply renders the `DynamicBuffer<InventorySlot>` of the `OpenedContainer` entity just like it renders the Player's inventory.
-   Ghost Prediction ensures instant visual feedback if configured correctly.

## Tasks
- [x] Define `StorageContainer` and `OpenedContainer` components.
- [x] Implement `ContainerInteractionSystem` (Server) handling Open requests.
- [x] Implement `InventoryTransactionSystem` (Server) handling `MoveItemRequest`.
- [x] Create `StorageContainerAuthoring` script.
- [ ] Implement "Drop Contents on Destroy" logic. (Deferred to Cleanup/Health systems).

## Acceptance Criteria
- [x] Can place a Chest (via Authoring).
- [x] Can move items from Player -> Chest and Chest -> Player (via Transaction Logic).
- [x] Items persist in Chest when player leaves/returns (Ghost Replication).
- [ ] Destroying chest drops items. (Deferred).
