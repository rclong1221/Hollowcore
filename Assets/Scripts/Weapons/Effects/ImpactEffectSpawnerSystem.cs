using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// Spawns impact effects (decals, particles, sounds) when projectiles hit surfaces.
    ///
    /// This system:
    /// 1. Consumes ImpactEffectRequest buffer elements
    /// 2. Determines appropriate effect based on surface material
    /// 3. Spawns decal entities with proper orientation
    /// 4. Queues particle effects and audio events
    ///
    /// Decals are projected onto surfaces and fade over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ImpactEffectSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Process impact effect requests
            foreach (var (impactRequests, entity) in
                     SystemAPI.Query<DynamicBuffer<ImpactEffectRequest>>()
                     .WithEntityAccess())
            {
                for (int i = 0; i < impactRequests.Length; i++)
                {
                    var request = impactRequests[i];
                    SpawnImpactEffect(ref ecb, request);
                }

                // Clear the buffer after processing
                impactRequests.Clear();
            }
        }

        private void SpawnImpactEffect(ref EntityCommandBuffer ecb, ImpactEffectRequest request)
        {
            // Create decal entity
            var decalEntity = ecb.CreateEntity();

            // Calculate decal orientation (facing along surface normal)
            quaternion decalRotation = quaternion.LookRotationSafe(-request.Normal, math.up());

            // Offset slightly from surface to prevent z-fighting
            float3 decalPosition = request.Position + request.Normal * 0.01f;

            ecb.AddComponent(decalEntity, new LocalTransform
            {
                Position = decalPosition,
                Rotation = decalRotation,
                Scale = request.Scale > 0 ? request.Scale : 0.2f
            });

            // Determine decal lifetime based on impact type
            float lifetime = request.Type switch
            {
                ImpactType.Bullet => 30f,    // Bullet holes last 30s
                ImpactType.Melee => 20f,     // Slash marks last 20s
                ImpactType.Explosion => 60f, // Scorch marks last 60s
                _ => 15f
            };

            ecb.AddComponent(decalEntity, new DecalTag
            {
                Lifetime = lifetime,
                ElapsedTime = 0f,
                FadeStartTime = lifetime - 5f // Start fading 5s before removal
            });

            // Add surface material info for rendering
            ecb.AddComponent(decalEntity, new ImpactDecalInfo
            {
                SurfaceMaterialId = request.SurfaceMaterialId,
                ImpactType = request.Type
            });

            // Create particle effect entity (separate from decal)
            var particleEntity = ecb.CreateEntity();

            ecb.AddComponent(particleEntity, new LocalTransform
            {
                Position = request.Position,
                Rotation = decalRotation,
                Scale = 1f
            });

            ecb.AddComponent(particleEntity, new ImpactParticleTag
            {
                SurfaceMaterialId = request.SurfaceMaterialId,
                ImpactType = request.Type
            });

            ecb.AddComponent(particleEntity, new EffectLifetime
            {
                RemainingTime = 2f // Particles last 2s
            });
        }
    }

    /// <summary>
    /// Info component for impact decals.
    /// </summary>
    public struct ImpactDecalInfo : IComponentData
    {
        public int SurfaceMaterialId;
        public ImpactType ImpactType;
    }

    /// <summary>
    /// Tag for impact particle effects.
    /// </summary>
    public struct ImpactParticleTag : IComponentData
    {
        public int SurfaceMaterialId;
        public ImpactType ImpactType;
    }

    /// <summary>
    /// System to manage decal fading and cleanup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ImpactEffectSpawnerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct DecalLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (decal, transform, entity) in
                     SystemAPI.Query<RefRW<DecalTag>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
            {
                ref var d = ref decal.ValueRW;
                d.ElapsedTime += deltaTime;

                // Fade out by scaling down
                if (d.ElapsedTime > d.FadeStartTime)
                {
                    float fadeProgress = (d.ElapsedTime - d.FadeStartTime) /
                                        (d.Lifetime - d.FadeStartTime);
                    fadeProgress = math.saturate(fadeProgress);

                    // Scale down to zero
                    transform.ValueRW.Scale = math.lerp(0.2f, 0f, fadeProgress);
                }

                // Destroy when lifetime exceeded
                if (d.ElapsedTime >= d.Lifetime)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }

    /// <summary>
    /// Helper system to create impact requests from projectile hits.
    /// Called by ProjectileImpactPresentationSystem or similar.
    /// </summary>
    public static class ImpactEffectHelper
    {
        /// <summary>
        /// Queue an impact effect request on a buffer.
        /// </summary>
        public static void QueueImpact(DynamicBuffer<ImpactEffectRequest> buffer,
            float3 position, float3 normal, ImpactType type,
            int surfaceMaterialId = 0, float scale = 0.2f)
        {
            buffer.Add(new ImpactEffectRequest
            {
                Position = position,
                Normal = normal,
                Type = type,
                SurfaceMaterialId = surfaceMaterialId,
                Scale = scale
            });
        }
    }
}
