# EPIC 8: Core Voxel Engine (Base)

**Status**: 🔄 IN PROGRESS  
**Goal**: Build a robust, high-performance, multiplayer-ready Voxel Engine using Unity DOTS (ECS + Jobs + Burst).

This document serves as both the **Epic Tracking** sheet and the **Technical Documentation** for the `DIG.Voxel` system.

---

## Quick Start Guide

### 1. Scene Setup
To create a voxel world in a new scene:
1. Create a **SubScene** in your main scene (Right-click > New SubScene).
2. Create an empty GameObject inside the SubScene.
3. Add the `VoxelWorldAuthoring` component (System will auto-bootstrap).
4. Run the scene.

*(Note: Currently `ChunkSpawnerSystem` auto-spawns chunks around the camera for testing. In the future, `ChunkStreamingSystem` will handle this based on loading zones).*

### 2. Configuration
Core constants are defined in `VoxelConstants.cs`:
- **Chunk Size**: 32x32x32
- **Voxel Size**: 1.0 units
- **Iso Level**: 127 (Density > 127 is solid)

### 3. Visual Debugging
- Use **Entity Debugger** to view `Chunk` entities.
- Chunks are named `Chunk_{x}_{y}_{z}` (e.g., `Chunk_0_0_0`).
- Generated meshes are attached to GameObjects linked to these entities.

---

## Implemented Components (Epic 8.1)

### Core Data Structure
Location: `Assets/Scripts/Voxel/Core/`

| Component | Description |
|-----------|-------------|
| `VoxelData` | The BlobAsset containing voxel bytes (Density, Material). Memory efficient. |
| `ChunkVoxelData` | ECS Component holding the `BlobAssetReference<VoxelData>`. |
| `ChunkPosition` | ECS Component defining the chunk's integer grid coordinates. |
| `ChunkMeshState` | ECS Component tracking if the mesh is Dirty (needs rebuild) and current version. |
| `ChunkGameObject` | ECS Managed Component holding the reference to the visible `GameObject`. |

### Utility Classes
| Class | Description |
|-------|-------------|
| `CoordinateUtils` | Burst-compiled static methods for converting World <-> Chunk <-> Local coordinates. |
| `VoxelDensity` | Helper to calculate/modify density values (Gradient helpers). |
| `MarchingCubesTables` | Lookup tables for the meshing algorithm (Edges, Triangles). |

---

## Systems Architecture (Effectively Implemented)

### 1. Generation Pipeline (Epic 8.2)
**System**: `ChunkGenerationSystem`
- **Trigger**: Queries chunks with `ChunkVoxelData` where `IsValid == false`.
- **Action**: Schedules `GenerateVoxelDataJob`.
- **Job**: Calculates 3D gradient density (Ground at Y=0) + Simplex Noise.
- **Output**: Populates the `VoxelBlob`.

### 2. Meshing Pipeline (Epic 8.5)
**System**: `ChunkMeshingSystem`
- **Trigger**: Queries chunks with `ChunkNeedsRemesh` tag or `ChunkMeshState.IsDirty`.
- **Action**: 
    1. Prepares a padding density buffer (34x34x34).
    2. Runs `GenerateMarchingCubesMeshJob`.
    3. Updates the Unity `Mesh` object on the linked GameObject.
- **Output**: Visible 3D terrain.

### 3. Spatial Lookup
**System**: `ChunkLookupSystem`
- **Function**: Maintains a `NativeHashMap<int3, Entity>` of all loaded chunks.
- **Usage**: Allows other systems (like physics or gameplay) to find the chunk entity for a given world position in O(1).

---

## Integration Guide for Developers

### How to Modify Voxels (Planned/Draft)
To modify the world, you should not edit arrays directly. Use the Event system:
1. Create a `VoxelModificationRequest` entity.
2. Set `Position`, `Radius`, `TargetDensity`.
3. The `VoxelModificationSystem` will apply changes and trigger a Remesh.

### Reading Data
```csharp
// Example: Get density at a world position
float3 worldPos = ...;
int3 chunkPos = CoordinateUtils.WorldToChunkPos(worldPos);
int3 localPos = CoordinateUtils.WorldToLocalVoxelPos(worldPos);

if (ChunkLookup.TryGet(chunkPos, out Entity chunkEntity)) {
    var data = EntityManager.GetComponentData<ChunkVoxelData>(chunkEntity);
    byte density = data.Data.Value.Densities[CoordinateUtils.VoxelPosToIndex(localPos)];
}
```

---

## Sub-Epic Breakdown & Status

| Epic | Name | Priority | Status |
|------|------|----------|--------|
| **8.1** | **Core Data Structures** | CRITICAL | ✅ **COMPLETED** |
| **8.2** | **Chunk Generation** | CRITICAL | ✅ **COMPLETED** |
| 8.3 | Blocky Meshing | LOW | ⏭️ SKIPPED (Obsolete) |
| 8.4 | Blocky Collision | LOW | ⏭️ SKIPPED (Obsolete) |
| **8.5** | **Marching Cubes Meshing** | HIGH | ✅ **COMPLETED** |
| **8.6** | **Marching Cubes Collision** | MEDIUM | ✅ **COMPLETED** |
| **8.7** | **Voxel API & Modification** | HIGH | ✅ **COMPLETED (Verified)** |
| **8.8** | **Chunk Streaming** | HIGH | ✅ **COMPLETED** |
| **8.9** | **Basic Networking** | HIGH | ✅ **COMPLETED** |
| **8.10** | **Materials & Texturing** | MEDIUM | ✅ **COMPLETED** |
| 8.11 | Procedural Generation Base | LOW | 🔲 PENDING |
| **8.12** | **Seamless Meshing** | HIGH | ✅ **COMPLETED** |
| **8.13** | **Editor Tools** | HIGH | ✅ **COMPLETED** |
| **8.14** | **Memory & Cleanup** | HIGH | ✅ **COMPLETED** |
| **8.15** | **Loot & Bug Fixes** | MEDIUM | ✅ **COMPLETED** |
| **[8.16](EPIC8.16.md)** | **Quick Setup & Test Automation** | MEDIUM | ✅ **COMPLETED** |

**Total Completion**: ~95%

---

## Technical Specifications

| Feature | Spec |
|---------|------|
| **Voxel Format** | `Density` (byte) + `MaterialID` (byte) = 2 bytes/voxel |
| **Chunk Size** | 32 x 32 x 32 (32,768 voxels) |
| **Mesh Format** | Vertex Buffer (Position, Normal, Color, UV) |
| **Neighbor Sampling** | 34³ padded buffer with 6-neighbor lookup |
| **Collision** | DOTS MeshCollider + Unity MeshCollider (hybrid) |
| **Interaction** | DDA Raycast + Event-based Loot System |
| **LOD** | Currently Single LOD (Epic 9 adds Octrees) |
| **Memory** | ~64KB per chunk (Data) + Mesh Buffers |
| **Networking** | RPC-based Authoritative (Netcode) |
| **Stability** | Deferred Physics Disposal (Crash prevention) |

---

## Next Steps
1. **Epic 8.10 (Materials & Texturing)**: Deepen material system (texture arrays).
2. **Epic 8.11 (Terrain Generation)**: Add rudimentary biomes/caves beyond simple accumulation.
