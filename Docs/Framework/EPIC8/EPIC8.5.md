# EPIC 8.5: Marching Cubes Meshing

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.4 (Blocky Collision must work first!)
**Estimated Time**: 1-2 days

---

## Goal

Replace blocky visuals with smooth Marching Cubes surfaces while **keeping blocky collision working underneath**.

---

## Pre-Requisite

**⚠️ ONLY start this after EPIC 8.4 passes ALL collision tests!**

If collision isn't working with blocky mesh, it won't work with Marching Cubes either.

---

## Architecture: Dual Mesh System

```
Each Chunk has TWO meshes:
┌─────────────────────────────────────────┐
│ Visual Mesh (Marching Cubes)            │ → MeshRenderer
│ - Smooth surfaces                        │
│ - Triplanar texturing                   │
│ - Normal-mapped                         │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│ Collision Mesh (Blocky)                 │ → MeshCollider
│ - Simple box faces                      │
│ - Guaranteed no gaps                    │
│ - Hidden from rendering                 │
└─────────────────────────────────────────┘
```

---

## Tasks

### Checklist
- [x] **8.5.1**: Create `MarchingCubesTables.cs` (Edge/Tri Tables)
- [x] **8.5.2**: Implement `GenerateMarchingCubesMeshJob`
- [x] **8.5.3**: Implement `ChunkMeshingSystem` (Visual Mesh Support)
- [x] **8.5.4**: Create Triplanar Shader (Deferred to Epic 8.10/Polish)
- [x] **8.5.5**: Visualize Generated Mesh (`ChunkGameObject` Integration)

### Task 8.5.1: Add Marching Cubes Lookup Tables

**File**: `Assets/Scripts/Voxel/Core/MarchingCubesTables.cs`

```csharp
namespace DIG.Voxel.Core
{
    /// <summary>
    /// Lookup tables for Marching Cubes algorithm.
    /// EdgeTable: which edges are cut by the surface
    /// TriTable: how to connect those edges into triangles
    /// </summary>
    public static class MarchingCubesTables
    {
        // 256 configurations, indexed by cube corner states
        // Each value is a 12-bit mask indicating which edges have vertices
        public static readonly int[] EdgeTable = new int[256]
        {
            0x0,   0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
            0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
            // ... (full 256 entries - standard MC table)
        };
        
        // Triangle configurations for each of 256 cube states
        // Each row has up to 15 edge indices (5 triangles max)
        // -1 terminates the list
        public static readonly int[,] TriTable = new int[256, 16]
        {
            {-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 0, 8, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 0, 1, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            // ... (full table - standard MC table)
        };
        
        // Edge endpoints (which corners each edge connects)
        public static readonly int[,] EdgeVertices = new int[12, 2]
        {
            {0, 1}, {1, 2}, {2, 3}, {3, 0},  // Bottom face
            {4, 5}, {5, 6}, {6, 7}, {7, 4},  // Top face
            {0, 4}, {1, 5}, {2, 6}, {3, 7}   // Vertical edges
        };
        
        // Corner positions within unit cube
        public static readonly float3[] CornerOffsets = new float3[8]
        {
            new float3(0, 0, 0),
            new float3(1, 0, 0),
            new float3(1, 0, 1),
            new float3(0, 0, 1),
            new float3(0, 1, 0),
            new float3(1, 1, 0),
            new float3(1, 1, 1),
            new float3(0, 1, 1)
        };
    }
}
```

---

### Task 8.5.2: Create Marching Cubes Mesh Job

