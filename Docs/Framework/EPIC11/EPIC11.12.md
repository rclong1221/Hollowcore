# Epic 11.12: Loot Tables & Drops

**Priority**: MEDIUM
**Status**: **IMPLEMENTED**
**Goal**: Implement data-driven weighted loot generation for enemies and blocks.

## Design Notes
1.  **Weighted Random**:
    *   Loot tables define a list of items with relative weights.
    *   Algorithm sums weights and rolls a random number `[0, TotalWeight)`.
2.  **Blob Storage**:
    *   Tables are baked into `BlobAssets` for efficient, pointer-chasing access in Burst jobs.
3.  **Entity Integration**:
    *   Entities (Mobs) hold a `LootSource` component pointing to the Blob.
    *   On Death/Destruction, a `GenerateLootRequest` is spawned.

## Implemented Components

### LootComponents.cs
Location: `Assets/Scripts/Items/Loot/LootComponents.cs`

| Component | Description |
|-----------|-------------|
| `LootSource` | Holds `BlobAssetReference<LootTableBlob>`. |
| `GenerateLootRequest` | Request to spawn drops at a position from a source entity. |
| `LootRandom` | Singleton holding random state. |

### LootTableBlob.cs
Location: `Assets/Scripts/Items/Loot/LootTableBlob.cs`
- `BlobArray<LootEntryBlob>`: (ItemID, Weight, Min, Max).

## Implemented Systems

### LootGenerationSystem
Location: `Assets/Scripts/Items/Systems/LootGenerationSystem.cs`
- **Server**: Consumes `GenerateLootRequest`.
- Reads `LootSource`.
- Performs Weighted Random Roll.
- Instantiates `GlobalItemConfig.GenericDropPrefab`.
- Sets `DroppedItem` data and `PhysicsVelocity`.

### VoxelLootSystem
Location: `Assets/Scripts/Items/Systems/VoxelLootSystem.cs`
- **Server**: Listens for `VoxelDestroyedEvent`.
- Uses `ItemRegistry.DefaultLootTable` (ScriptableObject) for now.
- Spawns drops.

## Authoring Components

### LootTableAuthoring
Location: `Assets/Scripts/Items/Loot/LootTableAuthoring.cs`
- **Input**: `LootTable` ScriptableObject.
- **Bake**: Converts SO to Blob → `LootSource`.

## Integration Guide

### 1. Creating a Loot Table
1.  Right Click Project > `Create > DIG > Items > Loot Table`.
2.  Add Entries:
    - **Item**: Select ItemDef (e.g., OreIron).
    - **Weight**: 100 (Common), 10 (Rare).
    - **Count**: Min-Max.

### 2. Mob Setup
1.  Add `LootTableAuthoring` to Enemy Prefab.
2.  Assign the Loot Table asset.

### 3. Voxel Setup
1.  Currently uses `Resources/Loot/DefaultVoxelLoot`. Update this asset to change global block drops.

## Testing
1.  **Voxel**: Drill a block. Verify `DroppedItem` spawns.
2.  **Logic**: Check `VoxelLootSystem` logs or entity debugger to see drop count and ID logic correctness.
