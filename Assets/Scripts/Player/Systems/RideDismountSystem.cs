using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Handles the dismount animation and cleanup.
    /// Server-authoritative - only runs on server, client receives replicated state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct RideDismountSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (rideState, transform, entity) 
                in SystemAPI.Query<RefRW<RideState>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Process DismountComplete phase -> None (brief window for animator to exit)
                if (rideState.ValueRO.RidePhase == RidePhaseConstants.DismountComplete)
                {
                    rideState.ValueRW.MountProgress += deltaTime;
                    
                    // Stay in DismountComplete for 0.3s to let animator exit Ride layer
                    const float completeDelay = 0.3f;
                    
                    if (rideState.ValueRO.MountProgress >= completeDelay)
                    {
                        UnityEngine.Debug.Log("[Blitz Dismount] DismountComplete -> None, fully reset");
                        rideState.ValueRW.RidePhase = RidePhaseConstants.None;
                        rideState.ValueRW.MountEntity = Entity.Null; // Clear mount reference
                        rideState.ValueRW.MountProgress = 0f;
                        rideState.ValueRW.DismountRequested = false;
                        rideState.ValueRW.FromLeftSide = false;
                    }
                    continue;
                }
                
                // Only process dismounting phase
                if (rideState.ValueRO.RidePhase != RidePhaseConstants.Dismounting)
                    continue;
                    
                // Progress dismount animation
                rideState.ValueRW.MountProgress += deltaTime;
                
                // Dismount animation takes ~0.8 seconds
                const float dismountDuration = 0.8f;
                
                if (rideState.ValueRO.MountProgress >= dismountDuration)
                {
                    Entity mountEntity = rideState.ValueRO.MountEntity;
                    
                    // Clear rideable state
                    if (SystemAPI.HasComponent<RideableState>(mountEntity))
                    {
                        var rideableState = SystemAPI.GetComponentRW<RideableState>(mountEntity);
                        rideableState.ValueRW.HasRider = false;
                        rideableState.ValueRW.RiderEntity = Entity.Null;
                    }
                    
                    // Position player beside mount
                    if (SystemAPI.HasComponent<LocalTransform>(mountEntity))
                    {
                        var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                        float3 dismountOffset = rideState.ValueRO.FromLeftSide 
                            ? new float3(-1.5f, 0, 0) 
                            : new float3(1.5f, 0, 0);
                        
                        transform.ValueRW.Position = mountTransform.Position + 
                            math.mul(mountTransform.Rotation, dismountOffset);
                    }
                    
                    // Transition to DismountComplete (animator sends AbilityIntData=6)
                    UnityEngine.Debug.Log("[Blitz Dismount] Dismounting -> DismountComplete");
                    rideState.ValueRW.RidePhase = RidePhaseConstants.DismountComplete;
                    rideState.ValueRW.IsRiding = false;
                    rideState.ValueRW.MountProgress = 0f; // Reset for DismountComplete timer
                }
            }
        }
    }
}

