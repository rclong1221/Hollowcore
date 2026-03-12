using Unity.Entities;
using Unity.Burst;
using Unity.NetCode;
using Player.Components;
using Player.Animation;

namespace Player.Systems
{
    /// <summary>
    /// Sets animator parameters based on ride state.
    /// Integrates with existing animation state components.
    /// 
    /// Note: This system looks for a PlayerAnimationState component.
    /// If your project uses a different animation state component,
    /// modify the query accordingly.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RideDismountSystem))]
    [BurstCompile]
    public partial struct RideAnimationStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Query for entities with both RideState and RideAnimationOutput
            foreach (var (rideState, animOutput) 
                in SystemAPI.Query<RefRO<RideState>, RefRW<RideAnimationOutput>>())
            {
                if (rideState.ValueRO.RidePhase == RidePhaseConstants.None)
                {
                    animOutput.ValueRW.IsActive = false;
                    continue;
                }
                
                animOutput.ValueRW.IsActive = true;
                    
                // Set AbilityIndex to RIDE
                animOutput.ValueRW.AbilityIndex = OpsiveAnimatorConstants.ABILITY_RIDE;
                
                // Set AbilityIntData based on phase and side
                int intData = rideState.ValueRO.RidePhase switch
                {
                    RidePhaseConstants.Mounting => rideState.ValueRO.FromLeftSide 
                        ? OpsiveAnimatorConstants.RIDE_MOUNT_LEFT 
                        : OpsiveAnimatorConstants.RIDE_MOUNT_RIGHT,
                    RidePhaseConstants.Riding => OpsiveAnimatorConstants.RIDE_RIDING,
                    RidePhaseConstants.Dismounting => rideState.ValueRO.FromLeftSide 
                        ? OpsiveAnimatorConstants.RIDE_DISMOUNT_LEFT 
                        : OpsiveAnimatorConstants.RIDE_DISMOUNT_RIGHT,
                    _ => 0
                };
                
                animOutput.ValueRW.AbilityIntData = intData;
            }
        }
    }
    
    /// <summary>
    /// Output component for ride animation state.
    /// Read by ClimbAnimatorBridge or similar to drive the animator.
    /// Add this component to entities with RideState.
    /// </summary>
    public struct RideAnimationOutput : IComponentData
    {
        /// <summary>True when ride animation should be applied.</summary>
        public bool IsActive;
        
        /// <summary>Ability index (401 for Ride).</summary>
        public int AbilityIndex;
        
        /// <summary>Ability int data (1-6 for ride phases).</summary>
        public int AbilityIntData;
    }
}

