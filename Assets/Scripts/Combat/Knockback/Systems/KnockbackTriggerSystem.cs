using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Detects trigger events between KnockbackSourceConfig entities and
    /// KnockbackState entities. Creates KnockbackRequest for each qualifying trigger event.
    /// Used for environmental hazards: steam vents, push traps, geysers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(KnockbackResolveSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct KnockbackTriggerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<KnockbackSourceConfig>();
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var sourceLookup = SystemAPI.GetComponentLookup<KnockbackSourceConfig>(false);
            var knockbackStateLookup = SystemAPI.GetComponentLookup<KnockbackState>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

            var simulation = SystemAPI.GetSingleton<SimulationSingleton>();

            // Process trigger events
            foreach (var triggerEvent in simulation.AsSimulation().TriggerEvents)
            {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                // Identify which is source and which is target
                Entity sourceEntity = Entity.Null;
                Entity targetEntity = Entity.Null;

                if (sourceLookup.HasComponent(entityA) && knockbackStateLookup.HasComponent(entityB))
                {
                    sourceEntity = entityA;
                    targetEntity = entityB;
                }
                else if (sourceLookup.HasComponent(entityB) && knockbackStateLookup.HasComponent(entityA))
                {
                    sourceEntity = entityB;
                    targetEntity = entityA;
                }

                if (sourceEntity == Entity.Null) continue;

                var sourceConfig = sourceLookup[sourceEntity];

                // Check cooldown
                if (elapsedTime - sourceConfig.LastTriggerTime < sourceConfig.Cooldown)
                    continue;

                // Update last trigger time
                sourceConfig.LastTriggerTime = elapsedTime;
                sourceLookup[sourceEntity] = sourceConfig;

                // Compute direction
                float3 direction;
                if (transformLookup.HasComponent(sourceEntity) && transformLookup.HasComponent(targetEntity))
                {
                    float3 sourcePos = transformLookup[sourceEntity].Position;
                    float3 targetPos = transformLookup[targetEntity].Position;
                    direction = math.normalizesafe(targetPos - sourcePos, new float3(0, 1, 0));
                }
                else
                {
                    direction = new float3(0, 1, 0); // Default: push up
                }

                // Compute distance for falloff
                float distance = 0f;
                if (sourceConfig.Falloff != KnockbackFalloff.None && sourceConfig.Radius > 0f)
                {
                    if (transformLookup.HasComponent(sourceEntity) && transformLookup.HasComponent(targetEntity))
                    {
                        distance = math.length(
                            transformLookup[targetEntity].Position - transformLookup[sourceEntity].Position);
                    }
                }

                // Create knockback request
                var kbEntity = ecb.CreateEntity();
                ecb.AddComponent(kbEntity, new KnockbackRequest
                {
                    TargetEntity = targetEntity,
                    SourceEntity = sourceEntity,
                    Direction = direction,
                    Force = sourceConfig.Force,
                    Type = sourceConfig.Type,
                    Falloff = sourceConfig.Falloff,
                    Distance = distance,
                    MaxRadius = sourceConfig.Radius,
                    Easing = sourceConfig.Easing,
                    TriggersInterrupt = sourceConfig.TriggersInterrupt
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
