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
    /// EPIC 15.16 Task 1: Over-the-Shoulder System
    /// 
    /// Manages camera offset (left/right shoulder), shoulder swapping,
    /// and ADS zoom behavior for over-the-shoulder camera mode.
    /// 
    /// NOTE: OverTheShoulderState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct OverTheShoulderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if OTS mode is active
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if (behavior.BehaviorType != LockBehaviorType.OverTheShoulder)
                return;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            float defaultShoulderSide = behavior.ShoulderSide;
            
            foreach (var (moduleLink, camSettings, lockState, viewConfig, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRW<PlayerCameraSettings>, RefRO<CameraTargetLockState>, RefRW<CameraViewConfig>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<OverTheShoulderState>(moduleEntity))
                    continue;
                
                var otsState = SystemAPI.GetComponentRW<OverTheShoulderState>(moduleEntity);
                
                // Initialize shoulder side if not set
                if (otsState.ValueRO.DesiredShoulderSide == 0f)
                {
                    otsState.ValueRW.DesiredShoulderSide = defaultShoulderSide;
                    otsState.ValueRW.CurrentShoulderSide = defaultShoulderSide;
                }
                
                // Handle ADS (Aim Down Sights) - use secondary fire or dedicated ADS button
                bool isAiming = input.ValueRO.AltUse.IsSet;
                otsState.ValueRW.IsAiming = isAiming;
                
                // Set desired zoom based on ADS
                otsState.ValueRW.DesiredZoom = isAiming ? 0.6f : 1f; // 0.6 = zoomed in
                
                // Smooth zoom interpolation
                float currentZoom = otsState.ValueRO.CurrentZoom;
                if (currentZoom == 0f) currentZoom = 1f; // Initialize
                float targetZoom = otsState.ValueRO.DesiredZoom;
                otsState.ValueRW.CurrentZoom = math.lerp(currentZoom, targetZoom, deltaTime * 8f);
                
                // Handle shoulder swap input
                // Using Q/E or bumpers for shoulder swap
                // For now, detect if aim crosses center when locked
                if (lockState.ValueRO.IsLocked)
                {
                    // Auto-swap shoulder if target would be occluded
                    // This is a simplified check - real implementation would raycast
                    float3 targetDir = lockState.ValueRO.LastTargetPosition - 
                        SystemAPI.GetComponent<LocalTransform>(entity).Position;
                    
                    float3 right = GetCameraRight(camSettings.ValueRO.Yaw);
                    float side = math.dot(math.normalize(targetDir), right);
                    
                    // If target is significantly to one side, swap shoulder
                    if (math.abs(side) > 0.3f)
                    {
                        // Swap so target is on the camera side (not shoulder side)
                        otsState.ValueRW.DesiredShoulderSide = side > 0 ? -1f : 1f;
                    }
                }
                
                // Smooth shoulder interpolation
                float currentShoulder = otsState.ValueRO.CurrentShoulderSide;
                float targetShoulder = otsState.ValueRO.DesiredShoulderSide;
                otsState.ValueRW.CurrentShoulderSide = math.lerp(currentShoulder, targetShoulder, deltaTime * 5f);
                
                // Apply shoulder offset to camera
                float shoulderOffset = otsState.ValueRO.CurrentShoulderSide * 0.5f; // 0.5m offset
                float zoomOffset = (1f - otsState.ValueRO.CurrentZoom) * 1f; // Move camera closer when zoomed
                
                // Update view config offsets
                // CombatCameraOffset.x = shoulder offset, .z = zoom offset
                var combatOffset = viewConfig.ValueRO.CombatCameraOffset;
                combatOffset.x = shoulderOffset;
                combatOffset.z = -2f + zoomOffset; // Base -2m + zoom closer
                viewConfig.ValueRW.CombatCameraOffset = combatOffset;
            }
        }
        
        private float3 GetCameraRight(float yaw)
        {
            float yawRad = math.radians(yaw);
            return new float3(math.cos(yawRad), 0, -math.sin(yawRad));
        }
    }
}
