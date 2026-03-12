# EPIC 8.13: Unity Editor Tools

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.1, 8.8  

---

## Overview

A suite of Unity Editor tools designed to accelerate development, debugging, and configuration of the DIG Voxel Engine. These tools provide visibility into the internal state of chunks, visualize streaming logic, and offer quick setup for testing.

---

## 🚀 Quick Start Guide

### For Designers (Level Design & Config)
1.  **Validate Project**: Run `DIG > Voxel > Validate Project Setup` to ensure layers and resources are correct.
2.  **Create Scene**: Run `DIG > Voxel > Create Test Scene` to scaffold a new voxel environment.
3.  **Debug Window**: Open `DIG > Voxel > Debug Window` to monitor chunk count and memory usage.
4.  **Edit Materials**: Select `VoxelMaterialRegistry` in Resources to add or modify block types.

### For Developers (Debugging)
1.  **Visualize Chunks**: In the Debug Window, enable "Draw Chunk Bounds" and "Draw Material Colors".
2.  **Inspect Chunks**: Select a `Chunk_X_Y_Z` object in the scene to see its mesh statistics in the Inspector.
3.  **Profiler**: Check the "Performance" foldout in the Debug Window to see average Generation and Meshing times (in ms).
4.  **Force Reload**: Click "Reload All Chunks" in the Debug Window to force a full mesh rebuild.

---

## 🛠️ Tools Breakdown

### 1. Voxel Debug Window
**Menu**: `DIG > Voxel > Debug Window`
**Script**: `Assets/Scripts/Voxel/Editor/VoxelDebugWindow.cs`

A central dashboard for monitoring the voxel engine.
*   **Chunk Statistics**: Real-time count of total chunks, chunks with data/mesh, and pending remeshes.
*   **Memory Usage**: Estimated VoxelBlob memory usage.
*   **Gizmo Settings**: Toggles for drawing chunk bounds, density points (slow!), and material colors.
*   **Performance**: Displays average execution time for `ChunkGenerationSystem` and `ChunkMeshingSystem` using `VoxelProfiler`.

### 2. Chunk Gizmo Visualizer
**Script**: `Assets/Scripts/Voxel/Editor/ChunkGizmoDrawer.cs`

Draws debug information in the Scene View.
*   **Blue Wireframe**: Chunk has valid Voxel Data.
*   **Green Wireframe**: Chunk has a generated Mesh.
*   **Yellow Wireframe**: Chunk is pending a remesh.
*   **Labels**: Shows chunk coordinates (X, Y, Z) at the center of each chunk.

### 3. Chunk Mesh Inspector
**Script**: `Assets/Scripts/Voxel/Editor/ChunkInspector.cs`

Custom Inspector for Chunk GameObjects.
*   Displays vertex count, triangle count, and mesh bounds.
*   Shows MeshCollider status.
*   Useful for verifying mesh density and optimization.

### 4. Test Scene Generator
**Menu**: `DIG > Voxel > Create Test Scene`
**Script**: `Assets/Scripts/Voxel/Editor/VoxelTestSceneSetup.cs`

Automates the creation of a functional test environment.
*   Creates directional light and camera.
*   Sets up the `ChunkMeshPool`.
*   Adds the `CollisionDebugVisualizer`.
*   Adds UI instructions.
*   Validates project settings (Layers, Resources).

### 5. Voxel Profiler
**Script**: `Assets/Scripts/Voxel/Debug/VoxelProfiler.cs`
**Usage**: `VoxelProfiler.BeginSample("Name")` / `EndSample("Name")`

A lightweight, dictionary-based profiler for tracking specific subsystem performance (Generation vs Meshing) without the overhead of the Unity Profiler deep profile.

---

## 🔧 Setup & Integration

### Project Validation
Run `DIG > Voxel > Validate Project Setup` to check for:
*   **Layers**: Existence of "Voxel" and "Player" layers.
*   **Resources**: Presence of `VoxelMaterialRegistry` and `StreamingConfig`.

### Adding New Tools
To add a new tool, create a script in `Assets/Scripts/Voxel/Editor` and add a `[MenuItem("DIG/Voxel/...")]`.

---

## ✅ Acceptance Criteria
- [x] Debug Window shows live chunk stats.
- [x] Chunk bounds visible in Scene view.
- [x] Test scene creates with all needed components.
- [x] Validate Setup catches missing layers/configs.
- [x] Performance timings logged via VoxelProfiler.
