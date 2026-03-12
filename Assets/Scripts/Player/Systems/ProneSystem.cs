using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Player.Components;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.NetCode;
using UnityEngine;

namespace Player.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class ProneSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Don't require specific input component - process both NetCode and hybrid paths
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process NetCode predicted input path (networked multiplayer)
            foreach (var (inputRO, proneRW, ltRO, entity) in SystemAPI.Query<RefRO<PlayerInput>, RefRW<ProneStateComponent>, RefRO<Unity.Transforms.LocalTransform>>().WithAll<Simulate>().WithEntityAccess())
            {
                var input = inputRO.ValueRO;
                ref var prone = ref proneRW.ValueRW;
                var lt = ltRO.ValueRO;

                // If press Prone (toggle), start transition
                if (input.Prone.IsSet)
                {
                    Debug.Log($"[ProneSystem] input.Prone.IsSet=TRUE for entity {entity}");
                    if (prone.IsProne == 0)
                    {
                        // Enter prone immediately
                        prone.IsProne = 1;
                        prone.IsCrawling = 0;
                        prone.TransitionTimer = prone.TransitionDuration;
                        ecb.SetComponent(entity, prone);

                        if (EntityManager.HasComponent<PlayerState>(entity))
                        {
                            var ps = EntityManager.GetComponentData<PlayerState>(entity);
                            ps.Stance = PlayerStance.Prone;
                            ps.LastStanceChangeTime = (float)SystemAPI.Time.ElapsedTime;
                            // Set target height from stance config if available
                            if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
                            {
                                var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                                ps.TargetHeight = cfg.ProneHeight;
                            }
                            else
                            {
                                ps.TargetHeight = PlayerStanceConfig.Default.ProneHeight;
                            }

                            // If per-entity tuning for interpolation exists, use it for height transitions
                            if (SystemAPI.HasComponent<ProneTuning>(entity))
                            {
                                var tuning = SystemAPI.GetComponent<ProneTuning>(entity);
                                ps.HeightTransitionSpeed = tuning.HeightInterpSpeed;
                            }
                            ecb.SetComponent(entity, ps);
                        }
                    }
                    else
                    {
                        // request to stand up - perform safe-stand check before clearing
                        bool canStand = SafeStandCheck(lt.Position, entity);
                        if (canStand)
                        {
                            prone.IsProne = 0;
                            prone.IsCrawling = 0;
                            prone.TransitionTimer = prone.TransitionDuration;
                            ecb.SetComponent(entity, prone);

                            if (EntityManager.HasComponent<PlayerState>(entity))
                            {
                                var ps = EntityManager.GetComponentData<PlayerState>(entity);
                                ps.Stance = PlayerStance.Standing;
                                ps.LastStanceChangeTime = (float)SystemAPI.Time.ElapsedTime;
                                if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
                                {
                                    var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                                    ps.TargetHeight = cfg.StandingHeight;
                                }
                                else
                                {
                                    ps.TargetHeight = PlayerStanceConfig.Default.StandingHeight;
                                }

                                // If per-entity tuning for interpolation exists, use it for height transitions
                                if (SystemAPI.HasComponent<ProneTuning>(entity))
                                {
                                    var tuning = SystemAPI.GetComponent<ProneTuning>(entity);
                                    ps.HeightTransitionSpeed = tuning.HeightInterpSpeed;
                                }
                                ecb.SetComponent(entity, ps);
                            }
                        }
                    }
                }
            }

            // Process hybrid input path (local/non-networked)
            foreach (var (inputRO, proneRW, ltRO, entity) in SystemAPI.Query<RefRO<Player.Components.PlayerInputComponent>, RefRW<ProneStateComponent>, RefRO<Unity.Transforms.LocalTransform>>().WithNone<PlayerInput>().WithEntityAccess())
            {
                var input = inputRO.ValueRO;
                ref var prone = ref proneRW.ValueRW;
                var lt = ltRO.ValueRO;

                // If press Prone (toggle), start transition
                if (input.Prone != 0)
                {
                    if (prone.IsProne == 0)
                    {
                        // Enter prone immediately
                        prone.IsProne = 1;
                        prone.IsCrawling = 0;
                        prone.TransitionTimer = prone.TransitionDuration;
                        ecb.SetComponent(entity, prone);

                        if (EntityManager.HasComponent<PlayerState>(entity))
                        {
                            var ps = EntityManager.GetComponentData<PlayerState>(entity);
                            ps.Stance = PlayerStance.Prone;
                            ps.LastStanceChangeTime = (float)SystemAPI.Time.ElapsedTime;
                            // Set target height from stance config if available
                            if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
                            {
                                var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                                ps.TargetHeight = cfg.ProneHeight;
                            }
                            else
                            {
                                ps.TargetHeight = PlayerStanceConfig.Default.ProneHeight;
                            }

                            // If per-entity tuning for interpolation exists, use it for height transitions
                            if (SystemAPI.HasComponent<ProneTuning>(entity))
                            {
                                var tuning = SystemAPI.GetComponent<ProneTuning>(entity);
                                ps.HeightTransitionSpeed = tuning.HeightInterpSpeed;
                            }
                            ecb.SetComponent(entity, ps);
                        }
                    }
                    else
                    {
                        // request to stand up - perform safe-stand check before clearing
                        bool canStand = SafeStandCheck(lt.Position, entity);
                        if (canStand)
                        {
                            prone.IsProne = 0;
                            prone.IsCrawling = 0;
                            prone.TransitionTimer = prone.TransitionDuration;
                            ecb.SetComponent(entity, prone);

                            if (EntityManager.HasComponent<PlayerState>(entity))
                            {
                                var ps = EntityManager.GetComponentData<PlayerState>(entity);
                                ps.Stance = PlayerStance.Standing;
                                ps.LastStanceChangeTime = (float)SystemAPI.Time.ElapsedTime;
                                if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
                                {
                                    var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                                    ps.TargetHeight = cfg.StandingHeight;
                                }
                                else
                                {
                                    ps.TargetHeight = PlayerStanceConfig.Default.StandingHeight;
                                }

                                // If per-entity tuning for interpolation exists, use it for height transitions
                                if (SystemAPI.HasComponent<ProneTuning>(entity))
                                {
                                    var tuning = SystemAPI.GetComponent<ProneTuning>(entity);
                                    ps.HeightTransitionSpeed = tuning.HeightInterpSpeed;
                                }
                                ecb.SetComponent(entity, ps);
                            }
                        }
                        else
                        {
                            // cannot stand - keep prone
                        }
                    }
                }

                // If currently prone and movement input present, enable crawling flag
                if (prone.IsProne != 0 && (input.Move.x != 0 || input.Move.y != 0))
                {
                    if (prone.IsCrawling == 0)
                    {
                        prone.IsCrawling = 1;
                        ecb.SetComponent(entity, prone);
                    }
                }
                else if (prone.IsProne != 0 && input.Move.x == 0 && input.Move.y == 0)
                {
                    if (prone.IsCrawling != 0)
                    {
                        prone.IsCrawling = 0;
                        ecb.SetComponent(entity, prone);
                    }
                }

                // Advance transition timer
                if (prone.TransitionTimer > 0f)
                {
                    prone.TransitionTimer = math.max(0f, prone.TransitionTimer - dt);
                    ecb.SetComponent(entity, prone);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }


            // Burst-safe collector that keeps the closest RaycastHit (prone-specific)
            private struct ProneRaycastCollector : ICollector<Unity.Physics.RaycastHit>
            {
                public bool EarlyOutOnFirstHit => false;
                public float MaxFraction { get; set; }
                public Unity.Physics.RaycastHit Hit;
                public int NumHits => (MaxFraction < float.MaxValue) ? 1 : 0;

                public ProneRaycastCollector(float maxFraction)
                {
                    MaxFraction = maxFraction;
                    Hit = default(Unity.Physics.RaycastHit);
                }

                public bool AddHit(Unity.Physics.RaycastHit hit)
                {
                    if (hit.Fraction < MaxFraction)
                    {
                        MaxFraction = hit.Fraction;
                        Hit = hit;
                    }
                    return true;
                }
            }

        // Conservative upward clearance check using the CollisionWorld. Samples a few upward rays
        // around the character position to ensure there is sufficient overhead clearance.
        private bool SafeStandCheck(float3 worldPosition, Entity entity)
        {
            // Use the project's PhysicsWorld access pattern (PhysicsWorldSingleton)
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var collisionWorld = physicsWorld.CollisionWorld;

            // Determine desired standing/prone capsule dimensions
            float standingHeight = PlayerStanceConfig.Default.StandingHeight;
            float proneHeight = PlayerStanceConfig.Default.ProneHeight;
            float radius = 0.4f;

            if (SystemAPI.HasComponent<PlayerStanceConfig>(entity))
            {
                var cfg = SystemAPI.GetComponent<PlayerStanceConfig>(entity);
                standingHeight = cfg.StandingHeight;
                proneHeight = cfg.ProneHeight;
            }

            if (SystemAPI.HasComponent<CharacterControllerSettings>(entity))
            {
                var settings = SystemAPI.GetComponent<CharacterControllerSettings>(entity);
                radius = settings.Radius;
            }

            if (SystemAPI.HasComponent<PlayerState>(entity))
            {
                var ps = SystemAPI.GetComponent<PlayerState>(entity);
                if (ps.CurrentHeight > 0f)
                    proneHeight = ps.CurrentHeight;
            }

            // Approximate morph sweep by sampling a few intermediate heights between prone and standing.
            int steps = 4;
            int radialSamples = 8;
            float clearanceEps = 0.05f;

            // If per-entity tuning exists, read it and override defaults
            if (SystemAPI.HasComponent<ProneTuning>(entity))
            {
                var tuning = SystemAPI.GetComponent<ProneTuning>(entity);
                steps = math.max(1, tuning.SafeStandSteps);
                radialSamples = math.max(3, tuning.SafeStandRadialSamples);
                clearanceEps = math.max(0f, tuning.ClearanceMargin);
            }

            for (int s = 1; s <= steps; ++s)
            {
                float t = (float)s / (float)steps;
                float testHeight = math.lerp(proneHeight, standingHeight, t);

                // sample three vertical layers across the capsule (bottom/mid/top)
                float h0 = 0.05f;
                float h1 = math.clamp(testHeight * 0.5f, 0.05f, testHeight - 0.05f);
                float h2 = math.max(0.05f, testHeight - clearanceEps - 0.05f);

                float radiusSample = math.max(0.01f, radius - 0.02f);

                for (int ih = 0; ih < 3; ++ih)
                {
                    float h = (ih == 0) ? h0 : (ih == 1) ? h1 : h2;
                    for (int r = 0; r < radialSamples; ++r)
                    {
                        float ang = (2 * math.PI * r) / radialSamples;
                        float3 offset = new float3(math.cos(ang) * radiusSample, 0, math.sin(ang) * radiusSample);
                        var rayStart = worldPosition + new float3(offset.x, h, offset.z);
                        var rayEnd = rayStart + new float3(0f, testHeight + clearanceEps, 0f);

                        var rayInput = new RaycastInput
                        {
                            Start = rayStart,
                            End = rayEnd,
                            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u, GroupIndex = 0 }
                        };

                        var collector = new ProneRaycastCollector(float.MaxValue);
                        collisionWorld.CastRay(rayInput, ref collector);
                        if (collector.MaxFraction < float.MaxValue)
                        {
                            var hit = collector.Hit;
                            // ignore floor-like hits (up-facing normals)
                            if (hit.SurfaceNormal.y > 0.6f)
                                continue;
                            // otherwise treat as blocking
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        // (removed) ClosestCollectorForCast was unused after switching to ray-based morph approximation
    }
}
