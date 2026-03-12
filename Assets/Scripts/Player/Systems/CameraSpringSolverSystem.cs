using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Player.Components;
using Unity.Collections;
using Unity.NetCode;

namespace Player.Systems
{
    /// <summary>
    /// Solves spring physics for camera procedural motion (shakes, recoil, bob).
    /// Updates CameraSpringState.Value based on Velocity, Stiffness, and Damping.
    /// Runs before camera control so the latest spring values are available for the frame.
    /// </summary>
    [UpdateBefore(typeof(PlayerCameraControlSystem))]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    public partial struct CameraSpringSolverSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Iterate over all entities with CameraSpringState
            foreach (var (spring, entity) in SystemAPI.Query<RefRW<CameraSpringState>>().WithEntityAccess())
            {
                SolveSpring(ref spring.ValueRW, deltaTime);
            }
        }

        private static void SolveSpring(ref CameraSpringState spring, float deltaTime)
        {
            // EPIC 15.25: Use analytical solver when frequency > 0, otherwise Opsive solver
            bool useAnalyticalPos = math.any(spring.PositionFrequency > float3.zero);
            bool useAnalyticalRot = math.any(spring.RotationFrequency > float3.zero);

            // Position Spring
            if (useAnalyticalPos)
                DIG.ProceduralMotion.Systems.WeaponSpringSolverSystem.SolveAnalytical(
                    ref spring.PositionValue, ref spring.PositionVelocity,
                    spring.PositionFrequency, spring.PositionDampingRatio, deltaTime);
            else
                SolveAxis(ref spring.PositionValue, ref spring.PositionVelocity, in spring.PositionStiffness, in spring.PositionDamping, deltaTime);

            // Rotation Spring
            if (useAnalyticalRot)
                DIG.ProceduralMotion.Systems.WeaponSpringSolverSystem.SolveAnalytical(
                    ref spring.RotationValue, ref spring.RotationVelocity,
                    spring.RotationFrequency, spring.RotationDampingRatio, deltaTime);
            else
                SolveAxis(ref spring.RotationValue, ref spring.RotationVelocity, in spring.RotationStiffness, in spring.RotationDamping, deltaTime);

            // Clamp values
            spring.PositionValue = math.clamp(spring.PositionValue, spring.MinValue, spring.MaxValue);
            spring.RotationValue = math.clamp(spring.RotationValue, spring.MinValue, spring.MaxValue);
        }

        private static void SolveAxis(ref float3 value, ref float3 velocity, in float3 stiffness, in float3 damping, float deltaTime)
        {
            // Opsive Algorithm: 
            // Velocity += (RestValue - Value) * (1 - Stiffness)
            // Velocity *= Damping
            // Value += Velocity
            // Note: Opsive uses 0-1 range for Stiffness/Damping where 1 is very stiff/damp.
            // But they apply it per tick. With variable delta time, we should dampen based on time.
            // However, to maintain parity with the algorithm logic we derived:

            float3 restValue = float3.zero; // Always rest at zero offset
            
            // Calculate force (Hooke's Law variation)
            // Opsive's formula is frame-rate dependent if just applied as +=.
            // We'll normalize by delta time for consistent behavior.
            // Stiffness determines how fast it returns to 0.
            
            // Force = (Target - Current) * Stiffness
            float3 force = (restValue - value) * stiffness;
            
            // Apply force to velocity
            velocity += force * (deltaTime * 60f); // Scale to 60fps baseline
            
            // Apply damping (Resistance)
            // Velocity *= Damping (pow for time independence)
            velocity *= math.pow(damping, deltaTime * 60f);
            
            // Update position
            value += velocity * (deltaTime * 60f);
            
            // Check for near-zero stop to prevent micro-jitter
            if (math.lengthsq(velocity) < 0.000001f && math.lengthsq(value) < 0.000001f)
            {
                velocity = float3.zero;
                value = float3.zero;
            }
        }
    }
}
