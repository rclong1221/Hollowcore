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
    /// EPIC 15.16 Task 9: Snap Aim System
    /// 
    /// Quick snap to nearest target when ADS or lock button pressed.
    /// Configurable max snap angle.
    /// 
    /// NOTE: AimAssistState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct SnapAimSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if snap aim is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.SnapAim) == 0)
                return;
            
            // Data-driven config (EPIC 15.16 optimization)
            float maxSnapAngle = behavior.MaxLockAngle > 0 ? behavior.MaxLockAngle : 30f;
            float maxRange = behavior.MaxLockRange > 0 ? behavior.MaxLockRange : 30f;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (moduleLink, camSettings, lockState, transform, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRW<PlayerCameraSettings>, RefRO<CameraTargetLockState>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Snap happens when lock input is pressed (rising edge)
                // The lock state handles the actual locking, we just snap the aim
                
                bool isLockPressed = input.ValueRO.Grab.IsSet;
                bool wasLockPressed = lockState.ValueRO.WasGrabPressed;
                
                // Only snap on rising edge of lock button when not already locked
                if (!isLockPressed || wasLockPressed) continue;
                if (lockState.ValueRO.IsLocked) continue;
                
                // Find best snap target
                float3 playerPos = transform.ValueRO.Position;
                float3 cameraForward = GetCameraForward(camSettings.ValueRO.Yaw, camSettings.ValueRO.Pitch);
                
                Entity bestTarget = Entity.Null;
                float bestAngle = maxSnapAngle;
                float3 bestTargetPos = float3.zero;
                
                foreach (var (lockOnTarget, targetTransform, targetEntity) in 
                    SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    if (targetEntity == entity) continue;
                    
                    float3 targetPos = targetTransform.ValueRO.Position;
                    float3 toTarget = targetPos - playerPos;
                    float dist = math.length(toTarget);
                    
                    if (dist < 1f || dist > maxRange) continue;
                    
                    float3 toTargetNorm = toTarget / dist;
                    float dot = math.dot(cameraForward, toTargetNorm);
                    float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                    
                    if (angle < bestAngle)
                    {
                        bestTarget = targetEntity;
                        bestAngle = angle;
                        bestTargetPos = targetPos;
                    }
                }
                
                if (bestTarget != Entity.Null)
                {
                    // Snap camera to look at target
                    float3 dirToTarget = bestTargetPos - playerPos;
                    dirToTarget.y = 0; // Flatten for yaw
                    
                    if (math.lengthsq(dirToTarget) > 0.001f)
                    {
                        float3 dirNorm = math.normalize(dirToTarget);
                        float desiredYaw = math.degrees(math.atan2(dirNorm.x, dirNorm.z));
                        
                        // Normalize to 0-360
                        if (desiredYaw < 0) desiredYaw += 360f;
                        
                        // Instant snap (could be smoothed)
                        camSettings.ValueRW.Yaw = desiredYaw;
                        
                        // Calculate pitch to target
                        float3 fullDir = math.normalize(bestTargetPos - (playerPos + new float3(0, 1.5f, 0)));
                        float desiredPitch = math.degrees(math.asin(-fullDir.y));
                        desiredPitch = math.clamp(desiredPitch, -60f, 60f);
                        
                        camSettings.ValueRW.Pitch = desiredPitch;
                        
                        UnityEngine.Debug.Log($"[SnapAim] Snapped to Entity {bestTarget.Index} (angle: {bestAngle:F1}°)");
                    }
                }
            }
        }
        
        private float3 GetCameraForward(float yaw, float pitch)
        {
            float yawRad = math.radians(yaw);
            float pitchRad = math.radians(pitch);
            
            return new float3(
                math.sin(yawRad) * math.cos(pitchRad),
                -math.sin(pitchRad),
                math.cos(yawRad) * math.cos(pitchRad)
            );
        }
    }
}
