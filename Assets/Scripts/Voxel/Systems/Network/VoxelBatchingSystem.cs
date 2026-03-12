using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Voxel.Core;
using Unity.Burst;

namespace DIG.Voxel.Systems.Network
{
    /// <summary>
    /// Server system that batches voxel modifications into single RPCs.
    /// Reduces network traffic by combining multiple changes per network tick.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelModificationServerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct VoxelBatchingSystem : ISystem
    {
        private double _lastBatchTime;
        private uint _currentTick;
        private Entity _singletonEntity;
        
        // Stats
        private double _lastStatTime;
        private int _modsThisSecond;
        private int _batchesThisSecond;
        
        // Config
        private const float BATCH_INTERVAL = 0.05f; // 20 batches per second max
        private const int MAX_MODS_PER_BATCH = 64;
        private const int MIN_MODS_FOR_BATCH_RPC = 10;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            // Create singleton to hold data
            _singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(_singletonEntity, new VoxelBatchingQueue 
            { 
                Value = new NativeList<PendingModification>(256, Allocator.Persistent) 
            });
            state.EntityManager.AddComponentData(_singletonEntity, new VoxelHistory 
            { 
                Value = new NativeList<PendingModification>(1024, Allocator.Persistent) 
            });
            state.EntityManager.AddComponentData(_singletonEntity, new VoxelBatchingStats());
            
            UnityEngine.Debug.Log("[VoxelBatching] Server batching system created (ISystem)");
        }

