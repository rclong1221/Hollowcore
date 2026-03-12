using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using Player.Components;
using Player.Settings;
using Player.Systems;
using DIG.Targeting.Core;

/// <summary>
/// Controls player camera based on modular ViewTypes and Spring Physics.
/// Handles switching between Combat, Adventure, and FirstPerson views.
/// Applies spring offsets for recoil and shakes.
/// Updates CameraTarget component which is read by CameraManager.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[UpdateAfter(typeof(CameraSpringSolverSystem))] // Apply springs after solving
public partial struct PlayerCameraControlSystem : ISystem
{
    private int _logFrameCounter;
    private EntityQuery _playerQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _logFrameCounter = 0;
        
        // Cache the query
        _playerQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[] {
                ComponentType.ReadWrite<PlayerCameraSettings>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<CameraTarget>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<CameraViewConfig>(), // New
                ComponentType.ReadOnly<CameraSpringState>()  // New
            }
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        var em = state.EntityManager;
        var entities = _playerQuery.ToEntityArray(Allocator.Temp);

        for (int ei = 0; ei < entities.Length; ++ei)
        {
            var entity = entities[ei];

            var settings = em.GetComponentData<PlayerCameraSettings>(entity);
            var playerTransform = em.GetComponentData<LocalTransform>(entity);
            var target = em.GetComponentData<CameraTarget>(entity);
            var viewConfig = em.GetComponentData<CameraViewConfig>(entity);
            var spring = em.GetComponentData<CameraSpringState>(entity);
            
            // ===== 1. INPUT HANDLING =====
            float2 lookDelta = float2.zero;
            float zoomDelta = 0f;
            if (em.HasComponent<PlayerInput>(entity))
            {
                var netInp = em.GetComponentData<PlayerInput>(entity);
                lookDelta = netInp.LookDelta;
                zoomDelta = netInp.ZoomDelta;
            }
            else if (em.HasComponent<PlayerInputComponent>(entity))
            {
                var hyb = em.GetComponentData<PlayerInputComponent>(entity);
                lookDelta = hyb.LookDelta;
                zoomDelta = hyb.ZoomDelta;
            }

            // ===== 2. VIEW LOGIC (Switch based on ViewType) =====
            
            float3 baseCameraPos = playerTransform.Position;
            quaternion baseCameraRot = quaternion.identity;
            float targetFOV = settings.BaseFOV;
            
            // Shared Rotation Logic (Yaw/Pitch)
            // EPIC 15.16: Read lock state and mode to determine input dampening
            bool isLocked = false;
            float lookInputScale = 1f; // 1 = full input, 0 = no input (camera controlled by lock system)
            float3 lockTargetPos = float3.zero;
            bool hasLockTarget = false;
            float lockRotationStrength = 0f;
            
            if (em.HasComponent<CameraTargetLockState>(entity))
            {
                var lockState = em.GetComponentData<CameraTargetLockState>(entity);
                isLocked = lockState.IsLocked;
                if (isLocked)
                {
                    lockTargetPos = lockState.LastTargetPosition;
                    hasLockTarget = lockState.TargetEntity != Entity.Null;
                }
            }
            
            // Get lock behavior mode to determine input scaling
            // EPIC 15.16: For HardLock, Cinemachine handles camera via LookAt - ECS just disables input
            if (isLocked && SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var lockBehavior))
            {
                switch (lockBehavior.BehaviorType)
                {
                    case DIG.Targeting.Core.LockBehaviorType.HardLock:
                        lookInputScale = 0f; // Camera fully controlled by Cinemachine LookAt
                        lockRotationStrength = 0f; // Don't fight with Cinemachine
                        break;
                    case DIG.Targeting.Core.LockBehaviorType.SoftLock:
                        lookInputScale = 0.7f; // Allow 70% input, lock system provides 30% bias
                        lockRotationStrength = 0.3f;
                        break;
                    case DIG.Targeting.Core.LockBehaviorType.OverTheShoulder:
                        lookInputScale = 0.5f; // 50% input during shoulder view
                        lockRotationStrength = 0.5f;
                        break;
                    case DIG.Targeting.Core.LockBehaviorType.FirstPerson:
                    case DIG.Targeting.Core.LockBehaviorType.TwinStick:
                        lookInputScale = 1f; // Full input, aim assist only
                        lockRotationStrength = 0f;
                        break;
                    case DIG.Targeting.Core.LockBehaviorType.IsometricLock:
                        lookInputScale = 0f; // Camera doesn't move in isometric
                        lockRotationStrength = 0f;
                        break;
                    default:
                        lookInputScale = 0f;
                        lockRotationStrength = 0f; // Cinemachine handles it
                        break;
                }
            }
            else if (isLocked)
            {
                // Default to Cinemachine handling if no behavior set
                lookInputScale = 0f;
                lockRotationStrength = 0f;
            }
            
            // Apply look input with scaling based on lock mode
            float yawBeforeInput = settings.Yaw;
            settings.Yaw += lookDelta.x * settings.LookSensitivity * lookInputScale;
            

            
            float minPitch = settings.MinPitch;
            float maxPitch = settings.MaxPitch;
            
