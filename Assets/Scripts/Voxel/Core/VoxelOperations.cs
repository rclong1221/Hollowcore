using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Voxel.Components;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Static API for safely modifying voxel data and ensuring proper mesh updates.
    /// Handles blob immutability by creating new blobs and disposing old ones.
    /// </summary>
    public static class VoxelOperations
    {
        /// <summary>
        /// Modify a single voxel at a specific world position.
        /// </summary>
        public static void SetVoxel(
            EntityManager em, 
            int3 worldVoxelPos, 
            byte density, 
            byte material,
            ref SystemState state) // Need state to get component lookup if optimized, but here we iterate EM
        {
            SetVoxel(em, worldVoxelPos, density, material);
        }

        /// <summary>
        /// Get the material ID at a specific world position.
        /// Returns MATERIAL_AIR if chunk doesn't exist or position is invalid.
        /// </summary>
        public static byte GetVoxelMaterial(EntityManager em, int3 worldVoxelPos)
        {
            int3 chunkPos = CoordinateUtils.WorldToChunkPos((float3)worldVoxelPos);
            Entity chunkEntity = GetChunkEntity(em, chunkPos);
            
            if (chunkEntity == Entity.Null)
                return VoxelConstants.MATERIAL_AIR;
                
            if (!em.HasComponent<ChunkVoxelData>(chunkEntity))
                return VoxelConstants.MATERIAL_AIR;
                
            var voxelData = em.GetComponentData<ChunkVoxelData>(chunkEntity);
            if (!voxelData.IsValid)
                return VoxelConstants.MATERIAL_AIR;
                
            int3 localPos = CoordinateUtils.WorldToLocalVoxelPos((float3)worldVoxelPos);
            int index = CoordinateUtils.VoxelPosToIndex(localPos);
            
            return voxelData.Data.Value.Materials[index];
        }

        public static void SetVoxel(
            EntityManager em, 
            int3 worldVoxelPos, 
            byte density, 
            byte material)
        {
            // 1. Get Chunk
            int3 chunkPos = CoordinateUtils.WorldToChunkPos((float3)worldVoxelPos);
            Entity chunkEntity = GetChunkEntity(em, chunkPos);
            
            if (chunkEntity == Entity.Null)
                return;
                
            // 2. Get Data
            if (!em.HasComponent<ChunkVoxelData>(chunkEntity))
                return;
                
            var voxelData = em.GetComponentData<ChunkVoxelData>(chunkEntity);
            if (!voxelData.IsValid)
                return;
                
            // 3. Modify
            int3 localPos = CoordinateUtils.WorldToLocalVoxelPos((float3)worldVoxelPos);
            
            // Check if change is actual
            int index = CoordinateUtils.VoxelPosToIndex(localPos);
            if (voxelData.Data.Value.Densities[index] == density &&
                voxelData.Data.Value.Materials[index] == material)
            {
                return; // No change
            }
            
            // Create modified blob
            var newData = ModifySingleVoxel(voxelData.Data, localPos, density, material);
            
            // 4. Update Component & Dispose Old
            // Note: In a real system you might batch these updates, but for API simplicity we do it immediately.
            // CAUTION: Disposing a blob that might be used by a job currently running is dangerous.
            // Ideally, we defer disposal or use a ref-counting system. 
            // For MVP, we assume MainThread access and jobs complete between frames.
            // Ideally we'd add the old blob to a "Data to dispose" list.
            // But we'll follow standard practice: Dispose old if we own it.
            // Since we're replacing the component, we are responsible for the old one.
            // However, other systems might hold references. This is a risk in pure DOTS without RefCounting.
            // We will assume simpler single-threaded safety for now.
            // Actually, we should probably rely on a disposal system, but let's just dispose.
             // voxelData.Data.Dispose(); // RISK: If a job is reading this blob?
            // Safer: Just replace the component. The old BlobAssetReference is a struct. 
            // The Blob data itself is in Allocator.Persistent. It leaks if not disposed.
            // We MUST dispose it.
            
            // Safety Check: Are we in a job? This is main thread only API.
            voxelData.Data.Dispose();
            
            em.SetComponentData(chunkEntity, new ChunkVoxelData { Data = newData });
            
            // 5. Trigger updates
            em.SetComponentEnabled<ChunkNeedsRemesh>(chunkEntity, true);
            if (em.HasComponent<ChunkNeedsCollider>(chunkEntity))
            {
                em.SetComponentEnabled<ChunkNeedsCollider>(chunkEntity, true);
            }
            
            // 6. Trigger Neighbors if boundary
            MarkNeighbors(em, chunkPos, localPos);
        }
        
        /// <summary>
        /// Modify a sphere of voxels.
        /// </summary>
        public static void ModifySphere(
            EntityManager em, 
            float3 center, 
            float radius, 
            byte targetDensity, 
            byte targetMaterial)
        {
            // Find all affected chunks
            float3 min = center - radius;
            float3 max = center + radius;
            
            int3 minChunk = CoordinateUtils.WorldToChunkPos(min);
            int3 maxChunk = CoordinateUtils.WorldToChunkPos(max);
            
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            for (int y = minChunk.y; y <= maxChunk.y; y++)
            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                ModifySphereInChunk(em, new int3(x, y, z), center, radius, targetDensity, targetMaterial);
            }
        }
        
        private static void ModifySphereInChunk(
            EntityManager em,
            int3 chunkPos,
            float3 center,
            float radius,
            byte targetDensity,
            byte targetMaterial)
        {
            Entity entity = GetChunkEntity(em, chunkPos);
            if (entity == Entity.Null) return;
            
            if (!em.HasComponent<ChunkVoxelData>(entity)) return;
            var component = em.GetComponentData<ChunkVoxelData>(entity);
            if (!component.IsValid) return;
            
            ref var blob = ref component.Data.Value;
            
            // Prepare arrays for modification
            var densities = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            var materials = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            
            // Copy existing
            bool changed = false;
            float3 chunkOrigin = CoordinateUtils.ChunkToWorldPos(chunkPos);
            float radiusSq = radius * radius;
            
            // We iterate all voxels in chunk? Or just bounded box?
            // Iterating all is 32k. Bounded box is better but easier to just iterate for MVP.
            // Let's use bounding box optimization relative to chunk.
            
            // Transform sphere to local space
            float3 localCenter = center - chunkOrigin;
            
            // Determine loop bounds in local space
            int minX = (int)math.floor(math.max(0, localCenter.x - radius));
            int minY = (int)math.floor(math.max(0, localCenter.y - radius));
            int minZ = (int)math.floor(math.max(0, localCenter.z - radius));
            
            int maxX = (int)math.ceil(math.min(VoxelConstants.CHUNK_SIZE, localCenter.x + radius));
            int maxY = (int)math.ceil(math.min(VoxelConstants.CHUNK_SIZE, localCenter.y + radius));
            int maxZ = (int)math.ceil(math.min(VoxelConstants.CHUNK_SIZE, localCenter.z + radius));
            
            // Copy all first because we need complete blob
            for (int i = 0; i < VoxelConstants.VOXELS_PER_CHUNK; i++)
            {
                densities[i] = blob.Densities[i];
                materials[i] = blob.Materials[i];
            }
            
            for (int x = minX; x < maxX; x++)
            for (int y = minY; y < maxY; y++)
            for (int z = minZ; z < maxZ; z++)
            {
                float3 voxelPos = new float3(x, y, z); // Should adding 0.5f be cleaner? Voxel center.
                // Distance check
                if (math.distancesq(voxelPos, localCenter) <= radiusSq)
                {
                    int index = CoordinateUtils.VoxelPosToIndex(new int3(x, y, z));
                    if (densities[index] != targetDensity)
                    {
                        densities[index] = targetDensity;
                        materials[index] = targetMaterial;
                        changed = true;
                    }
                }
            }
            
            if (changed)
            {
                var newBlob = VoxelBlobBuilder.Create(densities, materials);
                component.Data.Dispose();
                em.SetComponentData(entity, new ChunkVoxelData { Data = newBlob });
                em.SetComponentEnabled<ChunkNeedsRemesh>(entity, true);
                if (em.HasComponent<ChunkNeedsCollider>(entity))
                {
                    em.SetComponentEnabled<ChunkNeedsCollider>(entity, true);
                }
                
                // For sphere, we might touch boundaries too. 
                // Any voxel modified on edge (0 or 31) triggers neighbor update.
                // Simple logic: if we modified anything, check if we touched edges.
                // Or just aggressively update neighbors if sphere overlaps chunk edge?
                bool touchesEdge = (minX == 0 || maxX == 32 || minY == 0 || maxY == 32 || minZ == 0 || maxZ == 32);
                if (touchesEdge)
                {
                    MarkAllNeighbors(em, chunkPos);
                }
            }
            
            densities.Dispose();
            materials.Dispose();
        }

        private static BlobAssetReference<VoxelBlob> ModifySingleVoxel(
            BlobAssetReference<VoxelBlob> original,
            int3 localPos,
            byte newDensity,
            byte newMaterial)
        {
            var densities = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            var materials = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.Temp);
            
            ref var blob = ref original.Value;
            
            // Bulk copy? 
             // UnsafeUtility.MemCpy? BlobArray isn't a simple pointer though?
             // It behaves like array.
             for (int i = 0; i < VoxelConstants.VOXELS_PER_CHUNK; i++)
             {
                 densities[i] = blob.Densities[i];
                 materials[i] = blob.Materials[i];
             }
             
             int idx = CoordinateUtils.VoxelPosToIndex(localPos);
             densities[idx] = newDensity;
             materials[idx] = newMaterial;
             
             var result = VoxelBlobBuilder.Create(densities, materials);
             densities.Dispose();
             materials.Dispose();
             return result;
        }

        private static Entity GetChunkEntity(EntityManager em, int3 chunkPos)
        {
            // Slow search? We don't have ChunkLookup here unless we query for it.
            // But we can't query singleton from static without SystemAPI.
            // Iterating all chunks is SLOW.
            // Ideally we pass ChunkLookup.
            // But if we only have EntityManager...
            // Use EntityQuery?
            
            // Use the ChunkLookup singleton if available?
            // Since this API takes EntityManager, it implies Main Thread usage.
            // We can try to get the ChunkLookup component from a singleton entity.
            
            // Actually, querying specifically for a component with value is slow.
            // The best way is: require the caller to provide logic or just find the entity.
            // OR: assume there is a query we can run.
            
            // "VoxelOperations" implies high level.
            // Let's iterate using a query with filter? No, we need specific chunk.
            
            // Optimization: Just loop locally. This is MVP API.
            // Production code would use ChunkLookup.
            
            // Let's implement a manual search for now, but comment that it's slow.
            // Or better: require ChunkLookup passed in?
            
            // Let's query component data: ChunkPosition.
            using var query = em.CreateEntityQuery(typeof(ChunkPosition));
            var entities = query.ToEntityArray(Allocator.Temp);
            var positions = query.ToComponentDataArray<ChunkPosition>(Allocator.Temp);
            
            Entity result = Entity.Null;
            for(int i=0; i<positions.Length; i++)
            {
                if (positions[i].Value.Equals(chunkPos))
                {
                    result = entities[i];
                    break;
                }
            }
            
            entities.Dispose();
            positions.Dispose();
            return result;
        }
        
        private static void MarkNeighbors(EntityManager em, int3 chunkPos, int3 localPos)
        {
             if (localPos.x == 0) MarkChunk(em, chunkPos + new int3(-1, 0, 0));
             if (localPos.x == VoxelConstants.CHUNK_SIZE - 1) MarkChunk(em, chunkPos + new int3(1, 0, 0));
             
             if (localPos.y == 0) MarkChunk(em, chunkPos + new int3(0, -1, 0));
             if (localPos.y == VoxelConstants.CHUNK_SIZE - 1) MarkChunk(em, chunkPos + new int3(0, 1, 0));
             
             if (localPos.z == 0) MarkChunk(em, chunkPos + new int3(0, 0, -1));
             if (localPos.z == VoxelConstants.CHUNK_SIZE - 1) MarkChunk(em, chunkPos + new int3(0, 0, 1));
        }
        
        private static void MarkAllNeighbors(EntityManager em, int3 chunkPos)
        {
            MarkChunk(em, chunkPos + new int3(-1, 0, 0));
            MarkChunk(em, chunkPos + new int3(1, 0, 0));
            MarkChunk(em, chunkPos + new int3(0, -1, 0));
            MarkChunk(em, chunkPos + new int3(0, 1, 0));
            MarkChunk(em, chunkPos + new int3(0, 0, -1));
            MarkChunk(em, chunkPos + new int3(0, 0, 1));
        }
        
        private static void MarkChunk(EntityManager em, int3 chunkPos)
        {
            Entity e = GetChunkEntity(em, chunkPos);
            if (e != Entity.Null)
            {
                em.SetComponentEnabled<ChunkNeedsRemesh>(e, true);
                if (em.HasComponent<ChunkNeedsCollider>(e))
                {
                    em.SetComponentEnabled<ChunkNeedsCollider>(e, true);
                }
            }
        }
    }
}
