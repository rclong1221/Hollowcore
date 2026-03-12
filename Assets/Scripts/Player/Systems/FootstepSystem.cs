using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using Unity.Physics.Systems;
using Player.Components;
using Audio.Systems;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 13.18.4: Footstep System with Ground Surface Detection
    /// 
    /// Scans player movement and emits footstep events when appropriate.
    /// Uses ground raycast to detect surface material from the ground entity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class FootstepSystem : SystemBase
    {
        private const float GROUND_RAYCAST_DISTANCE = 0.5f;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            // When using the hybrid Animator->ECS migration we let the Animator drive
            // precise footstep timing on the client. DOTS-side emitter is gated
            // by this flag so it can be re-enabled for server-side compact events.
            if (Audio.Systems.AudioSettings.UseAnimatorForFootsteps) return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            double now = SystemAPI.Time.ElapsedTime;

            foreach (var (pState, xf, physVel, entity) in 
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>()
                     .WithEntityAccess())
            {
                var state = pState.ValueRO;
                if (!state.IsGrounded) continue;

                var vel = physVel.ValueRO.Linear;
                float speed = math.length(new float3(vel.x, 0, vel.z)); // Horizontal speed only
                if (speed < 0.1f) continue;

                // Read or create FootstepTimer
                double last = 0.0;
                if (EntityManager.HasComponent<FootstepTimer>(entity))
                {
                    last = EntityManager.GetComponentData<FootstepTimer>(entity).LastStepTime;
                }

                // Calculate interval based on speed (faster = more frequent footsteps)
                double baseInterval = 0.5;
                double interval = math.max(0.25, baseInterval / math.max(1f, speed / 3f));

                if (now - last > interval)
                {
                    // EPIC 13.18.4: Ground raycast to detect surface material
                    int matId = DetectGroundSurfaceMaterial(physicsWorld, xf.ValueRO.Position);

                    var fe = new FootstepEvent
                    {
                        Position = xf.ValueRO.Position,
                        MaterialId = matId,
                        Stance = (int)state.Stance
                    };

                    ecb.AddComponent(entity, fe);

                    if (EntityManager.HasComponent<FootstepTimer>(entity))
                    {
                        ecb.SetComponent(entity, new FootstepTimer { LastStepTime = now });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new FootstepTimer { LastStepTime = now });
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Raycast downward to detect ground surface material.
        /// </summary>
        private int DetectGroundSurfaceMaterial(PhysicsWorldSingleton physicsWorld, float3 position)
        {
            // Cast slightly above player feet downward
            var rayStart = position + new float3(0, 0.1f, 0);
            var rayEnd = position - new float3(0, GROUND_RAYCAST_DISTANCE, 0);

            var rayInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u, // All layers
                    GroupIndex = 0
                }
            };

            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                // Try to get surface material from hit entity
                if (EntityManager.HasComponent<SurfaceMaterialId>(hit.Entity))
                {
                    return EntityManager.GetComponentData<SurfaceMaterialId>(hit.Entity).Id;
                }

                // Fallback: Try to resolve via SurfaceDetectionService (hybrid path)
                // This requires access to GameObject which is not available in pure ECS
                // Return default for now
            }

            return SurfaceDetectionService.GetDefaultId();
        }
    }
}