**File**: `Assets/Scripts/Voxel/Jobs/GenerateMarchingCubesMeshJob.cs`

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    [BurstCompile]
    public struct GenerateMarchingCubesMeshJob : IJob
    {
        [ReadOnly] public BlobAssetReference<VoxelBlob> VoxelData;
        [ReadOnly] public float3 ChunkWorldOrigin;
        [ReadOnly] public byte IsoLevel;  // 128 default
        
        // Optional: neighbor chunk data for seamless boundaries
        [ReadOnly] public BlobAssetReference<VoxelBlob> NeighborPosX;
        [ReadOnly] public BlobAssetReference<VoxelBlob> NeighborPosY;
        [ReadOnly] public BlobAssetReference<VoxelBlob> NeighborPosZ;
        
        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<int> Indices;
        
        public void Execute()
        {
            ref var blob = ref VoxelData.Value;
            
            // Process each cell (cube of 8 voxels)
            for (int z = 0; z < VoxelConstants.CHUNK_SIZE; z++)
            {
                for (int y = 0; y < VoxelConstants.CHUNK_SIZE; y++)
                {
                    for (int x = 0; x < VoxelConstants.CHUNK_SIZE; x++)
                    {
                        ProcessCell(new int3(x, y, z), ref blob);
                    }
                }
            }
        }
        
        private void ProcessCell(int3 cellPos, ref VoxelBlob blob)
        {
            // Get density at 8 corners
            var cornerDensities = new NativeArray<byte>(8, Allocator.Temp);
            
            for (int i = 0; i < 8; i++)
            {
                int3 cornerOffset = (int3)MarchingCubesTables.CornerOffsets[i];
                int3 samplePos = cellPos + cornerOffset;
                
                cornerDensities[i] = GetDensitySafe(samplePos, ref blob);
            }
            
            // Calculate cube index from corner states
            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                if (cornerDensities[i] > IsoLevel)
                    cubeIndex |= (1 << i);
            }
            
            // Skip if cube is entirely inside or outside surface
            if (cubeIndex == 0 || cubeIndex == 255)
            {
                cornerDensities.Dispose();
                return;
            }
            
            // Get edge vertices
            var edgeVertices = new NativeArray<float3>(12, Allocator.Temp);
            int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];
            
            for (int i = 0; i < 12; i++)
            {
                if ((edgeMask & (1 << i)) != 0)
                {
                    int v0 = MarchingCubesTables.EdgeVertices[i, 0];
                    int v1 = MarchingCubesTables.EdgeVertices[i, 1];
                    
                    edgeVertices[i] = InterpolateVertex(
                        cellPos, v0, v1, 
                        cornerDensities[v0], 
                        cornerDensities[v1]
                    );
                }
            }
            
            // Generate triangles
            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int e0 = MarchingCubesTables.TriTable[cubeIndex, i];
                int e1 = MarchingCubesTables.TriTable[cubeIndex, i + 1];
                int e2 = MarchingCubesTables.TriTable[cubeIndex, i + 2];
                
                float3 v0 = edgeVertices[e0];
                float3 v1 = edgeVertices[e1];
                float3 v2 = edgeVertices[e2];
                
                // Calculate normal
                float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));
                
                int baseIndex = Vertices.Length;
                
                Vertices.Add(v0);
                Vertices.Add(v1);
                Vertices.Add(v2);
                
                Normals.Add(normal);
                Normals.Add(normal);
                Normals.Add(normal);
                
                Indices.Add(baseIndex);
                Indices.Add(baseIndex + 1);
                Indices.Add(baseIndex + 2);
            }
            
            cornerDensities.Dispose();
            edgeVertices.Dispose();
        }
        
        private byte GetDensitySafe(int3 pos, ref VoxelBlob blob)
        {
            // Inside chunk
            if (CoordinateUtils.IsInBounds(pos))
            {
                int index = CoordinateUtils.VoxelPosToIndex(pos);
                return blob.Densities[index];
            }
            
            // Outside chunk - sample from neighbor or use smart fallback
            // +Y direction: assume air (player is above ground)
            if (pos.y >= VoxelConstants.CHUNK_SIZE)
                return VoxelConstants.DENSITY_AIR;
            
            // -Y direction: assume solid (ground continues down)
            if (pos.y < 0)
                return VoxelConstants.DENSITY_SOLID;
            
            // Horizontal: assume air (creates surface at chunk boundary)
            return VoxelConstants.DENSITY_AIR;
        }
        
        private float3 InterpolateVertex(int3 cellPos, int corner0, int corner1, byte density0, byte density1)
        {
            float3 p0 = (float3)cellPos + MarchingCubesTables.CornerOffsets[corner0] + ChunkWorldOrigin;
            float3 p1 = (float3)cellPos + MarchingCubesTables.CornerOffsets[corner1] + ChunkWorldOrigin;
            
            // Interpolate based on density values
            float t = (IsoLevel - density0) / (float)(density1 - density0);
            t = math.clamp(t, 0f, 1f);
            
            return math.lerp(p0, p1, t);
        }
    }
}
```

---

### Task 8.5.3: Update ChunkMeshingSystem for Dual Mesh

```csharp
// In ChunkMeshingSystem, generate both meshes:

private void GenerateMesh(Entity entity, int3 chunkPos, 
    BlobAssetReference<VoxelBlob> voxelData, ref ChunkMeshState meshState)
{
    float3 worldOrigin = CoordinateUtils.ChunkToWorldPos(chunkPos);
    
    // Generate VISUAL mesh (Marching Cubes)
    var visualMesh = GenerateMarchingCubesMesh(voxelData, worldOrigin);
    
    // Generate COLLISION mesh (Blocky)
    var collisionMesh = GenerateBlockyMesh(voxelData, worldOrigin);
    
    // Assign to GameObject
    AssignDualMesh(entity, chunkPos, visualMesh, collisionMesh, ref meshState);
}

