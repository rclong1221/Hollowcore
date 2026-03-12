using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Network
{
    // 1. CLIENT: Process local requests and send to Server
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class VoxelModificationClientSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Find all Modification Requests created by Interaction System
            foreach (var (req, entity) in SystemAPI.Query<RefRO<VoxelModificationRequest>>().WithEntityAccess())
            {
                 // Create RPC Entity
                 var rpcEntity = ecb.CreateEntity();
                 ecb.AddComponent(rpcEntity, new VoxelModificationRpc 
                 {
                     ChunkPos = req.ValueRO.ChunkPos,
                     LocalVoxelPos = req.ValueRO.LocalVoxelPos,
                     NewDensity = req.ValueRO.TargetDensity,
                     NewMaterial = req.ValueRO.TargetMaterial,
                     RequestTick = 0 // Polishing: Add NetworkTime usage later
                 });
                 // Route to Server
                 ecb.AddComponent(rpcEntity, new SendRpcCommandRequest());
                 
                 UnityEngine.Debug.Log($"[Client] Sending VoxelModificationRpc for {req.ValueRO.ChunkPos}:{req.ValueRO.LocalVoxelPos}");
                 
                 // Clean up the local request
                 ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    // 2. SERVER: Receive RPCs, Apply, and Broadcast via Batching System
    [UpdateAfter(typeof(VoxelBatchingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class VoxelModificationServerSystem : SystemBase
    {
        private Entity _eventEntity;
        // private VoxelBatchingSystem _batchingSystem; // REPLACED

        protected override void OnCreate()
        {
            // Create event singleton on server for LootSpawnServerSystem
            _eventEntity = EntityManager.CreateEntity();
            EntityManager.AddBuffer<VoxelDestroyedEvent>(_eventEntity);
            EntityManager.AddComponent<VoxelEventsSingleton>(_eventEntity);
            EntityManager.SetName(_eventEntity, "VoxelEvents_Server");
        }

        protected override void OnUpdate()
        {
             // Get batching queue lazily
             bool hasBatching = SystemAPI.HasSingleton<VoxelBatchingQueue>();
             NativeList<PendingModification> batchQueue = default;
             if (hasBatching)
             {
                 var singleton = SystemAPI.GetSingletonEntity<VoxelBatchingQueue>();
                 batchQueue = EntityManager.GetComponentData<VoxelBatchingQueue>(singleton).Value;
             }
             
             var ecb = new EntityCommandBuffer(Allocator.Temp);
             var eventBuffer = EntityManager.GetBuffer<VoxelDestroyedEvent>(_eventEntity);

             // Process incoming RPCs from remote clients
             foreach (var (rpc, entity) in SystemAPI.Query<VoxelModificationRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
             {
                  // A. Apply to Server World
                  int3 worldPos = (rpc.ChunkPos * VoxelConstants.CHUNK_SIZE) + rpc.LocalVoxelPos;
                  
                  // Get current material BEFORE modification for loot
                  byte oldMaterial = VoxelOperations.GetVoxelMaterial(EntityManager, worldPos);
                  
                  // Use VoxelOperations to safely modify and trigger remesh
                  VoxelOperations.SetVoxel(EntityManager, worldPos, rpc.NewDensity, rpc.NewMaterial);
                  
                  UnityEngine.Debug.Log($"[Server] Applied modification at {rpc.ChunkPos}:{rpc.LocalVoxelPos} from RPC (Material: {oldMaterial})");
                  
                  // B. Emit VoxelDestroyedEvent for loot spawning (if destroying solid voxel)
                  if (rpc.NewDensity == VoxelConstants.DENSITY_AIR && oldMaterial != VoxelConstants.MATERIAL_AIR)
                  {
                      float3 eventPos = (float3)worldPos + new float3(0.5f, 0.5f, 0.5f); // Center of voxel
                      eventBuffer.Add(new VoxelDestroyedEvent
                      {
                          Position = eventPos,
                          MaterialID = oldMaterial,
                          Amount = 1
                      });
                  }
                  
                  // C. Use batching system for broadcast
                  if (hasBatching)
                  {
                      batchQueue.Add(new PendingModification
                      {
                          ChunkPos = rpc.ChunkPos,
                          LocalPos = rpc.LocalVoxelPos,
                          Density = rpc.NewDensity,
                          Material = rpc.NewMaterial,
                          Tick = 0 // Tick updated by batching system
                      });
                  }
                  else
                  {
                      // Fallback: direct broadcast
                      BroadcastModification(ecb, rpc.ChunkPos, rpc.LocalVoxelPos, rpc.NewDensity, rpc.NewMaterial);
                  }

                  // Clean up the RPC request entity
                  ecb.DestroyEntity(entity);
             }
             
             // Process Local Requests (Host/Server generated)
             foreach (var (req, entity) in SystemAPI.Query<RefRO<VoxelModificationRequest>>().WithEntityAccess())
             {
                  // A. Get old material BEFORE modification for loot
                  int3 worldPos = (req.ValueRO.ChunkPos * VoxelConstants.CHUNK_SIZE) + req.ValueRO.LocalVoxelPos;
                  byte oldMaterial = VoxelOperations.GetVoxelMaterial(EntityManager, worldPos);
                  
                  // B. Apply to Server World
                  VoxelOperations.SetVoxel(EntityManager, worldPos, req.ValueRO.TargetDensity, req.ValueRO.TargetMaterial);
                  
                  UnityEngine.Debug.Log($"[Server] Applied modification at {req.ValueRO.ChunkPos}:{req.ValueRO.LocalVoxelPos} from Local Request (Material: {oldMaterial})");
                  
                  // C. Emit VoxelDestroyedEvent for loot spawning (if destroying solid voxel)
                  if (req.ValueRO.TargetDensity == VoxelConstants.DENSITY_AIR && oldMaterial != VoxelConstants.MATERIAL_AIR)
                  {
                      float3 eventPos = (float3)worldPos + new float3(0.5f, 0.5f, 0.5f); // Center of voxel
                      eventBuffer.Add(new VoxelDestroyedEvent
                      {
                          Position = eventPos,
                          MaterialID = oldMaterial,
                          Amount = 1
                      });
                  }
                  
                  // D. Use batching system for broadcast
                  if (hasBatching)
                  {
                      batchQueue.Add(new PendingModification
                      {
                          ChunkPos = req.ValueRO.ChunkPos,
                          LocalPos = req.ValueRO.LocalVoxelPos,
                          Density = req.ValueRO.TargetDensity,
                          Material = req.ValueRO.TargetMaterial,
                          Tick = 0 // Tick updated by batching system
                      });
                  }
                  else
                  {
                      // Fallback: direct broadcast
                      BroadcastModification(ecb, req.ValueRO.ChunkPos, req.ValueRO.LocalVoxelPos, req.ValueRO.TargetDensity, req.ValueRO.TargetMaterial);
                  }

                  // Clean up the request entity
                  ecb.DestroyEntity(entity);
             }

             ecb.Playback(EntityManager);
             ecb.Dispose();
        }

        // Fallback for when batching system isn't available
        private void BroadcastModification(EntityCommandBuffer ecb, int3 chunkPos, int3 localPos, byte density, byte material)
        {
              var broadcastData = new VoxelModificationBroadcast
              {
                  ChunkPos = chunkPos,
                  LocalVoxelPos = localPos,
                  NewDensity = density,
                  NewMaterial = material,
                  ServerTick = 0
              };
              
              foreach (var (id, targetEntity) in SystemAPI.Query<NetworkId>().WithEntityAccess())
              {
                  var broadcastEntity = ecb.CreateEntity();
                  ecb.AddComponent(broadcastEntity, broadcastData);
                  ecb.AddComponent(broadcastEntity, new SendRpcCommandRequest { TargetConnection = targetEntity });
              }
        }
    }
    
    // 3. CLIENT: Receive Broadcasts from Server
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class VoxelModificationReceiveSystem : SystemBase
    {
         protected override void OnUpdate()
         {
             var ecb = new EntityCommandBuffer(Allocator.Temp);
             
             // 1. Process Batched Broadcasts (Preferred)
             foreach (var (rpc, entity) in SystemAPI.Query<VoxelModificationBatchRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
             {
                 int count = math.min(rpc.Count, 64); // Safety cap
                 
                 for (int i = 0; i < count; i++)
                 {
                     int3 chunkPos = new int3(rpc.ChunkX[i], rpc.ChunkY[i], rpc.ChunkZ[i]);
                     int3 localPos = new int3(rpc.LocalX[i], rpc.LocalY[i], rpc.LocalZ[i]);
                     
                     int3 worldPos = (chunkPos * VoxelConstants.CHUNK_SIZE) + localPos;
                     
                     // Apply to Client World
                     VoxelOperations.SetVoxel(EntityManager, worldPos, rpc.NewDensity[i], rpc.NewMaterial[i]);
                 }
                 
                 UnityEngine.Debug.Log($"[Client] Received Batch of {count} modifications");
                 
                 // Clean up
                 ecb.DestroyEntity(entity);
             }
             
             // 2. Process Legacy/Fallback Individual Broadcasts
             foreach (var (rpc, entity) in SystemAPI.Query<VoxelModificationBroadcast>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
             {
                  int3 worldPos = (rpc.ChunkPos * VoxelConstants.CHUNK_SIZE) + rpc.LocalVoxelPos;
                  
                  // Apply to Client World
                  VoxelOperations.SetVoxel(EntityManager, worldPos, rpc.NewDensity, rpc.NewMaterial);
                  
                  UnityEngine.Debug.Log($"[Client] Received Broadcast and applied at {rpc.ChunkPos}:{rpc.LocalVoxelPos}");
                  
                  // Clean up
                  ecb.DestroyEntity(entity);
             }
             ecb.Playback(EntityManager);
             ecb.Dispose();
         }
    }

    // 4. LOCAL/STANDALONE: Offline Mode
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class VoxelModificationLocalSystem : SystemBase
    {
        protected override void OnUpdate()
        {
             var ecb = new EntityCommandBuffer(Allocator.Temp);
             foreach (var (req, entity) in SystemAPI.Query<RefRO<VoxelModificationRequest>>().WithEntityAccess())
             {
                  // Direct application
                  int3 worldPos = (req.ValueRO.ChunkPos * VoxelConstants.CHUNK_SIZE) + req.ValueRO.LocalVoxelPos;
                  VoxelOperations.SetVoxel(EntityManager, worldPos, req.ValueRO.TargetDensity, req.ValueRO.TargetMaterial);
                  
                  UnityEngine.Debug.Log($"[Local] Applied modification at {req.ValueRO.ChunkPos}:{req.ValueRO.LocalVoxelPos}");
                  
                  ecb.DestroyEntity(entity);
             }
             ecb.Playback(EntityManager);
             ecb.Dispose();
        }
    }
}
