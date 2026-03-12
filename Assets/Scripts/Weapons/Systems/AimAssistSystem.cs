using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Provides aim assistance by adjusting aim toward valid targets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct AimAssistSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            foreach (var (aimAssist, request, transform, entity) in 
                     SystemAPI.Query<RefRO<AimAssist>, RefRW<UseRequest>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                var config = aimAssist.ValueRO;
                if (config.Strength <= 0)
                    continue;

                float3 position = transform.ValueRO.Position + math.up() * 1.5f;
                float3 aimDir = math.normalize(request.ValueRO.AimDirection);

                // Find best target in cone
                float coneRad = math.radians(config.ConeAngle * 0.5f);
                Entity bestTarget = Entity.Null;
                float bestScore = float.MaxValue;
                float3 bestTargetPos = float3.zero;

                // Cast rays in a cone pattern to find targets
                var hits = new NativeList<RaycastHit>(Allocator.Temp);
                
                // Simple approach: cast center ray and find targets
                var rayInput = new RaycastInput
                {
                    Start = position,
                    End = position + aimDir * config.Range,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    // Check if hit entity is a valid target
                    if (hit.Entity != entity && hit.Entity != Entity.Null)
                    {
                        float distance = hit.Fraction * config.Range;
                        float3 toTarget = hit.Position - position;
                        float angle = math.acos(math.dot(math.normalize(toTarget), aimDir));
                        
                        if (angle < coneRad)
                        {
                            // Score based on angle and distance
                            float score = angle * 100f + distance * 0.1f;
                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestTarget = hit.Entity;
                                bestTargetPos = hit.Position;
                            }
                        }
                    }
                }
                hits.Dispose();

                // Adjust aim toward best target
                if (bestTarget != Entity.Null)
                {
                    float3 toTarget = math.normalize(bestTargetPos - position);
                    float3 adjustedAim = math.lerp(aimDir, toTarget, config.Strength * config.Magnetism);
                    request.ValueRW.AimDirection = math.normalize(adjustedAim);
                }
            }
        }
    }
}
