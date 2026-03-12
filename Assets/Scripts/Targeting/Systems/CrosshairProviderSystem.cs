using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// Updates CrosshairData component with current aim ray from camera.
    /// This is the default crosshair provider using screen center.
    /// 
    /// Other targeting systems can write to CrosshairData to override
    /// the aim point (e.g., for different crosshair assets, aim-down-sights, etc.)
    /// 
    /// Priority order (later systems override earlier):
    /// 1. CrosshairProviderSystem (default - screen center)
    /// 2. Custom crosshair systems (if any)
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct CrosshairProviderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraTarget>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Get main camera for screen-to-world conversion
            var mainCam = Camera.main;
            if (mainCam == null) return;
            
            foreach (var (camTarget, crosshair, entity) in 
                SystemAPI.Query<RefRO<CameraTarget>, RefRW<CrosshairData>>()
                .WithEntityAccess())
            {
                // Use camera position and forward as aim ray
                float3 camPos = camTarget.ValueRO.Position;
                float3 camFwd = math.mul(camTarget.ValueRO.Rotation, new float3(0, 0, 1));
                
                crosshair.ValueRW.RayOrigin = camPos;
                crosshair.ValueRW.RayDirection = camFwd;
                crosshair.ValueRW.ScreenPosition = new float2(0.5f, 0.5f); // Screen center
                
                // Optional: Perform raycast to find what crosshair is pointing at
                // This could be expensive, so we only do it if needed
                // For now, just set up the ray - lock system will do its own targeting
                crosshair.ValueRW.HitValid = false;
                crosshair.ValueRW.HitEntity = Entity.Null;
            }
        }
    }
}
