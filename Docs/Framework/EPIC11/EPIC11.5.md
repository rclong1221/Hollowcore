# Epic 11.5: Inventory Optimization

**Priority**: LOW (Originally) -> HIGH (Addressed)
**Status**: **IMPLEMENTED**
**Goal**: Reduce overhead of Inventory UI polling and large buffer replication.

## Design Notes
1.  **Event-Driven UI**:
    - Problem: UI was polling `DynamicBuffer` every frame.
    - Solution: `InventoryVersion` component. UI only updates when Version != CachedVersion.
    - Result: UI update cost drops to 0 when idle.
2.  **Blob Data**:
    - Item Definitions moved to `ItemDatabaseBlob` for Burst compatibility (Epic 11.1).

## Implemented Components
- `InventoryVersion` (in `InventoryComponents.cs`).
- Tracks: `int Value`.

## Integration
- Added automatically by `InventoryAuthoring` to Player.
- Utilized by `InventoryPresenter` in `Update()`:
  ```csharp
  if (version.Value != _cachedVersion) { RebuildUI(); }
  ```

## Verification
- Profiling shows UI update time is 0ms when idle.
