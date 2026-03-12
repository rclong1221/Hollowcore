using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Voxel.Core;

namespace DIG.Voxel.Systems.Network
{
    /// <summary>
    /// RPC for requesting late-join sync from server.
    /// </summary>
    public struct LateJoinSyncRequest : IRpcCommand
    {
        public uint ClientLastTick; // 0 = full sync needed
    }
    
    /// <summary>
    /// RPC for sending sync header to late-joining client.
    /// </summary>
    public struct LateJoinSyncHeader : IRpcCommand
    {
        public int TotalModifications;
        public int TotalBatches;
        public uint ServerTick;
    }
    
    /// <summary>
    /// RPC for sending a batch of modifications to late-joining client.
    /// </summary>
    public struct LateJoinSyncBatch : IRpcCommand
    {
        public int BatchIndex;
        public int ModificationCount;
        
        // Packed modification data (up to 8 per batch)
        public int3 ChunkPos0; public int3 LocalPos0; public byte Density0; public byte Material0;
        public int3 ChunkPos1; public int3 LocalPos1; public byte Density1; public byte Material1;
        public int3 ChunkPos2; public int3 LocalPos2; public byte Density2; public byte Material2;
        public int3 ChunkPos3; public int3 LocalPos3; public byte Density3; public byte Material3;
        public int3 ChunkPos4; public int3 LocalPos4; public byte Density4; public byte Material4;
        public int3 ChunkPos5; public int3 LocalPos5; public byte Density5; public byte Material5;
        public int3 ChunkPos6; public int3 LocalPos6; public byte Density6; public byte Material6;
        public int3 ChunkPos7; public int3 LocalPos7; public byte Density7; public byte Material7;
    }
    
