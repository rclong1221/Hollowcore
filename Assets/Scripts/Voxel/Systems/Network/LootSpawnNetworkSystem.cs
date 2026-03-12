using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Interaction;

namespace DIG.Voxel.Systems.Network
{
    /// <summary>
    /// SERVER: Listens for VoxelDestroyedEvents and broadcasts loot spawn commands to all clients.
    /// This ensures all players see loot when ANY player mines voxels.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelModificationServerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class LootSpawnServerSystem : SystemBase
    {
        private VoxelMaterialRegistry _registry;
        private Unity.Mathematics.Random _random;
        private NativeList<PendingLoot> _pendingLoot;
        private double _lastBatchTime;
        
        private const int MAX_LOOT_PER_BATCH = 32;

        public struct PendingLoot
        {
            public float3 Position;
            public float3 MinePoint; // For radial scatter direction
            public byte MaterialID;
        }

        protected override void OnCreate()
        {
            _random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            _pendingLoot = new NativeList<PendingLoot>(128, Allocator.Persistent);
            
            // Load physics settings
            _physicsSettings = Resources.Load<LootPhysicsSettings>("LootPhysicsSettings");
            if (_physicsSettings == null)
            {
                _physicsSettings = LootPhysicsSettings.CreateDefault();
            }
        }
        
        private LootPhysicsSettings _physicsSettings;

        protected override void OnDestroy()
        {
             if (_pendingLoot.IsCreated) _pendingLoot.Dispose();
        }

        protected override void OnUpdate()
        {
            // 1. Ensure Registry
            if (_registry == null)
            {
                _registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
                if (_registry == null) return;
                _registry.Initialize();
            }

            // 2. Process Voxel Events -> Pending Loot
            if (SystemAPI.TryGetSingletonEntity<VoxelEventsSingleton>(out Entity eventEntity)) 
            {
                var buffer = EntityManager.GetBuffer<VoxelDestroyedEvent>(eventEntity);
                if (!buffer.IsEmpty)
                {
                    if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnServer] Processing {buffer.Length} VoxelDestroyedEvents");
                    foreach (var evt in buffer)
                    {
                        var matDef = _registry.GetMaterial(evt.MaterialID);
                        if (matDef == null || matDef.LootPrefab == null || matDef.DropChance <= 0f) continue;
                        
                        if (_random.NextFloat() > matDef.DropChance) continue;

                        int dropCount = _random.NextInt(matDef.MinDropCount, matDef.MaxDropCount + 1);
                        
                        for (int i = 0; i < dropCount; i++)
                        {
                            // Task 10.17.11: Improved spawn spread
                            float3 pos = evt.Position + new float3(
                                _random.NextFloat(-0.25f, 0.25f),
                                _random.NextFloat(0.1f, 0.4f),
                                _random.NextFloat(-0.25f, 0.25f)
                            );

                            _pendingLoot.Add(new PendingLoot
                            {
                                Position = pos,
                                MinePoint = evt.Position,
                                MaterialID = evt.MaterialID
                            });
                        }
                        if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnServer] Material {evt.MaterialID} ({matDef?.MaterialName}) -> {dropCount} items pending");
                    }
                    buffer.Clear();
                }
            }

