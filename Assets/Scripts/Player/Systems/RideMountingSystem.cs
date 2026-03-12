using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Handles the mount animation phase.
    /// Server-authoritative - only runs on server, client receives replicated state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RideMountingSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (rideState, transform, entity) 
                in SystemAPI.Query<RefRW<RideState>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Debug: Log current phase if not None (only periodically to avoid spam)
                if (rideState.ValueRO.RidePhase != RidePhaseConstants.None && 
                    (int)(SystemAPI.Time.ElapsedTime * 10) % 20 == 0)
                {
                    Debug.Log($"[Blitz Mounting] Entity phase={rideState.ValueRO.RidePhase}, Progress={rideState.ValueRO.MountProgress:F2}");
                }
                
                // Only process mounting phase
                if (rideState.ValueRO.RidePhase != RidePhaseConstants.Mounting)
                    continue;
                
                Entity mountEntity = rideState.ValueRO.MountEntity;
                
                // Debug log at start of mounting
                if (rideState.ValueRO.MountProgress == 0f)
                {
                    Debug.Log($"[Blitz Mounting] Starting mount sequence, MountEntity={mountEntity}");
                }
                    
                // Progress mount animation
                rideState.ValueRW.MountProgress += deltaTime;
                
                // During mounting, lerp player towards mount position
                if (SystemAPI.HasComponent<LocalTransform>(mountEntity) && 
                    SystemAPI.HasComponent<RideableState>(mountEntity))
                {
                    var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                    var rideableState = SystemAPI.GetComponent<RideableState>(mountEntity);
                    
                    // Calculate target seat position
                    float3 seatPos = mountTransform.Position + 
                        math.mul(mountTransform.Rotation, rideableState.SeatOffset);
                    
                    // Lerp player position towards seat during mount
                    float t = math.saturate(rideState.ValueRO.MountProgress / 1.0f);
                    transform.ValueRW.Position = math.lerp(transform.ValueRO.Position, seatPos, t * 0.1f);
                }
                
                // Mount animation takes ~1 second (adjust as needed)
                const float mountDuration = 1.0f;
                
                if (rideState.ValueRO.MountProgress >= mountDuration)
                {
                    Debug.Log($"[Blitz Mounting] Mount complete! Transitioning to Riding phase");
                    
                    // Mounting complete - transition to riding
                    rideState.ValueRW.RidePhase = RidePhaseConstants.Riding;
                    rideState.ValueRW.IsRiding = true;
                    rideState.ValueRW.MountProgress = 0f;
                    
                    // Update rideable state
                    if (SystemAPI.HasComponent<RideableState>(mountEntity))
                    {
                        var rideableState = SystemAPI.GetComponentRW<RideableState>(mountEntity);
                        rideableState.ValueRW.HasRider = true;
                        rideableState.ValueRW.RiderEntity = entity;
                    }
                }
            }
        }
    }
}

