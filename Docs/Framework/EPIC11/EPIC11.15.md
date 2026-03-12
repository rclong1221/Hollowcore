# Epic 11.15: Crafting System & Temporary Inventory Sub-System UIs & Test Suite

**Priority**: HIGH
**Status**: ✅ **COMPLETE**
**Goal**: Create temporary UI components and Test Objects to validate Crafting, Storage, and Equipment systems.

---

## Quick Start Guide

### Step 1: Add PlayerInventoryAuthoring to Player Prefab

**This is REQUIRED for inventory to work!**

1. Open your **Player Prefab** (e.g., `Warrok_Server`).
2. Click `Add Component`.
3. Search for `PlayerInventoryAuthoring` and add it.
4. Configure:
   - **Inventory Size**: 30 (default)
   - **Max Weight**: 100 (default)
5. **Save the prefab**.

### Step 2: Add HUD Builders to Canvas

1. Find or create a `Canvas` in your scene.
2. Add these scripts to the Canvas:
   - `InventoryHUDBuilder` - Main inventory grid (Tab to toggle)
   - `CraftingHUDBuilder` - Shows recipes (always visible)
   - `EquipmentHUDBuilder` - Shows equipped items
   - `ContainerHUDBuilder` - Shows open container

### Step 3: Create Sample Recipes (One-Time Setup)

1. **Exit Play Mode** if running.
2. Go to `DIG > Create Sample Recipes` in Unity menu.
3. This creates 2 recipes in `Resources/Recipes/`:
   - **recipe.medkit** (Iron x2 + Stone x1 → Medkit)
   - **recipe.drill** (Iron x5 + Stone x3 → Drill)

### Step 4: Test in Play Mode

1. Press **Play**.
2. Press **Tab** to open inventory.
3. Go to `DIG > Test Objects > Give All Items`.
4. Items appear in the inventory grid!
5. Click a recipe in Crafting HUD - item is crafted!

---

## What Each Component Does

| Component | Purpose |
|-----------|---------|
| `PlayerInventoryAuthoring` | Bakes inventory into player prefab |
| `InventoryHUDBuilder` | Main inventory grid - Tab to toggle |
| `CraftingHUDBuilder` | Shows recipes, sends CraftItemRequest RPC |
| `EquipmentHUDBuilder` | Shows Head/Chest/Legs/Feet slots |
| `ContainerHUDBuilder` | Shows container contents when opened |
| `StorageContainerAuthoring` | Makes objects interactable storage |

---

## Test Objects (Editor Menu)

| Menu Path | Action |
|-----------|--------|
| `DIG/Test Objects/Spawn Chest` | Creates a Storage Container entity |
| `DIG/Test Objects/Give All Items` | Adds 5 of every item to player |
| `DIG/Test Objects/Complete Inventory Setup` | Runs both above |
| `DIG/Create Sample Recipes` | Creates medkit and drill recipes |

---

## Crafting System

When you click a recipe button:
1. `CraftingHUDBuilder` sends `CraftItemRequest` RPC to server
2. `CraftingSystem` (ServerWorld) processes the request:
   - Finds player entity by NetworkId
   - Validates ingredients in player's inventory
   - Consumes ingredients
   - Adds crafted item result
3. Inventory HUD updates automatically (via InventoryVersion)

**Requirements:**
- `RecipeDatabase` singleton must exist (created by `RecipeDatabaseSystem`)
- Player must have ingredients in inventory
- Recipes must be created via `DIG > Create Sample Recipes`

---

## Container Interaction

Storage containers are **interactable**! When you look at a container:
- `InteractionDetectionSystem` detects it via raycast
- Shows "Open Container" prompt
- Press `E` (Interact) to open
- `ContainerInteractionSystem` adds `OpenedContainer` to player
- `ContainerHUDBuilder` shows the container contents

### Components Added to Containers:
- `StorageContainer` - Tag
- `Interactable` - With `InteractionType.OpenContainer`
- `InventorySlot` buffer - Stores items
- `InventoryCapacity` - Max slots and weight

---

## Inventory Replication

The HUD intelligently selects the correct world:
1. **Tries ClientWorld first** - Uses `GhostOwnerIsLocal` to find your player
2. **Falls back to ServerWorld** - For editor single-player testing

If you see "Replication issue?" warning, ensure:
- `PlayerInventoryAuthoring` is on the player prefab
- The prefab is properly baked
- `InventorySlot` has `[GhostField]` attributes (already done)

---

## Files Created/Modified

| File | Purpose |
|------|---------|
| `Items/Authoring/PlayerInventoryAuthoring.cs` | Bakes inventory into player |
| `Items/Authoring/StorageContainerAuthoring.cs` | Makes containers interactable |
| `Items/Editor/RecipeCreator.cs` | Creates sample recipe assets |
| `Items/Systems/RecipeRegistry.cs` | Added Reset() for domain reload |
| `UI/Inventory/Debug/InventoryHUDBuilder.cs` | Self-building inventory grid |
| `UI/Inventory/Debug/CraftingHUDBuilder.cs` | Self-building crafting UI + RPC |
| `UI/Inventory/Debug/ContainerHUDBuilder.cs` | Self-building container UI |
| `UI/Inventory/Debug/EquipmentHUDBuilder.cs` | Self-building equipment UI |
| `Items/Editor/InventoryTestObjects.cs` | Editor test menu |
| `Shared/Interaction/InteractionComponents.cs` | Added `OpenContainer` type |

---

## Acceptance Criteria

- [x] Inventory HUD shows player items (Tab to toggle)
- [x] Equipment HUD shows equipment slots
- [x] Crafting HUD shows registered recipes
- [x] Crafting actually works (sends RPC, crafts item)
- [x] Container HUD shows when container opened
- [x] Test menu can spawn chests and give items
- [x] Containers are interactable via raycast
- [x] Recipes can be created via editor menu
- [x] RecipeRegistry reloads properly on domain reload
- [x] Works with NetCode (ClientWorld + ServerWorld)
