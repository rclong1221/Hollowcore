# EPIC 8.1: Core Data Structures

**Status**: ✅ COMPLETED  
**Priority**: CRITICAL  
**Dependencies**: None (foundational)
**Estimated Time**: 0.5 day

---

## Goal

Define the fundamental data structures for the voxel system. These must be:
- **Thread-safe** for Burst jobs
- **Memory-efficient** for large worlds
- **Simple** enough to debug
- **Support material IDs** for loot drops

---

## Key Insight: Gradient Density

The previous system failed because all voxels were binary (0 or 255).

**Marching Cubes needs gradients to generate surfaces!**

```
Binary (WRONG):           Gradient (CORRECT):
y=5:  0   0   0          y=5:   0    0    0
y=4:  0   0   0          y=4:  32   32   32
y=3:  0   0   0          y=3:  96   96   96    ← Surface will form here
y=2: 255 255 255         y=2: 160  160  160
y=1: 255 255 255         y=1: 224  224  224
y=0: 255 255 255         y=0: 255  255  255

IsoLevel = 128
Surface forms where density crosses 128
```

---

## Tasks

### Checklist
- [x] **8.1.1**: Create Assembly Definition `DIG.Voxel.asmdef`
- [x] **8.1.2**: Define Constants in `VoxelConstants.cs`
- [x] **8.1.3**: Define `VoxelBlob` and `VoxelBlobBuilder`
- [x] **8.1.4**: Define `VoxelDensity` utility (Gradient support)
- [x] **8.1.5**: Define `CoordinateUtils` (Burst compatible)
- [x] **8.1.6**: Create ECS Components (`ChunkPosition`, `ChunkVoxelData`, etc.)
- [x] **8.1.7**: Create Folder Structure
- [x] **8.1.8**: Validate Coordinate Conversion Logic (Unit Tests)

### Task 8.1.1: Create Assembly Definition

**File**: `Assets/Scripts/Voxel/DIG.Voxel.asmdef`

```json
{
    "name": "DIG.Voxel",
    "rootNamespace": "DIG.Voxel",
    "references": [
        "Unity.Entities",
        "Unity.Collections",
        "Unity.Burst",
        "Unity.Mathematics",
        "Unity.Physics",
        "Unity.Transforms",
        "Unity.NetCode"
    ],
    "allowUnsafeCode": true
}
```

**Acceptance**: Project compiles without errors.

---

### Task 8.1.2: Define VoxelConstants

**File**: `Assets/Scripts/Voxel/Core/VoxelConstants.cs`

```csharp
namespace DIG.Voxel.Core
{
    public static class VoxelConstants
    {
        // Chunk dimensions
        public const int CHUNK_SIZE = 32;
        public const int CHUNK_SIZE_SQ = CHUNK_SIZE * CHUNK_SIZE;
        public const int VOXELS_PER_CHUNK = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE; // 32,768
        
        // World scale
        public const float VOXEL_SIZE = 1.0f;
        public const float CHUNK_SIZE_WORLD = CHUNK_SIZE * VOXEL_SIZE;
        
        // Density values
        public const byte DENSITY_AIR = 0;
        public const byte DENSITY_SURFACE = 128;  // IsoLevel for Marching Cubes
        public const byte DENSITY_SOLID = 255;
        
        // Gradient configuration
        public const float GRADIENT_WIDTH = 2.0f;  // Voxels over which density transitions
        
        // Material IDs
        public const byte MATERIAL_AIR = 0;
        public const byte MATERIAL_STONE = 1;
        public const byte MATERIAL_DIRT = 2;
        public const byte MATERIAL_IRON_ORE = 3;
        public const byte MATERIAL_GOLD_ORE = 4;
        public const byte MATERIAL_COPPER_ORE = 5;
        // Add more as needed
    }
}
```

---

### Task 8.1.3: Define VoxelData Blob Structure

**File**: `Assets/Scripts/Voxel/Core/VoxelData.cs`

