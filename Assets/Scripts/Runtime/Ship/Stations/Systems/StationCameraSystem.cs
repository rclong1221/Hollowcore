using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// Client-side system that manages camera transitions when entering/exiting stations.
    /// Switches to station camera view when piloting, restores player camera on exit.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    
    public partial class StationCameraSystem : SystemBase
    {
        private bool _wasOperating;
        private Entity _previousStation;
        private StationType _previousStationType;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Find local player
            bool isOperating = false;
            Entity currentStation = Entity.Null;
            StationType stationType = StationType.Helm;
            Entity playerEntity = Entity.Null;

            foreach (var (operating, entity) in
                     SystemAPI.Query<RefRO<OperatingStation>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                if (operating.ValueRO.IsOperating)
                {
                    isOperating = true;
                    currentStation = operating.ValueRO.StationEntity;
                    stationType = operating.ValueRO.StationType;
                    playerEntity = entity;
                    break;
                }
            }

            // Check for local player without OperatingStation (just exited or never entered)
            if (!isOperating)
            {
                foreach (var (playerState, entity) in
                         SystemAPI.Query<RefRO<PlayerState>>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithNone<OperatingStation>()
                         .WithEntityAccess())
                {
                    playerEntity = entity;
                    break;
                }
            }

            // Detect state changes
            if (isOperating && !_wasOperating)
            {
                // Just started operating
                OnEnterStation(playerEntity, currentStation, stationType);
            }
            else if (!isOperating && _wasOperating)
            {
                // Just stopped operating
                OnExitStation(playerEntity, _previousStation, _previousStationType);
            }
            else if (isOperating && currentStation != _previousStation)
            {
                // Switched stations (rare, but handle it)
                OnExitStation(playerEntity, _previousStation, _previousStationType);
                OnEnterStation(playerEntity, currentStation, stationType);
            }

            _wasOperating = isOperating;
            _previousStation = currentStation;
            _previousStationType = stationType;
        }

        private void OnEnterStation(Entity playerEntity, Entity stationEntity, StationType stationType)
        {
            // Get station's camera target if any
            if (!SystemAPI.HasComponent<OperableStation>(stationEntity))
                return;

            var station = SystemAPI.GetComponent<OperableStation>(stationEntity);

            // Save current camera settings for restoration
            if (SystemAPI.HasComponent<PlayerCameraSettings>(playerEntity))
            {
                var camSettings = SystemAPI.GetComponent<PlayerCameraSettings>(playerEntity);
                
                // Add camera override component
                EntityManager.AddComponentData(playerEntity, new StationCameraOverride
                {
                    CameraTargetEntity = station.CameraTarget,
                    OriginalDistance = camSettings.CurrentDistance,
                    OriginalPitch = camSettings.Pitch,
                    OriginalYaw = camSettings.Yaw
                });

                // For helm/piloting, switch to a wider view
                if (stationType == StationType.Helm)
                {
                    camSettings.TargetDistance = 12f; // Further back for ship view
                    camSettings.Pitch = 15f; // Slightly elevated
                    camSettings.MinDistance = 5f; // Don't allow too close
                    EntityManager.SetComponentData(playerEntity, camSettings);
                }
                else if (stationType == StationType.WeaponStation)
                {
                    // Weapon stations might have a targeting camera
                    camSettings.TargetDistance = 0f; // First person for aiming
                    camSettings.Pitch = 0f;
                    EntityManager.SetComponentData(playerEntity, camSettings);
                }
            }

            // Find camera controller and notify
            var cameraController = Object.FindAnyObjectByType<StationCameraController>();
            if (cameraController != null)
            {
                cameraController.OnEnterStation(stationType, stationEntity);
            }
        }

        private void OnExitStation(Entity playerEntity, Entity stationEntity, StationType stationType)
        {
            // Restore camera settings
            if (SystemAPI.HasComponent<StationCameraOverride>(playerEntity) &&
                SystemAPI.HasComponent<PlayerCameraSettings>(playerEntity))
            {
                var cameraOverride = SystemAPI.GetComponent<StationCameraOverride>(playerEntity);
                var camSettings = SystemAPI.GetComponent<PlayerCameraSettings>(playerEntity);

                // Restore original settings
                camSettings.TargetDistance = cameraOverride.OriginalDistance;
                camSettings.CurrentDistance = cameraOverride.OriginalDistance;
                camSettings.Pitch = cameraOverride.OriginalPitch;
                camSettings.Yaw = cameraOverride.OriginalYaw;
                camSettings.MinDistance = 0f; // Allow FPS again

                EntityManager.SetComponentData(playerEntity, camSettings);
                EntityManager.RemoveComponent<StationCameraOverride>(playerEntity);
            }

            // Notify camera controller
            var cameraController = Object.FindAnyObjectByType<StationCameraController>();
            if (cameraController != null)
            {
                cameraController.OnExitStation(stationType);
            }
        }
    }
}
