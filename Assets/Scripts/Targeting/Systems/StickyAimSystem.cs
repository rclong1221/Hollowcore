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
    /// EPIC 15.16 Task 8: Sticky Aim System
    /// 
    /// Aim movement slows when crosshair is near valid targets.
    /// Essential for controller aiming - only activates on gamepad input.
    /// 
    /// NOTE: AimAssistState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct StickyAimSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if sticky aim is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.StickyAim) == 0)
                return;
                
            float stickyStrength = behavior.StickyAimStrength;
            if (stickyStrength <= 0) return;
            
            // Query players with targeting module links
            foreach (var (moduleLink, camSettings, transform, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRW<PlayerCameraSettings>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<AimAssistState>(moduleEntity))
                    continue;
                
                var aimAssist = SystemAPI.GetComponentRW<AimAssistState>(moduleEntity);
                
                float2 lookInput = input.ValueRO.LookDelta;
                
                // Only process if there's look input
                if (math.lengthsq(lookInput) < 0.01f)
                {
                    aimAssist.ValueRW.InStickyZone = false;
                    aimAssist.ValueRW.CurrentStickyStrength = 0;
                    continue;
                }
                
                // Find nearest target in view
                float3 playerPos = transform.ValueRO.Position;
                float3 cameraForward = GetCameraForward(camSettings.ValueRO.Yaw, camSettings.ValueRO.Pitch);
                
                Entity nearestInView = Entity.Null;
                float nearestAngle = 15f; // Sticky aim cone (degrees)
                
                foreach (var (lockOnTarget, targetTransform, targetEntity) in 
                    SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    if (targetEntity == entity) continue;
                    
                    float3 toTarget = targetTransform.ValueRO.Position - playerPos;
                    float dist = math.length(toTarget);
                    
                    if (dist < 1f || dist > 50f) continue;
                    
                    float3 toTargetNorm = toTarget / dist;
                    float dot = math.dot(cameraForward, toTargetNorm);
                    float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                    
                    if (angle < nearestAngle)
                    {
                        nearestInView = targetEntity;
                        nearestAngle = angle;
                    }
                }
                
                if (nearestInView != Entity.Null)
                {
                    // Calculate sticky strength based on angle (closer to center = stickier)
                    float normalizedAngle = nearestAngle / 15f; // 0 = dead center, 1 = edge
                    float effectiveStrength = stickyStrength * (1f - normalizedAngle);
                    
                    aimAssist.ValueRW.InStickyZone = true;
                    aimAssist.ValueRW.StickyTarget = nearestInView;
                    aimAssist.ValueRW.CurrentStickyStrength = effectiveStrength;
                }
                else
                {
                    aimAssist.ValueRW.InStickyZone = false;
                    aimAssist.ValueRW.StickyTarget = Entity.Null;
                    aimAssist.ValueRW.CurrentStickyStrength = 0;
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
