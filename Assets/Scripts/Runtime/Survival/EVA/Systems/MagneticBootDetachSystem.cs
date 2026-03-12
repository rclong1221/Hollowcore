using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Detaches magnetic boots when velocity exceeds threshold or boots are disabled.
    /// Allows jetpack thrust or jumps to break attachment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MagneticBootAttachSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MagneticBootDetachSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MagneticBootDetachJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct MagneticBootDetachJob : IJobEntity
        {
            void Execute(
                ref MagneticBootState bootState,
                in PhysicsVelocity velocity)
            {
                // Only check attached boots
                if (!bootState.IsAttached)
                {
                    return;
                }

                // Detach if boots are disabled
                if (!bootState.IsEnabled)
                {
                    bootState.IsAttached = false;
                    return;
                }

                // Calculate velocity in the direction away from the surface
                // (opposite to attached normal = "up" relative to surface)
                float3 detachDirection = bootState.AttachedNormal;
                float detachVelocity = math.dot(velocity.Linear, detachDirection);

                // Detach if moving away from surface fast enough
                if (detachVelocity > bootState.DetachVelocityThreshold)
                {
                    bootState.IsAttached = false;
                }

                // Also check total velocity magnitude (for sudden lateral movements)
                float totalSpeed = math.length(velocity.Linear);
                if (totalSpeed > bootState.DetachVelocityThreshold * 2f)
                {
                    bootState.IsAttached = false;
                }
            }
        }
    }
}
