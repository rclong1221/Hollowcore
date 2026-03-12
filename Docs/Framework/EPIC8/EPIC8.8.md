# EPIC 8.8: Chunk Streaming

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.2 (Generation)

---

## Overview

The **Chunk Streaming System** dynamically loads and unloads voxel chunks based on the player's position. It enables an "infinite" world experience by ensuring that only the relevant chunks around the user are in memory and processing.

## Components

### 1. `ChunkStreamingSystem`
**Location**: `Assets/Scripts/Voxel/Systems/Generation/ChunkStreamingSystem.cs`

This system replaces the static `ChunkSpawnerSystem`. It runs every frame (throttled) to:
*   **Track Viewer**: Finds the `Main Camera` or Editor `SceneView Camera`.
*   **Calculate Visibility**: Determines which chunks should be visible based on `VIEW_DISTANCE`.
*   **Unload**: Identifies chunks outside `UNLOAD_DISTANCE` and destroys them (disposing of resources).
*   **Load**: Identifies missing chunks within `VIEW_DISTANCE` and spawns them.

### 2. Configuration (`ChunkStreamingSystem.cs`)
Currently hardcoded constants (move to `VoxelConfig` singleton in future):
*   `VIEW_DISTANCE = 4`: Radius of chunks to keep loaded (approx 128m).
*   `UNLOAD_DISTANCE = 6`: Radius to unload (hysteresis prevents flickering).
*   `CHUNKS_TO_PROCESS_PER_FRAME = 2`: Limits operations to prevent fps drops.

---

## Quick Start Guide

### For Designers
1.  **Open Scene**: Open the Voxel SubScene.
2.  **Play Mode**: Enter Play Mode.
3.  **Move**: Fly around with the camera.
    *   **Observation**: You will see new chunks generating in the distance and old chunks disappearing behind you (if you look back and check the Entity Debugger).
    *   **Fog**: Ideally, set Unity Fog to match the `VIEW_DISTANCE` (~120m) to hide the "pop-in" effect.

### For Developers
1.  **Debug View**: Open **Window > Analysis > Entity Debugger**.
2.  **Filter**: Search for `ChunkStreamingSystem`.
3.  **Inspect**: Watch the `_loadedChunks` HashMap count. It should stabilize around `(2*R+1)^2 * Depth` chunks (approx 400).

---

## Setup & Integration

### Changing View Distance
Modify the constants in `ChunkStreamingSystem.cs`:
```csharp
private const int VIEW_DISTANCE = 8; // Increase for farther visibility (impacts performance)
private const int UNLOAD_DISTANCE = 10;
```

### Adding Loading Screens
If you need a loading screen (e.g., initial spawn):
1.  Check `_loadedChunks.Count`.
2.  Wait until it reaches a target threshold (e.g., center 3x3 chunks ready).
3.  Fade out loading screen.

### Handling Teleportation
If the player teleports far away:
1.  The system will automatically detect the huge distance.
2.  It will unload **all** current chunks (over multiple frames).
3.  It will start loading the new area.
*Tip*: For instant teleport, you might want to call a "Reset" method on the system to clear the hashmap immediately, though the current iterative approach works fine, just takes a few seconds to flush.

---

## Deferred Tasks & Optimization

### Completed
*   **Dynamic Loading**: Helper "Spiral" algorithm loads closest chunks first.
*   **Dynamic Unloading**: Distance check removes far chunks.
*   **Resource Cleanup**: `UnloadChunk` manually disposes `VoxelBlob` and destroys the managed `GameObject`.

### Outstanding (Epic 9)
*   **Async Job Completion**: Currently, generation jobs might complete synchronously if pushed hard.
*   **Memory Pooling**: We allocate new `NativeArrays` for every chunk generation. A pool would reduce GC pressure.
*   **LOD**: Distant chunks are full resolution. Epic 9.2 will introduce Lower Level of Detail meshes.

