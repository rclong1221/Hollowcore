using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using UnityEngine;
using Player.Components;
using DIG.Validation;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Player.Systems
{
    /// <summary>
    /// CLIENT-SIDE debug system for Epic 4.5 (Respawn/Revive).
    /// Detects debug key inputs and sends RPC to server.
    /// 
    /// FIX FOR BUG 2.8.1: Previously this ran on server with GhostOwnerIsLocal
    /// which is invalid - GhostOwnerIsLocal is a client-side concept.
    /// Now properly splits: Client detects input -> Server applies to correct player.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RespawnDebugClientSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
        }
        
        protected override void OnUpdate()
        {
            if (!Application.isEditor) return;

            bool shiftPressed = false;
            bool ctrlPressed = false;
            bool kPressed = false;
            bool dPressed = false;
            bool rPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                shiftPressed = kb.shiftKey.isPressed;
                ctrlPressed = kb.ctrlKey.isPressed;
                kPressed = kb.kKey.wasPressedThisFrame;
                dPressed = kb.dKey.wasPressedThisFrame;
                rPressed = kb.rKey.wasPressedThisFrame;
            }
#else
            shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            kPressed = Input.GetKeyDown(KeyCode.K);
            dPressed = Input.GetKeyDown(KeyCode.D);
            rPressed = Input.GetKeyDown(KeyCode.R);
#endif

            // Ctrl + Shift + K: Kill (Direct to Dead)
            if (ctrlPressed && shiftPressed && kPressed)
            {
                SendDeathDebugRpc(DeathPhase.Dead, false);
                Debug.Log("[DebugClient] Sending DeathDebugRpc: Dead");
            }
            
            // Ctrl + Shift + D: Downed
            if (ctrlPressed && shiftPressed && dPressed)
            {
                SendDeathDebugRpc(DeathPhase.Downed, false);
                Debug.Log("[DebugClient] Sending DeathDebugRpc: Downed");
            }
            
            // Ctrl + Shift + R: Revive (Alive + Restore Health)
            if (ctrlPressed && shiftPressed && rPressed)
            {
                SendDeathDebugRpc(DeathPhase.Alive, true);
                Debug.Log("[DebugClient] Sending DeathDebugRpc: Alive + RestoreHealth");
            }
        }

        private void SendDeathDebugRpc(DeathPhase phase, bool restoreHealth)
        {
            var rpcEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rpcEntity, new DeathDebugRpc
            {
                RequestedPhase = phase,
                RestoreHealth = restoreHealth
            });
            EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest());
        }
    }

    /// <summary>
    /// SERVER-SIDE system that processes DeathDebugRpc requests.
    /// Uses the connection entity to identify which player sent the request.
    /// 
    /// This ensures only the requesting player is affected, not all players.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class RespawnDebugServerSystem : SystemBase
    {
        private ComponentLookup<NetworkId> _networkIdLookup;
        
        protected override void OnCreate()
        {
            _networkIdLookup = GetComponentLookup<NetworkId>(isReadOnly: true);
            RequireForUpdate<NetworkTime>();
        }
        
        protected override void OnUpdate()
        {
            if (!Application.isEditor) return;
            
            _networkIdLookup.Update(this);
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Periodic heartbeat to confirm system is running
            // if ((int)(currentTime * 2) % 10 == 0)
            // {
            //     int rawRpcCount = 0;
            //     foreach (var _ in SystemAPI.Query<RefRO<DeathDebugRpc>>())
            //         rawRpcCount++;
                    
            //     int rpcWithReceive = 0;
            //     foreach (var _ in SystemAPI.Query<RefRO<DeathDebugRpc>, RefRO<ReceiveRpcCommandRequest>>())
            //         rpcWithReceive++;
                    
            //     Debug.Log($"[DebugServer:HEARTBEAT] t={currentTime:F1} RawRpcs={rawRpcCount} WithReceive={rpcWithReceive}");
            // }
            
            // FORCED DEBUG: Count incoming RPCs
            int rpcCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<DeathDebugRpc>, RefRO<ReceiveRpcCommandRequest>>())
                rpcCount++;
            if (rpcCount > 0)
                Debug.Log($"[DebugServer:FORCED] Found {rpcCount} DeathDebugRpc entities to process");
            
            // Process incoming RPC requests
            foreach (var (rpc, receiveRequest, rpcEntity) in 
                SystemAPI.Query<RefRO<DeathDebugRpc>, RefRO<ReceiveRpcCommandRequest>>()
                    .WithEntityAccess())
            {
                var sourceConnection = receiveRequest.ValueRO.SourceConnection;

                // Get the NetworkId of the connection that sent this RPC
                if (!_networkIdLookup.HasComponent(sourceConnection))
                {
                    Debug.LogWarning("[DebugServer] RPC source connection has no NetworkId");
                    ecb.DestroyEntity(rpcEntity);
                    continue;
                }

                // --- ANTI-CHEAT: Rate limit check ---
                Entity respawnPlayer = Entity.Null;
                if (SystemAPI.HasComponent<CommandTarget>(sourceConnection))
                    respawnPlayer = SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
                if (respawnPlayer != Entity.Null && EntityManager.HasComponent<ValidationLink>(respawnPlayer))
                {
                    var valChild = EntityManager.GetComponentData<ValidationLink>(respawnPlayer).ValidationChild;
                    if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.RESPAWN))
                    {
                        RateLimitHelper.CreateViolation(EntityManager, respawnPlayer,
                            ViolationType.RateLimit, 0.5f, RpcTypeIds.RESPAWN, 0);
                        ecb.DestroyEntity(rpcEntity);
                        continue;
                    }
                }
                // --- END ANTI-CHEAT ---

                var networkId = _networkIdLookup[sourceConnection].Value;

                // Find the player entity owned by this connection
                bool foundPlayer = false;
                foreach (var (ghostOwner, deathState, health, playerEntity) in 
                    SystemAPI.Query<RefRO<GhostOwner>, RefRW<DeathState>, RefRW<Health>>()
                        .WithEntityAccess())
                {
                    // Match player by GhostOwner.NetworkId
                    if (ghostOwner.ValueRO.NetworkId != networkId)
                        continue;
                    
                    // Apply the requested death phase
                    deathState.ValueRW.Phase = rpc.ValueRO.RequestedPhase;
                    deathState.ValueRW.StateStartTime = currentTime;
                    
                    // Set health based on phase
                    if (rpc.ValueRO.RequestedPhase == DeathPhase.Downed || 
                        rpc.ValueRO.RequestedPhase == DeathPhase.Dead)
                    {
                        health.ValueRW.Current = 0f;
                    }
                    else if (rpc.ValueRO.RestoreHealth)
                    {
                        health.ValueRW.Current = health.ValueRO.Max;
                    }
                    
                    Debug.Log($"[DebugServer] Applied DeathPhase.{rpc.ValueRO.RequestedPhase} to player NetworkId={networkId}");
                    foundPlayer = true;
                    break; // Only one player per connection
                }
                
                if (!foundPlayer)
                {
                    Debug.LogWarning($"[DebugServer] No player found for NetworkId={networkId}");
                }
                
                // Consume the RPC
                ecb.DestroyEntity(rpcEntity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
