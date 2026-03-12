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
    /// EPIC 15.16 Task 2: Twin-Stick Aiming System
    /// 
    /// Move with left stick, aim with right stick independently.
    /// Lock = sticky aim (aim slows near targets).
    /// Designed for top-down/isometric cameras.
    /// 
    /// NOTE: AimAssistState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct TwinStickAimSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if twin-stick mode is active
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if (behavior.BehaviorType != LockBehaviorType.TwinStick)
                return;
            
            float stickyStrength = behavior.StickyAimStrength;
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (moduleLink, camSettings, lockState, transform, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRW<PlayerCameraSettings>, RefRW<CameraTargetLockState>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<AimAssistState>(moduleEntity))
                    continue;
                
                var aimAssist = SystemAPI.GetComponentRW<AimAssistState>(moduleEntity);
                
                // Get aim direction from right stick (Look input)
                float2 aimInput = input.ValueRO.LookDelta;
                
                // Only process if there's aim input
                if (math.lengthsq(aimInput) < 0.1f)
                {
                    continue;
                }
                
                // Convert aim input to world direction (isometric projection)
                // In isometric, we interpret the stick as a 2D direction on the ground plane
                float3 aimWorldDir = new float3(aimInput.x, 0, aimInput.y);
                aimWorldDir = math.normalize(aimWorldDir);
                
                // Find nearest target in aim direction
                float3 playerPos = transform.ValueRO.Position;
                float maxRange = 20f;
                float aimCone = 45f; // degrees
                
                Entity nearestTarget = Entity.Null;
                float nearestDist = maxRange;
                float3 nearestPos = float3.zero;
                
                foreach (var (lockOnTarget, targetTransform, targetEntity) in 
                    SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    if (targetEntity == entity) continue;
                    
                    float3 targetPos = targetTransform.ValueRO.Position;
                    float3 toTarget = targetPos - playerPos;
                    toTarget.y = 0; // Flatten for isometric
                    
                    float dist = math.length(toTarget);
                    if (dist < 1f || dist > maxRange) continue;
                    
                    float3 toTargetNorm = toTarget / dist;
                    float dot = math.dot(aimWorldDir, toTargetNorm);
                    float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                    
                    if (angle < aimCone && dist < nearestDist)
                    {
                        nearestTarget = targetEntity;
                        nearestDist = dist;
                        nearestPos = targetPos;
                    }
                }
                
                if (nearestTarget != Entity.Null)
                {
                    // Apply sticky aim
                    float stickyFactor = stickyStrength * (1f - nearestDist / maxRange);
                    aimAssist.ValueRW.InStickyZone = true;
                    aimAssist.ValueRW.CurrentStickyStrength = stickyFactor;
                    aimAssist.ValueRW.StickyTarget = nearestTarget;
                    
                    // Auto-target the nearest in aim direction
                    bool wasLocked = lockState.ValueRO.IsLocked;
                    Entity prevTarget = lockState.ValueRO.TargetEntity;
                    lockState.ValueRW.TargetEntity = nearestTarget;
                    lockState.ValueRW.IsLocked = true;
                    lockState.ValueRW.LastTargetPosition = nearestPos;
                    
                    // Set Phase based on whether target changed
                    if (!wasLocked || prevTarget != nearestTarget)
                    {
                        lockState.ValueRW.Phase = LockPhase.Locking;
                    }
                }
                else
                {
                    aimAssist.ValueRW.InStickyZone = false;
                    aimAssist.ValueRW.CurrentStickyStrength = 0;
                    aimAssist.ValueRW.StickyTarget = Entity.Null;
                    
                    // No target in aim direction
                    if (lockState.ValueRO.IsLocked)
                    {
                        lockState.ValueRW.JustUnlocked = true;
                    }
                    lockState.ValueRW.IsLocked = false;
                    lockState.ValueRW.TargetEntity = Entity.Null;
                    lockState.ValueRW.Phase = LockPhase.Unlocked;
                }
                
                // For twin-stick, the character should face the aim direction
                // This would be handled by the character rotation system reading aim input
            }
        }
    }
}
