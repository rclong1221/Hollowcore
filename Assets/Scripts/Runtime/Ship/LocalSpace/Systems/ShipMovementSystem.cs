#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// System that drives ship movement based on helm input.
    /// Reads StationInput from helm station and applies to ship kinematics.
    /// </summary>
    // [BurstCompile]  // Disable for debug logging
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(ShipInertialCorrectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShipMovementSystem : ISystem
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Debug: Count ships
            int shipCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<ShipKinematics>, RefRO<ShipRoot>>().WithAll<Simulate>())
                shipCount++;
            if (shipCount == 0)
            {
                // Only log occasionally to reduce spam
                if (DebugEnabled)
                {
                    UnityEngine.Debug.Log($"[ShipMovement] No ships found with ShipKinematics+ShipRoot+Simulate");
                }
            }

            // Process ship movement 
            foreach (var (kinematics, transform, shipRoot, entity) in
                     SystemAPI.Query<RefRW<ShipKinematics>, RefRW<LocalTransform>, RefRO<ShipRoot>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var kin = ref kinematics.ValueRW;
                ref var t = ref transform.ValueRW;

                // Check if there's a helm station with input
                float3 thrustInput = float3.zero;
                float3 rotationInput = float3.zero;
                bool hasInput = false;

                // Find helm station for this ship with active input
                foreach (var (station, stationInput, stationEntity) in
                         SystemAPI.Query<RefRO<Stations.OperableStation>, RefRO<Stations.StationInput>>()
                         .WithEntityAccess())
                {
                    // Only process helm stations
                    if (station.ValueRO.Type != Stations.StationType.Helm)
                        continue;

                    // Check if station has an operator
                    if (station.ValueRO.CurrentOperator == Entity.Null)
                        continue;

                    // Verify station belongs to this ship (Input Targeting Fix)
                        if (station.ValueRO.ShipEntity != entity)
                        {
                            if (DebugEnabled)
                            {
                                UnityEngine.Debug.Log($"[ShipMovement] Helm {stationEntity.Index} ShipEntity {station.ValueRO.ShipEntity.Index} != Ship {entity.Index}");
                            }
                            continue;
                        }

                    var input = stationInput.ValueRO;
                    if (DebugEnabled)
                    {
                        UnityEngine.Debug.Log($"[ShipMovement] Helm found! Move={input.Move}, Modifier={input.Modifier}, Primary={input.Primary}");
                    }

                    // Map input to thrust/rotation
                    // Move.x = yaw (turn left/right)
                    // Move.y = forward/backward thrust
                    // Modifier = vertical thrust (up/down)
                    thrustInput = new float3(
                        0f,                                    // Side thrust not mapped
                        input.Modifier > 0 ? 1f : 0f,         // Vertical thrust (jump = up)
                        input.Move.y                           // Forward/backward
                    );

                    rotationInput = new float3(
                        input.Look.y * 0.5f,  // Pitch from look
                        input.Move.x,          // Yaw from move
                        -input.Look.x * 0.3f   // Roll from look (inverted, reduced)
                    );

                    // Primary = boost, Secondary = brake
                    if (input.Primary > 0)
                    {
                        thrustInput *= 2f; // Boost
                    }
                    if (input.Secondary > 0)
                    {
                        // Brake - reduce velocity
                        kin.LinearVelocity *= 0.95f;
                        kin.AngularVelocity *= 0.9f;
                    }

                    hasInput = true;
                    // We found our ship's helm, stop searching
                    break;
                }

                // Apply thrust
                if (hasInput && math.lengthsq(thrustInput) > 0.01f)
                {
                    // Convert local thrust to world space
                    float3 worldThrust = math.mul(t.Rotation, thrustInput * 10f * deltaTime);
                    kin.LinearVelocity += worldThrust;
                    if (DebugEnabled)
                    {
                        UnityEngine.Debug.Log($"[ShipMovement] Applied thrust! worldThrust={worldThrust}, newVelocity={kin.LinearVelocity}");
                    }
                }

                // Apply rotation
                if (hasInput && math.lengthsq(rotationInput) > 0.01f)
                {
                    kin.AngularVelocity += rotationInput * 0.5f * deltaTime;
                }

                // Apply velocity damping (space friction)
                kin.LinearVelocity *= 0.99f;
                kin.AngularVelocity *= 0.95f;

                // Clamp velocities
                float linearSpeed = math.length(kin.LinearVelocity);
                if (linearSpeed > kin.MaxLinearSpeed)
                {
                    kin.LinearVelocity = math.normalize(kin.LinearVelocity) * kin.MaxLinearSpeed;
                }

                float angularSpeed = math.length(kin.AngularVelocity);
                if (angularSpeed > kin.MaxAngularSpeed)
                {
                    kin.AngularVelocity = math.normalize(kin.AngularVelocity) * kin.MaxAngularSpeed;
                }

                // Update IsMoving flag
                kin.IsMoving = linearSpeed > 0.01f || angularSpeed > 0.01f;

                // Apply velocity to transform OR PhysicsBody (Physics Integration Fix)
                if (SystemAPI.HasComponent<Unity.Physics.PhysicsVelocity>(entity))
                {
                    // Dynamic Physics Body
                    var physVel = SystemAPI.GetComponentRW<Unity.Physics.PhysicsVelocity>(entity);
                    physVel.ValueRW.Linear = kin.LinearVelocity;
                    physVel.ValueRW.Angular = kin.AngularVelocity;
                }
                else
                {
                    // Kinematic (Transform-based)
                    float3 oldPos = t.Position;
                    t.Position += kin.LinearVelocity * deltaTime;

                    if (angularSpeed > 0.001f)
                    {
                        quaternion angularDelta = quaternion.EulerXYZ(kin.AngularVelocity * deltaTime);
                        t.Rotation = math.mul(t.Rotation, angularDelta);
                        t.Rotation = math.normalize(t.Rotation);
                    }
                    
                    // Debug: Show ship movement
                    if (kin.IsMoving)
                    {
                        if (DebugEnabled)
                        {
                            UnityEngine.Debug.Log($"[ShipMovement] Ship {entity.Index} moved from {oldPos} to {t.Position}, velocity={kin.LinearVelocity}");
                        }
                    }
                }
            }
        }
    }
}
