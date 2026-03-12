# Epic 9.6: Material Definition Workflow

**Status**: ✅ COMPLETE
**Priority**: HIGH  
**Dependencies**: EPIC 8.10 (Materials & Textures), EPIC 9.1 (Visual Refinement)  
**Estimated Time**: 2 days

---

## Goal

Create a streamlined workflow for defining voxel materials that:
- **Auto-detects** texture types from filenames
- **Validates** all required fields before save
- **Previews** material in 3D
- **Generates** loot prefabs automatically
- **Batch creates** materials from texture folders
- **Integrates** with `VoxelVisualMaterial` from Epic 9.1

> **Integration Note**: The workflow now creates both `VoxelMaterialDefinition` (Gameplay) and `VoxelVisualMaterial` (Rendering) assets, linking them automatically.

---

## Quick Start Guide

### Creating a New Material (Wizard)
1. Go to **Tools > DIG > Voxel > Material Creator Wizard**.
2. **Step 1: Basic Info**: Enter Name (e.g., "Basalt") and a unique ID (e.g., 20).
3. **Step 2: Textures**: Drag and drop your texture maps (Albedo, Normal, Height, etc.) into the drop zone. The tool auto-assigns them based on suffixes (e.g., `_albedo`, `_nrm`).
4. **Step 3: Properties**: Set Hardness (mining time) and Visual properties (Smoothness/Metallic).
5. **Step 4: Loot**: Keep "Generate Loot Prefab" checked to auto-create a mineable drop.
6. **Step 5: Review**: Click **Create Material**.
7. **Result**: Assets are created in `Assets/Content/VoxelMaterials/Basalt/`. The material is automatically added to the `VoxelMaterialRegistry`.

### Batch Importing Materials
1. Organize your textures into folders, e.g., `Materials/Stone/`, `Materials/Dirt/`.
2. Go to **Tools > DIG > Voxel > Batch Material Import**.
3. Drag the parent folder (e.g., `Materials`) into the "Root Folder" field.
4. Set the **Starting ID** (auto-increments for each new material).
5. Click **Import X Materials**.
6. **Result**: All valid folders are converted into complete material assets.

---

## Tool 1: Voxel Material Creator Wizard

One-stop shop for creating new voxel materials. Handles both gameplay logic and visual definition.

**File**: `Assets/Scripts/Voxel/Editor/VoxelMaterialWizard.cs`

### Features
- **Auto-Assign**: Drag-and-drop logic detects `_albedo`, `_normal`, `_height`, `_detail` suffixes.
- **Validation**: Prevents creation if ID is duplicate or textures are missing.
- **Loot Generation**: Automatically creates a prefab with Mesh, Material, Collider, and Rigidbody.
- **Registry Integration**: Automatically registers the new material.

---

## Tool 2: Batch Material Importer

Import multiple materials from organized texture folders.

**File**: `Assets/Scripts/Voxel/Editor/BatchMaterialImporter.cs`

### Features
- **Folder Scanning**: Recursively finds subfolders containing textures.
- **Smart ID Assignment**: Skips IDs already used in the Registry.
- **Error Handling**: Skips invalid folders (e.g., missing Albedo) without stopping the batch.

---

## Component Reference

### `VoxelMaterialDefinition` (`ScriptableObject`)
Defines the **Gameplay** properties of a voxel type.
- **MaterialID**: Unique byte identifier (0-255).
- **Hardness**: Time in seconds to mine.
- **LootPrefab**: Object spawned when destroyed.
- **VisualMaterial**: Link to the `VoxelVisualMaterial` asset.

### `VoxelVisualMaterial` (`ScriptableObject`)
Defines the **Rendering** properties (Epic 9.1).
- **Textures**: Albedo, Normal, Height, Detail maps.
- **Surface**: Smoothness, Metallic, Tint.
- **DisplayName**: UI-friendly name.

---

## Acceptance Criteria

- [x] Wizard guides through complete material creation
- [x] Textures auto-assigned by filename
- [x] Loot prefabs generated automatically
- [x] Batch import works for multiple folders
- [x] Integrated with `VoxelMaterialRegistry` and `VoxelVisualMaterial`

- [ ] Material IDs validated for uniqueness
- [ ] Preview shows material appearance