            if (viewConfig.ActiveViewType == CameraViewType.Combat)
            {
                minPitch = viewConfig.CombatMinPitch;
                maxPitch = viewConfig.CombatMaxPitch;
            }
            
            // EPIC 15.16: Apply lookInputScale to pitch as well for proper lock behavior
            settings.Pitch -= lookDelta.y * settings.LookSensitivity * lookInputScale;
            settings.Pitch = math.clamp(settings.Pitch, minPitch, maxPitch);
            
            // EPIC 15.16: Apply lock rotation directly in this system to avoid cross-system data race
            if (isLocked && hasLockTarget && lockRotationStrength > 0f)
            {
                float3 playerPos = playerTransform.Position;
                float3 dirToTarget = lockTargetPos - playerPos;
                dirToTarget.y = 0; // Flatten for yaw
                float distSq = math.lengthsq(dirToTarget);
                
                if (distSq > 0.001f)
                {
                    float3 dirNorm = math.normalize(dirToTarget);
                    float desiredYaw = math.degrees(math.atan2(dirNorm.x, dirNorm.z));
                    if (desiredYaw < 0) desiredYaw += 360f;
                    
                    // Calculate pitch to look at target (from camera position, not player feet)
                    float3 cameraPos = playerPos + new float3(0, 1.7f, 0); // Approximate camera height
                    float3 fullDir = lockTargetPos - cameraPos;
                    float horizontalDist = math.sqrt(fullDir.x * fullDir.x + fullDir.z * fullDir.z);
                    
                    // Only calculate pitch if we have meaningful horizontal distance
                    // This prevents extreme pitch values when directly above/below target
                    float desiredPitch = 0f;
                    if (horizontalDist > 1f)
                    {
                        // Use atan2 for more stable pitch calculation
                        desiredPitch = math.degrees(math.atan2(-fullDir.y, horizontalDist));
                    }
                    else
                    {
                        // Too close - just use a sensible default looking slightly down
                        desiredPitch = fullDir.y < 0 ? 15f : -15f;
                    }
                    
                    // CRITICAL: Lock-on pitch should be constrained to reasonable values
                    // Don't let camera look straight up or straight down
                    float lockPitchMin = -30f; // Don't look too far up
                    float lockPitchMax = 45f;  // Don't look too far down
                    float rawPitch = desiredPitch;
                    desiredPitch = math.clamp(desiredPitch, lockPitchMin, lockPitchMax);
                    

                    
                    // Handle yaw wrap-around
                    float yawDiff = desiredYaw - settings.Yaw;
                    if (yawDiff > 180f) yawDiff -= 360f;
                    if (yawDiff < -180f) yawDiff += 360f;
                    
                    float oldYaw = settings.Yaw;
                    float oldPitch = settings.Pitch;
                    
                    if (lockRotationStrength >= 0.99f)
                    {
                        // Hard lock - snap to target
                        settings.Yaw = desiredYaw;
                        settings.Pitch = desiredPitch;
                    }
                    else
                    {
                        // Soft lock - blend toward target
                        float lerpFactor = lockRotationStrength;
                        settings.Yaw = settings.Yaw + yawDiff * lerpFactor;
                        settings.Pitch = math.lerp(settings.Pitch, desiredPitch, lerpFactor);
                    }
                    
                    // Normalize yaw
                    if (settings.Yaw < 0) settings.Yaw += 360f;
                    if (settings.Yaw >= 360f) settings.Yaw -= 360f;
                }
            }

            // Calculate rotation from Pitch/Yaw
            quaternion viewRotation = quaternion.Euler(math.radians(settings.Pitch), math.radians(settings.Yaw), 0);

