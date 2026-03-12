using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Survival.Physics;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// SERVER: Receives RagdollPushRpc and applies impulse to ragdoll physics entities.
    /// This allows players on any client to push dead bodies and have the result sync to all clients.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RagdollPushServerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (rpc, rpcEntity) in 
                SystemAPI.Query<RefRO<RagdollPushRpc>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess())
            {
                var targetGhostId = rpc.ValueRO.TargetGhostId;
                var impulse = rpc.ValueRO.Impulse;
                var worldPos = rpc.ValueRO.WorldPosition;
                
                // Find the ragdoll entity by ghostId
                Entity ragdollEntity = Entity.Null;
                Entity pelvisEntity = Entity.Null;
                
                foreach (var (ghost, ragdollCtrl, entity) in 
                    SystemAPI.Query<RefRO<GhostInstance>, RefRO<RagdollController>>()
                        .WithEntityAccess())
                {
                    if (ghost.ValueRO.ghostId == targetGhostId && ragdollCtrl.ValueRO.IsRagdolled)
                    {
                        ragdollEntity = entity;
                        pelvisEntity = ragdollCtrl.ValueRO.Pelvis;
                        break;
                    }
                }
                
                // Apply impulse to pelvis if found and has physics
                if (pelvisEntity != Entity.Null && SystemAPI.HasComponent<PhysicsVelocity>(pelvisEntity))
                {
                    var velocity = SystemAPI.GetComponentRW<PhysicsVelocity>(pelvisEntity);
                    var mass = SystemAPI.HasComponent<PhysicsMass>(pelvisEntity) 
                        ? SystemAPI.GetComponent<PhysicsMass>(pelvisEntity) 
                        : PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 50f);
                    
                    // Apply impulse (convert from impulse to velocity change based on mass)
                    float invMass = mass.InverseMass;
                    velocity.ValueRW.Linear += impulse * invMass;
                    
                    if (RagdollSettleClientSystem.DiagnosticsEnabled)
                    {
                        Debug.Log($"[RagdollPush:Server] Applied impulse {impulse} to Ghost {targetGhostId} pelvis. New velocity: {velocity.ValueRO.Linear}");
                    }
                }
                else if (RagdollSettleClientSystem.DiagnosticsEnabled)
                {
                    Debug.LogWarning($"[RagdollPush:Server] Could not find ragdolling entity for Ghost {targetGhostId} or pelvis missing physics");
                }
                
                // Consume the RPC
                ecb.DestroyEntity(rpcEntity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// CLIENT: Detects ragdoll push events from RagdollPresentationBridge and sends RPC to server.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RagdollPushClientSystem : SystemBase
    {
        private Unity.NetCode.Hybrid.GhostPresentationGameObjectSystem _presentationSystem;
        
        // Static queue for push events from MonoBehaviour (RagdollPresentationBridge)
        private static System.Collections.Generic.Queue<PushEvent> _pendingPushes = new();
        private static object _pushLock = new object();
        
        public struct PushEvent
        {
            public int TargetGhostId;
            public float3 WorldPosition;
            public float3 Impulse;
        }
        
        /// <summary>
        /// Called by RagdollPresentationBridge when a push is detected.
        /// </summary>
        public static void QueuePush(int targetGhostId, Vector3 worldPos, Vector3 impulse)
        {
            lock (_pushLock)
            {
                _pendingPushes.Enqueue(new PushEvent
                {
                    TargetGhostId = targetGhostId,
                    WorldPosition = new float3(worldPos.x, worldPos.y, worldPos.z),
                    Impulse = new float3(impulse.x, impulse.y, impulse.z)
                });
            }
        }

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Process pending push events
            lock (_pushLock)
            {
                while (_pendingPushes.Count > 0)
                {
                    var pushEvent = _pendingPushes.Dequeue();
                    SendPushRpc(pushEvent);
                }
            }
        }
        
        private void SendPushRpc(PushEvent pushEvent)
        {
            // Find connection entity
            Entity connectionEntity = Entity.Null;
            foreach (var (_, entity) in 
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess())
            {
                connectionEntity = entity;
                break;
            }
            
            if (connectionEntity == Entity.Null)
            {
                if (RagdollSettleClientSystem.DiagnosticsEnabled)
                    Debug.LogWarning("[RagdollPush:Client] No connection entity found");
                return;
            }
            
            // Create and send RPC
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);
            
            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new RagdollPushRpc
            {
                TargetGhostId = pushEvent.TargetGhostId,
                WorldPosition = pushEvent.WorldPosition,
                Impulse = pushEvent.Impulse
            });
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });
            
            if (RagdollSettleClientSystem.DiagnosticsEnabled)
            {
                Debug.Log($"[RagdollPush:Client] Sent push RPC for Ghost {pushEvent.TargetGhostId}: impulse={pushEvent.Impulse}");
            }
        }
    }
}
