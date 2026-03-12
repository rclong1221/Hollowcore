using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using Player.Components; // For PlayerState

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Handles generic detection logic for abilities.
    /// Runs raycasts/overlaps based on DetectObjectAbility/DetectGroundAbility components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateBefore(typeof(AbilityTriggerSystem))] // Detection happens before trigger evaluation
    public partial struct AbilityDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PhysicsWorldSingleton>()) return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Job for Object Detection
            new DetectObjectJob
            {
                PhysicsWorld = physicsWorld
            }.ScheduleParallel();
            
            // Job for Ground Detection
            new DetectGroundJob
            {
                // In a real implementation, we might access persistent GroundInfo from CharacterController
                // For now, we'll assume we can read from player state or do fresh casts
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct DetectObjectJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;

            public void Execute(
                ref DetectObjectAbility detection,
                in LocalTransform transform)
            {
                // Simple overlap sphere or raycast logic would go here.
                // For MVP, we'll placeholder the logic.
                
                float3 origin = transform.Position + math.rotate(transform.Rotation, detection.DetectionOffset);
                
                // --- Placeholder Logic ---
                // detection.TargetDetected = PhysicsWorld.CastRay(...)
            }
        }
        
        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct DetectGroundJob : IJobEntity
        {
            public void Execute(
                ref DetectGroundAbility detection,
                in PlayerState playerState) // Assuming we reuse core player state
            {
                if (playerState.IsGrounded)
                {
                    // Calculate angle from normal
                    float angle = math.degrees(math.acos(math.dot(playerState.GroundNormal, math.up())));
                    
                    detection.CurrentGroundAngle = angle;
                    detection.CurrentGroundNormal = playerState.GroundNormal;
                    
                    bool angleValid = angle >= detection.MinGroundAngle && angle <= detection.MaxGroundAngle;
                    detection.IsValidGround = angleValid;
                }
                else
                {
                    detection.IsValidGround = false;
                }
            }
        }
    }
}
