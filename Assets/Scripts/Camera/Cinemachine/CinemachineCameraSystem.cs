using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;

namespace DIG.CameraSystem.Cinemachine
{
    /// <summary>
    /// EPIC 14.18 - Cinemachine Camera System
    /// ECS system that updates Cinemachine camera controller from player input.
    /// 
    /// Responsibilities:
    /// - Reads player input (look delta, zoom delta)
    /// - Updates PlayerCameraSettings (yaw, pitch, distance)
    /// - Does NOT compute camera position (Cinemachine handles that)
    /// 
    /// This replaces the camera positioning logic from PlayerCameraControlSystem
    /// while keeping the input processing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Unity.NetCode.PredictedSimulationSystemGroup))]
    public partial struct CinemachineCameraSystem : ISystem
    {
        private int _frameCounter;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Only run if CinemachineCameraController exists
            if (!CinemachineCameraController.HasInstance) return;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            var controller = CinemachineCameraController.Instance;
            
            foreach (var (settings, viewConfig, transform, input, entity) in 
                SystemAPI.Query<RefRW<PlayerCameraSettings>, RefRO<CameraViewConfig>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                    .WithAll<GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                // EPIC 15.16: Check if locked - if so, skip input processing (handled by PlayerCameraControlSystem)
                bool isLocked = false;
                if (SystemAPI.HasComponent<CameraTargetLockState>(entity))
                {
                    var lockState = SystemAPI.GetComponent<CameraTargetLockState>(entity);
                    isLocked = lockState.IsLocked;
                }
                
                // Skip input processing when locked - lock system handles camera rotation
                if (isLocked) continue;
                
                // ===== INPUT PROCESSING =====
                float2 lookDelta = input.ValueRO.LookDelta;
                float zoomDelta = input.ValueRO.ZoomDelta;
                
                // Update yaw (horizontal rotation)
                settings.ValueRW.Yaw += lookDelta.x * settings.ValueRO.LookSensitivity;
                
                // Normalize yaw to 0-360
                while (settings.ValueRW.Yaw > 360f) settings.ValueRW.Yaw -= 360f;
                while (settings.ValueRW.Yaw < 0f) settings.ValueRW.Yaw += 360f;
                
                // Update pitch (vertical rotation)
                float minPitch = settings.ValueRO.MinPitch;
                float maxPitch = settings.ValueRO.MaxPitch;
                
                // Use view-specific pitch limits if in combat mode
                if (viewConfig.ValueRO.ActiveViewType == CameraViewType.Combat)
                {
                    minPitch = viewConfig.ValueRO.CombatMinPitch;
                    maxPitch = viewConfig.ValueRO.CombatMaxPitch;
                }
                
                settings.ValueRW.Pitch -= lookDelta.y * settings.ValueRO.LookSensitivity;
                settings.ValueRW.Pitch = math.clamp(settings.ValueRW.Pitch, minPitch, maxPitch);
                
                // ===== ZOOM PROCESSING =====
                settings.ValueRW.TargetDistance -= zoomDelta * settings.ValueRO.ZoomSpeed;
                settings.ValueRW.TargetDistance = math.clamp(
                    settings.ValueRW.TargetDistance, 
                    settings.ValueRO.MinDistance, 
                    settings.ValueRO.MaxDistance
                );
                
                // Smooth zoom interpolation
                settings.ValueRW.CurrentDistance = math.lerp(
                    settings.ValueRO.CurrentDistance, 
                    settings.ValueRO.TargetDistance, 
                    deltaTime * 10f
                );
                
                // ===== DEBUG LOGGING =====
                if (_frameCounter % 120 == 0)
                {
                    DebugLog.LogCamera($"[CinemachineCameraSystem] Yaw: {settings.ValueRO.Yaw:F1} Pitch: {settings.ValueRO.Pitch:F1} Distance: {settings.ValueRO.CurrentDistance:F1}");
                }
            }
            
            // Also check for hybrid input component
            foreach (var (settings, viewConfig, transform, input, entity) in 
                SystemAPI.Query<RefRW<PlayerCameraSettings>, RefRO<CameraViewConfig>, RefRO<LocalTransform>, RefRO<PlayerInputComponent>>()
                    .WithAll<GhostOwnerIsLocal>()
                    .WithNone<PlayerInput>()
                    .WithEntityAccess())
            {
                // EPIC 15.16: Check if locked - if so, skip input processing (handled by PlayerCameraControlSystem)
                bool isLocked = false;
                if (SystemAPI.HasComponent<CameraTargetLockState>(entity))
                {
                    var lockState = SystemAPI.GetComponent<CameraTargetLockState>(entity);
                    isLocked = lockState.IsLocked;
                }
                
                // Skip input processing when locked - lock system handles camera rotation
                if (isLocked) continue;
                
                float2 lookDelta = input.ValueRO.LookDelta;
                float zoomDelta = input.ValueRO.ZoomDelta;
                
                settings.ValueRW.Yaw += lookDelta.x * settings.ValueRO.LookSensitivity;
                while (settings.ValueRW.Yaw > 360f) settings.ValueRW.Yaw -= 360f;
                while (settings.ValueRW.Yaw < 0f) settings.ValueRW.Yaw += 360f;
                
                float minPitch = settings.ValueRO.MinPitch;
                float maxPitch = settings.ValueRO.MaxPitch;
                
                if (viewConfig.ValueRO.ActiveViewType == CameraViewType.Combat)
                {
                    minPitch = viewConfig.ValueRO.CombatMinPitch;
                    maxPitch = viewConfig.ValueRO.CombatMaxPitch;
                }
                
                settings.ValueRW.Pitch -= lookDelta.y * settings.ValueRO.LookSensitivity;
                settings.ValueRW.Pitch = math.clamp(settings.ValueRW.Pitch, minPitch, maxPitch);
                
                settings.ValueRW.TargetDistance -= zoomDelta * settings.ValueRO.ZoomSpeed;
                settings.ValueRW.TargetDistance = math.clamp(
                    settings.ValueRW.TargetDistance, 
                    settings.ValueRO.MinDistance, 
                    settings.ValueRO.MaxDistance
                );
                
                settings.ValueRW.CurrentDistance = math.lerp(
                    settings.ValueRO.CurrentDistance, 
                    settings.ValueRO.TargetDistance, 
                    deltaTime * 10f
                );
            }
            
            _frameCounter++;
        }
    }
}