```csharp
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Blob structure for voxel data. Immutable once created.
    /// Each voxel has a density (for surface generation) and material (for loot).
    /// </summary>
    public struct VoxelBlob
    {
        public BlobArray<byte> Densities;   // 32,768 bytes
        public BlobArray<byte> Materials;   // 32,768 bytes
    }
    
    /// <summary>
    /// Helper class to create VoxelBlob assets.
    /// </summary>
    public static class VoxelBlobBuilder
    {
        public static BlobAssetReference<VoxelBlob> Create(
            NativeArray<byte> densities, 
            NativeArray<byte> materials)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VoxelBlob>();
            
            // Copy densities
            var densityArray = builder.Allocate(ref root.Densities, densities.Length);
            for (int i = 0; i < densities.Length; i++)
                densityArray[i] = densities[i];
            
            // Copy materials
            var materialArray = builder.Allocate(ref root.Materials, materials.Length);
            for (int i = 0; i < materials.Length; i++)
                materialArray[i] = materials[i];
            
            return builder.CreateBlobAssetReference<VoxelBlob>(Allocator.Persistent);
        }
        
        public static BlobAssetReference<VoxelBlob> CreateEmpty()
        {
            var densities = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            var materials = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            
            // All air
            for (int i = 0; i < densities.Length; i++)
            {
                densities[i] = VoxelConstants.DENSITY_AIR;
                materials[i] = VoxelConstants.MATERIAL_AIR;
            }
            
            var result = Create(densities, materials);
            densities.Dispose();
            materials.Dispose();
            return result;
        }
    }
}
```

---

### Task 8.1.4: Define VoxelDensity Utility

**File**: `Assets/Scripts/Voxel/Core/VoxelDensity.cs`

```csharp
using Unity.Burst;
using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Utility for calculating gradient density values.
    /// This is CRITICAL for Marching Cubes to work!
    /// </summary>
    [BurstCompile]
    public static class VoxelDensity
    {
        /// <summary>
        /// Calculate density based on signed distance to surface.
        /// Positive = below surface (solid)
        /// Negative = above surface (air)
        /// </summary>
        [BurstCompile]
        public static byte CalculateGradient(float signedDistance)
        {
            // Gradient width determines how smooth the transition is
            float halfWidth = VoxelConstants.GRADIENT_WIDTH * 0.5f;
            
            // Clamp to gradient range
            float normalized = math.clamp(signedDistance / halfWidth, -1f, 1f);
            
            // Map from [-1, 1] to [0, 255]
            // -1 (far above surface) → 0 (air)
            // 0 (at surface) → 128 (IsoLevel)
            // +1 (far below surface) → 255 (solid)
            float density = (normalized + 1f) * 0.5f * 255f;
            
            return (byte)math.clamp(density, 0f, 255f);
        }
        
        /// <summary>
        /// Check if density represents solid material.
        /// </summary>
        [BurstCompile]
        public static bool IsSolid(byte density)
        {
            return density > VoxelConstants.DENSITY_SURFACE;
        }
        
        /// <summary>
        /// Check if density represents air.
        /// </summary>
        [BurstCompile]
        public static bool IsAir(byte density)
        {
            return density <= VoxelConstants.DENSITY_SURFACE;
        }
    }
}
```

---

### Task 8.1.5: Define Coordinate Utilities

**File**: `Assets/Scripts/Voxel/Core/CoordinateUtils.cs`