            // 3. Send Batches
            if (_pendingLoot.Length > 0)
            {
                SendBatches();
            }
        }

        private void SendBatches()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp); 
            
            int processed = 0;
            while (processed < _pendingLoot.Length)
            {
                int remaining = _pendingLoot.Length - processed;
                int batchSize = math.min(remaining, MAX_LOOT_PER_BATCH);
                
                var batchRpc = new LootSpawnBatchRpc
                {
                    Count = batchSize
                };
                
                for (int i = 0; i < batchSize; i++)
                {
                    var item = _pendingLoot[processed + i];
                    batchRpc.PosX.Add(item.Position.x);
                    batchRpc.PosY.Add(item.Position.y);
                    batchRpc.PosZ.Add(item.Position.z);
                    // Send mine point for radial scatter direction calculation on client
                    batchRpc.VelX.Add(item.MinePoint.x);
                    batchRpc.VelY.Add(item.MinePoint.y);
                    batchRpc.VelZ.Add(item.MinePoint.z);
                    batchRpc.Materials.Add(item.MaterialID);
                }
                
                // Broadcast
                foreach (var (id, targetEntity) in SystemAPI.Query<NetworkId>().WithEntityAccess())
                {
                    var broadcastEntity = ecb.CreateEntity();
                    ecb.AddComponent(broadcastEntity, batchRpc);
                    ecb.AddComponent(broadcastEntity, new SendRpcCommandRequest { TargetConnection = targetEntity });
                }
                if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnServer] Sending batch of {batchSize} loot items to clients");
                
                processed += batchSize;
            }
            
            _pendingLoot.Clear();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// CLIENT: Receives LootSpawnBroadcast and instantiates visual loot prefabs.
    /// This is cosmetic-only - no authoritative loot state.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class LootSpawnClientSystem : SystemBase
    {
        private VoxelMaterialRegistry _registry;
        private LootPhysicsSettings _physicsSettings;
        private System.Random _random;

        protected override void OnCreate()
        {
            _random = new System.Random();
            
            // Load physics settings
            _physicsSettings = Resources.Load<LootPhysicsSettings>("LootPhysicsSettings");
            if (_physicsSettings == null)
            {
                _physicsSettings = LootPhysicsSettings.CreateDefault();
            }
        }

        protected override void OnUpdate()
        {
            // Ensure registry is loaded
            if (_registry == null)
            {
                _registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
                if (_registry == null) return;
                _registry.Initialize();
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Process Batched Loot Spawns
            foreach (var (rpc, entity) in SystemAPI.Query<LootSpawnBatchRpc>()
                .WithAll<ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                 int count = math.min(rpc.Count, 32);                  if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnClient] Received batch RPC with {count} items");
                 
                 for (int i = 0; i < count; i++)
                 {
                     byte matID = rpc.Materials[i];
                     var matDef = _registry.GetMaterial(matID);
                     
                     if (matDef != null && matDef.LootPrefab != null)
                     {
                         float3 pos = new float3(rpc.PosX[i], rpc.PosY[i], rpc.PosZ[i]);
                         float3 minePoint = new float3(rpc.VelX[i], rpc.VelY[i], rpc.VelZ[i]);
                         
                         // Instantiate
                         var go = Object.Instantiate(matDef.LootPrefab, pos, Quaternion.identity);
                          if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnClient] Spawned {go.name} at {pos}");
                         
                         // Task 10.17.11 & 10.17.12: Apply physics settings
                         // Calculate radial scatter velocity 
                         Vector3 scatterVel = _physicsSettings.CalculateScatterVelocity(
                             minePoint, pos, _random);
                         
                         Vector3 angularVel = new Vector3(
                             (float)(_random.NextDouble() * 2 - 1) * 5f,
                             (float)(_random.NextDouble() * 2 - 1) * 5f,
                             (float)(_random.NextDouble() * 2 - 1) * 5f
                         );
                         
                         // Check if Unity's built-in physics is disabled (DOTS/NetCode mode)
                         if (UnityEngine.Physics.simulationMode == SimulationMode.Script)
                         {
                             // Use manual physics simulator
                             var simulator = go.GetComponent<LootPhysicsSimulator>();
                             if (simulator == null)
                             {
                              simulator = go.AddComponent<LootPhysicsSimulator>();
                             }
                             
                             // Ensure proper collision setup for Player Interaction
                             var col = go.GetComponentInChildren<Collider>();
                             if (col == null) 
                                 col = go.AddComponent<BoxCollider>();
                             
                             col.isTrigger = false; // Must be solid for KCC to detect it
                             go.layer = 0; // Default layer ensures KCC hits it
                             
                             simulator.Initialize(scatterVel, angularVel, _physicsSettings.Drag, _physicsSettings.AngularDrag);
                             
                             // Disable Rigidbody to prevent conflicts
                             var rb = go.GetComponent<Rigidbody>();
                             if (rb != null)
                             {
                                 rb.isKinematic = true;
                             }
                             
                             if (_physicsSettings.EnableDebugLogs) UnityEngine.Debug.Log($"[LootSpawnClient] Using manual physics simulator for {go.name}, velocity: {scatterVel}");
                         }
                         else
                         {
                             // Use Unity's built-in Rigidbody physics
                             var rb = go.GetComponent<Rigidbody>();
                             if (rb == null)
                             {
                                 rb = go.AddComponent<Rigidbody>();
                             }
                             
                             float massMultiplier = matDef.LootMassMultiplier > 0 ? matDef.LootMassMultiplier : 1f;
                             _physicsSettings.ConfigureRigidbody(rb, massMultiplier);
                             rb.constraints = RigidbodyConstraints.None;
                             rb.WakeUp();
                             rb.linearVelocity = scatterVel;
                             rb.angularVelocity = angularVel;
                         }
                         
                         // Task 10.17.15: Add lifetime
                         var lifetime = go.GetComponent<LootLifetime>();
                         if (lifetime == null)
                         {
                             lifetime = go.AddComponent<LootLifetime>();
                         }
                         lifetime.Initialize(_physicsSettings.Lifetime, _physicsSettings.FadeDuration);
                     }
                 }
                 ecb.DestroyEntity(entity);
            }

            // 2. Process Single Loot Spawns (Legacy/Fallback)
            foreach (var (rpc, entity) in SystemAPI.Query<LootSpawnBroadcast>()
                .WithAll<ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                var matDef = _registry.GetMaterial(rpc.MaterialID);
                if (matDef != null && matDef.LootPrefab != null)
                {
                    var go = Object.Instantiate(matDef.LootPrefab, rpc.Position, Quaternion.identity);
                    
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = go.AddComponent<Rigidbody>();
                    }
                    
                    float massMultiplier = matDef.LootMassMultiplier > 0 ? matDef.LootMassMultiplier : 1f;
                    _physicsSettings.ConfigureRigidbody(rb, massMultiplier);
                    rb.AddForce(rpc.Velocity, ForceMode.Impulse);
                    
                    var lifetime = go.GetComponent<LootLifetime>();
                    if (lifetime == null)
                    {
                        lifetime = go.AddComponent<LootLifetime>();
                    }
                    lifetime.Initialize(_physicsSettings.Lifetime, _physicsSettings.FadeDuration);
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