        public void OnDestroy(ref SystemState state)
        {
            if (state.EntityManager.Exists(_singletonEntity))
            {
                if (state.EntityManager.HasComponent<VoxelBatchingQueue>(_singletonEntity))
                {
                    var queue = state.EntityManager.GetComponentData<VoxelBatchingQueue>(_singletonEntity);
                    if (queue.Value.IsCreated) queue.Value.Dispose();
                }
                
                if (state.EntityManager.HasComponent<VoxelHistory>(_singletonEntity))
                {
                    var history = state.EntityManager.GetComponentData<VoxelHistory>(_singletonEntity);
                    if (history.Value.IsCreated) history.Value.Dispose();
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // Update tick
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            _currentTick = netTime.ServerTick.TickIndexForValidTick;
            
            // Get data
            if (!SystemAPI.HasComponent<VoxelBatchingQueue>(_singletonEntity) || 
                !SystemAPI.HasComponent<VoxelHistory>(_singletonEntity) ||
                !SystemAPI.HasComponent<VoxelBatchingStats>(_singletonEntity)) return;

            var queueComp = state.EntityManager.GetComponentData<VoxelBatchingQueue>(_singletonEntity);
            var historyComp = state.EntityManager.GetComponentData<VoxelHistory>(_singletonEntity);
            var statsComp = state.EntityManager.GetComponentData<VoxelBatchingStats>(_singletonEntity);
            
            // Should we send a batch?
            double currentTime = SystemAPI.Time.ElapsedTime;
            bool shouldSend = (currentTime - _lastBatchTime >= BATCH_INTERVAL) && queueComp.Value.Length > 0;
            bool batchFull = queueComp.Value.Length >= MAX_MODS_PER_BATCH;
            
            if (shouldSend || batchFull)
            {
                // Process Batch
                SendBatch(ref state, ref queueComp.Value, _currentTick, ref statsComp);
                
                // Update ticks for history
                for (int i = 0; i < queueComp.Value.Length; i++)
                {
                    var mod = queueComp.Value[i];
                    mod.Tick = _currentTick;
                    queueComp.Value[i] = mod;
                }
                
                // Add to History and Clear
                historyComp.Value.AddRange(queueComp.Value.AsArray());
                
                queueComp.Value.Clear();
                
                _lastBatchTime = currentTime;
            }

            // Update Stats
            if (currentTime - _lastStatTime >= 1.0)
            {
                statsComp.RollingModsPerSec = _modsThisSecond;
                statsComp.RollingBatchesPerSec = _batchesThisSecond;
                
                _modsThisSecond = 0;
                _batchesThisSecond = 0;
                _lastStatTime = currentTime;
            }
            statsComp.ModificationsThisSecond = _modsThisSecond;
            
            // Only PendingCount is dynamic based on queue
            // But queue is cleared immediately in this system if sent.
            // If we are accumulating, it will show up.
            // But VoxelBatchingStats doesn't store pending count, we can access it from Queue component in editor window.
            // Wait, editor window uses PendingCount property. I should probably add it to the struct or logic?
            // Actually, Editor window loop can query VoxelBatchingQueue instead.
            // But let's check VoxelBatchingStats definition again.
            // I didn't add PendingCount to VoxelBatchingStats. It's fine, Editor can read Queue length.
            // But the error says 'VoxelBatchingSystem' does not contain 'PendingCount'.
            // So Editor is trying to read it from System.
            // I will fix Editor to read from Queue component.
            
            state.EntityManager.SetComponentData(_singletonEntity, statsComp);
        }

        [BurstCompile]
        private void SendBatch(ref SystemState state, ref NativeList<PendingModification> pendingBatch, uint currentTick, ref VoxelBatchingStats stats)
        {
            if (pendingBatch.Length == 0) return;
            
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            // Loop until all pending are sent
            int processed = 0;
            while (processed < pendingBatch.Length)
            {
                int remaining = pendingBatch.Length - processed;
                bool useBatchRpc = remaining >= MIN_MODS_FOR_BATCH_RPC;
                
                if (useBatchRpc)
                {
                    int batchSize = math.min(remaining, MAX_MODS_PER_BATCH);
                    
                    var rpcBuffer = new VoxelModificationBatchRpc
                    {
                        Count = batchSize,
                        ServerTick = currentTick
                    };
                    
                    // Pack data into FixedLists
                    for (int i = 0; i < batchSize; i++)
                    {
                        var mod = pendingBatch[processed + i];
                        rpcBuffer.ChunkX.Add(mod.ChunkPos.x);
                        rpcBuffer.ChunkY.Add(mod.ChunkPos.y);
                        rpcBuffer.ChunkZ.Add(mod.ChunkPos.z);
                        rpcBuffer.LocalX.Add(mod.LocalPos.x);
                        rpcBuffer.LocalY.Add(mod.LocalPos.y);
                        rpcBuffer.LocalZ.Add(mod.LocalPos.z);
                        rpcBuffer.NewDensity.Add(mod.Density);
                        rpcBuffer.NewMaterial.Add(mod.Material);
                    }
                    
                    // Broadcast
                    foreach (var (id, targetEntity) in SystemAPI.Query<NetworkId>().WithEntityAccess())
                    {
                        var broadcastEntity = ecb.CreateEntity();
                        ecb.AddComponent(broadcastEntity, rpcBuffer);
                        ecb.AddComponent(broadcastEntity, new SendRpcCommandRequest { TargetConnection = targetEntity });
                    }
                    
                    stats.TotalBatchesSent++;
                    _batchesThisSecond++;
                    
                    processed += batchSize;
                }
                else
                {
                    // Use Individual RPCs for small remaining count
                    for (int i = 0; i < remaining; i++)
                    {
                        var mod = pendingBatch[processed + i];
                        var broadcastData = new VoxelModificationBroadcast
                        {
                            ChunkPos = mod.ChunkPos,
                            LocalVoxelPos = mod.LocalPos,
                            NewDensity = mod.Density,
                            NewMaterial = mod.Material,
                            ServerTick = currentTick
                        };
                        
                        foreach (var (id, targetEntity) in SystemAPI.Query<NetworkId>().WithEntityAccess())
                        {
                            var broadcastEntity = ecb.CreateEntity();
                            ecb.AddComponent(broadcastEntity, broadcastData);
                            ecb.AddComponent(broadcastEntity, new SendRpcCommandRequest { TargetConnection = targetEntity });
                        }
                        
                        // Treat individual broadcasts as a "batch" of 1 for stats purposes?
                        // Or just don't count them as batches?
                        // Previous code might have ignored them or counted them.
                        // Let's count them as 1 batch/msg for bandwidth estimation purposes
                        // But TotalBatchesSent usually implies distinct RPCs sent.
                        // Here we send N entities.
                        stats.TotalBatchesSent++; // Each single RPC entity is a "batch" effectively
                        _batchesThisSecond++;
                    }
                    processed += remaining;
                }
            }
            
            stats.TotalModificationsSent += pendingBatch.Length;
            _modsThisSecond += pendingBatch.Length;
        }
    }
}
