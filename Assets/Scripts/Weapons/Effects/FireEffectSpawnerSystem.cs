using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// Spawns fire effects (muzzle flash, shell ejection) when weapons are fired.
    ///
    /// This system:
    /// 1. Watches for FireEffectRequest components
    /// 2. Spawns appropriate visual effects (particles, lights)
    /// 3. Queues audio events for the audio system
    /// 4. Handles tracer spawning based on config probability
    ///
    /// Effects are spawned as entities with limited lifetime for automatic cleanup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct FireEffectSpawnerSystem : ISystem
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

            // Process fire effect requests
            foreach (var (request, effectConfig, transform, entity) in
                     SystemAPI.Query<RefRW<FireEffectRequest>, RefRO<WeaponEffectConfig>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                ref var req = ref request.ValueRW;

                if (!req.Pending)
                    continue;

                var config = effectConfig.ValueRO;
                var weaponTransform = transform.ValueRO;

                // Calculate muzzle position
                float3 muzzlePos = req.Position;
                if (math.lengthsq(config.MuzzleOffset) > 0.001f)
                {
                    // Transform muzzle offset by weapon rotation
                    muzzlePos = weaponTransform.Position +
                               math.mul(weaponTransform.Rotation, config.MuzzleOffset);
                }

                // Spawn muzzle flash
                if (config.MuzzleFlashPrefabIndex >= 0)
                {
                    SpawnMuzzleFlash(ref ecb, muzzlePos, req.Direction, config.MuzzleFlashPrefabIndex);
                }

                // Spawn shell ejection
                if (config.ShellEjectPrefabIndex >= 0)
                {
                    float3 shellPos = weaponTransform.Position +
                                     math.mul(weaponTransform.Rotation, config.ShellEjectOffset);
                    float3 shellDir = math.mul(weaponTransform.Rotation, config.ShellEjectDirection);

                    SpawnShellEject(ref ecb, shellPos, shellDir, config.ShellEjectSpeed,
                        config.ShellEjectPrefabIndex);
                }

                // Spawn tracer (probability-based)
                if (config.TracerPrefabIndex >= 0 && config.TracerProbability > 0)
                {
                    // Use deterministic random based on frame and entity
                    var random = Unity.Mathematics.Random.CreateFromIndex(
                        (uint)(entity.Index + SystemAPI.Time.ElapsedTime * 1000));

                    if (random.NextFloat() < config.TracerProbability)
                    {
                        SpawnTracer(ref ecb, muzzlePos, req.Direction, config.TracerPrefabIndex);
                    }
                }

                // Clear the request
                req.Pending = false;
            }
        }

        private void SpawnMuzzleFlash(ref EntityCommandBuffer ecb, float3 position,
            float3 direction, int prefabIndex)
        {
            // Create a temporary entity for the muzzle flash
            // In a full implementation, this would instantiate from a prefab registry
            var flashEntity = ecb.CreateEntity();

            ecb.AddComponent(flashEntity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.LookRotationSafe(direction, math.up()),
                Scale = 1f
            });

            // Add lifetime for auto-cleanup
            ecb.AddComponent(flashEntity, new EffectLifetime
            {
                RemainingTime = 0.1f // Muzzle flash lasts ~100ms
            });

            // Tag for identification
            ecb.AddComponent(flashEntity, new MuzzleFlashTag { PrefabIndex = prefabIndex });
        }

        private void SpawnShellEject(ref EntityCommandBuffer ecb, float3 position,
            float3 direction, float speed, int prefabIndex)
        {
            var shellEntity = ecb.CreateEntity();

            ecb.AddComponent(shellEntity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            // Add physics-like movement
            ecb.AddComponent(shellEntity, new ShellCasingMovement
            {
                Velocity = direction * speed + new float3(0, 2f, 0), // Add upward component
                AngularVelocity = new float3(10f, 5f, 3f), // Random spin
                Gravity = 9.81f
            });

            ecb.AddComponent(shellEntity, new EffectLifetime
            {
                RemainingTime = 3f // Shells last 3 seconds
            });

            ecb.AddComponent(shellEntity, new ShellCasingTag { PrefabIndex = prefabIndex });
        }

        private void SpawnTracer(ref EntityCommandBuffer ecb, float3 startPos,
            float3 direction, int prefabIndex)
        {
            var tracerEntity = ecb.CreateEntity();

            ecb.AddComponent(tracerEntity, new LocalTransform
            {
                Position = startPos,
                Rotation = quaternion.LookRotationSafe(direction, math.up()),
                Scale = 1f
            });

            ecb.AddComponent(tracerEntity, new ActiveTracer
            {
                StartPosition = startPos,
                EndPosition = startPos + direction * 100f, // Default range
                Speed = 500f, // Fast tracer
                Progress = 0f,
                MaxLifetime = 0.5f,
                ElapsedTime = 0f
            });

            ecb.AddComponent(tracerEntity, new TracerTag { PrefabIndex = prefabIndex });
        }
    }

    /// <summary>
    /// Lifetime component for auto-destroying effects.
    /// </summary>
    public struct EffectLifetime : IComponentData
    {
        public float RemainingTime;
    }

    /// <summary>
    /// Tag for muzzle flash effects.
    /// </summary>
    public struct MuzzleFlashTag : IComponentData
    {
        public int PrefabIndex;
    }

    /// <summary>
    /// Tag for shell casing effects.
    /// </summary>
    public struct ShellCasingTag : IComponentData
    {
        public int PrefabIndex;
    }

    /// <summary>
    /// Tag for tracer effects.
    /// </summary>
    public struct TracerTag : IComponentData
    {
        public int PrefabIndex;
    }

    /// <summary>
    /// Movement component for shell casings.
    /// </summary>
    public struct ShellCasingMovement : IComponentData
    {
        public float3 Velocity;
        public float3 AngularVelocity;
        public float Gravity;
    }

    /// <summary>
    /// System to update and cleanup effect entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(FireEffectSpawnerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct EffectLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Update lifetimes and destroy expired effects
            foreach (var (lifetime, entity) in
                     SystemAPI.Query<RefRW<EffectLifetime>>()
                     .WithEntityAccess())
            {
                lifetime.ValueRW.RemainingTime -= deltaTime;

                if (lifetime.ValueRO.RemainingTime <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            // Update shell casing movement
            foreach (var (movement, transform) in
                     SystemAPI.Query<RefRW<ShellCasingMovement>, RefRW<LocalTransform>>())
            {
                ref var move = ref movement.ValueRW;
                ref var xform = ref transform.ValueRW;

                // Apply gravity
                move.Velocity.y -= move.Gravity * deltaTime;

                // Update position
                xform.Position += move.Velocity * deltaTime;

                // Update rotation
                xform.Rotation = math.mul(xform.Rotation,
                    quaternion.Euler(move.AngularVelocity * deltaTime));
            }

            // Update tracers
            foreach (var (tracer, transform) in
                     SystemAPI.Query<RefRW<ActiveTracer>, RefRW<LocalTransform>>())
            {
                ref var t = ref tracer.ValueRW;
                ref var xform = ref transform.ValueRW;

                t.ElapsedTime += deltaTime;
                t.Progress = math.saturate(t.ElapsedTime * t.Speed /
                    math.distance(t.StartPosition, t.EndPosition));

                // Lerp position
                xform.Position = math.lerp(t.StartPosition, t.EndPosition, t.Progress);
            }
        }
    }
}
