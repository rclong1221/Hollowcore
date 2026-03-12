using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Redirects player input to mount while riding.
    /// Keeps player attached to mount seat position.
    /// Also handles dismount input.
    /// Server-authoritative - only runs on server, client receives replicated state.
    /// 
    /// Based on Opsive's Ride.cs pattern:
    /// - Player input is forwarded to the mount's locomotion
    /// - Player position is locked to the mount's seat position
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RideControlSystem : ISystem
    {
        private bool _loggedRiding;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
            _loggedRiding = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rideState, playerInput, transform, entity) 
                in SystemAPI.Query<RefRW<RideState>, RefRO<PlayerInput>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Only process riding phase
                if (rideState.ValueRO.RidePhase != RidePhaseConstants.Riding)
                    continue;
                
                // Log once when riding starts
                if (!_loggedRiding)
                {
                    Debug.Log($"[Blitz Control] [Server] Player is now riding Blitz!");
                    _loggedRiding = true;
                }
                    
                Entity mountEntity = rideState.ValueRO.MountEntity;
                if (!SystemAPI.Exists(mountEntity))
                {
                    Debug.Log($"[Blitz Control] [Server] Mount entity destroyed - forcing dismount");
                    // Mount destroyed - force dismount
                    rideState.ValueRW.RidePhase = RidePhaseConstants.None;
                    rideState.ValueRW.IsRiding = false;
                    rideState.ValueRW.MountEntity = Entity.Null;
                    _loggedRiding = false;
                    continue;
                }
                
                // === FORWARD PLAYER INPUT TO MOUNT ===
                // This is how Opsive does it - player's WASD controls the horse
                if (SystemAPI.HasComponent<MountMovementInput>(mountEntity))
                {
                    var mountInput = SystemAPI.GetComponentRW<MountMovementInput>(mountEntity);
                    
                    // Forward input (W/S) - PlayerInput uses Vertical (int -1, 0, 1)
                    float forward = playerInput.ValueRO.Vertical;
                    
                    // Horizontal input (A/D) - for turning - PlayerInput uses Horizontal (int -1, 0, 1)
                    float horizontal = playerInput.ValueRO.Horizontal;
                    
                    // Sprint input (Shift) - makes the horse run/gallop
                    byte sprint = playerInput.ValueRO.Sprint.IsSetByte;
                    
                    // Only log when input changes
                    bool hasInput = math.abs(forward) > 0.01f || math.abs(horizontal) > 0.01f;
                    bool hadInput = math.abs(mountInput.ValueRO.ForwardInput) > 0.01f || math.abs(mountInput.ValueRO.HorizontalInput) > 0.01f;
                    
                    if (hasInput && !hadInput)
                    {
                        Debug.Log($"[Blitz Control] [Server] Forwarding input to mount: H={horizontal}, F={forward}, Sprint={sprint}");
                    }
                    
                    mountInput.ValueRW.ForwardInput = forward;
                    mountInput.ValueRW.HorizontalInput = horizontal;
                    mountInput.ValueRW.SprintInput = sprint;
                    
                    // Use camera yaw for look direction if valid
                    if (playerInput.ValueRO.CameraYawValid == 1)
                    {
                        float yawRad = math.radians(playerInput.ValueRO.CameraYaw);
                        mountInput.ValueRW.LookDirection = new float3(math.sin(yawRad), 0, math.cos(yawRad));
                    }
                }
                else
                {
                    Debug.LogWarning($"[Blitz Control] [Server] Mount entity {mountEntity.Index} has no MountMovementInput component!");
                }
                
                // === KEEP PLAYER ATTACHED TO MOUNT ===
                if (SystemAPI.HasComponent<LocalTransform>(mountEntity))
                {
                    var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                    
                    // Get seat offset from rideable state
                    float3 seatOffset = new float3(0, 1.5f, 0);
                    if (SystemAPI.HasComponent<RideableState>(mountEntity))
                    {
                        seatOffset = SystemAPI.GetComponent<RideableState>(mountEntity).SeatOffset;
                    }
                    
                    // Position player at mount seat (like Opsive's ApplyPosition)
                    transform.ValueRW.Position = mountTransform.Position + 
                        math.mul(mountTransform.Rotation, seatOffset);
                    // Rotate player with mount (like Opsive's ApplyRotation)
                    transform.ValueRW.Rotation = mountTransform.Rotation;
                }
                
                // === CHECK FOR DISMOUNT REQUEST ===
                if (rideState.ValueRO.DismountRequested)
                {
                    Debug.Log($"[Blitz Control] [Server] Dismount requested - transitioning to dismounting phase");
                    rideState.ValueRW.RidePhase = RidePhaseConstants.Dismounting;
                    rideState.ValueRW.DismountRequested = false;
                    rideState.ValueRW.MountProgress = 0f;
                    _loggedRiding = false;
                    
                    // Clear mount input when dismounting
                    if (SystemAPI.HasComponent<MountMovementInput>(mountEntity))
                    {
                        var mountInput = SystemAPI.GetComponentRW<MountMovementInput>(mountEntity);
                        mountInput.ValueRW.ForwardInput = 0f;
                        mountInput.ValueRW.HorizontalInput = 0f;
                    }
                }
            }
        }
    }
}

