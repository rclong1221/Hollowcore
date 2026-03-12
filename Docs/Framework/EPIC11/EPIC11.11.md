# Epic 11.11: Action Bar Backend

**Priority**: HIGH
**Status**: **IMPLEMENTED**
**Goal**: Backend systems for managing the hotbar/action bar selection and input.

## Design Notes
1.  **Input Driven**:
    *   `HotbarInputSystem` listens for keys `1` through `0` (Alpha1-Alpha0).
    *   Sends `SetHotbarSlotRequest` RPC.
2.  **Server Authority**:
    *   Server validates request (0-9 range).
    *   Updates `ActiveHotbarSlot` component.
    *   This triggers `HeldItemSystem` (Epic 11.10) to update the actual held item.

## Implemented Components

### InventoryComponents.cs
Location: `Assets/Scripts/Items/Components/InventoryComponents.cs`

| Component | Description |
|-----------|-------------|
| `SetHotbarSlotRequest` | RPC Command (SlotIndex). |
| `ActiveHotbarSlot` | Stores current selection index. |

## Implemented Systems

### HotbarInputSystem
Location: `Assets/Scripts/Items/Systems/HotbarInputSystem.cs`
- **Client**: Reads `Unity.InputSystem` Keyboard.
- Creates `SetHotbarSlotRequest` entity targeting Server Connection.

### HotbarSystem
Location: `Assets/Scripts/Items/Systems/HotbarSystem.cs`
- **Server**: Consumes `SetHotbarSlotRequest`.
- Validates index.
- Updates `ActiveHotbarSlot` on the Player entity owned by the sender.

## Integration Guide

### 1. Input Setup
- Ensure `Unity Input System` package is installed.
- Ensure `DIG.Items` assembly references `Unity.InputSystem`.

### 2. Player Setup
- `InventoryAuthoring` adds `ActiveHotbarSlot` (Default 0).

## Testing
1.  **Play Mode**: Start Client + Server.
2.  **Input**: Press '1', '2', '3'.
3.  **Inspect**: Select Player Entity in Entity Debugger.
    - Check `ActiveHotbarSlot`. It should match input-1 (0, 1, 2).
4.  **Verify**: Log "Server: Set hotbar to X".
