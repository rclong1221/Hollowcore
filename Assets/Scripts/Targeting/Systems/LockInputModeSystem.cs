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
    /// EPIC 15.16 Task 10: Lock Input Mode System
    /// 
    /// Handles different input modes: Toggle, Hold, ClickTarget, AutoNearest, HoverTarget.
    /// Writes to TargetingState based on the current input mode.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct LockInputModeSystem : ISystem
    {
        // Cached config values for this frame (EPIC 15.16 optimization)
        private float _maxLockRange;
        private float _maxLockAngle;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
            
            // EPIC 15.16: Check if this system should handle lock input
            // If InputHandler is set to CameraLockOnSystem (default), skip processing here
            if (behavior.InputHandler == LockInputHandler.CameraLockOnSystem)
                return;
            
            var inputMode = behavior.InputMode;
            
            // Cache config values for helper methods (EPIC 15.16 optimization)
            _maxLockRange = behavior.MaxLockRange > 0 ? behavior.MaxLockRange : 30f;
            _maxLockAngle = behavior.MaxLockAngle > 0 ? behavior.MaxLockAngle : 30f;
            
            // Check if target locking is allowed
            bool allowTargetLock = true;
            if (SystemAPI.TryGetSingleton<TargetLockSettings>(out var settings))
            {
                allowTargetLock = settings.AllowTargetLock;
            }
            
            if (!allowTargetLock) return;
            
            // Dispatch to appropriate handler based on mode
            switch (inputMode)
            {
                case LockInputMode.Toggle:
                    HandleToggleMode(ref state);
                    break;
                case LockInputMode.Hold:
                    HandleHoldMode(ref state);
                    break;
                case LockInputMode.AutoNearest:
                    HandleAutoNearestMode(ref state);
                    break;
                case LockInputMode.ClickTarget:
                    // Handled by mouse input system (isometric)
                    break;
                case LockInputMode.HoverTarget:
                    HandleHoverMode(ref state);
                    break;
            }
        }
        
        private void HandleToggleMode(ref SystemState state)
        {
            // Toggle mode: press to lock, press again to unlock
            // Note: This only runs if InputHandler == LockInputModeSystem
        }
        
        private void HandleHoldMode(ref SystemState state)
        {
            // Hold mode: Lock while button held, unlock on release
            foreach (var (lockState, transform, input, entity) in 
                SystemAPI.Query<RefRW<CameraTargetLockState>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                bool isHeld = input.ValueRO.Grab.IsSet;
                
                if (isHeld && !lockState.ValueRO.IsLocked)
                {
                    // Just started holding - acquire target
                    Entity target = FindNearestTarget(ref state, transform.ValueRO.Position, entity);
                    if (target != Entity.Null)
                    {
                        lockState.ValueRW.IsLocked = true;
                        lockState.ValueRW.TargetEntity = target;
                        // Start in Locking phase - camera is en route
                        lockState.ValueRW.Phase = LockPhase.Locking;
                    }
                }
                else if (!isHeld && lockState.ValueRO.IsLocked)
                {
                    // Released - unlock
                    lockState.ValueRW.IsLocked = false;
                    lockState.ValueRW.TargetEntity = Entity.Null;
                    lockState.ValueRW.Phase = LockPhase.Unlocked;
                    lockState.ValueRW.JustUnlocked = true;
                }
            }
        }
        
        private void HandleAutoNearestMode(ref SystemState state)
        {
            // Auto mode: Always target nearest, no input required
            foreach (var (lockState, transform, entity) in 
                SystemAPI.Query<RefRW<CameraTargetLockState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                Entity target = FindNearestTarget(ref state, transform.ValueRO.Position, entity);
                
                if (target != Entity.Null)
                {
                    if (!lockState.ValueRO.IsLocked || lockState.ValueRO.TargetEntity != target)
                    {
                        lockState.ValueRW.IsLocked = true;
                        lockState.ValueRW.TargetEntity = target;
                        // Start in Locking phase - camera is en route
                        lockState.ValueRW.Phase = LockPhase.Locking;
                    }
                }
                else if (lockState.ValueRO.IsLocked)
                {
                    lockState.ValueRW.IsLocked = false;
                    lockState.ValueRW.TargetEntity = Entity.Null;
                    lockState.ValueRW.Phase = LockPhase.Unlocked;
                    lockState.ValueRW.JustUnlocked = true;
                }
            }
        }
        
        private void HandleHoverMode(ref SystemState state)
        {
            // Hover mode: Target is whatever is under the crosshair
            // This would require raycasting from camera - simplified version uses nearest to center
            foreach (var (lockState, transform, camSettings, entity) in 
                SystemAPI.Query<RefRW<CameraTargetLockState>, RefRO<LocalTransform>, RefRO<PlayerCameraSettings>>()
                .WithEntityAccess())
            {
                float3 cameraForward = GetCameraForward(camSettings.ValueRO.Yaw, camSettings.ValueRO.Pitch);
                Entity target = FindTargetInCrosshair(ref state, transform.ValueRO.Position, cameraForward, entity);
                
                if (target != Entity.Null)
                {
                    // Soft lock (for aim assist, not hard lock)
                    lockState.ValueRW.TargetEntity = target;
                    // Don't set IsLocked - this is hover, not explicit lock
                }
                else
                {
                    lockState.ValueRW.TargetEntity = Entity.Null;
                }
            }
        }
        
        private Entity FindNearestTarget(ref SystemState state, float3 playerPos, Entity self)
        {
            float maxRange = _maxLockRange;
            Entity bestTarget = Entity.Null;
            float bestDistSq = maxRange * maxRange;
            int bestPriority = int.MinValue;
            
            foreach (var (lockOnTarget, transform, targetEntity) in 
                SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (targetEntity == self) continue;
                
                float3 targetPos = transform.ValueRO.Position;
                float distSq = math.distancesq(playerPos, targetPos);
                
                if (distSq > maxRange * maxRange) continue;
                
                int priority = lockOnTarget.ValueRO.Priority;
                
                if (priority > bestPriority || (priority == bestPriority && distSq < bestDistSq))
                {
                    bestTarget = targetEntity;
                    bestDistSq = distSq;
                    bestPriority = priority;
                }
            }
            
            return bestTarget;
        }
        
        private Entity FindTargetInCrosshair(ref SystemState state, float3 playerPos, float3 forward, Entity self)
        {
            float maxRange = _maxLockRange;
            float maxAngle = 5f; // Very tight cone for crosshair (intentionally tight)
            Entity bestTarget = Entity.Null;
            float bestAngle = maxAngle;
            
            foreach (var (lockOnTarget, transform, targetEntity) in 
                SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (targetEntity == self) continue;
                
                float3 toTarget = transform.ValueRO.Position - playerPos;
                float dist = math.length(toTarget);
                
                if (dist < 1f || dist > maxRange) continue;
                
                float3 toTargetNorm = toTarget / dist;
                float dot = math.dot(forward, toTargetNorm);
                float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                
                if (angle < bestAngle)
                {
                    bestTarget = targetEntity;
                    bestAngle = angle;
                }
            }
            
            return bestTarget;
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
