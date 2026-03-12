using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Moves mount entities based on input forwarded from their rider.
    /// Runs on BOTH client and server (predicted) for smooth local movement.
    /// 
    /// Based on Opsive's pattern where player input controls the mount's locomotion.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct MountMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MountMovementInput>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (movementInput, movementConfig, transform, rideableState) 
                in SystemAPI.Query<RefRO<MountMovementInput>, RefRO<MountMovementConfig>, RefRW<LocalTransform>, RefRO<RideableState>>()
                    .WithAll<Simulate>())
            {
                // Only move if has a rider
                if (!rideableState.ValueRO.HasRider)
                {
                    continue;
                }
                
                float forward = movementInput.ValueRO.ForwardInput;
                float horizontal = movementInput.ValueRO.HorizontalInput;
                bool isSprinting = movementInput.ValueRO.SprintInput > 0;
                
                // Skip if no input
                if (math.abs(forward) < 0.01f && math.abs(horizontal) < 0.01f)
                    continue;
                
                // === TURNING ===
                // A/D input turns the mount
                if (math.abs(horizontal) > 0.01f)
                {
                    float turnAmount = horizontal * movementConfig.ValueRO.TurnSpeed * deltaTime;
                    quaternion turnRotation = quaternion.AxisAngle(math.up(), math.radians(turnAmount));
                    transform.ValueRW.Rotation = math.mul(turnRotation, transform.ValueRO.Rotation);
                }
                
                // === FORWARD MOVEMENT ===
                // W/S input moves the mount forward/backward
                // Shift makes horse run (gallop) vs walk (trot)
                if (math.abs(forward) > 0.01f)
                {
                    float3 forwardDir = math.mul(transform.ValueRO.Rotation, math.forward());
                    float moveSpeed = isSprinting ? movementConfig.ValueRO.RunSpeed : movementConfig.ValueRO.WalkSpeed;
                    float speed = forward * moveSpeed * deltaTime;
                    transform.ValueRW.Position += forwardDir * speed;
                }
            }
        }
    }
}

