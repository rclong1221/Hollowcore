# Epic 11.10: Handheld Item Integration

**Priority**: HIGH
**Status**: **IMPLEMENTED**
**Goal**: Visualize the currently active item in the player's hands and drive tool logic.

## Design Notes
1.  **State Synchronization**:
    *   Server tracks `ActiveHotbarSlot`.
    *   Updates `ItemDatabaseBlob` and `InventorySlot` to determine **ItemID**.
    *   Replicates `HeldItem` component (ItemID) to all clients.
2.  **Visuals**:
    *   **Hybrid Approach**: Uses `HeldItemVisualSystem` (Client-Side) to instantiate `GameObject` prefabs from `ItemDef.WorldPrefab`.
    *   **Bone Attachment**: Parents visual to `RightHand` bone defined in `HeldItemAuthoring`.
3.  **Tool Logic**:
    *   Specific tools (Drill) have dedicated systems (`DrillToolIntegrationSystem`) that read `HeldItem` to enable/disable specific logic.

## Implemented Components

### InventoryComponents.cs
Location: `Assets/Scripts/Items/Components/InventoryComponents.cs`

| Component | Description |
|-----------|-------------|
| `ActiveHotbarSlot` | Which slot (0-9) is selected. Server Authoritative. |
| `HeldItem` | (Derived) The Item ID currently held. Replicated to all. |

## Implemented Systems

### HeldItemSystem
Location: `Assets/Scripts/Items/Systems/HeldItemSystem.cs`
- **Server**: Reads `ActiveHotbarSlot` and `InventoryBuffer`.
- Updates `HeldItem` component with current Item ID and Count.

### HeldItemVisualSystem
Location: `Assets/Scripts/Items/Systems/HeldItemVisualSystem.cs`
- **Client**: Monitors `HeldItem` changes.
- Instantiates/Destroys Visual GameObjects.
- Attaches to Hand Bone.

### DrillToolIntegrationSystem
Location: `Assets/Scripts/Items/Systems/DrillToolIntegrationSystem.cs`
- **Client/Server**: Checks if `HeldItem` is a Drill.
- Enables `DrillTool` component logic (mining rays).

## Authoring Components

### InventoryAuthoring (Updated)
Location: `Assets/Scripts/Items/Authoring/InventoryAuthoring.cs`
- Adds `ActiveHotbarSlot` and `HeldItem` components.

### HeldItemAuthoring
Location: `Assets/Scripts/Items/Authoring/HeldItemAuthoring.cs`
- **Refs**: `HandBone` (Transform), `ItemRoot` (Transform).
- Configures where items appear.

## Integration Guide

### 1. Player Setup
1.  Ensure `InventoryAuthoring` is on the Player Prefab.
2.  Add `HeldItemAuthoring` (Managed) to the **Client Presentation** or **Hybrid root**.
    - Assign `HandBone` (e.g., Mixamorig:RightHand).
3.  Ensure `ItemRegistry` has valid `ItemDef` assets with `WorldPrefab` assigned.

### 2. Item Setup
1.  Create `ToolItemDef` (Drill). Assign a Prefab (e.g., DrillModel).
2.  Add to `Resources/Items`.
3.  Run game.

## Testing
1.  **Selection**: Press 1-9 to switch slots.
2.  **Verify**: Log shows "Switched to Slot X".
3.  **Visuals**: If slot has item, Prefab appears in hand.
4.  **Network**: Connect a second client. Visuals should replicate.
