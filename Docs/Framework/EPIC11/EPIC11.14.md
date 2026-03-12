# Epic 11.14: Advanced Inventory UI & Optimization

**Priority**: MED
**Status**: **IMPLEMENTED**
**Goal**: Polish the inventory experience with Context Menus, Splitting, and Event-Driven updates.

## Design Notes
1.  **Optimization**: Moved from Polling (Every Frame) to Event-Driven (`InventoryVersion`).
2.  **Context Menu**: Right-click on slots opens a menu (Use, Split, Drop).
    - **Use**: Triggers item consumption.
    - **Split**: Splits stack (default 1).
    - **Drop**: Drops item to world.

## Implemented Components

### InventoryPresenter.cs
Location: `Assets/Scripts/UI/Inventory/InventoryPresenter.cs`
- **Features**:
    - `InventoryVersion` checking (Optimization).
    - `ContextMenuPanel` overlay management.
    - RPC Dispatchers (`RequestUse`, `RequestSplit`, `RequestDropItem`).

### SplitItemSystem
Location: `Assets/Scripts/Items/Systems/SplitItemSystem.cs`
- **Logic**:
    - Validates source slot has items.
    - Finds empty slot.
    - Moves `Amount` from Source to New Slot.
    - Increments `InventoryVersion`.

## Integration Guide

### 1. UI Setup
1.  Open `InventoryCanvas` prefab.
2.  Create a Panel `ContextMenu` (Image + VLayout).
3.  Add 3 Buttons: Use, Split, Drop.
4.  Assign references to `InventoryPresenter` inspector:
    - `ContextMenuPanel`
    - `BtnUse`, `BtnSplit`, `BtnDrop`

### 2. Testing
1.  **Split**: Right click Stack -> Split. Watch it move to empty slot.
2.  **Use**: Right click Food -> Use. Watch Health increase.
3.  **Drop**: Right Click -> Drop. Watch item spawn in world.
