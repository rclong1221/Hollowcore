using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// System that prepares movement polish data for the main movement system.
    /// Updates state tracking for momentum/slope calculations.
    /// <para>
    /// <b>Architecture:</b> Runs before PlayerMovementSystem to prepare polish state.
    /// All calculations are Burst-compiled and parallelized.
    /// </para>
    /// <para><b>Performance:</b> Logic is inlined in jobs - no utility function overhead.</para>
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct MovementPolishSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MotorPolishSettings>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update motor polish state (previous velocity, slope detection)
            state.Dependency = new UpdateMotorPolishStateJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
            
            // Process soft force buffer
            state.Dependency = new ProcessSoftForcesJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
        }
        
        /// <summary>
        /// Job to update motor polish state each frame.
        /// Tracks previous velocity and detects slope movement.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct UpdateMotorPolishStateJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref MotorPolishState polishState,
                in LocalTransform transform,
                in PlayerState playerState,
                in PhysicsVelocity vel)
            {
                // Store current horizontal velocity for next frame
                float3 currentHorizontal = new float3(vel.Linear.x, 0, vel.Linear.z);
                
                // Calculate slope angle from ground normal
                float3 groundNormal = playerState.IsGrounded 
                    ? playerState.GroundNormal 
                    : new float3(0, 1, 0);
                
                float slopeAngle = math.degrees(math.acos(math.saturate(groundNormal.y)));
                
                // Determine if moving uphill by comparing velocity direction to slope
                float3 slopeDirection = math.normalizesafe(new float3(groundNormal.x, 0, groundNormal.z));
                float velocityDot = math.dot(math.normalizesafe(currentHorizontal), slopeDirection);
                byte isUphill = (velocityDot > 0.1f) ? (byte)1 : (byte)0;
                
                // Update state
                polishState.PrevHorizontalVelocity = currentHorizontal;
                polishState.PrevMotorRotation = transform.Rotation;
                polishState.CurrentSlopeAngle = slopeAngle;
                polishState.IsMovingUphill = isUphill;
            }
        }
        
        /// <summary>
        /// Job to process soft force buffer and apply distributed forces.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct ProcessSoftForcesJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref SoftForceSettings settings,
                ref DynamicBuffer<SoftForceFrame> forceBuffer,
                ref PhysicsVelocity velocity)
            {
                if (forceBuffer.Length == 0) return;
                
                int index = settings.CurrentFrameIndex % settings.MaxSoftForceFrames;
                if (index < forceBuffer.Length)
                {
                    float3 force = forceBuffer[index].Force;
                    if (math.lengthsq(force) > 0.001f)
                    {
                        velocity.Linear += force;
                        
                        // Clear the applied force
                        forceBuffer[index] = new SoftForceFrame { Force = float3.zero };
                    }
                }
                
                settings.CurrentFrameIndex++;
            }
        }
    }
    
    /// <summary>
    /// Static helper for motor polish calculations.
    /// NOTE: These are provided for external systems. For jobs, inline the math directly.
    /// </summary>
    public static class MotorPolishMath
    {
        /// <summary>
        /// Calculates the backwards movement multiplier.
        /// For Burst jobs, inline this: inputY >= 0 ? 1f : math.lerp(1f, backwardsMultiplier, math.abs(inputY))
        /// </summary>
        public static float GetBackwardsMultiplier(float inputY, float backwardsMultiplier)
        {
            if (inputY >= 0) return 1f;
            return math.lerp(1f, backwardsMultiplier, math.abs(inputY));
        }
        
        /// <summary>
        /// Calculates slope-adjusted speed multiplier.
        /// For Burst jobs, inline this logic directly.
        /// </summary>
        public static float GetSlopeMultiplier(byte adjustOnSlope, byte isUphill, float slopeAngle, float slopeForceUp, float slopeForceDown)
        {
            if (adjustOnSlope == 0) return 1f;
            if (slopeAngle < 5f) return 1f;
            
            float slopeFactor = math.saturate(slopeAngle / 45f);
            float baseMultiplier = isUphill == 1 ? slopeForceUp : slopeForceDown;
            return math.lerp(1f, baseMultiplier, slopeFactor);
        }
    }
}
