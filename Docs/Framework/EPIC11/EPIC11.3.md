# Epic 11.3: Inventory UI

**Priority**: HIGH
**Status**: **IMPLEMENTED**
**Goal**: Visualize the ECS Inventory Buffer using Unity UI Toolkit.

## Design Notes
1.  **UI Toolkit**: Uses `UIDocument`, `VisualElement`, and USS for styling.
2.  **Architecture**:
    *   **Presenter**: `InventoryPresenter.cs` connects ECS World to UI.
    *   **Optimization**: Monitors `InventoryVersion` component. Only rebuilds/refreshes UI when Version changes.
    *   **Input**: Handles clicks/drags and sends RPCs (Client -> Server).

## Implemented Components

### InventoryPresenter.cs
Location: `Assets/Scripts/UI/Inventory/InventoryPresenter.cs`

- **Responsibilities**:
    - Query `LocalPlayer`.
    - Read `InventorySlot` buffer.
    - Generate Grid of Slots.
    - Update Icons/Counts.
    - Handle Slot Clicks (Active Hotbar Selection).

### InventoryConnect.cs
- **Responsibilities**:
    - Bootstrap UI when Game starts.

## Integration Guide

### 1. UI Setup
1.  Create `UIDocument` in Scene. Assign `Inventory.uxml`.
2.  Add `InventoryPresenter` MonoBehaviour.
3.  Assign `UIDocument` reference.

### 2. Styling
- Edit `Assets/UI/Styles/Inventory.uss`.
- Classes: `.slot`, `.slot.selected`, `.item-icon`.

## Testing
1.  **Play**: Inventory Grid appears.
2.  **Interact**: Click slots. Selection highlight moves.
3.  **Updates**: Pickup item -> UI updates instantly.
