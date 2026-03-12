using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Components;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 6: Predictive Aim System
    /// 
    /// Shows lead indicator for moving targets.
    /// Calculates intercept point based on target velocity and projectile speed.
    /// 
    /// NOTE: PredictiveAimState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct PredictiveAimSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if predictive aim is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.PredictiveAim) == 0)
                return;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime < 0.0001f) return;
            
            var em = state.EntityManager;
            
            // Default projectile speed (could be per-weapon)
            float projectileSpeed = 50f;
            
            foreach (var (moduleLink, lockState, transform, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRO<CameraTargetLockState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<PredictiveAimState>(moduleEntity))
                    continue;
                
                var predictive = SystemAPI.GetComponentRW<PredictiveAimState>(moduleEntity);
                
                if (!lockState.ValueRO.IsLocked)
                {
                    predictive.ValueRW.IsValid = false;
                    continue;
                }
                
                Entity target = lockState.ValueRO.TargetEntity;
                if (!em.Exists(target) || !em.HasComponent<LocalTransform>(target))
                {
                    predictive.ValueRW.IsValid = false;
                    continue;
                }
                
                float3 targetPos = em.GetComponentData<LocalTransform>(target).Position;
                float3 previousPos = predictive.ValueRO.PreviousTargetPosition;
                
                // Calculate velocity from position change
                float3 velocity;
                if (math.lengthsq(previousPos) < 0.001f)
                {
                    // First frame, no velocity yet
                    velocity = float3.zero;
                }
                else
                {
                    velocity = (targetPos - previousPos) / deltaTime;
                }
                
                // Smooth velocity to avoid jitter
                float3 smoothedVelocity = math.lerp(predictive.ValueRO.TargetVelocity, velocity, 0.3f);
                predictive.ValueRW.TargetVelocity = smoothedVelocity;
                predictive.ValueRW.PreviousTargetPosition = targetPos;
                
                // Calculate intercept point
                float3 playerPos = transform.ValueRO.Position;
                float3 toTarget = targetPos - playerPos;
                float distance = math.length(toTarget);
                
                if (distance < 1f)
                {
                    predictive.ValueRW.IsValid = false;
                    continue;
                }
                
                // Time for projectile to reach target (simplified - assumes straight line)
                float travelTime = distance / projectileSpeed;
                
                // Predict where target will be when projectile arrives
                float3 predictedPos = targetPos + smoothedVelocity * travelTime;
                
                // Iterate to refine (one iteration is usually enough)
                float3 toPredicted = predictedPos - playerPos;
                float predictedDist = math.length(toPredicted);
                float refinedTime = predictedDist / projectileSpeed;
                predictedPos = targetPos + smoothedVelocity * refinedTime;
                
                predictive.ValueRW.PredictedAimPoint = predictedPos;
                predictive.ValueRW.TimeToIntercept = refinedTime;
                predictive.ValueRW.IsValid = true;
                
                // Debug visualization
                UnityEngine.Debug.DrawLine(playerPos, targetPos, Color.red);
                UnityEngine.Debug.DrawLine(playerPos, predictedPos, Color.green);
                UnityEngine.Debug.DrawLine(targetPos, predictedPos, Color.yellow);
            }
        }
    }
}
