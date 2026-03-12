using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Physics;
using Player.Components;
using Player.Systems;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// Handles movement when player is swimming.
    /// Applies drag, buoyancy, surface positioning, and 3D directional movement.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WaterDetectionSystem))]
    [UpdateBefore(typeof(CharacterControllerSystem))]
    public partial struct SwimmingMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            foreach (var (swimState, swimSettings, physicsSettings, playerInput, velocity, transform, entity) in
                SystemAPI.Query<
                    RefRO<SwimmingState>,
                    RefRO<SwimmingMovementSettings>,
                    RefRO<SwimmingPhysicsSettings>,
                    RefRO<PlayerInput>,
                    RefRW<PhysicsVelocity>,
                    RefRW<LocalTransform>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                if (!swimState.ValueRO.IsSwimming)
                    continue;
                
                // Get water properties if available
                float viscosity = 0.5f;
                float3 currentVelocity = float3.zero;
                
                if (swimState.ValueRO.WaterZoneEntity != Entity.Null && 
                    SystemAPI.HasComponent<WaterProperties>(swimState.ValueRO.WaterZoneEntity))
                {
                    var waterProps = SystemAPI.GetComponent<WaterProperties>(swimState.ValueRO.WaterZoneEntity);
                    viscosity = waterProps.Viscosity;
                    currentVelocity = waterProps.CurrentVelocity;
                }
                
                // Get camera rotation for directional swimming
                // PRIORITY: 1. Input CameraYaw (Client authoritative look) 2. PlayerCameraSettings (Server backup) 3. Transform Forward
                float3 forward = math.forward(transform.ValueRO.Rotation);
                float3 right = math.rotate(transform.ValueRO.Rotation, math.right());
                
                if (playerInput.ValueRO.CameraYawValid != 0)
                {
                    // Use the camera yaw sent by the client input
                    var yawRotation = quaternion.Euler(0f, math.radians(playerInput.ValueRO.CameraYaw), 0f);
                    forward = math.rotate(yawRotation, math.forward());
                    right = math.rotate(yawRotation, math.right());
                }
                else if (SystemAPI.HasComponent<PlayerCameraSettings>(entity))
                {
                    // Fallback to component data
                    var camState = SystemAPI.GetComponent<PlayerCameraSettings>(entity);
                    var rotation = quaternion.Euler(math.radians(camState.Pitch), math.radians(camState.Yaw), 0);
                    forward = math.rotate(rotation, math.forward());
                    right = math.rotate(quaternion.Euler(0, math.radians(camState.Yaw), 0), math.right());
                }
                
                // Build input direction
                float2 moveInput = new float2(playerInput.ValueRO.Horizontal, playerInput.ValueRO.Vertical);
                float3 moveDirection = float3.zero;
                
                // Horizontal movement (camera-relative)
                moveDirection += forward * moveInput.y;
                moveDirection += right * moveInput.x;
                
                // Explicit Vertical movement with Space/Crouch
                if (playerInput.ValueRO.Jump.IsSet)
                {
                    moveDirection.y += 1f; // Ascend
                }
                if (playerInput.ValueRO.Crouch.IsSet)
                {
                    moveDirection.y -= 1f; // Descend
                }
                
                // Debug values to verify inputs
                // if (playerInput.ValueRO.Jump.IsSet || playerInput.ValueRO.Crouch.IsSet)
                // {
                //      UnityEngine.Debug.Log($"[SwimVertical] Jump={playerInput.ValueRO.Jump.IsSet} Crouch={playerInput.ValueRO.Crouch.IsSet} Y={moveDirection.y}");
                // }
                
                if (math.lengthsq(moveDirection) > 0.01f)
                {
                    moveDirection = math.normalize(moveDirection);
                }
                
                // Check for ANY vertical intent (Explicit Input OR Look direction)
                // This allows diving by looking down and pressing forward
                bool hasVerticalInput = math.abs(moveDirection.y) > 0.1f;
                bool hasAnyInput = math.lengthsq(moveDirection) > 0.01f;

                // Calculate target velocity
                float speed = swimSettings.ValueRO.SwimSpeed;
                if (playerInput.ValueRO.Sprint.IsSet)
                {
                    speed *= swimSettings.ValueRO.SprintMultiplier;
                }
                
                float3 targetVelocity = moveDirection * speed;
                
                // Apply water current
                targetVelocity += currentVelocity;
                
                // Get current velocity
                float3 currentVel = velocity.ValueRO.Linear;
                
                // Apply acceleration/deceleration
                float3 velDiff = targetVelocity - currentVel;
                float accel = hasAnyInput 
                    ? swimSettings.ValueRO.Acceleration 
                    : swimSettings.ValueRO.Deceleration;
                
                float3 newVelocity = currentVel + velDiff * math.min(1f, accel * dt);
                
                // Apply drag (viscous resistance)
                float drag = swimSettings.ValueRO.DragCoefficient * viscosity;
                newVelocity *= math.max(0f, 1f - drag * dt);
                
                // Calculate distance from surface (positive = below surface, negative = above)
                float distanceFromSurface = swimState.ValueRO.WaterSurfaceY - transform.ValueRO.Position.y;
                bool nearSurface = distanceFromSurface >= 0 && distanceFromSurface < physicsSettings.ValueRO.SurfaceThreshold;

                // Surface Positioning - Anchor player at water surface logic
                // ONLY apply if we are NOT trying to move vertically
                if (nearSurface && !hasVerticalInput && !swimState.ValueRO.IsSubmerged)
                {
                    // Calculate target Y position (water surface + offset)
                    float targetY = swimState.ValueRO.WaterSurfaceY + physicsSettings.ValueRO.SurfaceAnchorOffset;
                    float currentY = transform.ValueRO.Position.y;

                    // Smoothly move towards target position
                    float yDiff = targetY - currentY;
                    float anchorForce = yDiff * physicsSettings.ValueRO.SurfaceAnchorSpeed;

                    // Apply as velocity adjustment (clamped)
                    newVelocity.y = math.lerp(newVelocity.y, anchorForce, dt * physicsSettings.ValueRO.SurfaceAnchorSpeed);

                    // Dampen horizontal velocity when idle at surface
                    if (!hasAnyInput)
                    {
                        newVelocity.x *= math.max(0f, 1f - 3f * dt);
                        newVelocity.z *= math.max(0f, 1f - 3f * dt);
                    }
                }
                else if (swimState.ValueRO.SubmersionDepth > 0.1f)
                {
                    // Apply Buoyancy
                    // DISABLE buoyancy if player is intentionally swimming down/up
                    // This prevents fighting against input
                    if (!hasVerticalInput)
                    {
                        // Spring-based Buoyancy: Creates equilibrium at neck level (80% submerged)
                        float ratio = math.saturate(swimState.ValueRO.SubmersionDepth / math.max(0.1f, swimState.ValueRO.PlayerHeight));
                        float targetRatio = 0.8f; 
                        float stiffness = 20f;

                        float springForce = (ratio - targetRatio) * stiffness;
                        newVelocity.y += springForce * dt;
                    }
                }

                // Store velocity
                velocity.ValueRW.Linear = newVelocity;

                // ROTATION LOGIC: Face the movement direction
                // Only rotate if there is significant horizontal movement
                float3 horizontalMoveDir = new float3(newVelocity.x, 0f, newVelocity.z);
                if (math.lengthsq(horizontalMoveDir) > 0.001f)
                {
                    // Calculate target rotation (yaw only)
                    quaternion targetRotation = quaternion.LookRotationSafe(math.normalize(horizontalMoveDir), math.up());
                    
                    // Smoothly interpolate rotation
                    // Use a high rotation speed for responsive turning
                    float rotationSpeed = 10f; 
                    transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRotation, dt * rotationSpeed);
                }
            }
        }
    }
}
