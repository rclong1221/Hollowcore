using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 13.17.3: Moves player to interaction position and orientation.
    ///
    /// Enhanced features:
    /// - Size: Configurable arrival tolerance radius
    /// - Angle: Configurable rotation tolerance
    /// - PrecisionStart: Smooth blend at beginning of movement
    /// - MovementMultiplier: Speed adjustment factor
    ///
    /// This system is Burst-compiled and runs in PredictedSimulationSystemGroup
    /// for proper network prediction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MoveTowardsLocationSystem : ISystem
    {
        // Default tolerances when not specified
        private const float DefaultPositionTolerance = 0.05f;
        private const float DefaultAngleTolerance = 5.0f; // degrees
        private const float DefaultMovementMultiplier = 1.0f;

        // Precision start blend zone
        private const float PrecisionStartBlendDistance = 0.5f;
        private const float PrecisionStartMinSpeedFactor = 0.1f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (moveTowards, transform, entity) in
                     SystemAPI.Query<RefRW<MoveTowardsLocation>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var moveRef = ref moveTowards.ValueRW;
                ref var transformRef = ref transform.ValueRW;

                if (!moveRef.IsMoving)
                    continue;

                if (moveRef.HasArrived)
                    continue;

                // Get positions
                float3 currentPos = transformRef.Position;
                float3 targetPos = moveRef.TargetPosition;
                float3 direction = targetPos - currentPos;
                float distance = math.length(direction);

                // EPIC 13.17.3: Calculate effective speed with multiplier
                float speedMultiplier = moveRef.MovementMultiplier > 0f ?
                    moveRef.MovementMultiplier : DefaultMovementMultiplier;
                float baseSpeed = moveRef.MoveSpeed * speedMultiplier;

                // EPIC 13.17.3: Apply precision start if enabled
                float effectiveSpeed = baseSpeed;
                if (moveRef.PrecisionStart && distance > PrecisionStartBlendDistance)
                {
                    // Smooth acceleration from min to full speed over the blend zone
                    float t = math.saturate((distance - PrecisionStartBlendDistance) / PrecisionStartBlendDistance);
                    effectiveSpeed = math.lerp(baseSpeed * PrecisionStartMinSpeedFactor, baseSpeed, t);
                }

                // Move towards target position
                if (distance > 0.001f)
                {
                    float moveAmount = effectiveSpeed * deltaTime;
                    if (moveAmount >= distance)
                    {
                        transformRef.Position = targetPos;
                    }
                    else
                    {
                        transformRef.Position = currentPos + math.normalize(direction) * moveAmount;
                    }
                }

                // Rotate towards target rotation
                quaternion currentRot = transformRef.Rotation;
                quaternion targetRot = moveRef.TargetRotation;
                float rotateSpeed = moveRef.RotateSpeed * speedMultiplier;
                float rotateAmount = rotateSpeed * deltaTime;
                transformRef.Rotation = math.slerp(currentRot, targetRot, math.saturate(rotateAmount));

                // EPIC 13.17.3: Calculate arrival thresholds from component or use defaults
                float positionTolerance = moveRef.Size > 0f ? moveRef.Size : DefaultPositionTolerance;

                // Convert angle tolerance to quaternion dot product threshold
                // dot(q1, q2) = cos(theta/2) where theta is the angle between rotations
                // For small angles: 1 - cos(theta/2) ~= theta^2/8 (in radians)
                float angleTolerance = moveRef.Angle > 0f ? moveRef.Angle : DefaultAngleTolerance;
                float angleRad = math.radians(angleTolerance);
                float rotationTolerance = 1f - math.cos(angleRad * 0.5f);

                // Check if arrived
                float posDistance = math.length(targetPos - transformRef.Position);
                float rotDiff = 1f - math.abs(math.dot(transformRef.Rotation.value, targetRot.value));

                if (posDistance < positionTolerance && rotDiff < rotationTolerance)
                {
                    // Snap to exact target
                    transformRef.Position = targetPos;
                    transformRef.Rotation = targetRot;
                    moveRef.HasArrived = true;
                    moveRef.IsMoving = false;
                }
            }
        }
    }
}
