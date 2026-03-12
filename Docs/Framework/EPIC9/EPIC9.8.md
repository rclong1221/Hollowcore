# Epic 9.8: Chunk Loading Visualization

**Status**: ✅ COMPLETE
**Priority**: MEDIUM  
**Dependencies**: EPIC 8.8 (Chunk Streaming), EPIC 9.2 (LOD System)  
**Estimated Time**: 1 day

---

## Goal

Visualize chunk streaming in real-time to debug loading issues:
- Chunk loading/unloading states
- **LOD levels per chunk** (integrates with 9.2)
- Memory usage
- Streaming priorities

---

## Quick Start Guide

1.  **AddComponent**: Add `StreamingVisualizer` to any GameObject in your scene (e.g., a "DebugTools" object).
2.  **Play Mode**: Enter Play Mode.
3.  **Toggle**: Press **F8** to toggle the on-screen overlay.
4.  **Scene View**: Enable Gizmos in the Scene View to see color-coded boxes representing loaded chunks and their LOD levels.

---

## Component Reference

### StreamingVisualizer (MonoBehaviour)

**Location**: `Assets/Scripts/Voxel/Debug/StreamingVisualizer.cs`

Main entry point for visualization.

**Properties**:
*   **Show Overlay**: Toggle the game view UI.
*   **Toggle Key**: Default `F8`.
*   **Show Gizmos**: Toggle scene view wireframes.
*   **Draw Chunk Labels**: Shows coordinates and LOD level as text in Scene View (can be cluttered).
*   **LOD Colors**: Customize colors for LOD 0-3.

### VoxelStreamingStats (IComponentData)

**Location**: `Assets/Scripts/Voxel/Components/VoxelStreamingStats.cs`

Singleton component updated by `ChunkStreamingSystem` containing:
*   `LoadedChunks`: Count of currently active chunk entities.
*   `ChunksToSpawnQueue`: Number of chunks queued for generation.
*   `ChunksToUnloadQueue`: Number of chunks queued for unloading.
*   `EstimatedMemoryMB`: Rough estimate of voxel data usage.

---

## Integration Guide for Developers

If you want to access streaming stats in your own tools:

```csharp
// In an ECS System:
if (SystemAPI.HasSingleton<VoxelStreamingStats>())
{
    var stats = SystemAPI.GetSingleton<VoxelStreamingStats>();
    Debug.Log($"Loaded Chunks: {stats.LoadedChunks}");
}

// In a MonoBehaviour:
var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
var query = entityManager.CreateEntityQuery(typeof(VoxelStreamingStats));
if (query.HasSingleton<VoxelStreamingStats>())
{
    var stats = query.GetSingleton<VoxelStreamingStats>();
}
```

---

## Acceptance Criteria

- [x] Overlay visible with hotkey (F8)
- [x] Chunk states clearly color-coded in Scene View (LOD levels)
- [x] Memory usage displayed in UI
- [x] Scene view shows same visualization (Gizmos)
