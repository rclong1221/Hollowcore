# EPIC 8.3: Blocky Meshing (Visual Test)

**Status**: ⏭️ SKIPPED (Obsolete)  
**Priority**: CRITICAL  
**Dependencies**: EPIC 8.2 (Chunk Generation)
**Estimated Time**: 1 day

---

## Goal

Generate simple cube-based meshes to:
1. **Verify voxel data is correct**
2. **See the terrain** before adding collision
3. **Establish mesh generation pipeline**

This is NOT the final visual - it's a debugging step.

---

## Why Blocky First?

| Reason | Benefit |
|--------|---------|
| Simpler algorithm | Fewer bugs |
| Exact 1:1 with voxels | Easy to validate |
| No edge interpolation | No boundary issues |
| Fast to implement | Quick feedback |

After blocky works, we add Marching Cubes (8.5).

---

## Tasks

### Task 8.3.1: Create GenerateBlockyMeshJob

**File**: `Assets/Scripts/Voxel/Jobs/GenerateBlockyMeshJob.cs`

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    /// <summary>
    /// Generates blocky (Minecraft-style) mesh from voxel data.
    /// Only creates faces between solid and air voxels.
    /// </summary>
    [BurstCompile]
    public struct GenerateBlockyMeshJob : IJob
    {
        [ReadOnly] public BlobAssetReference<VoxelBlob> VoxelData;
        [ReadOnly] public float3 ChunkWorldOrigin;
        
        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<float2> UVs;
        public NativeList<int> Indices;
        
        // For loot system: track material at each vertex
        public NativeList<byte> VertexMaterials;
        
        public void Execute()
        {
            ref var blob = ref VoxelData.Value;
            
            for (int z = 0; z < VoxelConstants.CHUNK_SIZE; z++)
            {
                for (int y = 0; y < VoxelConstants.CHUNK_SIZE; y++)
                {
                    for (int x = 0; x < VoxelConstants.CHUNK_SIZE; x++)
                    {
                        int3 pos = new int3(x, y, z);
                        int index = CoordinateUtils.VoxelPosToIndex(pos);
                        
                        byte density = blob.Densities[index];
                        if (!VoxelDensity.IsSolid(density)) continue;
                        
                        byte material = blob.Materials[index];
                        
                        // Check each face
                        if (ShouldRenderFace(pos, new int3(0, 1, 0)))
                            AddFace(pos, FaceDirection.Top, material);
                        
                        if (ShouldRenderFace(pos, new int3(0, -1, 0)))
                            AddFace(pos, FaceDirection.Bottom, material);
                        
                        if (ShouldRenderFace(pos, new int3(1, 0, 0)))
                            AddFace(pos, FaceDirection.Right, material);
                        
                        if (ShouldRenderFace(pos, new int3(-1, 0, 0)))
                            AddFace(pos, FaceDirection.Left, material);
                        
                        if (ShouldRenderFace(pos, new int3(0, 0, 1)))
                            AddFace(pos, FaceDirection.Front, material);
                        
                        if (ShouldRenderFace(pos, new int3(0, 0, -1)))
                            AddFace(pos, FaceDirection.Back, material);
                    }
                }
            }
        }
        
        private bool ShouldRenderFace(int3 pos, int3 direction)
        {
            int3 neighborPos = pos + direction;
            
            // If neighbor is outside chunk, render face (for now)
            if (!CoordinateUtils.IsInBounds(neighborPos))
                return true;
            
            int neighborIndex = CoordinateUtils.VoxelPosToIndex(neighborPos);
            ref var blob = ref VoxelData.Value;
            byte neighborDensity = blob.Densities[neighborIndex];
            
            // Render face if neighbor is air
            return VoxelDensity.IsAir(neighborDensity);
        }
        
        private enum FaceDirection { Top, Bottom, Left, Right, Front, Back }
        
        private void AddFace(int3 voxelPos, FaceDirection face, byte material)
        {
            float3 p = (float3)voxelPos + ChunkWorldOrigin;
            int startIndex = Vertices.Length;
            
            float3 normal;
            float3 v0, v1, v2, v3;
            
            switch (face)
            {
                case FaceDirection.Top:
                    normal = new float3(0, 1, 0);
                    v0 = p + new float3(0, 1, 0);
                    v1 = p + new float3(1, 1, 0);
                    v2 = p + new float3(1, 1, 1);
                    v3 = p + new float3(0, 1, 1);
                    break;
                    
                case FaceDirection.Bottom:
                    normal = new float3(0, -1, 0);
                    v0 = p + new float3(0, 0, 1);
                    v1 = p + new float3(1, 0, 1);
                    v2 = p + new float3(1, 0, 0);
                    v3 = p + new float3(0, 0, 0);
                    break;
                    
                case FaceDirection.Right:
                    normal = new float3(1, 0, 0);
                    v0 = p + new float3(1, 0, 0);
                    v1 = p + new float3(1, 0, 1);
                    v2 = p + new float3(1, 1, 1);
                    v3 = p + new float3(1, 1, 0);
                    break;
                    
                case FaceDirection.Left:
                    normal = new float3(-1, 0, 0);
                    v0 = p + new float3(0, 0, 1);
                    v1 = p + new float3(0, 0, 0);
                    v2 = p + new float3(0, 1, 0);
                    v3 = p + new float3(0, 1, 1);
                    break;
                    
                case FaceDirection.Front:
                    normal = new float3(0, 0, 1);
                    v0 = p + new float3(0, 0, 1);
                    v1 = p + new float3(0, 1, 1);
                    v2 = p + new float3(1, 1, 1);
                    v3 = p + new float3(1, 0, 1);
                    break;
                    
                case FaceDirection.Back:
                default:
                    normal = new float3(0, 0, -1);
                    v0 = p + new float3(1, 0, 0);
                    v1 = p + new float3(1, 1, 0);
                    v2 = p + new float3(0, 1, 0);
                    v3 = p + new float3(0, 0, 0);
                    break;
            }
            
            // Add vertices
            Vertices.Add(v0);
            Vertices.Add(v1);
            Vertices.Add(v2);
            Vertices.Add(v3);
            
            // Add normals
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            
            // Add UVs
            UVs.Add(new float2(0, 0));
            UVs.Add(new float2(0, 1));
            UVs.Add(new float2(1, 1));
            UVs.Add(new float2(1, 0));
            
            // Add materials (for loot)
            VertexMaterials.Add(material);
            VertexMaterials.Add(material);
            VertexMaterials.Add(material);
            VertexMaterials.Add(material);
            
            // Add indices (two triangles)
            Indices.Add(startIndex + 0);
            Indices.Add(startIndex + 1);
            Indices.Add(startIndex + 2);
            
            Indices.Add(startIndex + 0);
            Indices.Add(startIndex + 2);
            Indices.Add(startIndex + 3);
        }
    }
}
```

---

### Task 8.3.2: Create ChunkMeshPool

**File**: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshPool.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Voxel.Systems.Meshing
{
    /// <summary>
    /// Pool of GameObjects with MeshFilter, MeshRenderer, and MeshCollider.
    /// </summary>
    public class ChunkMeshPool : MonoBehaviour
    {
        private static ChunkMeshPool _instance;
        
        [SerializeField] private Material _voxelMaterial;
        [SerializeField] private int _initialPoolSize = 64;
        
        private Stack<GameObject> _pool;
        private int _createdCount = 0;
        
        private void Awake()
        {
            _instance = this;
            _pool = new Stack<GameObject>(_initialPoolSize);
            
            // Pre-populate pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                var go = CreateChunkGameObject();
                go.SetActive(false);
                _pool.Push(go);
            }
        }
        
        public static GameObject Get()
        {
            if (_instance == null)
            {
                Debug.LogError("[Voxel] ChunkMeshPool not initialized!");
                return null;
            }
            
            GameObject go;
            if (_instance._pool.Count > 0)
            {
                go = _instance._pool.Pop();
            }
            else
            {
                go = _instance.CreateChunkGameObject();
            }
            
            go.SetActive(true);
            return go;
        }
        
        public static void Return(GameObject go)
        {
            if (_instance == null || go == null) return;
            
            go.SetActive(false);
            
            // Clear mesh to prevent stale data
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshFilter.sharedMesh.Clear();
            }
            
            // Disable collider
            var meshCollider = go.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.enabled = false;
                meshCollider.sharedMesh = null;
            }
            
            _instance._pool.Push(go);
        }
        
        private GameObject CreateChunkGameObject()
        {
            _createdCount++;
            var go = new GameObject($"Chunk_{_createdCount}");
            go.transform.SetParent(transform);
            
            // Add components
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _voxelMaterial;
            
            var collider = go.AddComponent<MeshCollider>();
            collider.convex = false;  // Required for concave terrain
            collider.enabled = false; // Disabled until mesh is assigned
            
            // Add to Voxel layer
            go.layer = LayerMask.NameToLayer("Voxel");
            
            return go;
        }
    }
}
```

