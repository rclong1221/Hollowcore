using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// Request to create an explosion crater at a position.
    /// Can be created by any system (weapons, physics events, etc.)
    /// </summary>
    public struct CreateCraterRequest : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float Strength; // 0-1, affects crater depth/completeness
        public byte ReplaceMaterial; // What to fill the crater with (usually AIR)
        public bool SpawnLoot; // Whether to spawn loot drops for destroyed voxels
        public bool FromRpc; // True if this request came from network, prevents rebroadcasting
        public uint Seed; // Task 10.16.3: Deterministic seed
    }

    /// <summary>
    /// Component marking a crater as processed (for statistics/effects).
    /// </summary>
    public struct CraterCreated : IComponentData
    {
        public float3 Center;
        public float Radius;
        public int VoxelsDestroyed;
        public float Timestamp;
    }

    /// <summary>
    /// Tracks the progress of an explosion being processed over multiple frames.
    /// </summary>
    public struct ExplosionState : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float Strength;
        public byte ReplaceMaterial;
        public bool SpawnLoot;
        public int TotalVoxelsDestroyed;
        public int TotalChunks;
        public int ProcessedChunks;
        public uint Seed; // Added for determinism
    }

    /// <summary>
    /// Buffer element storing chunks waiting to be processed for an explosion.
    /// </summary>
    public struct PendingExplosionChunk : IBufferElementData
    {
        public int3 Value;
    }

    /// <summary>
    /// System that processes crater creation requests.
    /// Runs on Server in multiplayer, or Local in single-player.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class VoxelExplosionSystem : SystemBase
    {
        private const int MAX_CHUNKS_PER_FRAME = 8; // Adjust based on frame budget
        private Entity _eventEntity;
        private bool _enableDebugLogs = true;
        private bool _isServer;
        
        protected override void OnCreate()
        {
            // Determine if we're on the server world
            _isServer = World.Name == "ServerWorld" || World.Name.Contains("Server");
            
            // Create or find event singleton for loot spawning
            var query = EntityManager.CreateEntityQuery(typeof(VoxelEventsSingleton));
            if (query.IsEmpty)
            {
                _eventEntity = EntityManager.CreateEntity();
                EntityManager.AddBuffer<VoxelDestroyedEvent>(_eventEntity);
                EntityManager.AddComponent<VoxelEventsSingleton>(_eventEntity);
                EntityManager.SetName(_eventEntity, "VoxelEvents_Explosion");
            }
            
            if (_enableDebugLogs) UnityEngine.Debug.Log($"[VoxelExplosion] System created in {World.Name}");
        }
        
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Find event entity if not cached
            if (_eventEntity == Entity.Null)
            {
                var query = EntityManager.CreateEntityQuery(typeof(VoxelEventsSingleton));
                if (!query.IsEmpty)
                {
                    using var entities = query.ToEntityArray(Allocator.Temp);
                    if (entities.Length > 0)
                        _eventEntity = entities[0];
                }
            }

            // Get ChunkLookup for O(1) access
            ChunkLookup chunkLookup = default;
            bool hasLookup = SystemAPI.TryGetSingleton<ChunkLookup>(out chunkLookup);
            
            if (!hasLookup && UnityEngine.Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.LogWarning($"[VoxelExplosion] ChunkLookup Singleton missing! Cannot identify affected chunks. World={World.Name}");
            }
            
            // 1. INITIALIZATION: Convert Requests to Async State
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<CreateCraterRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                // Identify all affected chunks immediately
                var chunksToProcess = new NativeList<int3>(Allocator.Temp);
                
                if (hasLookup)
                {
                    float3 min = req.Center - req.Radius;
                    float3 max = req.Center + req.Radius;
                    int3 minChunk = CoordinateUtils.WorldToChunkPos(min);
                    int3 maxChunk = CoordinateUtils.WorldToChunkPos(max);
                    
                    for (int cx = minChunk.x; cx <= maxChunk.x; cx++)
                    for (int cy = minChunk.y; cy <= maxChunk.y; cy++)
                    for (int cz = minChunk.z; cz <= maxChunk.z; cz++)
                    {
                        int3 chunkPos = new int3(cx, cy, cz);
                        if (chunkLookup.TryGetChunk(chunkPos, out Entity chunkEntity))
                        {
                            if (EntityManager.HasComponent<ChunkVoxelData>(chunkEntity))
                            {
                                 // Add valid chunk to queue
                                 chunksToProcess.Add(chunkPos);
                            }
                        }
                    }
                }
                
                // If no chunks affected, finish immediately
                if (chunksToProcess.Length == 0)
                {
                    if (_enableDebugLogs) UnityEngine.Debug.LogWarning($"[VoxelExplosion] No chunks affected by explosion at {req.Center} with radius {req.Radius}. Destroying request.");
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                if (_enableDebugLogs) UnityEngine.Debug.Log($"[VoxelExplosion] Accepted request at {req.Center}, affected chunks: {chunksToProcess.Length}");
                
                // Initialize State on the same entity (remove Request, add State + Buffer)
                ecb.RemoveComponent<CreateCraterRequest>(entity);
                ecb.AddComponent(entity, new ExplosionState
                {
                    Center = req.Center,
                    Radius = req.Radius,
                    Strength = req.Strength,
                    ReplaceMaterial = req.ReplaceMaterial,
                    SpawnLoot = req.SpawnLoot,
                    TotalChunks = chunksToProcess.Length,
                    ProcessedChunks = 0,
                    TotalVoxelsDestroyed = 0,
                    Seed = req.Seed == 0 ? 1 : req.Seed // Propagate Seed (Sanitized)
                });
                
                var buffer = ecb.AddBuffer<PendingExplosionChunk>(entity);
                foreach(var chunkPos in chunksToProcess)
                {
                    buffer.Add(new PendingExplosionChunk { Value = chunkPos });
                }
                
                chunksToProcess.Dispose();
            }
            
            // Playback ECB to ensure State/Buffers are ready for processing step (if we want frame-1 start, but safe to wait next frame)
            ecb.Playback(EntityManager);
            ecb.Dispose();
            
            // 2. PROCESSING: Time-Slice Execution
            // Re-acquire lookup in case it changed (safety)
            hasLookup = SystemAPI.TryGetSingleton<ChunkLookup>(out chunkLookup);
            if (!hasLookup) return;
            
            var ecbEnd = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (state, buffer, entity) in 
                SystemAPI.Query<RefRW<ExplosionState>, DynamicBuffer<PendingExplosionChunk>>().WithEntityAccess())
            {
                if (buffer.IsEmpty)
                {
                    // Should have been cleaned up, but safe check
                    FinalizeExplosion(ecbEnd, entity, state.ValueRO, _isServer);
                    continue;
                }
                
                // Take a batch of chunks
                int countToProcess = math.min(MAX_CHUNKS_PER_FRAME, buffer.Length);
                
                var currentBatchEntities = new NativeList<Entity>(countToProcess, Allocator.TempJob);
                var currentBatchPositions = new NativeList<int3>(countToProcess, Allocator.TempJob);
                
                // Indices to remove from buffer (scan backwards or just remove range at end if order doesn't matter)
                // Order doesn't matter for explosions. We remove from end = O(1).
                
                for (int i = 0; i < countToProcess; i++)
                {
                    int lastIdx = buffer.Length - 1;
                    int3 chunkPos = buffer[lastIdx].Value;
                    buffer.RemoveAt(lastIdx);
                    
                    if (chunkLookup.TryGetChunk(chunkPos, out Entity chunkEntity))
                    {
                        if (EntityManager.HasComponent<ChunkVoxelData>(chunkEntity) &&
                            EntityManager.GetComponentData<ChunkVoxelData>(chunkEntity).IsValid)
                        {
                            currentBatchEntities.Add(chunkEntity);
                            currentBatchPositions.Add(chunkPos);
                        }
                    }
                }
                
                // Process Batch
                int voxelsDestroyed = ProcessBatch(currentBatchEntities, currentBatchPositions, state.ValueRO, chunkLookup);
                
                state.ValueRW.ProcessedChunks += countToProcess; // Note: countToProcess includes skipped/invalid chunks
                state.ValueRW.TotalVoxelsDestroyed += voxelsDestroyed;
                
                // Cleanup Batch Lists
                currentBatchEntities.Dispose();
                currentBatchPositions.Dispose();
                
                // Check Completion
                if (buffer.IsEmpty)
                {
                    FinalizeExplosion(ecbEnd, entity, state.ValueRO, _isServer);
                }
            }
            
            ecbEnd.Playback(EntityManager);
            ecbEnd.Dispose();
        }
        
        private void FinalizeExplosion(EntityCommandBuffer ecb, Entity entity, ExplosionState state, bool isServer)
        {
             // Create completion marker
            var completionEntity = ecb.CreateEntity();
            ecb.AddComponent(completionEntity, new CraterCreated
            {
                Center = state.Center,
                Radius = state.Radius,
                VoxelsDestroyed = state.TotalVoxelsDestroyed,
                Timestamp = UnityEngine.Time.time
            });
            
            // Only destroy the entity on the server - client ghosts are automatically despawned when server destroys them
            // This prevents ghost desync errors ("Found a ghost in the ghost map which does not have an entity connected to it")
            if (isServer)
            {
                ecb.DestroyEntity(entity);
            }
            else
            {
                // On client, just remove the explosion state so we don't process it again
                ecb.RemoveComponent<ExplosionState>(entity);
            }
        }

        private int ProcessBatch(NativeList<Entity> chunksToProcess, NativeList<int3> chunkPositions, ExplosionState state, ChunkLookup chunkLookup)
        {
            int chunkCount = chunksToProcess.Length;
            if (chunkCount == 0) return 0;
            
            // 2. Allocate flattened buffers for all chunks
            int voxelsPerChunk = VoxelConstants.VOXELS_PER_CHUNK;
            int totalBytes = chunkCount * voxelsPerChunk;
            
            var flatDensities = new NativeArray<byte>(totalBytes, Allocator.TempJob);
            var flatMaterials = new NativeArray<byte>(totalBytes, Allocator.TempJob);
            var chunkModified = new NativeArray<bool>(chunkCount, Allocator.TempJob);
            var chunkLootCounts = new NativeArray<int>(chunkCount * 256, Allocator.TempJob); 
            
            // 3. Extract logic (Main Thread)
            for (int i = 0; i < chunkCount; i++)
            {
                var inputData = EntityManager.GetComponentData<ChunkVoxelData>(chunksToProcess[i]);
                ref var blob = ref inputData.Data.Value;
                
                int offset = i * voxelsPerChunk;
                for (int v = 0; v < voxelsPerChunk; v++)
                {
                    flatDensities[offset + v] = blob.Densities[v];
                    flatMaterials[offset + v] = blob.Materials[v];
                }
            }
            
            // 4. Schedule Burst Job
            var job = new BurstExplosionJob
            {
                ChunkPositions = chunkPositions.AsArray(),
                Densities = flatDensities,
                Materials = flatMaterials,
                ChunkModified = chunkModified,
                LootCounts = chunkLootCounts,
                Center = state.Center,
                Radius = state.Radius,
                RadiusSq = state.Radius * state.Radius,
                Strength = state.Strength,
                ReplaceMaterial = state.ReplaceMaterial,
                SpawnLoot = state.SpawnLoot,
                ChunkSize = VoxelConstants.CHUNK_SIZE,
                Seed = state.Seed // Use deterministic seed
            };
            
            job.Schedule(chunkCount, 1, default).Complete(); // Sync processing of batch
            
            // 5. Apply results and create new Blobs
            int voxelsDestroyedTotal = 0;
            
            // Get event buffer if valid
            DynamicBuffer<VoxelDestroyedEvent>? eventBuffer = null;
            if (state.SpawnLoot && _eventEntity != Entity.Null && EntityManager.HasBuffer<VoxelDestroyedEvent>(_eventEntity))
                eventBuffer = EntityManager.GetBuffer<VoxelDestroyedEvent>(_eventEntity);

            for (int i = 0; i < chunkCount; i++)
            {
                if (chunkModified[i])
                {
                    int offset = i * voxelsPerChunk;
                    
                    var newD = new NativeArray<byte>(voxelsPerChunk, Allocator.Temp);
                    var newM = new NativeArray<byte>(voxelsPerChunk, Allocator.Temp);
                    NativeArray<byte>.Copy(flatDensities, offset, newD, 0, voxelsPerChunk);
                    NativeArray<byte>.Copy(flatMaterials, offset, newM, 0, voxelsPerChunk);
                    
                    var newBlob = VoxelBlobBuilder.Create(newD, newM);
                    
                    var chunkEntity = chunksToProcess[i];
                    var vd = EntityManager.GetComponentData<ChunkVoxelData>(chunkEntity);
                    vd.Data.Dispose(); 
                    
                    EntityManager.SetComponentData(chunkEntity, new ChunkVoxelData { Data = newBlob });
                    
                    UnityEngine.Debug.Log($"[VoxelSystem] Chunk {chunkEntity.Index} modified. Triggering Remesh...");
                    EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(chunkEntity, true);
                    
                    // Trigger collider build (Server/Authority)
                    if (EntityManager.HasComponent<ChunkNeedsCollider>(chunkEntity))
                    {
                        if (_enableDebugLogs) UnityEngine.Debug.Log($"[VoxelExplosionSystem] Tagging chunk {chunkEntity} for collider update in {World.Name}");
                        EntityManager.SetComponentEnabled<ChunkNeedsCollider>(chunkEntity, true);
                    }
                    
                    newD.Dispose();
                    newM.Dispose();

                    // Aggregated Loot
                    if (eventBuffer.HasValue)
                    {
                        int lootOffset = i * 256;
                        for (int mat = 0; mat < 256; mat++)
                        {
                            int count = chunkLootCounts[lootOffset + mat];
                            if (count > 0)
                            {
                                voxelsDestroyedTotal += count;
                                eventBuffer.Value.Add(new VoxelDestroyedEvent
                                {
                                    Position = state.Center, 
                                    MaterialID = (byte)mat,
                                    Amount = count
                                });
                            }
                        }
                    }
                }
            }
            
            // Cleanup
            flatDensities.Dispose();
            flatMaterials.Dispose();
            chunkModified.Dispose();
            chunkLootCounts.Dispose();
            
            return voxelsDestroyedTotal;
        }

        [Unity.Burst.BurstCompile]
        struct BurstExplosionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int3> ChunkPositions;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Densities;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Materials;
            [NativeDisableParallelForRestriction] public NativeArray<bool> ChunkModified;
            [NativeDisableParallelForRestriction] public NativeArray<int> LootCounts;
            
            public float3 Center;
            public float Radius;
            public float RadiusSq;
            public float Strength;
            public byte ReplaceMaterial;
            public bool SpawnLoot;
            public int ChunkSize;
            public uint Seed;
            
            public void Execute(int index)
            {
                int3 chunkPos = ChunkPositions[index];
                float3 chunkWorldPos = new float3(chunkPos.x * ChunkSize, chunkPos.y * ChunkSize, chunkPos.z * ChunkSize);
                
                int offset = index * (ChunkSize * ChunkSize * ChunkSize);
                int lootOffset = index * 256;
                bool modified = false;
                
                // CRATER SHAPE CONSTANTS
                // Spherical shape, but with noise for rough edges
                float3 distortion = new float3(1.0f, 1.0f, 1.0f); 
                
                // NOISE CONSTANTS - Tuned for connectivity
                float noiseFreq = 0.10f; // Lower freq = larger, more connected features
                float noiseAmp = 1.0f;   // Reduced amplitude to keep shape closer to sphere
                
                for (int lx = 0; lx < ChunkSize; lx++)
                for (int ly = 0; ly < ChunkSize; ly++)
                for (int lz = 0; lz < ChunkSize; lz++)
                {
                    float3 voxelWorld = chunkWorldPos + new float3(lx + 0.5f, ly + 0.5f, lz + 0.5f);
                    
                    // 1. Spheroid Distance (Flattened Y)
                    float3 relative = (voxelWorld - Center) * distortion;
                    float distSq = math.lengthsq(relative);
                    
                    // 2. Coherent Noise Check
                    // Use Simplex noise to perturb the effective radius
                    // This creates continuous blobs rather than white noise scattering
                    float n = noise.snoise(voxelWorld * noiseFreq); // Range -1 to 1
                    float effectiveRadius = Radius + (n * noiseAmp * Strength);
                    
                    // Check against effective radius
                    if (distSq <= effectiveRadius * effectiveRadius)
                    {
                        // Helper inline for VoxelPosToIndex: x + y*Size + z*Size*Size
                        int vIdx = offset + (lx + ly * ChunkSize + lz * ChunkSize * ChunkSize);
                        
                        byte oldDensity = Densities[vIdx];
                        byte oldMaterial = Materials[vIdx];
                        
                        // Only destroy if not already air
                        if (oldDensity > 0)
                        {
                            Densities[vIdx] = 0; // Air
                            Materials[vIdx] = ReplaceMaterial;
                            modified = true;
                            
                            if (SpawnLoot && oldMaterial != 0) 
                            {
                                LootCounts[lootOffset + oldMaterial]++;
                            }
                        }
                    }
                }
                
                ChunkModified[index] = modified;
            }
        }
    }
    
    /// <summary>
    /// Static helper for creating explosions from any code.
    /// </summary>
    public static class VoxelExplosion
    {
        /// <summary>
        /// Queue an explosion crater to be created next frame.
        /// </summary>
        public static void CreateCrater(EntityManager em, float3 center, float radius, float strength = 1f, bool spawnLoot = true)
        {
            var entity = em.CreateEntity();
            // Generate a random seed for this explosion
            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

            em.AddComponentData(entity, new CreateCraterRequest
            {
                Center = center,
                Radius = radius,
                Strength = math.clamp(strength, 0f, 1f),
                ReplaceMaterial = VoxelConstants.MATERIAL_AIR,
                SpawnLoot = spawnLoot,
                Seed = seed
            });
        }
        
        /// <summary>
        /// Create crater immediately (blocks until complete).
        /// Use CreateCrater() for non-blocking version.
        /// </summary>
        public static int CreateCraterImmediate(EntityManager em, float3 center, float radius, byte replaceMaterial = 0)
        {
            // Uses VoxelOperations.ModifySphere for immediate execution
            VoxelOperations.ModifySphere(em, center, radius, VoxelConstants.DENSITY_AIR, replaceMaterial);
            
            // Estimate voxels destroyed (sphere volume)
            float volume = (4f / 3f) * math.PI * radius * radius * radius;
            return (int)volume;
        }
    }
}
