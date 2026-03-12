using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Decorators
{
    /// <summary>
    /// OPTIMIZATION 10.5.12: GPU Instancing for Decorators.
    /// Batches decorators of the same type into single draw calls using Graphics.DrawMeshInstanced.
    /// 
    /// OPTIMIZATION 10.9.9: Persistent Buffer + Dirty Flags
    /// - Maintains persistent visible matrices buffer
    /// - Per-chunk dirty flags to skip recalculation
    /// - Uses CameraData singleton instead of Camera.main
    /// - Hierarchical frustum culling (chunk-level first)
    /// </summary>
    public class DecoratorInstancingSystem : MonoBehaviour
    {
        private static DecoratorInstancingSystem _instance;
        public static DecoratorInstancingSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Only create in play mode
                    if (!Application.isPlaying)
                        return null;
                        
                    var go = new GameObject("DecoratorInstancingSystem");
                    _instance = go.AddComponent<DecoratorInstancingSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        // Instancing data per decorator type
        private class InstanceBatch
        {
            public Mesh Mesh;
            public Material Material;
            public List<Matrix4x4> Matrices = new List<Matrix4x4>(1023);
            public List<int3> ChunkPositions = new List<int3>(1023);
            public MaterialPropertyBlock PropertyBlock;
            public int Layer;
            
            // OPTIMIZATION 10.9.9: Cached visible matrices (persistent buffer)
            public List<Matrix4x4> CachedVisibleMatrices = new List<Matrix4x4>(1023);
            public bool IsDirty = true;
            
            // GPU instancing supports max 1023 instances per draw call
            public const int MAX_INSTANCES_PER_BATCH = 1023;
        }
        
        // Batches per decorator ID
        private Dictionary<byte, List<InstanceBatch>> _batches = new Dictionary<byte, List<InstanceBatch>>();
        
        // Track which decorators use instancing vs pooling
        private HashSet<byte> _instancedDecorators = new HashSet<byte>();
        
        // OPTIMIZATION 10.4.15: Track occluded chunks
        private HashSet<int> _occludedChunks = new HashSet<int>();
        
        // OPTIMIZATION 10.9.9: Track dirty chunks (need visibility recalculation)
        private HashSet<int> _dirtyChunks = new HashSet<int>();
        
        // OPTIMIZATION 10.9.9: Cache last camera chunk to detect camera movement
        private int3 _lastCameraChunk;
        private bool _hasCameraChunkCache = false;
        
        // OPTIMIZATION 10.4.15: Cache chunk frustum visibility per frame
        private Dictionary<int, bool> _chunkFrustumCache = new Dictionary<int, bool>();

        /// <summary>
        /// OPTIMIZATION 10.4.15: Mark a chunk as occluded (not visible through occlusion culling).
        /// Call from chunk visibility system when chunk is behind walls.
        /// </summary>
        public void SetChunkOccluded(int3 chunkPos, bool occluded)
        {
            int hash = GetChunkHash(chunkPos);
            bool changed = occluded ? _occludedChunks.Add(hash) : _occludedChunks.Remove(hash);
            if (changed)
            {
                // OPTIMIZATION 10.9.9: Mark chunk as dirty when occlusion changes
                MarkChunkDirty(chunkPos);
            }
        }
        
        /// <summary>
        /// OPTIMIZATION 10.5.16: Check if chunk is occluded.
        /// </summary>
        public bool IsChunkOccluded(int3 chunkPos)
        {
            return _occludedChunks.Contains(GetChunkHash(chunkPos));
        }
        
        /// <summary>
        /// Clear all occlusion state. Call when camera moves significantly.
        /// </summary>
        public void ClearOcclusionState()
        {
            _occludedChunks.Clear();
            MarkAllDirty();
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.9: Mark a chunk as needing visibility recalculation.
        /// </summary>
        public void MarkChunkDirty(int3 chunkPos)
        {
            _dirtyChunks.Add(GetChunkHash(chunkPos));
            // Mark all batches as dirty
            MarkAllBatchesDirty();
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.9: Mark all batches as dirty (camera moved, etc).
        /// </summary>
        private void MarkAllDirty()
        {
            MarkAllBatchesDirty();
            _chunkFrustumCache.Clear();
        }
        
        private void MarkAllBatchesDirty()
        {
            foreach (var kvp in _batches)
            {
                foreach (var batch in kvp.Value)
                {
                    batch.IsDirty = true;
                }
            }
        }
        
        private static int GetChunkHash(int3 pos)
        {
            // Simple spatial hash for chunk positions
            return pos.x * 73856093 ^ pos.y * 19349663 ^ pos.z * 83492791;
        }
        
        /// <summary>
        /// Register a decorator for GPU instancing.
        /// Call during initialization for decorators that should use instancing.
        /// </summary>
        public void RegisterForInstancing(byte decoratorID, Mesh mesh, Material material, int layer = 0)
        {
            if (mesh == null || material == null) return;
            
            // Ensure material supports instancing
            if (!material.enableInstancing)
            {
                UnityEngine.Debug.LogWarning($"[DecoratorInstancing] Material {material.name} doesn't support instancing. Consider enabling GPU Instancing on the material.");
                return;
            }
            
            _instancedDecorators.Add(decoratorID);
            
            if (!_batches.ContainsKey(decoratorID))
            {
                _batches[decoratorID] = new List<InstanceBatch>();
            }
            
            // Create initial batch
            var batch = new InstanceBatch
            {
                Mesh = mesh,
                Material = material,
                PropertyBlock = new MaterialPropertyBlock(),
                Layer = layer,
                IsDirty = true
            };
            _batches[decoratorID].Add(batch);
        }
        
        /// <summary>
        /// Check if a decorator is registered for GPU instancing.
        /// </summary>
        public bool IsInstancedDecorator(byte decoratorID)
        {
            return _instancedDecorators.Contains(decoratorID);
        }
        
        /// <summary>
        /// Add an instance to the batch for rendering.
        /// OPTIMIZATION 10.9.9: Marks batch as dirty.
        /// </summary>
        public void AddInstance(byte decoratorID, float3 position, quaternion rotation, float scale, int3 chunkPos = default)
        {
            if (!_batches.TryGetValue(decoratorID, out var batches))
                return;
            
            var matrix = Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));
            
            // Find batch with space or create new one
            var lastBatch = batches[batches.Count - 1];
            if (lastBatch.Matrices.Count >= InstanceBatch.MAX_INSTANCES_PER_BATCH)
            {
                // Create new batch
                var newBatch = new InstanceBatch
                {
                    Mesh = lastBatch.Mesh,
                    Material = lastBatch.Material,
                    PropertyBlock = new MaterialPropertyBlock(),
                    Layer = lastBatch.Layer,
                    IsDirty = true
                };
                batches.Add(newBatch);
                lastBatch = newBatch;
            }
            
            lastBatch.Matrices.Add(matrix);
            lastBatch.ChunkPositions.Add(chunkPos);
            lastBatch.IsDirty = true; // OPTIMIZATION 10.9.9
        }
        
        /// <summary>
        /// Remove all instances for a decorator type.
        /// </summary>
        public void ClearInstances(byte decoratorID)
        {
            if (_batches.TryGetValue(decoratorID, out var batches))
            {
                foreach (var batch in batches)
                {
                    batch.Matrices.Clear();
                    batch.ChunkPositions.Clear();
                    batch.CachedVisibleMatrices.Clear();
                    batch.IsDirty = true;
                }
                
                // Keep only first batch
                while (batches.Count > 1)
                {
                    batches.RemoveAt(batches.Count - 1);
                }
            }
        }
        
        /// <summary>
        /// Clear all instances for all decorators.
        /// </summary>
        public void ClearAllInstances()
        {
            foreach (var kvp in _batches)
            {
                ClearInstances(kvp.Key);
            }
        }
        
        private void LateUpdate()
        {
            // OPTIMIZATION 10.9.9: Use CameraData singleton if available
            float3 camPos;
            Plane[] frustumPlanes;
            int3 cameraChunk;
            
            if (TryGetCameraData(out camPos, out frustumPlanes, out cameraChunk))
            {
                // Check if camera moved to a new chunk
                if (!_hasCameraChunkCache || !cameraChunk.Equals(_lastCameraChunk))
                {
                    _lastCameraChunk = cameraChunk;
                    _hasCameraChunkCache = true;
                    MarkAllDirty();
                }
            }
            else
            {
                // Fallback: No camera data available
                return;
            }
            
            // Render all batched instances
            foreach (var kvp in _batches)
            {
                foreach (var batch in kvp.Value)
                {
                    if (batch.Matrices.Count == 0) continue;
                    
                    // OPTIMIZATION 10.9.9: Only rebuild visible list if dirty
                    if (batch.IsDirty)
                    {
                        RebuildVisibleMatrices(batch, frustumPlanes);
                        batch.IsDirty = false;
                    }
                    
                    if (batch.CachedVisibleMatrices.Count == 0) continue;
                    
                    // Draw visible instances in batches of 1023
                    int count = batch.CachedVisibleMatrices.Count;
                    int offset = 0;
                    
                    // OPTIMIZATION 10.13.3: Distance-based shadow culling for decorators
                    // Match the same shadow distance as voxel chunks (60m)
                    const float DECORATOR_SHADOW_DISTANCE_SQ = 60f * 60f;
                    var shadowMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Default to off
                    
                    // Check if any visible instances are within shadow distance
                    // (Simple check: use first visible instance as proxy)
                    // Note: camPos is already defined in LateUpdate scope
                    if (count > 0)
                    {
                        var firstMatrix = batch.CachedVisibleMatrices[0];
                        float3 instancePos = new float3(firstMatrix.m03, firstMatrix.m13, firstMatrix.m23);
                        float distSq = math.distancesq(camPos, instancePos);
                        
                        if (distSq < DECORATOR_SHADOW_DISTANCE_SQ)
                        {
                            shadowMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        }
                    }
                    
                    while (offset < count)
                    {
                        int batchSize = Mathf.Min(count - offset, InstanceBatch.MAX_INSTANCES_PER_BATCH);
                        
                        // Copy to draw buffer
                        for (int i = 0; i < batchSize; i++)
                        {
                            _drawBuffer[i] = batch.CachedVisibleMatrices[offset + i];
                        }
                        
                        Graphics.DrawMeshInstanced(
                            batch.Mesh,
                            0, // submesh index
                            batch.Material,
                            _drawBuffer,
                            batchSize,
                            batch.PropertyBlock,
                            shadowMode,
                            true, // receive shadows
                            batch.Layer
                        );
                        
                        offset += batchSize;
                    }
                }
            }
            
            // Clear dirty chunks cache at end of frame
            _dirtyChunks.Clear();
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.9: Try to get camera data from CameraData singleton.
        /// </summary>
        private bool TryGetCameraData(out float3 position, out Plane[] planes, out int3 chunkPos)
        {
            position = default;
            planes = null;
            chunkPos = default;
            
            // Try CameraData singleton first (from ECS)
            // Since we're a MonoBehaviour, we can't use SystemAPI, so fall back to Camera.main
            // In a future refactor, we could access the ECS world directly
            
            var cam = Camera.main;
            if (cam == null) return false;
            
            position = cam.transform.position;
            
            // Reuse plane array to avoid allocations
            if (_cachedFrustumPlanes == null)
            {
                _cachedFrustumPlanes = new Plane[6];
            }
            GeometryUtility.CalculateFrustumPlanes(cam, _cachedFrustumPlanes);
            planes = _cachedFrustumPlanes;
            
            chunkPos = new int3(
                (int)math.floor(position.x / DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(position.y / DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD),
                (int)math.floor(position.z / DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD)
            );
            
            return true;
        }
        
        private Plane[] _cachedFrustumPlanes;
        
        /// <summary>
        /// OPTIMIZATION 10.9.9: Rebuild visible matrices for a batch.
        /// Only called when batch is dirty.
        /// </summary>
        private void RebuildVisibleMatrices(InstanceBatch batch, Plane[] frustumPlanes)
        {
            batch.CachedVisibleMatrices.Clear();
            
            Bounds meshBounds = batch.Mesh.bounds;
            
            for (int i = 0; i < batch.Matrices.Count; i++)
            {
                // OPTIMIZATION 10.9.9: Hierarchical culling
                if (i < batch.ChunkPositions.Count)
                {
                    int3 chunkPos = batch.ChunkPositions[i];
                    int chunkHash = GetChunkHash(chunkPos);
                    
                    // 1. Check strict occlusion (Portals)
                    if (_occludedChunks.Contains(chunkHash))
                        continue;
                        
                    // 2. Check Chunk Frustum Culling (Hierarchical)
                    if (!CheckChunkFrustum(chunkPos, chunkHash, frustumPlanes))
                        continue;
                }
                
                var matrix = batch.Matrices[i];
                
                // 3. Instance-level frustum culling
                Bounds worldBounds = TransformBounds(meshBounds, matrix);
                
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, worldBounds))
                {
                    batch.CachedVisibleMatrices.Add(matrix);
                }
            }
        }
        
        // Reusable buffer for draw calls
        private Matrix4x4[] _drawBuffer = new Matrix4x4[1023];
        
        /// <summary>
        /// OPTIMIZATION 10.5.15: Transform local bounds to world space using matrix.
        /// </summary>
        private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(localBounds.center);
            
            // Transform the 8 corners and find new AABB
            var extents = localBounds.extents;
            Vector3 min = center;
            Vector3 max = center;
            
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z
                );
                
                Vector3 worldCorner = matrix.MultiplyPoint3x4(localBounds.center + corner);
                min = Vector3.Min(min, worldCorner);
                max = Vector3.Max(max, worldCorner);
            }
            
            Bounds result = new Bounds();
            result.SetMinMax(min, max);
            return result;
        }
        
        /// <summary>
        /// OPTIMIZATION 10.1.15: Check if chunk is inside frustum, using cache.
        /// </summary>
        private bool CheckChunkFrustum(int3 chunkPos, int chunkHash, Plane[] planes)
        {
            if (_chunkFrustumCache.TryGetValue(chunkHash, out bool visible))
                return visible;
                
            // Calculate world bounds of chunk
            Vector3 center = new Vector3(
                chunkPos.x * DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD + DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD * 0.5f,
                chunkPos.y * DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD + DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD * 0.5f,
                chunkPos.z * DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD + DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD * 0.5f
            );
            Vector3 size = new Vector3(
                DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD,
                DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD,
                DIG.Voxel.Core.VoxelConstants.CHUNK_SIZE_WORLD
            );
            
            Bounds chunkBounds = new Bounds(center, size);
            visible = GeometryUtility.TestPlanesAABB(planes, chunkBounds);
            
            _chunkFrustumCache[chunkHash] = visible;
            return visible;
        }
        
        /// <summary>
        /// Get statistics for debugging.
        /// </summary>
        public (int decoratorTypes, int totalInstances, int visibleInstances, int drawCalls) GetStats()
        {
            int types = _batches.Count;
            int instances = 0;
            int visible = 0;
            int draws = 0;
            
            foreach (var kvp in _batches)
            {
                foreach (var batch in kvp.Value)
                {
                    instances += batch.Matrices.Count;
                    visible += batch.CachedVisibleMatrices.Count;
                    if (batch.CachedVisibleMatrices.Count > 0)
                    {
                        draws += (batch.CachedVisibleMatrices.Count - 1) / InstanceBatch.MAX_INSTANCES_PER_BATCH + 1;
                    }
                }
            }
            
            return (types, instances, visible, draws);
        }
        
        private void OnDestroy()
        {
            ClearAllInstances();
            _batches.Clear();
            _instancedDecorators.Clear();
            _instance = null;
        }
    }
}

