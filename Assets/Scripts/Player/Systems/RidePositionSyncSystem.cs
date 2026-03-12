using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Keeps player attached to mount seat position.
    /// Runs on BOTH client and server (predicted) for smooth local movement.
    /// 
    /// This is separate from RideControlSystem (server-only) because position
    /// attachment needs to run on client for smooth visuals.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MountMovementSystem))]
    public partial struct RidePositionSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RideState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rideState, transform) 
                in SystemAPI.Query<RefRO<RideState>, RefRW<LocalTransform>>()
                    .WithAll<Simulate>())
            {
                // Only sync position when actively riding (phase 2 = Riding)
                // Don't sync during mount/dismount animations
                if (rideState.ValueRO.RidePhase != RidePhaseConstants.Riding)
                    continue;
                
                Entity mountEntity = rideState.ValueRO.MountEntity;
                if (mountEntity == Entity.Null || !SystemAPI.Exists(mountEntity))
                    continue;
                
                // Get mount transform
                if (!SystemAPI.HasComponent<LocalTransform>(mountEntity))
                    continue;
                    
                var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                
                // Get seat offset from rideable state
                float3 seatOffset = new float3(0, 1.5f, 0);
                if (SystemAPI.HasComponent<RideableState>(mountEntity))
                {
                    seatOffset = SystemAPI.GetComponent<RideableState>(mountEntity).SeatOffset;
                }
                
                // Position player at mount seat
                transform.ValueRW.Position = mountTransform.Position + 
                    math.mul(mountTransform.Rotation, seatOffset);
                // Rotate player with mount
                transform.ValueRW.Rotation = mountTransform.Rotation;
            }
        }
    }
}

