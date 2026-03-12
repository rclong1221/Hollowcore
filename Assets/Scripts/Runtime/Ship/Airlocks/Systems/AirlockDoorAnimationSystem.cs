using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that animates airlock doors based on replicated state.
    /// Provides smooth visual feedback for door open/close transitions.
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.5: Presentation (Client-Only)
    /// - Door animation driven by replicated AirlockDoor state
    /// - Smooth interpolation for open/close transitions
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct AirlockDoorAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Animate doors based on their current state
            int doorCount = 0;
            foreach (var (door, doorAnim, transform) in
                     SystemAPI.Query<RefRO<AirlockDoor>, RefRW<AirlockDoorAnimation>, RefRW<LocalTransform>>())
            {
                doorCount++;
                ref var anim = ref doorAnim.ValueRW;
                bool shouldBeOpen = door.ValueRO.IsOpen;
                if (shouldBeOpen) UnityEngine.Debug.Log($"[DoorAnim] Door is OPEN! Target=1");

                // Update target
                float targetOpenness = shouldBeOpen ? 1f : 0f;

                // Smooth interpolation
                if (math.abs(anim.CurrentOpenness - targetOpenness) > 0.001f)
                {
                    float speed = anim.AnimationSpeed * deltaTime;
                    anim.CurrentOpenness = math.lerp(anim.CurrentOpenness, targetOpenness, speed);
                }
                else
                {
                    anim.CurrentOpenness = targetOpenness;
                }

                // Apply animation to transform
                // This supports sliding doors (along OpenDirection) or rotating doors
                if (anim.AnimationType == DoorAnimationType.Slide)
                {
                    // Slide animation: offset position along OpenDirection
                    float3 offset = anim.OpenDirection * anim.OpenDistance * anim.CurrentOpenness;
                    transform.ValueRW.Position = anim.ClosedPosition + offset;
                }
                else if (anim.AnimationType == DoorAnimationType.Rotate)
                {
                    // Rotate animation: rotate around up axis
                    float angle = anim.OpenAngle * anim.CurrentOpenness;
                    quaternion rotation = quaternion.AxisAngle(new float3(0, 1, 0), math.radians(angle));
                    transform.ValueRW.Rotation = math.mul(anim.ClosedRotation, rotation);
                }
            }
            if (doorCount == 0)
            {
                 // Throttle warning (every ~100 frames)
                 if (state.GlobalSystemVersion % 100 == 0) 
                     UnityEngine.Debug.LogWarning("[DoorAnim] No active airlock doors found on Client!");
            }
        }
    }

    /// <summary>
    /// Type of door animation.
    /// </summary>
    public enum DoorAnimationType : byte
    {
        /// <summary>Door slides along a direction.</summary>
        Slide = 0,

        /// <summary>Door rotates around an axis.</summary>
        Rotate = 1
    }

    /// <summary>
    /// Animation state for airlock doors.
    /// Added to door entities that need smooth visual transitions.
    /// </summary>
    public struct AirlockDoorAnimation : IComponentData
    {
        /// <summary>Type of animation to perform.</summary>
        public DoorAnimationType AnimationType;

        /// <summary>Current openness (0 = fully closed, 1 = fully open).</summary>
        public float CurrentOpenness;

        /// <summary>Speed of animation interpolation.</summary>
        public float AnimationSpeed;

        /// <summary>Direction to slide when opening (for Slide type).</summary>
        public float3 OpenDirection;

        /// <summary>Distance to slide when fully open (for Slide type).</summary>
        public float OpenDistance;

        /// <summary>Angle in degrees to rotate when fully open (for Rotate type).</summary>
        public float OpenAngle;

        /// <summary>Position when door is fully closed.</summary>
        public float3 ClosedPosition;

        /// <summary>Rotation when door is fully closed.</summary>
        public quaternion ClosedRotation;

        /// <summary>
        /// Creates a default sliding door animation.
        /// </summary>
        public static AirlockDoorAnimation DefaultSlide => new()
        {
            AnimationType = DoorAnimationType.Slide,
            CurrentOpenness = 0f,
            AnimationSpeed = 5f,
            OpenDirection = new float3(1, 0, 0), // Slide along X
            OpenDistance = 2f,
            OpenAngle = 0f,
            ClosedPosition = float3.zero,
            ClosedRotation = quaternion.identity
        };

        /// <summary>
        /// Creates a default rotating door animation.
        /// </summary>
        public static AirlockDoorAnimation DefaultRotate => new()
        {
            AnimationType = DoorAnimationType.Rotate,
            CurrentOpenness = 0f,
            AnimationSpeed = 5f,
            OpenDirection = float3.zero,
            OpenDistance = 0f,
            OpenAngle = 90f, // 90 degree swing
            ClosedPosition = float3.zero,
            ClosedRotation = quaternion.identity
        };
    }
}
