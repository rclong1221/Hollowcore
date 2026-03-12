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
    /// EPIC 15.16 Task 3: First Person Aim Assist System
    /// 
    /// Camera IS the view. Lock = aim magnetism only (no character rotation concept).
    /// Combines sticky aim and magnetism for controller players.
    /// 
    /// NOTE: AimAssistState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct FirstPersonAimAssistSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if FPS mode is active
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if (behavior.BehaviorType != LockBehaviorType.FirstPerson)
                return;
            
            float magnetismStrength = behavior.AimMagnetismStrength;
            float stickyStrength = behavior.StickyAimStrength;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
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
                
                // Only apply aim assist on controller (significant look input)
                // Mouse input is typically much more precise
                bool hasLookInput = math.lengthsq(lookInput) > 0.01f;
                
                if (!hasLookInput)
                {
                    aimAssist.ValueRW.MagnetismPull = float2.zero;
                    continue;
                }
                
                // Find target near crosshair
                float3 playerPos = transform.ValueRO.Position + new float3(0, 1.7f, 0); // Eye height
                float3 cameraForward = GetCameraForward(camSettings.ValueRO.Yaw, camSettings.ValueRO.Pitch);
                
                Entity nearestTarget = Entity.Null;
                float nearestAngle = 15f; // Aim assist cone
                float3 nearestDir = float3.zero;
                
                foreach (var (lockOnTarget, targetTransform, targetEntity) in 
                    SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    if (targetEntity == entity) continue;
                    
                    float3 targetPos = targetTransform.ValueRO.Position + new float3(0, 1f, 0); // Chest height
                    float3 toTarget = targetPos - playerPos;
                    float dist = math.length(toTarget);
                    
                    if (dist < 2f || dist > 40f) continue;
                    
                    float3 toTargetNorm = toTarget / dist;
                    float dot = math.dot(cameraForward, toTargetNorm);
                    float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                    
                    if (angle < nearestAngle)
                    {
                        nearestTarget = targetEntity;
                        nearestAngle = angle;
                        nearestDir = toTargetNorm;
                    }
                }
                
                if (nearestTarget != Entity.Null)
                {
                    // Apply sticky aim (reduce sensitivity)
                    float stickyFactor = stickyStrength * (1f - nearestAngle / 15f);
                    aimAssist.ValueRW.InStickyZone = true;
                    aimAssist.ValueRW.CurrentStickyStrength = stickyFactor;
                    aimAssist.ValueRW.StickyTarget = nearestTarget;
                    
                    // Calculate magnetism pull
                    // Convert target direction to yaw/pitch delta
                    float targetYaw = math.degrees(math.atan2(nearestDir.x, nearestDir.z));
                    float targetPitch = math.degrees(math.asin(-nearestDir.y));
                    
                    // Normalize yaw
                    if (targetYaw < 0) targetYaw += 360f;
                    
                    float currentYaw = camSettings.ValueRO.Yaw;
                    float currentPitch = camSettings.ValueRO.Pitch;
                    
                    // Calculate delta (accounting for wrap-around)
                    float yawDelta = targetYaw - currentYaw;
                    if (yawDelta > 180f) yawDelta -= 360f;
                    if (yawDelta < -180f) yawDelta += 360f;
                    float pitchDelta = targetPitch - currentPitch;
                    
                    // Apply magnetism pull (subtle)
                    float pullStrength = magnetismStrength * (1f - nearestAngle / 15f);
                    float2 magnetismPull = new float2(yawDelta, pitchDelta) * pullStrength * deltaTime;
                    
                    aimAssist.ValueRW.MagnetismPull = magnetismPull;
                    
                    // Apply the pull to camera
                    camSettings.ValueRW.Yaw += magnetismPull.x;
                    camSettings.ValueRW.Pitch = math.clamp(camSettings.ValueRO.Pitch + magnetismPull.y, -89f, 89f);
                }
                else
                {
                    aimAssist.ValueRW.InStickyZone = false;
                    aimAssist.ValueRW.CurrentStickyStrength = 0;
                    aimAssist.ValueRW.StickyTarget = Entity.Null;
                    aimAssist.ValueRW.MagnetismPull = float2.zero;
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