    /// <summary>
    /// Server system that handles late-join synchronization.
    /// Sends modification history to newly connected clients.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class LateJoinSyncServerSystem : SystemBase
    {
        private const int MODS_PER_BATCH = 8;
        private const int MAX_BATCHES_PER_FRAME = 5;
        public static bool DiagnosticsEnabled = false;

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Get batching history from singleton
            if (!SystemAPI.HasSingleton<VoxelHistory>()) return;
            
            var historyEntity = SystemAPI.GetSingletonEntity<VoxelHistory>();
            var historyList = EntityManager.GetComponentData<VoxelHistory>(historyEntity).Value;
            
            // Process sync requests
            foreach (var (request, requestEntity) in 
                SystemAPI.Query<LateJoinSyncRequest>()
                .WithAll<ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                // Get target connection
                var receiveRequest = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(requestEntity);
                var targetConnection = receiveRequest.SourceConnection;
                
                // Get modifications to send
                // Create temp list since we can't expose slice easily in this struct based setup
                var modifications = new NativeList<PendingModification>(Allocator.Temp);
                
                if (request.ClientLastTick == 0)
                {
                    modifications.AddRange(historyList.AsArray());
                }
                else
                {
                    // Filter by tick
                    for (int i = 0; i < historyList.Length; i++)
                    {
                        if (historyList[i].Tick > request.ClientLastTick)
                        {
                            modifications.Add(historyList[i]);
                        }
                    }
                }
                
                if (DiagnosticsEnabled)
                    UnityEngine.Debug.Log($"[LateJoinSync] Sending {modifications.Length} modifications to late-joiner");
                
                // Get current server tick
                uint serverTick = 0;
                if (SystemAPI.HasSingleton<NetworkTime>())
                {
                    serverTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;
                }
                
                // Send header
                int totalBatches = (modifications.Length + MODS_PER_BATCH - 1) / MODS_PER_BATCH;
                var headerEntity = ecb.CreateEntity();
                ecb.AddComponent(headerEntity, new LateJoinSyncHeader
                {
                    TotalModifications = modifications.Length,
                    TotalBatches = totalBatches,
                    ServerTick = serverTick
                });
                ecb.AddComponent(headerEntity, new SendRpcCommandRequest { TargetConnection = targetConnection });
                
                // Send batches
                for (int batchIdx = 0; batchIdx < totalBatches; batchIdx++)
                {
                    int startIdx = batchIdx * MODS_PER_BATCH;
                    int count = math.min(MODS_PER_BATCH, modifications.Length - startIdx);
                    
                    var batch = new LateJoinSyncBatch
                    {
                        BatchIndex = batchIdx,
                        ModificationCount = count
                    };
                    
                    // Pack modifications into batch
                    for (int i = 0; i < count; i++)
                    {
                        var mod = modifications[startIdx + i];
                        SetBatchModification(ref batch, i, mod.ChunkPos, mod.LocalPos, mod.Density, mod.Material);
                    }
                    
                    var batchEntity = ecb.CreateEntity();
                    ecb.AddComponent(batchEntity, batch);
                    ecb.AddComponent(batchEntity, new SendRpcCommandRequest { TargetConnection = targetConnection });
                }
                
                ecb.DestroyEntity(requestEntity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private void SetBatchModification(ref LateJoinSyncBatch batch, int index, int3 chunk, int3 local, byte density, byte material)
        {
            switch (index)
            {
                case 0: batch.ChunkPos0 = chunk; batch.LocalPos0 = local; batch.Density0 = density; batch.Material0 = material; break;
                case 1: batch.ChunkPos1 = chunk; batch.LocalPos1 = local; batch.Density1 = density; batch.Material1 = material; break;
                case 2: batch.ChunkPos2 = chunk; batch.LocalPos2 = local; batch.Density2 = density; batch.Material2 = material; break;
                case 3: batch.ChunkPos3 = chunk; batch.LocalPos3 = local; batch.Density3 = density; batch.Material3 = material; break;
                case 4: batch.ChunkPos4 = chunk; batch.LocalPos4 = local; batch.Density4 = density; batch.Material4 = material; break;
                case 5: batch.ChunkPos5 = chunk; batch.LocalPos5 = local; batch.Density5 = density; batch.Material5 = material; break;
                case 6: batch.ChunkPos6 = chunk; batch.LocalPos6 = local; batch.Density6 = density; batch.Material6 = material; break;
                case 7: batch.ChunkPos7 = chunk; batch.LocalPos7 = local; batch.Density7 = density; batch.Material7 = material; break;
            }
        }
    }
    
    /// <summary>
    /// Client system that handles late-join synchronization.
    /// Requests and applies modification history from server.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class LateJoinSyncClientSystem : SystemBase
    {
        private bool _syncRequested;
        private bool _syncInProgress;
        private int _expectedBatches;
        private int _receivedBatches;
        private int _appliedModifications;
        private int _failedModifications;
        private float _connectionTime;
        private const float SYNC_DELAY = 2.0f; // Wait for chunks to load before requesting sync
        
        // Deferred modifications for chunks that don't exist yet
        private struct DeferredModification
        {
            public int3 ChunkPos;
            public int3 LocalPos;
            public byte Density;
            public byte Material;
            public int RetryCount;
        }
        private System.Collections.Generic.List<DeferredModification> _deferredMods = new();
        
        public bool SyncInProgress => _syncInProgress;
        public int AppliedModifications => _appliedModifications;
        public int FailedModifications => _failedModifications;
        public float SyncProgress => _expectedBatches > 0 ? (float)_receivedBatches / _expectedBatches : 0f;
        public static bool DiagnosticsEnabled = false;

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Track when we connected
            if (_connectionTime == 0 && SystemAPI.HasSingleton<NetworkId>())
            {
                _connectionTime = UnityEngine.Time.time;
            }
            
            // Request sync after delay (gives chunks time to load)
            if (!_syncRequested && _connectionTime > 0 && UnityEngine.Time.time - _connectionTime >= SYNC_DELAY)
            {
                RequestSync(ecb);
                _syncRequested = true;
            }
            
            // Process sync header
            foreach (var (header, entity) in 
                SystemAPI.Query<LateJoinSyncHeader>()
                .WithAll<ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                if (DiagnosticsEnabled)
                    UnityEngine.Debug.Log($"[LateJoinSync] Client receiving {header.TotalModifications} modifications in {header.TotalBatches} batches");
                
                _syncInProgress = true;
                _expectedBatches = header.TotalBatches;
                _receivedBatches = 0;
                _appliedModifications = 0;
                _failedModifications = 0;
                
                ecb.DestroyEntity(entity);
            }
            
            // Process sync batches
            foreach (var (batch, entity) in 
                SystemAPI.Query<LateJoinSyncBatch>()
                .WithAll<ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                ApplyBatch(batch);
                _receivedBatches++;
                
                if (_receivedBatches >= _expectedBatches)
                {
                    _syncInProgress = false;
                    if (DiagnosticsEnabled)
                        UnityEngine.Debug.Log($"[LateJoinSync] Client sync complete: {_appliedModifications} applied, {_failedModifications} failed (deferred: {_deferredMods.Count})");
                }
                
                ecb.DestroyEntity(entity);
            }
            
            // Retry deferred modifications
            if (_deferredMods.Count > 0 && !_syncInProgress)
            {
                RetryDeferredModifications();
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private void RequestSync(EntityCommandBuffer ecb)
        {
            if (DiagnosticsEnabled)
                UnityEngine.Debug.Log("[LateJoinSync] Client requesting full sync");
            
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new LateJoinSyncRequest { ClientLastTick = 0 });
            ecb.AddComponent(requestEntity, new SendRpcCommandRequest());
        }
        
        private void ApplyBatch(LateJoinSyncBatch batch)
        {
            for (int i = 0; i < batch.ModificationCount; i++)
            {
                GetBatchModification(batch, i, out var chunk, out var local, out var density, out var material);
                
                int3 worldPos = (chunk * VoxelConstants.CHUNK_SIZE) + local;
                
                // Check if chunk exists before attempting modification
                if (ChunkExists(chunk))
                {
                    VoxelOperations.SetVoxel(EntityManager, worldPos, density, material);
                    _appliedModifications++;
                }
                else
                {
                    // Queue for later
                    _deferredMods.Add(new DeferredModification
                    {
                        ChunkPos = chunk,
                        LocalPos = local,
                        Density = density,
                        Material = material,
                        RetryCount = 0
                    });
                    _failedModifications++;
                    UnityEngine.Debug.LogWarning($"[LateJoinSync] Chunk {chunk} doesn't exist yet, deferring modification");
                }
            }
        }
        
        private void RetryDeferredModifications()
        {
            var toRemove = new System.Collections.Generic.List<int>();
            
            for (int i = 0; i < _deferredMods.Count; i++)
            {
                var mod = _deferredMods[i];
                
                if (ChunkExists(mod.ChunkPos))
                {
                    int3 worldPos = (mod.ChunkPos * VoxelConstants.CHUNK_SIZE) + mod.LocalPos;
                    VoxelOperations.SetVoxel(EntityManager, worldPos, mod.Density, mod.Material);
                    toRemove.Add(i);
                    _appliedModifications++;
                    _failedModifications--;
                    UnityEngine.Debug.Log($"[LateJoinSync] Deferred modification applied to chunk {mod.ChunkPos}");
                }
                else
                {
                    // Increment retry count
                    mod.RetryCount++;
                    _deferredMods[i] = mod;
                    
                    // Give up after too many retries
                    if (mod.RetryCount > 300) // ~5 seconds at 60fps
                    {
                        toRemove.Add(i);
                        UnityEngine.Debug.LogError($"[LateJoinSync] Gave up on modification for chunk {mod.ChunkPos}");
                    }
                }
            }
            
            // Remove in reverse order to maintain indices
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                _deferredMods.RemoveAt(toRemove[i]);
            }
        }
        
        private bool ChunkExists(int3 chunkPos)
        {
            // Check if chunk entity exists by querying for ChunkPosition
            using var query = EntityManager.CreateEntityQuery(typeof(Components.ChunkPosition));
            var positions = query.ToComponentDataArray<Components.ChunkPosition>(Allocator.Temp);
            
            bool found = false;
            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i].Value.Equals(chunkPos))
                {
                    found = true;
                    break;
                }
            }
            