```csharp
using Unity.Burst;
using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    [BurstCompile]
    public static class CoordinateUtils
    {
        /// <summary>
        /// Convert 3D local position to flat array index.
        /// </summary>
        [BurstCompile]
        public static int VoxelPosToIndex(int3 localPos)
        {
            return localPos.x + 
                   localPos.y * VoxelConstants.CHUNK_SIZE + 
                   localPos.z * VoxelConstants.CHUNK_SIZE_SQ;
        }
        
        /// <summary>
        /// Convert flat array index to 3D local position.
        /// </summary>
        [BurstCompile]
        public static int3 IndexToVoxelPos(int index)
        {
            int z = index / VoxelConstants.CHUNK_SIZE_SQ;
            int remainder = index % VoxelConstants.CHUNK_SIZE_SQ;
            int y = remainder / VoxelConstants.CHUNK_SIZE;
            int x = remainder % VoxelConstants.CHUNK_SIZE;
            return new int3(x, y, z);
        }
        
        /// <summary>
        /// Convert world position to chunk grid position.
        /// </summary>
        [BurstCompile]
        public static int3 WorldToChunkPos(float3 worldPos)
        {
            return new int3(
                (int)math.floor(worldPos.x / VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(worldPos.y / VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(worldPos.z / VoxelConstants.CHUNK_SIZE_WORLD)
            );
        }
        
        /// <summary>
        /// Convert chunk position to world origin (corner of chunk).
        /// </summary>
        [BurstCompile]
        public static float3 ChunkToWorldPos(int3 chunkPos)
        {
            return new float3(chunkPos) * VoxelConstants.CHUNK_SIZE_WORLD;
        }
        
        /// <summary>
        /// Convert world position to local voxel position within chunk.
        /// </summary>
        [BurstCompile]
        public static int3 WorldToLocalVoxelPos(float3 worldPos)
        {
            // Get chunk position
            int3 chunkPos = WorldToChunkPos(worldPos);
            
            // Get chunk world origin
            float3 chunkOrigin = ChunkToWorldPos(chunkPos);
            
            // Calculate local position
            float3 localFloat = (worldPos - chunkOrigin) / VoxelConstants.VOXEL_SIZE;
            
            return new int3(
                (int)math.floor(localFloat.x),
                (int)math.floor(localFloat.y),
                (int)math.floor(localFloat.z)
            );
        }
        
        /// <summary>
        /// Check if local position is within chunk bounds.
        /// </summary>
        [BurstCompile]
        public static bool IsInBounds(int3 localPos)
        {
            return localPos.x >= 0 && localPos.x < VoxelConstants.CHUNK_SIZE &&
                   localPos.y >= 0 && localPos.y < VoxelConstants.CHUNK_SIZE &&
                   localPos.z >= 0 && localPos.z < VoxelConstants.CHUNK_SIZE;
        }
    }
}
```

---

### Task 8.1.6: Define ECS Components

**File**: `Assets/Scripts/Voxel/Components/ChunkComponents.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// Grid position of the chunk (not world position).
    /// </summary>
    public struct ChunkPosition : IComponentData
    {
        public int3 Value;
    }
    
    /// <summary>
    /// Reference to the voxel data blob.
    /// </summary>
    public struct ChunkVoxelData : IComponentData
    {
        public BlobAssetReference<Core.VoxelBlob> Data;
        
        public bool IsValid => Data.IsCreated;
    }
    
    /// <summary>
    /// Tracks mesh generation state.
    /// </summary>
    public struct ChunkMeshState : IComponentData
    {
        public bool HasMesh;
        public bool IsDirty;
        public int VertexCount;
        public int TriangleCount;
    }
    
    /// <summary>
    /// Tracks collider state.
    /// </summary>
    public struct ChunkColliderState : IComponentData
    {
        public bool HasCollider;
        public bool IsActive;
    }
    
    /// <summary>
    /// Enableable tag for chunks needing mesh regeneration.
    /// </summary>
    public struct ChunkNeedsRemesh : IComponentData, IEnableableComponent { }
    
    /// <summary>
    /// Reference to the managed GameObject (mesh, collider).
    /// </summary>
    public class ChunkGameObject : IComponentData
    {
        public GameObject Value;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
    }
    
    /// <summary>
    /// References to 6 face-adjacent neighbor chunks.
    /// Used for boundary meshing.
    /// </summary>
    public struct ChunkNeighbors : IComponentData
    {
        public Entity NegX;
        public Entity PosX;
        public Entity NegY;
        public Entity PosY;
        public Entity NegZ;
        public Entity PosZ;
    }
}
```

---

---

## Implemented Components

### Core Data Structures
Location: `Assets/Scripts/Voxel/Core/`

