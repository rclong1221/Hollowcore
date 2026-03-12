using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// Client-side system for smoothing misprediction corrections.
    /// Interpolates position/rotation errors over a short window.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial class LocalSpaceSmoothingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (smoothing, transform, entity) in
                     SystemAPI.Query<RefRW<LocalSpaceSmoothing>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
            {
                ref var smooth = ref smoothing.ValueRW;

                if (smooth.SmoothingProgress >= 1f)
                    continue;

                // Advance smoothing
                float smoothingSpeed = 1f / math.max(smooth.SmoothingDuration, 0.01f);
                smooth.SmoothingProgress = math.min(smooth.SmoothingProgress + deltaTime * smoothingSpeed, 1f);

                // Apply remaining error (decreasing over time)
                float errorFactor = 1f - smooth.SmoothingProgress;

                ref var t = ref transform.ValueRW;
                t.Position += smooth.PositionError * errorFactor * deltaTime * 10f;

                // For rotation, slerp towards identity
                if (!smooth.RotationError.Equals(quaternion.identity))
                {
                    quaternion errorRotation = math.slerp(quaternion.identity, smooth.RotationError, errorFactor);
                    t.Rotation = math.mul(t.Rotation, errorRotation);
                }
            }
        }

        /// <summary>
        /// Called when a misprediction is detected.
        /// Sets up smoothing from current position to corrected position.
        /// </summary>
        public void SetupSmoothing(Entity entity, float3 positionError, quaternion rotationError)
        {
            if (!SystemAPI.HasComponent<LocalSpaceSmoothing>(entity))
                return;

            var smoothing = SystemAPI.GetComponent<LocalSpaceSmoothing>(entity);
            smoothing.SmoothingProgress = 0f;
            smoothing.PositionError = positionError;
            smoothing.RotationError = rotationError;
            
            SystemAPI.SetComponent(entity, smoothing);
        }
    }
}
