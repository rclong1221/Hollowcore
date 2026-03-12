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
    /// OPTIMIZATION 10.11.2: Enableable tag for chunks needing physics collider.
    /// Set by ChunkMeshingSystem after visual mesh is ready.
    /// Processed by ChunkColliderBuildSystem separately to avoid frame spikes.
    /// </summary>
    public struct ChunkNeedsCollider : IComponentData, IEnableableComponent { }
    
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

    /// <summary>
    /// Tracks the specific physics collider blob to prevent leaks.
    /// </summary>
    public struct ChunkPhysicsState : ICleanupComponentData
    {
        public BlobAssetReference<Unity.Physics.Collider> ColliderBlob;
    }

    /// <summary>
    /// Holds a collider blob that must be disposed after the next physics update.
    /// </summary>
    public struct ObsoleteChunkCollider : IComponentData
    {
        public BlobAssetReference<Unity.Physics.Collider> Blob;
    }
}
