using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Server system that receives RagdollSettledV3Rpc and updates player position.
    /// When a client's ragdoll settles, they send their final position here.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RagdollSettleServerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (rpc, rpcEntity) in 
                SystemAPI.Query<RefRO<RagdollSettledV3Rpc>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess())
            {
                var finalPos = rpc.ValueRO.FinalPosition;
                var finalRot = rpc.ValueRO.FinalRotation;
                var ghostId = rpc.ValueRO.PlayerGhostId;
                
                // Find entity by ghostId (O(N) lookup, but infrequent event)
                Entity playerEntity = Entity.Null;
                foreach (var (ghost, entity) in SystemAPI.Query<RefRO<GhostInstance>>().WithEntityAccess())
                {
                    if (ghost.ValueRO.ghostId == ghostId)
                    {
                        playerEntity = entity;
                        break;
                    }
                }
                
                // Update player's LocalTransform if found
                if (playerEntity != Entity.Null && SystemAPI.HasComponent<LocalTransform>(playerEntity))
                {
                    var transform = SystemAPI.GetComponentRW<LocalTransform>(playerEntity);
                    float dist = math.distance(transform.ValueRO.Position, finalPos);
                    
                    // Always warn on large jumps (Teleport detection)
                    if (dist > 3.0f && RagdollSettleClientSystem.DiagnosticsEnabled)
                    {
                        Debug.LogWarning($"[RagdollSettleServer] ⚠️ TELEPORT DETECTED: Player {playerEntity} (Ghost {ghostId}) snapped {dist:F2}m! Old:{transform.ValueRO.Position} New:{finalPos}");
                    }
                    
                    // DISABLED: Don't update ECS entity transform - this causes ghost sync to push the ragdoll
                    // The ragdoll position is purely visual and doesn't need to update the ECS entity
                    // transform.ValueRW.Position = finalPos;
                    // transform.ValueRW.Rotation = finalRot;
                    
                    if (RagdollSettleClientSystem.DiagnosticsEnabled)
                        Debug.Log($"[RagdollSettleServer] SKIPPED transform update for {playerEntity} (ghostId {ghostId}) - ragdoll at {finalPos} (Delta: {dist:F2}m)");
                }
                else
                {
                    // Debug.LogWarning($"[RagdollSettleServer] Could not find player entity for ghostId {ghostId}");
                }
                
                // Consume the RPC
                ecb.DestroyEntity(rpcEntity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Client system that sends RagdollSettledV2Rpc when notified by the presentation bridge.
    /// Uses a singleton component to receive the signal from the MonoBehaviour.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RagdollSettleClientSystem : SystemBase
    {
        /// <summary>
        /// Global diagnostic toggle for ragdoll systems. Set to true to enable verbose logging.
        /// </summary>
        public static bool DiagnosticsEnabled = false;

        private Unity.NetCode.Hybrid.GhostPresentationGameObjectSystem _presentationSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }
        
        protected override void OnUpdate()
        {
            // Lazy retrieval of presentation system
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<Unity.NetCode.Hybrid.GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null) return;
            }

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            // Iterate ONLY local players relative to this world
            // This ensures we don't process other clients' players if multiple clients are in same process
            foreach (var (ghost, isLocal, entity) in 
                SystemAPI.Query<RefRO<GhostInstance>, RefRO<GhostOwnerIsLocal>>()
                    .WithEntityAccess())
            {
                // Get presentation object for this LOCAL player
                var go = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (go == null) continue;

                var bridge = go.GetComponent<Player.Animation.RagdollPresentationBridge>();
                if (bridge == null) continue;

                // Poll settlement state
                if (bridge.HasSettled)
                {
                    float3 settlePos = bridge.SettledPosition;
                    quaternion settleRot = bridge.SettledRotation;
                    int ghostId = ghost.ValueRO.ghostId;
                    
                    // Consume event
                    bridge.ClearSettleState();

                    SendSettleRPC(ecb, ghostId, settlePos, settleRot);
                }
            }
        }

        private void SendSettleRPC(EntityCommandBuffer ecb, int ghostId, float3 position, quaternion rotation)
        {
            // Find connection entity to send RPC
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
                Debug.LogWarning("[RagdollSettleClient] No connection entity found");
                return;
            }
            
            // Create and send RPC
            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new RagdollSettledV3Rpc
            {
                FinalPosition = position,
                FinalRotation = rotation,
                PlayerGhostId = ghostId
            });
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });
            
            if (DiagnosticsEnabled)
                Debug.Log($"[RagdollSettleClient] Sent ragdoll settle RPC for Ghost {ghostId}: position={position} rotation={rotation}");
        }
    }
}
