# Epic 11.7: Crafting System

**Priority**: MED
**Status**: **IMPLEMENTED** (Backend)
**Goal**: Allow players to combine items (Ingredients) into new items (Result).

## Implementation Details

### 1. Recipe Definitions
-   **Asset**: `RecipeDef` (ScriptableObject). Create in project `Create -> DIG -> Items -> Recipe`.
-   **Structure**:
    -   `Result`: ItemDef.
    -   `ResultCount`: int.
    -   `Ingredients`: List of {ItemDef, Count}.
-   **Registry**: `RecipeRegistry` loads all recipes from "Recipes" Resources folder.

### 2. Backend (Recipe Database)
-   **BlobAsset**: `RecipeDatabaseBlob` stores all recipes in Burst-friendly format.
-   **System**: `RecipeDatabaseSystem` bakes this at startup (after ItemRegistry).

### 3. Crafting Logic
-   **RPC**: `CraftItemRequest` { RecipeIndex, Amount }.
-   **System**: `CraftingSystem` (Server).
-   **Verification**: Checks strict ingredient availability in Player Inventory.
-   **Execution**: Consumes ingredients -> Adds result. Supports stacking.
-   **Optimization**: Updates `InventoryVersion` upon completion.

## Integration Guide: Connecting UI

### 1. Displaying Recipes
The UI needs to know available recipes.
Currently, `RecipeRegistry.GetAll()` (Managed) provides the full list of definitions.
Client UI can iterate this list to populate a crafting menu.
*Note: The Index in the list corresponds to `RecipeIndex` in the RPC.*

### 2. Requesting Craft
When user clicks "Craft" on Recipe at index `i`:
```csharp
var req = new CraftItemRequest {
    RecipeIndex = i,
    Amount = 1
};
SendRpc(req);
```
(See `InventoryPresenter.cs` for example of sending RPCs).

### 3. Visual Feedback
-   Inventory updates automatically (Items removed/added) via NetCode prediction.
-   Inventory Slots will flash/update as ingredients vanish and result appears.

## Tasks
- [x] Create `RecipeDef` SriptableObject and `Ingredient` struct.
- [x] Implement `RecipeRegistry` and `RecipeDatabaseSystem`.
- [x] Create `RecipeDatabase` Blob structure.
- [x] Implement `CraftingSystem` (Server).
- [ ] Create UI for Crafting. (Deferred).

## Acceptance Criteria
- [x] Can define recipes in Editor.
- [x] Can craft items if ingredients exist (Server validation).
- [x] Cannot craft if ingredients missing.
- [x] Ingredients consumed, Result added correct count.
