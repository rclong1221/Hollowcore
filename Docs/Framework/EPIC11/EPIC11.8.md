# Epic 11.8: Modding Support & Tooling

**Priority**: LOW (Post-Launch)
**Status**: **IMPLEMENTED** (Item Loading)
**Goal**: Allow external modification of Items via JSON.

## Implementation Details

### 1. Mod Loader
-   **Class**: `ModLoader` (Static managed class).
-   **Path**: `StreamingAssets/Mods` (scans all subdirectories).
-   **Format**: `*.item.json`.
-   **Lifecycle**: Called by `ItemRegistry` during Initialization (Before ECS World creation).

### 2. JSON Structure
File: `sword.item.json`
```json
{
  "id": "mod.my_item",
  "displayName": "My Modded Item",
  "maxStack": 64,
  "weight": 1.5,
  "iconPath": "texture.png"
}
```

### 3. Registry Integration
-   `ItemRegistry` invokes `ModLoader.LoadItems()`.
-   ModLoader parses JSON -> Creates runtime `ItemDef` instance (ScriptableObject instance).
-   Loads texture (if present) -> Creates Sprite -> Assigns to `Icon`.
-   Adds to Registry.
-   **Note**: Prefabs are currently NOT supported for modded items (requires Addressables). Modded items use default fallback visuals.

## Integration Guide

### 1. Creating a Mod
1.  Navigate to `Assets/StreamingAssets/Mods` (Create if missing).
2.  Create a folder `MyMod`.
3.  Add `myitem.item.json`.

### 2. Testing
1.  Run Game. 
2.  Console logs: `[ModLoader] Loaded mod item: mod.my_item`.
3.  Use Command/Debug Menu to give yourself item `mod.my_item`.

## Tasks
- [x] Create `ItemJson` DTO.
- [x] Implement `ModLoader` for Items.
- [x] Update `ItemRegistry` to include mod items.
- [x] Implement Recipe JSON loading.
- [ ] Implement Voxel Texture loading (Deferred).