| Component | Description | Usage |
|-----------|-------------|-------|
| `VoxelData` | BlobAsset Definition | Immutable layout of density/material bytes (32^3). |
| `VoxelConstants` | Static Configuration | Defines Chunk Size (32), IsoLevel (128), and Dimensions. |
| `VoxelDensity` | Burst Utility | Calculates 0-255 density from signed distance. |
| `CoordinateUtils` | Burst Utility | Converts World <-> Chunk <-> Local coordinates safely. |

### ECS Components
Location: `Assets/Scripts/Voxel/Components/ChunkComponents.cs`

| Component | Type | Description |
|-----------|------|-------------|
| `ChunkPosition` | `IComponentData` | Logical integer grid position (int3). |
| `ChunkVoxelData` | `IComponentData` | Holds `BlobAssetReference<VoxelData>`. |
| `ChunkMeshState` | `IComponentData` | Tracks dirty state and vertex counts. |
| `ChunkGameObject` | `IComponentData` (Managed) | Reference to the visual GameObject/MeshFilter. |
| `ChunkNeighbors` | `IComponentData` | Links to adjacent chunk entities (for seamless meshing). |

---

## Integration Guide

### 1. Scene Setup
1.  Create a **SubScene** in your main scene (Right-click > New SubScene).
2.  Create an Empty GameObject named `VoxelWorld`.
3.  Add the `VoxelWorldAuthoring` component (System bootstrapper).
4.  Reference your `VoxelWorldConfig` asset in the Authoring inspector.

### 2. Validating Components
When the scene runs:
1.  Open **Window > Analysis > Entity Debugger**.
2.  Select the `Chunk` entities (e.g. `Chunk_0_0_0`).
3.  Verify the `ChunkVoxelData` component is present.
4.  Confirm `Data.IsCreated` is `true`.

### 3. Designer Workflow
*   **Materials**: Designers should edit `VoxelConstants.cs` (or the specific Config asset) to define new Material IDs.
*   **Layers**: Ensure the `VoxelWorld` object is on a Layer that is visible to the main camera.


---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        Chunk Creation                           │
├─────────────────────────────────────────────────────────────────┤
│  ChunkSpawnerSystem                                             │
│    └─ Creates Entity (ChunkPosition)                            │
│                                                                 │
│  ChunkGenerationSystem                                          │
│    ├─ Allocates NativeArrays                                    │
│    ├─ Runs GenerateVoxelDataJob (Burst)                         │
│    └─ Creates BlobAssetReference (Immutable)                    │
│                                                                 │
│  ChunkMeshingSystem                                             │
│    └─ Reads Blob -> Generates Mesh                              │
└─────────────────────────────────────────────────────────────────┘
```

---

## Testing

1.  **Unit Tests**:
    *   Open `Assets/Scripts/Voxel/Tests/CoordinateTests.cs`.
    *   Run in Test Runner.
    *   **Verify**: All conversions (World->Chunk, World->Local) pass.

2.  **Runtime Debug**:
    *   Play Scene.
    *   Open **Entity Debugger**.
    *   Select a `Chunk` Entity.
    *   **Verify**: `ChunkVoxelData` component exists and `Data.IsCreated` is true.

---

## Acceptance Criteria

- [x] Project compiles with new assembly
- [x] All coordinate conversions are mathematically correct
- [x] Gradient density produces values 0-255 with 128 at surface
- [x] Blob creation and disposal works without leaks
- [x] All code is Burst-compatible (no managed types in jobs)


| File | Description |
|------|-------------|
| `Assets/Scripts/Voxel/DIG.Voxel.asmdef` | Assembly Definition |
| `Assets/Scripts/Voxel/Core/VoxelConstants.cs` | core constants |
| `Assets/Scripts/Voxel/Core/VoxelData.cs` | Blob structure and builder |
| `Assets/Scripts/Voxel/Core/VoxelDensity.cs` | Gradient calculation utility |
| `Assets/Scripts/Voxel/Core/CoordinateUtils.cs` | Coordinate math helper |
| `Assets/Scripts/Voxel/Components/ChunkComponents.cs` | All ECS components |
| `Assets/Scripts/Voxel/Tests/CoordinateTests.cs` | Unit tests |

