using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Processes mount/dismount input from players.
    /// Uses the Interact input (T key) to mount nearby rideables or dismount.
    /// SERVER-ONLY: All state changes happen on server and replicate to client via GhostFields.
    /// This prevents client/server desync issues.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RideInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
            state.RequireForUpdate<PlayerInput>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Server-only: Process input and modify state authoritatively
            foreach (var (playerInput, rideState, entity) 
                in SystemAPI.Query<RefRO<PlayerInput>, RefRW<RideState>>()
                    .WithEntityAccess())
            {
                // Check if Interact (T key) was pressed this frame
                bool interactPressed = playerInput.ValueRO.Interact.IsSet;
                
                if (!interactPressed)
                    continue;
                
                // Debug
                bool hasNearby = state.EntityManager.HasComponent<NearbyRideable>(entity);
                Debug.Log($"[Blitz RideInput] [Server] T pressed! IsRiding={rideState.ValueRO.IsRiding}, " +
                          $"Phase={rideState.ValueRO.RidePhase}, HasNearbyBlitz={hasNearby}");
                
                // If currently riding, request dismount
                if (rideState.ValueRO.IsRiding)
                {
                    Debug.Log("[Blitz RideInput] [Server] Requesting dismount from Blitz");
                    rideState.ValueRW.DismountRequested = true;
                    continue;
                }
                
                // If not riding and not in ANY mount/dismount phase, try to mount nearby rideable
                if (rideState.ValueRO.RidePhase == RidePhaseConstants.None)
                {
                    // Check if there's a nearby rideable
                    if (state.EntityManager.HasComponent<NearbyRideable>(entity))
                    {
                        var nearbyRideable = state.EntityManager.GetComponentData<NearbyRideable>(entity);
                        
                        Debug.Log($"[Blitz RideInput] [Server] Starting mount! Entity={nearbyRideable.RideableEntity}, FromLeft={nearbyRideable.MountFromLeft}");
                        
                        // Start mounting - server authoritative
                        rideState.ValueRW.MountEntity = nearbyRideable.RideableEntity;
                        rideState.ValueRW.FromLeftSide = nearbyRideable.MountFromLeft;
                        rideState.ValueRW.RidePhase = RidePhaseConstants.Mounting;
                        rideState.ValueRW.MountProgress = 0f;
                    }
                }
            }
        }
    }
}