            positions.Dispose();
            return found;
        }
        
        private void GetBatchModification(LateJoinSyncBatch batch, int index, out int3 chunk, out int3 local, out byte density, out byte material)
        {
            switch (index)
            {
                case 0: chunk = batch.ChunkPos0; local = batch.LocalPos0; density = batch.Density0; material = batch.Material0; break;
                case 1: chunk = batch.ChunkPos1; local = batch.LocalPos1; density = batch.Density1; material = batch.Material1; break;
                case 2: chunk = batch.ChunkPos2; local = batch.LocalPos2; density = batch.Density2; material = batch.Material2; break;
                case 3: chunk = batch.ChunkPos3; local = batch.LocalPos3; density = batch.Density3; material = batch.Material3; break;
                case 4: chunk = batch.ChunkPos4; local = batch.LocalPos4; density = batch.Density4; material = batch.Material4; break;
                case 5: chunk = batch.ChunkPos5; local = batch.LocalPos5; density = batch.Density5; material = batch.Material5; break;
                case 6: chunk = batch.ChunkPos6; local = batch.LocalPos6; density = batch.Density6; material = batch.Material6; break;
                case 7: chunk = batch.ChunkPos7; local = batch.LocalPos7; density = batch.Density7; material = batch.Material7; break;
                default: chunk = int3.zero; local = int3.zero; density = 0; material = 0; break;
            }
        }
    }
}
