# Epic 11.4: Voxel Loot Integration

**Priority**: MEDIUM
**Status**: **IMPLEMENTED**
**Goal**: Connect Voxel destruction to Item Drops.

## Design Notes
1.  **Voxel -> Item Mapping**: Currently uses a "Default" table for all blocks. Future: Map VoxelTypeID to specific LootTables (Epic 8.5).
2.  **Loot System**: Leverages the core Loot System (Epic 11.12) logic.

## Implemented Systems

### VoxelLootSystem
Location: `Assets/Scripts/Items/Systems/VoxelLootSystem.cs`
- **Server**: Listens for `VoxelDestroyedEvent`.
- **Logic**:
    1.  Uses `ItemRegistry.DefaultLootTable` (ScriptableObject).
    2.  Executes Weighted Random Selection (standardized in Epic 11.12).
    3.  Spawns `DroppedItem` entity with velocity.

## Integration Guide

### 1. Configure Drops
1.  Edit `Assets/Resources/Loot/DefaultVoxelLoot` (LootTable asset).
2.  Add Entries (Item, Weight, Count).
    - e.g. Stone (Weight 100), Gold (Weight 5).

### 2. Events
- Ensure `VoxelModificationSystem` raises `VoxelDestroyedEvent` (it does via Epic 10).

## Relationship with 11.12
- **Epic 11.12** defined the generic Loot Structures (`LootTable`, `LootTableBlob`).
- **Epic 11.4** implements the specific connection to Voxel events.