---

### Task 8.3.3: Create ChunkMeshingSystem

**File**: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Jobs;

namespace DIG.Voxel.Systems.Meshing
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChunkMeshingSystem : SystemBase
    {
        private const int MAX_MESHES_PER_FRAME = 4;
        
        protected override void OnUpdate()
        {
            int meshed = 0;
            
            foreach (var (position, voxelData, meshState, entity) in 
                SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxelData>, RefRW<ChunkMeshState>>()
                    .WithAll<ChunkNeedsRemesh>()
                    .WithEntityAccess())
            {
                if (meshed >= MAX_MESHES_PER_FRAME) break;
                if (!voxelData.ValueRO.IsValid) continue;
                
                GenerateMesh(entity, position.ValueRO.Value, voxelData.ValueRO.Data, ref meshState.ValueRW);
                
                EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(entity, false);
                meshed++;
            }
        }
        
        private void GenerateMesh(Entity entity, int3 chunkPos, 
            BlobAssetReference<VoxelBlob> voxelData, ref ChunkMeshState meshState)
        {
            float3 worldOrigin = CoordinateUtils.ChunkToWorldPos(chunkPos);
            
            // Allocate buffers
            var vertices = new NativeList<float3>(4096, Allocator.TempJob);
            var normals = new NativeList<float3>(4096, Allocator.TempJob);
            var uvs = new NativeList<float2>(4096, Allocator.TempJob);
            var indices = new NativeList<int>(8192, Allocator.TempJob);
            var materials = new NativeList<byte>(4096, Allocator.TempJob);
            
            // Run mesh generation job
            var job = new GenerateBlockyMeshJob
            {
                VoxelData = voxelData,
                ChunkWorldOrigin = worldOrigin,
                Vertices = vertices,
                Normals = normals,
                UVs = uvs,
                Indices = indices,
                VertexMaterials = materials
            };
            
            job.Schedule().Complete();
            
            // Create Unity Mesh
            if (vertices.Length > 0)
            {
                CreateUnityMesh(entity, chunkPos, vertices, normals, uvs, indices, ref meshState);
            }
            else
            {
                Debug.Log($"[Voxel] Chunk {chunkPos} generated empty mesh (all air or all solid)");
            }
            
            // Cleanup
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            indices.Dispose();
            materials.Dispose();
        }
        
        private void CreateUnityMesh(Entity entity, int3 chunkPos,
            NativeList<float3> vertices, NativeList<float3> normals,
            NativeList<float2> uvs, NativeList<int> indices,
            ref ChunkMeshState meshState)
        {
            // Get or create GameObject
            ChunkGameObject chunkGO;
            if (EntityManager.HasComponent<ChunkGameObject>(entity))
            {
                chunkGO = EntityManager.GetComponentObject<ChunkGameObject>(entity);
            }
            else
            {
                var go = ChunkMeshPool.Get();
                chunkGO = new ChunkGameObject
                {
                    Value = go,
                    MeshFilter = go.GetComponent<MeshFilter>(),
                    MeshRenderer = go.GetComponent<MeshRenderer>(),
                    MeshCollider = go.GetComponent<MeshCollider>()
                };
                EntityManager.AddComponentObject(entity, chunkGO);
            }
            
            // Create or reuse mesh
            var mesh = chunkGO.MeshFilter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = $"Chunk_{chunkPos}";
                chunkGO.MeshFilter.sharedMesh = mesh;
            }
            
            mesh.Clear();
            
            // Convert NativeList to arrays
            var vertexArray = new Vector3[vertices.Length];
            var normalArray = new Vector3[vertices.Length];
            var uvArray = new Vector2[vertices.Length];
            var indexArray = new int[indices.Length];
            
            for (int i = 0; i < vertices.Length; i++)
            {
                vertexArray[i] = vertices[i];
                normalArray[i] = normals[i];
                uvArray[i] = uvs[i];
            }
            
            for (int i = 0; i < indices.Length; i++)
            {
                indexArray[i] = indices[i];
            }
            
            mesh.vertices = vertexArray;
            mesh.normals = normalArray;
            mesh.uv = uvArray;
            mesh.triangles = indexArray;
            
            mesh.RecalculateBounds();
            
            // CRITICAL: Keep mesh data on CPU for collider
            mesh.UploadMeshData(false);
            
            // Update state
            meshState.HasMesh = true;
            meshState.IsDirty = false;
            meshState.VertexCount = vertices.Length;
            meshState.TriangleCount = indices.Length / 3;
            
            Debug.Log($"[Voxel] Mesh created for {chunkPos}: " +
                $"{vertices.Length} verts, {indices.Length / 3} tris");
        }
    }
}
```

---

### Task 8.3.4: Create Default Voxel Material

1. Create material: `Assets/Materials/Voxel/VoxelDefault.mat`
2. Use URP Lit shader
3. Set base color to gray/brown
4. Create ChunkMeshPool prefab with material reference

---

## Validation

After completing 8.3, verify:

- [ ] Terrain is visible in Scene view
- [ ] Blocky cubes appear where ground level is
- [ ] Underground chunks show solid (exterior faces only)
- [ ] Above-ground chunks are empty (air)
- [ ] No visual gaps between chunks

### Expected Visual:

```
Looking down at Y=0:
┌─────┬─────┬─────┐
│█████│█████│█████│  ← Top faces visible
│█████│█████│█████│
│█████│█████│█████│
└─────┴─────┴─────┘
```

---

## Acceptance Criteria

- [ ] Terrain is visible
- [ ] Only exterior faces are rendered (interior culled)
- [ ] Console shows correct vertex/triangle counts
- [ ] Frame rate stable (> 60 FPS)
- [ ] No errors or warnings

---

## STOP GATE

**Before proceeding to 8.4:**

✅ Terrain is visually correct from Scene view

If terrain is invisible, do NOT proceed!