            // Handle View Types
            if (viewConfig.ActiveViewType == CameraViewType.FirstPerson)
            {
                // FPS MODE
                // Position at FPS Offset
                float3 eyePos = playerTransform.Position + viewConfig.FPSOffset;
                
                // Add height scaling if available
                if (em.HasComponent<PlayerState>(entity))
                {
                    var ps = em.GetComponentData<PlayerState>(entity);
                    float heightRatio = ps.CurrentHeight / 2.0f;
                    eyePos.y = playerTransform.Position.y + (viewConfig.FPSOffset.y * heightRatio);
                }

                baseCameraPos = eyePos;
                baseCameraRot = viewRotation;
                
                // FPS force distance 0
                settings.CurrentDistance = 0;
                settings.TargetDistance = 0;
            }
            else 
            {
                // ORBIT MODES (Combat, Adventure)
                
                // Update Zoom
                settings.TargetDistance -= zoomDelta * settings.ZoomSpeed;
                settings.TargetDistance = math.clamp(settings.TargetDistance, settings.MinDistance, settings.MaxDistance);
                settings.CurrentDistance = math.lerp(settings.CurrentDistance, settings.TargetDistance, deltaTime * 10f);
                
                // Check if zoomed in to FPS
                if (settings.CurrentDistance < 0.1f)
                {
                    // Transition to FPS logic visually even if ViewType is Combat
                     // (Optional: switch ViewType enum? For now just handle visual)
                     float3 eyePos = playerTransform.Position + viewConfig.FPSOffset;
                     baseCameraPos = eyePos;
                     baseCameraRot = viewRotation;
                }
                else
                {
                    // Calculate Pivot
                    float3 pivotOffset = (viewConfig.ActiveViewType == CameraViewType.Combat) 
                                        ? viewConfig.CombatPivotOffset 
                                        : viewConfig.AdventurePivotOffset;
                    
                    // Height Scaling
                    if (em.HasComponent<PlayerState>(entity))
                    {
                        var ps = em.GetComponentData<PlayerState>(entity);
                        float heightRatio = ps.CurrentHeight / 2.0f;
                        pivotOffset.y *= heightRatio;
                    }

                    float3 pivotPos = playerTransform.Position + pivotOffset;
                    
                    // Calculate Camera Position based on View Rotation and Offset
                    float3 lookDir = math.mul(viewRotation, new float3(0, 0, 1));
                    float3 backDir = -lookDir;
                    
                    // Apply Combat Camera Offset (e.g. over the shoulder)
                    // The offset applies in View Space
                    float3 localOffset = viewConfig.CombatCameraOffset;
                    float3 worldOffset = math.mul(viewRotation, localOffset);
                    
                    // Final Orbit Position
                    baseCameraPos = pivotPos + (backDir * settings.CurrentDistance) + worldOffset;
                    
                    // Look At Pivot (Standard Orbit) or strict View Rotation?
                    // Opsive Combat view typically uses strict View Rotation to keep crosshair aligned
                    baseCameraRot = viewRotation;
                }
            }
            
            // ===== 3. APPLY SPRINGS =====
            // CameraSpringSolverSystem has already updated spring.PositionValue and spring.RotationValue
            
            // Position Spring (Additive to camera position)
            // Transform spring vector by camera rotation to make it local (e.g. Kickback is Z-local)
            float3 springPosWorld = math.mul(baseCameraRot, spring.PositionValue);
            baseCameraPos += springPosWorld;
            
            // Rotation Spring (Additive to camera rotation)
            quaternion springRot = quaternion.Euler(
                math.radians(spring.RotationValue.x), 
                math.radians(spring.RotationValue.y), 
                math.radians(spring.RotationValue.z));
            
            baseCameraRot = math.mul(baseCameraRot, springRot);

            // ===== 4. LEAN MECHANIC (Legacy Support) =====
            // Apply lateral lean if needed (keeping existing feature)
             if (em.HasComponent<LeanState>(entity))
            {
                var lean = em.GetComponentData<LeanState>(entity);
                if (math.abs(lean.CurrentLean) > 0.001f)
                {
                    float leanDist = 0.5f; // Hardcoded default for now
                    float3 right = math.mul(baseCameraRot, new float3(1,0,0));
                    baseCameraPos += right * (lean.CurrentLean * leanDist);
                    
                    float rollRad = -math.radians(30f * lean.CurrentLean);
                    baseCameraRot = math.mul(quaternion.AxisAngle(math.mul(baseCameraRot, new float3(0,0,1)), rollRad), baseCameraRot);
                }
            }

            // ===== 5. WRITE OUTPUT =====
            target.Position = baseCameraPos;
            target.Rotation = baseCameraRot;
            target.FOV = targetFOV;
            
            em.SetComponentData(entity, settings);
            em.SetComponentData(entity, target);
            
            // Debug Log
            if (_logFrameCounter % 120 == 0)
            {
                 DebugLog.LogCamera($"[PlayerCameraControlSystem] {viewConfig.ActiveViewType} | SpringPos: {spring.PositionValue} | TargetPos: {target.Position}");
            }
        }
        
        entities.Dispose();
        _logFrameCounter++;
    }
    
    // Helper to extract euler angles from quaternion for debugging
    private static float3 ToEulerAngles(quaternion q)
    {
        float3 angles;
        
        // Roll (x-axis rotation)
        float sinr_cosp = 2 * (q.value.w * q.value.x + q.value.y * q.value.z);
        float cosr_cosp = 1 - 2 * (q.value.x * q.value.x + q.value.y * q.value.y);
        angles.x = math.atan2(sinr_cosp, cosr_cosp);
        
        // Pitch (y-axis rotation) 
        float sinp = 2 * (q.value.w * q.value.y - q.value.z * q.value.x);
        if (math.abs(sinp) >= 1)
            angles.y = math.sign(sinp) * math.PI / 2;
        else
            angles.y = math.asin(sinp);
            
        // Yaw (z-axis rotation)
        float siny_cosp = 2 * (q.value.w * q.value.z + q.value.x * q.value.y);
        float cosy_cosp = 1 - 2 * (q.value.y * q.value.y + q.value.z * q.value.z);
        angles.z = math.atan2(siny_cosp, cosy_cosp);
        
        return angles;
    }
}
