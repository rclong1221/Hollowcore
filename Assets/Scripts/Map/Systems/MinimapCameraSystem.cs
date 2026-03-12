using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Positions the orthographic minimap camera on the local player.
    /// Handles zoom input and rotation mode (rotate-with-player vs north-up).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MinimapCameraSystem : SystemBase
    {
        private const float kCameraHeight = 200f;

        protected override void OnCreate()
        {
            RequireForUpdate<MinimapConfig>();
            RequireForUpdate<MapManagedState>();
        }

        protected override void OnUpdate()
        {
            var managed = SystemAPI.ManagedAPI.GetSingleton<MapManagedState>();
            if (!managed.IsInitialized || managed.MinimapCamera == null) return;

            var config = SystemAPI.GetSingleton<MinimapConfig>();

            // Find local player position + rotation
            float3 playerPos = float3.zero;
            float playerYaw = 0f;
            bool foundPlayer = false;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = ltw.ValueRO.Position;
                // Extract yaw from forward direction
                float3 fwd = ltw.ValueRO.Forward;
                playerYaw = math.degrees(math.atan2(fwd.x, fwd.z));
                foundPlayer = true;
                break;
            }
            if (!foundPlayer) return;

            // Handle zoom input (mouse scroll wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (math.abs(scroll) > 0.01f && IsMinimapHovered())
            {
                float newZoom = config.Zoom - scroll * config.ZoomStep * 10f;
                config.Zoom = math.clamp(newZoom, config.MinZoom, config.MaxZoom);
                SystemAPI.SetSingleton(config);
            }

            // Position camera above player
            var cam = managed.MinimapCamera;
            cam.transform.position = new Vector3(playerPos.x, playerPos.y + kCameraHeight, playerPos.z);
            cam.orthographicSize = config.Zoom;

            // Rotation mode
            if (config.RotateWithPlayer)
                cam.transform.rotation = Quaternion.Euler(90, playerYaw, 0);
            else
                cam.transform.rotation = Quaternion.Euler(90, 0, 0); // North-up
        }

        private bool IsMinimapHovered()
        {
            // Simple check: only apply zoom when mouse is in lower-right quadrant of screen
            // More precise check would use UI raycast, but this avoids UI dependency
            var mousePos = Input.mousePosition;
            return mousePos.x > Screen.width * 0.7f && mousePos.y < Screen.height * 0.3f;
        }
    }
}
