using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Player.IK;
using DIG.Core.Input;

namespace DIG.Player.Systems.IK
{
    /// <summary>
    /// EPIC 14.18 - Updates AimDirection component based on camera settings.
    /// This drives the LookAt IK system for head turn.
    /// 
    /// EPIC 15.20 - Extended to support cursor-based aim for isometric/ARPG modes.
    /// - FPS/TPS: Aim point is camera forward (where crosshair points)
    /// - Isometric/ARPG: Aim point is cursor world position (where mouse is)
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LookAtIKSystem))]
    public partial struct AimDirectionSystem : ISystem
    {
        private const bool DebugEnabled = false;
        private int _frameCounter;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCounter++;
            bool foundAny = false;
            
            foreach (var (aimDir, cameraSettings, transform, entity) in
                SystemAPI.Query<RefRW<AimDirection>, RefRO<PlayerCameraSettings>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag, GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                foundAny = true;
                
                var cam = UnityEngine.Camera.main;
                if (cam == null) continue;
                
                float3 eyePosition = transform.ValueRO.Position + new float3(0, 1.6f, 0); // Eye level
                float3 aimPoint;
                
                // EPIC 15.20: Check for cursor-based aiming (isometric/ARPG modes)
                // Read directly from ParadigmStateMachine since ECS sync may not be ready at startup
                bool useCursorAim = false;
                var paradigmMachine = ParadigmStateMachine.Instance;
                if (paradigmMachine != null && paradigmMachine.ActiveProfile != null)
                {
                    useCursorAim = paradigmMachine.ActiveProfile.useScreenRelativeMovement;
                }
                // Fallback to ECS component if available
                else if (state.EntityManager.HasComponent<InputParadigmState>(entity))
                {
                    var paradigmState = state.EntityManager.GetComponentData<InputParadigmState>(entity);
                    useCursorAim = paradigmState.UseScreenRelativeMovement;
                }
                
                if (useCursorAim)
                {
                    // CURSOR-BASED AIM: Raycast from cursor to ground plane
                    // EPIC 15.21: Use PlayerInputState.CursorScreenPosition instead of direct Mouse.current
                    var cursorPos = global::Player.Systems.PlayerInputState.CursorScreenPosition;
                    var ray = cam.ScreenPointToRay(new UnityEngine.Vector3(cursorPos.x, cursorPos.y, 0));
                    
                    // Raycast to ground plane at character height
                    float groundY = transform.ValueRO.Position.y;
                    var groundPlane = new UnityEngine.Plane(UnityEngine.Vector3.up, new UnityEngine.Vector3(0, groundY, 0));
                    
                    if (groundPlane.Raycast(ray, out float distance))
                    {
                        aimPoint = ray.GetPoint(distance);
                        // Elevate aim point to character chest level for more natural head turn
                        aimPoint.y = eyePosition.y - 0.3f; // Slightly below eye level
                    }
                    else
                    {
                        // Fallback: point forward
                        float3 fwd = math.forward(transform.ValueRO.Rotation);
                        aimPoint = eyePosition + fwd * 5f;
                    }
                }
                else
                {
                    // CAMERA-FORWARD AIM: Standard FPS/TPS behavior
                    float3 lookDir = cam.transform.forward;
                    float lookDistance = 10f;
                    aimPoint = eyePosition + lookDir * lookDistance;
                }
                
                // Update component
                aimDir.ValueRW.AimPoint = aimPoint;
                aimDir.ValueRW.AimAngles = new float2(cameraSettings.ValueRO.Yaw, cameraSettings.ValueRO.Pitch);
                
                // Debug log every 60 frames (~1 second at 60fps) - more frequent for debugging
                if (DebugEnabled && _frameCounter % 60 == 0)
                {
                    bool hasParadigm = state.EntityManager.HasComponent<InputParadigmState>(entity);
                    UnityEngine.Debug.Log($"[AimDir] HasParadigm={hasParadigm} CursorAim={useCursorAim} AimPoint:{aimPoint}");
                }
            }
            
            // Log once if no entities found (helps debug)
            if (DebugEnabled && !foundAny && _frameCounter == 60)
            {
                UnityEngine.Debug.LogWarning("[LookAtIK] No entities found with AimDirection + PlayerCameraSettings + PlayerTag!");
            }
        }
    }
}
