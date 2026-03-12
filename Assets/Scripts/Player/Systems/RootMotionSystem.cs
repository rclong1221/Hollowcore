using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.NetCode;
using DIG.Player.Abilities;
using Player.Systems; // Required for CharacterControllerSystem

namespace DIG.Player.Systems
{
    /// <summary>
    /// Applies captured Root Motion deltas to PhysicsVelocity.
    /// Acts as the bridge between Animator (View) and Physics (Simulation).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CharacterControllerSystem))] // Apply velocity before movement
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RootMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RootMotionDelta>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0.0001f) return;

            foreach (var (rootMotion, velocity, transform) in 
                     SystemAPI.Query<RefRW<RootMotionDelta>, RefRW<PhysicsVelocity>, RefRO<Unity.Transforms.LocalTransform>>()
                     .WithAll<Simulate>())
            {
                if (!rootMotion.ValueRO.UseRootMotion)
                {
                    // Clear delta just in case
                    rootMotion.ValueRW.PositionDelta = float3.zero;
                    rootMotion.ValueRW.RotationDelta = quaternion.identity;
                    continue;
                }

                float3 deltaPos = rootMotion.ValueRO.PositionDelta;
                quaternion deltaRot = rootMotion.ValueRO.RotationDelta;

                // Apply to Velocity
                // Velocity = Delta / Time
                
                // 1. Angular Velocity
                // Convert delta rotation to angular velocity
                // q_new = q_old * q_delta
                // q_delta = [cos(theta/2), axis * sin(theta/2)]
                // angularVelocity = axis * theta / dt
                
                // Simplified: Just set rotation directly? No, physics handles rotation via velocity.
                // But CharacterController usually handles rotation via input. 
                // If using Root Motion rotation (e.g. Turn in Place), we want it to drive rotation.
                
                // For now, let's focus on Position (Linear Velocity).
                
                if (math.lengthsq(deltaPos) > 0.000001f)
                {
                    // Convert root motion delta (usually consistent with animation frame) to velocity
                    // Note: OnAnimatorMove happened on MainThread, wrote delta.
                    // This delta represents movement since LAST OnAnimatorMove.
                    // If frame rates match, Delta / dt is correct.
                    // If Physics runs faster, we might apply same delta multiple times? No, component should be cleared.
                    
                    velocity.ValueRW.Linear += deltaPos / deltaTime; // Additive or Set?
                    // Usually additive to gravity, but overrides input?
                    // UseRootMotion implies "Animation drives movement", so typically overrides Input.
                    
                    // Reset delta after consumption
                    rootMotion.ValueRW.PositionDelta = float3.zero;
                }
                
                // Apply Rotation Delta directly to Transform? Or AngularVelocity?
                // CC usually overrides rotation.
            }
        }
    }
}
