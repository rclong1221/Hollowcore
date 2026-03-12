using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.IK;
using Player.Components; // AimDirection

namespace DIG.Player.Systems.IK
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct LookAtIKSystem : ISystem
    {
        private int _frameCounter;
        private bool _enableDebugLogs;
        
        public void OnUpdate(ref SystemState state)
        {
            _frameCounter++;
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool foundAny = false;

            foreach (var (settings, ikState, aimDir, transform, entity) in
                     SystemAPI.Query<RefRO<LookAtIKSettings>, RefRW<LookAtIKState>, RefRO<AimDirection>, RefRO<LocalTransform>>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                foundAny = true;
                
                if (settings.ValueRO.Mode == LookAtMode.Disabled)
                {
                    ikState.ValueRW.TargetWeight = 0f;
                    ikState.ValueRW.HasTarget = false;
                }
                else
                {
                    // 1. Update Smoothed Target (Add Lag)
                    float3 targetPoint = aimDir.ValueRO.AimPoint;

                    // Lerp towards target
                    float lag = math.max(0.01f, settings.ValueRO.AimLagAmount);
                    ikState.ValueRW.SmoothedTarget = math.lerp(
                        ikState.ValueRO.SmoothedTarget,
                        targetPoint,
                        deltaTime * (1f / lag)
                    );
                    
                    ikState.ValueRW.LookTarget = ikState.ValueRW.SmoothedTarget;
                    ikState.ValueRW.HasTarget = true;

                    // 2. Calculate Angle & Distance Limits
                    float3 rootPos = transform.ValueRO.Position + new float3(0, 1.6f, 0); // Eye level approximation
                    float3 toTarget = ikState.ValueRW.LookTarget - rootPos;
                    float distSq = math.lengthsq(toTarget);
                    float maxDist = settings.ValueRO.MaxAimDistance;
                    
                    float3 fwd = transform.ValueRO.Forward();
                    float3 dirToTarget = math.normalize(toTarget);
                    float dot = math.dot(fwd, dirToTarget);
                    float angle = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                    
                    // 3. Weight Calculation
                    float targetWeight = settings.ValueRO.HeadWeight;

                    // Distance Fade
                    if (distSq > maxDist * maxDist)
                        targetWeight = 0f;
                    
                    // Angle Fade/Clamp
                    if (angle > settings.ValueRO.MaxTotalAngle)
                    {
                        targetWeight = 0f; 
                    }

                    // Speed Reduction (optional - only if entity has PhysicsVelocity)
                    if (state.EntityManager.HasComponent<PhysicsVelocity>(entity))
                    {
                        var velocity = state.EntityManager.GetComponentData<PhysicsVelocity>(entity);
                        float speed = math.length(velocity.Linear);
                        if (speed > settings.ValueRO.SpeedReductionStart)
                        {
                            float range = settings.ValueRO.SpeedReductionEnd - settings.ValueRO.SpeedReductionStart;
                            if (range > 0)
                            {
                                float t = (speed - settings.ValueRO.SpeedReductionStart) / range;
                                targetWeight *= (1f - math.saturate(t));
                            }
                        }
                    }
                    
                    ikState.ValueRW.TargetWeight = targetWeight;
                }

                // 4. Blend Current Weight
                ikState.ValueRW.CurrentWeight = math.lerp(
                    ikState.ValueRO.CurrentWeight,
                    ikState.ValueRW.TargetWeight,
                    deltaTime * settings.ValueRO.BlendSpeed
                );
                
                // Debug log
                if (_enableDebugLogs && _frameCounter % 300 == 0)
                {
                    UnityEngine.Debug.Log($"[LookAtIK] System: Entity={entity.Index}:{entity.Version} AimPoint={aimDir.ValueRO.AimPoint} LookTarget={ikState.ValueRW.LookTarget} SmoothedTarget={ikState.ValueRW.SmoothedTarget} Weight={ikState.ValueRW.CurrentWeight:F2}");
                }
            }
            
            // Warn if no entities found
            if (_enableDebugLogs && !foundAny && _frameCounter == 60)
            {
                UnityEngine.Debug.LogWarning("[LookAtIK] LookAtIKSystem found no entities with LookAtIKSettings + LookAtIKState + AimDirection!");
            }
        }
    }
}
