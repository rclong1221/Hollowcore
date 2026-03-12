using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that copies camera Yaw/Pitch/Distance from PlayerInput commands
    /// into PlayerCameraSettings. This makes the ghost-replicated [GhostField] fields
    /// on PlayerCameraSettings carry the correct values to non-owner clients,
    /// enabling spectators to see the watched player's camera perspective.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CameraSettingsServerSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (input, cameraSettings) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<PlayerCameraSettings>>())
            {
                if (input.ValueRO.CameraYawValid != 0)
                {
                    cameraSettings.ValueRW.Yaw = input.ValueRO.CameraYaw;
                    cameraSettings.ValueRW.Pitch = input.ValueRO.CameraPitch;
                    cameraSettings.ValueRW.CurrentDistance = input.ValueRO.CameraDistance;
                }
            }
        }
    }
}