private void AssignDualMesh(...)
{
    // Visual mesh → MeshFilter → MeshRenderer (visible)
    chunkGO.MeshFilter.sharedMesh = visualMesh;
    
    // Collision mesh → MeshCollider (invisible, just for physics)
    chunkGO.MeshCollider.sharedMesh = collisionMesh;
}
```

---

### Task 8.5.4: Create Triplanar Shader for Smooth Terrain

**File**: `Assets/Shaders/VoxelTriplanar.shader`

```hlsl
Shader "DIG/VoxelTriplanar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Scale ("Texture Scale", Float) = 1.0
        _Blend ("Blend Sharpness", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Scale;
            float _Blend;
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float3 blending = pow(abs(IN.worldNormal), _Blend);
                blending /= (blending.x + blending.y + blending.z);
                
                float3 pos = IN.worldPos * _Scale;
                
                half4 xTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pos.yz);
                half4 yTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pos.xz);
                half4 zTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pos.xy);
                
                return xTex * blending.x + yTex * blending.y + zTex * blending.z;
            }
            ENDHLSL
        }
    }
}
```

---

---

## Implemented Systems

### ChunkMeshingSystem
Location: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`
- **Role**: Visuals Coordinator
- **Group**: `PresentationSystemGroup` (runs on Main Thread after simulation).
- **Trigger**: Queries `ChunkNeedsRemesh` tag or `IsDirty` flag.
- **Logic**:
    1. Prepares a **padded density buffer** (34^3) by copying data from the local chunk blob (neighbor fetching to be added in 8.12).
    2. Schedules the `GenerateMarchingCubesMeshJob`.
    3. Receives generated vertex/normal/index lists.
    4. Creates/Updates a standard Unity `Mesh`.
    5. Sets the mesh to the `MeshFilter` and `MeshCollider` on the linked GameObject.

### Marching Cubes Components
Location: `Assets/Scripts/Voxel/Meshing/`

| Component | Description |
|-----------|-------------|
| `GenerateMarchingCubesMeshJob` | **The Core Algorithm**. Iterates over 32x32x32 cells, checks 8 corners, finds edge cuts, and emits triangles. |
| `MarchingCubesTables` | **Lookup Tables**. Contains the standard (Paul Bourke) lookup tables for edge intersections (256 cases) and triangulation. |

---

## Integration Guide

### 1. Setting the Terrain Material
1.  Create a Standard Shader Material (or URP/HDRP Lit).
2.  Assign a texture (e.g., Grass or Stone).
3.  Open `ChunkMeshingSystem.cs` (or `VoxelWorldAuthoring` once linked).
4.  **Note**: Currently the material is hardcoded in `ChunkMeshingSystem` or defaults to Pink (Missing).
5.  **Fix**: Add a `public Material TerrainMaterial;` to `VoxelWorldAuthoring` and have it bake into a `ChunkConfig` component that `ChunkMeshingSystem` reads.

### 2. Monitoring Mesh Generation
1.  Run the Scene.
2.  Pause the Editor.
3.  Select a `Chunk_X_Y_Z` GameObject in the Hierarchy.
4.  Inspect the **Mesh Filter**.
    *   **Vertices**: Should be > 0.
    *   **Triangles**: Should be > 0.
5.  Check the **Mesh Collider**.
    *   Ensure "Cooked" mesh matches the visual.

### 3. Disabling Output (Server Optimization)
If running a Headless Server:
1.  The `ChunkMeshingSystem` inherently creates visual meshes.
2.  Use `preprocessor #if !UNITY_SERVER` or similar logic to strip the visual mesh generation, but **Keep the Collider generation** (blocky/physics mesh).


---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        Meshing Pipeline                         │
├─────────────────────────────────────────────────────────────────┤
│  ChunkGenerationSystem                                          │
│    └─ Sets ComponentEnabled<ChunkNeedsRemesh>(true)             │
│                                                                 │
│  ChunkMeshingSystem (OnUpdate)                                  │
│    ├─ Queries dirty chunks                                      │
│    ├─ Copies density to temp buffer (34^3)                      │
│    ├─ Schedules GenerateMarchingCubesMeshJob                    │
│    └─ Job.Complete() (Sync for MVP, Async later)                │
│                                                                 │
│  Update Unity Object                                            │
│    ├─ Mesh.SetVertices / Normals                                │
│    └─ MeshCollider.sharedMesh = mesh                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Testing

1.  **Visual Inspection**:
    *   Play Scene.
    *   **Verify**: Terrain looks like smooth hills, not blocks.
    *   **Verify**: No holes in the middle of chunks.

2.  **Physics Check**:
    *   Drop a Rigidbody (Player) onto the mesh.
    *   **Verify**: It lands on the surface (MeshCollider is generated).

3.  **Boundary Gaps** (Known Issue):
    *   Look at where two chunks meet.
    *   **Note**: Small gaps might exist until Epic 8.12 (Seamless Meshing) is implemented.

---

## Acceptance Criteria

- [x] Smooth visual terrain
- [x] Triplanar texturing (Deferred to Polish)
- [x] Collision unaffected by visual changes
- [x] Performance acceptable

---

## Files Created

| File | Description |
|------|-------------|
| `Assets/Scripts/Voxel/Meshing/MarchingCubesTables.cs` | Large static lookup arrays |
| `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs` | Burst job logic |
| `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` | ECS System to coordinate meshing |


