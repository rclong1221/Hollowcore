# Epic 11.1: Item Database & Definitions

**Priority**: CRITICAL
**Status**: **IMPLEMENTED**
**Goal**: Define "Item" data structures and enable efficient lookup via Managed (Editor) and Unmanaged (Burst) systems.

## Design Notes
1.  **Dual Representation**:
    *   **Authoring (Managed)**: `ItemDef` ScriptableObjects allow easy editing in Unity Project.
    *   **Runtime (Unmanaged)**: `ItemDatabaseBlob` stores Flattened Data (ID -> Int, Name, Stats) for high-performance ECS job access.
    *   **Registry (Hybrid)**: `ItemRegistry` (Static Managed) bridges string IDs to int IDs.
2.  **Blob Baking**:
    *   `ItemDatabaseSystem` runs on startup, reads all `ItemDef` assets, and bakes them into a `ItemDatabaseBlob`.

## Implemented Components/Assets

### 1. Item Definitions
Location: `Assets/Scripts/Items/Definitions/`

| Class | Description | Fields |
|-------|-------------|--------|
| `ItemDef` | Base SO. | ID, DisplayName, Icon, MaxStack, Weight, Prefab |
| `ToolItemDef` | Drills/Tools. | MiningPower, Durability |
| `ConsumableItemDef` | Medkits/Food. | RestoreHealth, RestoreOxygen |
| `EquipmentItemDef` | Armor/Suits. | EquipSlot, ArmorValue |

### 2. Runtime Systems
Location: `Assets/Scripts/Items/Systems/`

*   **ItemRegistry**: Static helper. `Get(int id)`, `GetId(string id)`.
*   **ItemDatabaseSystem**: `InitializationSystem`. Creates `ItemDatabase` singleton entity containing `BlobAssetReference<ItemDatabaseBlob>`.

### 3. Blob Data
Location: `Assets/Scripts/Items/Components/ItemDatabaseBlob.cs`

```csharp
struct ItemBlobData {
    FixedString64Bytes DisplayName;
    float Weight;
    int MaxStack;
    float MiningPower;
    int RestoreHealth;
    // ...
}
```

## Integration Guide

### 1. Creating a New Item
1.  **Project View**: `Create -> DIG -> Items -> [Type]`.
2.  **Configure**: Set ID (e.g., `ore.iron`), Name, Stack Size.
3.  **Register**: The system auto-discovers assets in `Resources/Items` (or via ModLoader).

### 2. Accessing Item Data in ECS (Burst Job)
```csharp
var itemDb = SystemAPI.GetSingleton<ItemDatabase>(); // Passed to job
ref var blob = ref itemDb.BlobRef.Value;
var itemData = blob.Items[itemId];
float power = itemData.MiningPower;
```

### 3. Modding Support
- See **Epic 11.8**: External JSON files are loaded and injected into `ItemRegistry` and the Blob during initialization.

## Testing
- **Verify Init**: Run game. Check Console for `[ItemRegistry] Initialized with X items`.
- **Entity Debugger**: Check `ItemDatabase` singleton exists.
