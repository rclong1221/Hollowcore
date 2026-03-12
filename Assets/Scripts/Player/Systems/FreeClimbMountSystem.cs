using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Collections.LowLevel.Unsafe;
using Player.Components;
using UnityEngine;
using DIG.Interaction;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 14.26: Object Gravity Mount System
    /// 
    /// Responsibilities:
    /// 1. Detect if player wants to climb (Jump pressed near wall)
    /// 2. Initiate Adhesion (Snap to surface)
    /// 3. Handle Transitions for smooth entry
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbDetectionSystem))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbMountSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var currentTime = SystemAPI.Time.ElapsedTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            
            new FreeClimbMountJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                PhysicsWorld = physicsWorld.PhysicsWorld,
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                InteractAbilityLookup = SystemAPI.GetComponentLookup<InteractAbility>(true)
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FreeClimbMountJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<InteractAbility> InteractAbilityLookup;

            private void Execute(Entity entity, ref FreeClimbState climb, ref LocalTransform lt, ref PhysicsVelocity vel, ref PhysicsGravityFactor gravity, RefRO<FreeClimbSettings> settings, RefRO<PlayerInput> input, RefRO<CharacterControllerSettings> charSettings)
            {
                // SKIP if already climbing or transitioning
                if (climb.IsClimbing || climb.IsTransitioning)
                {
                    // Handle active transition logic here if needed
                    // (Lerping position towards surface)
                    if (climb.IsTransitioning)
                    {
                        HandleTransition(ref climb, ref lt, ref vel, ref gravity, settings.ValueRO);
                    }
                    return;
                }

                // BLOCK if interacting (e.g. seated, crafting) - EPIC 14.27
                if (InteractAbilityLookup.HasComponent(entity))
                {
                    if (InteractAbilityLookup[entity].IsInteracting) return;
                }

                // MOUNT TRIGGER: Jump pressed + valid surface nearby
                var inp = input.ValueRO;
                if (!inp.Jump.IsSet) return;

                // Cooldown check
                if (CurrentTime - climb.LastDismountTime < settings.ValueRO.ReMountCooldown) return;

                // Detect Surface
                var cfg = settings.ValueRO;
                float3 origin = lt.Position + new float3(0, charSettings.ValueRO.Height * 0.5f, 0);
                float radius = cfg.DetectionRadius;

                // Sphere cast to find nearest climbable
                // Just use a multi-ray check around player or relying on DetectionSystem if kept?
                // Let's do a direct forward cast for mounting intent
                
                float3 fwd = math.forward(lt.Rotation);
                float3 castDir = fwd;
                float dist = cfg.DetectionDistance;

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };
                
                // 1. Forward Ray
                // Use a Collector to ignore the Player entity and find the closest valid hit
                var collector = new IgnoreEntityCollector(entity);
                var rayInput = new RaycastInput { Start = origin, End = origin + castDir * dist, Filter = filter };
                
                PhysicsWorld.CastRay(rayInput, ref collector);
                
                if (collector.HasHit)
                {
                    var hit = collector.ClosestHit;
                    // Relaxed constraints (20-160 deg, 0.2 facing)
                    if (ValidateMountHit(hit, entity, fwd, 20f, 160f))
                    {
                        StartClimb(ref climb, ref lt, ref vel, ref gravity, hit, cfg, CurrentTime, TransformLookup);
                        return;
                    }
                }
                
                // 2. Slightly Up Ray (Ledging)
                // Same logic simply applied? Or skip for now?
                // Let's rely on the main ray first. Ledging usually implies we are close anyway.
            }
            
            private bool ValidateMountHit(RaycastHit hit, Entity self, float3 fwd, float minAngle, float maxAngle)
            {
                 // 1. Self check handled in loop now, but keep for safety
                if (hit.Entity == self) return false;
                
                // 2. Safe Normal Check
                if (!math.all(math.isfinite(hit.SurfaceNormal)) || math.lengthsq(hit.SurfaceNormal) < 0.001f) return false;

                // 3. Facing Check (Must face wall)
                // Relaxed to 0.2 (~78 degrees) to allow glancing mounts
                if (math.dot(fwd, -hit.SurfaceNormal) < 0.2f) return false;
                
                // 4. Angle Check (Must be wall-like)
                float angleFromUp = math.degrees(math.acos(math.clamp(math.dot(hit.SurfaceNormal, new float3(0, 1, 0)), -1f, 1f)));
                if (angleFromUp < minAngle || angleFromUp > maxAngle) return false;
                
                return true;
            }

            private void StartClimb(ref FreeClimbState climb, ref LocalTransform lt, ref PhysicsVelocity vel, ref PhysicsGravityFactor gravity, RaycastHit hit, FreeClimbSettings cfg, double time, ComponentLookup<LocalTransform> transformLookup)
            {
                climb.IsClimbing = true;
                climb.IsTransitioning = true;
                climb.TransitionProgress = 0f;
                climb.TransitionStartTime = time;
                climb.MountTime = time;
                
                // Disable Gravity
                gravity.Value = 0f;
                vel.Linear = float3.zero;
                
                // Set Adhesion
                climb.IsAdhered = true;
                climb.AdhesionStrength = 1.0f;
                climb.SurfaceEntity = hit.Entity;
                climb.SurfaceNormal = hit.SurfaceNormal;
                climb.SurfaceContactPoint = hit.Position;

                // EPIC FIX: Initialize Local Space Data immediately
                if (transformLookup.HasComponent(hit.Entity))
                {
                    var surfaceLT = transformLookup[hit.Entity];
                    climb.GripLocalPosition = surfaceLT.InverseTransformPoint(hit.Position);
                    climb.GripLocalNormal = math.rotate(math.inverse(surfaceLT.Rotation), hit.SurfaceNormal);
                }
                
                // Setup Transition Target
                climb.TransitionStartPos = lt.Position;
                climb.TransitionStartRot = lt.Rotation;
                
                // Target: Hit Pos + Normal * Offset
                climb.TransitionTargetPos = hit.Position + (hit.SurfaceNormal * cfg.SurfaceOffset);
                
                // Target Rot: Look at Adhesion
                float3 lookDir = -hit.SurfaceNormal;
                if (math.lengthsq(lookDir) > 0.001f)
                {
                    float3 upVec = math.up();
                    if (math.abs(math.dot(lookDir, upVec)) > 0.99f) upVec = new float3(1, 0, 0); // Fallback if collinear
                    climb.TransitionTargetRot = quaternion.LookRotation(lookDir, upVec);
                }
                else
                    climb.TransitionTargetRot = lt.Rotation;

                // Zero velocity
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;
            }

            private void HandleTransition(ref FreeClimbState climb, ref LocalTransform lt, ref PhysicsVelocity vel, ref PhysicsGravityFactor gravity, FreeClimbSettings cfg)
            {
                // Simple Linear Interpolation
                float speed = cfg.MountTransitionSpeed > 0 ? cfg.MountTransitionSpeed : 5.0f;
                climb.TransitionProgress += DeltaTime * speed;
                
                // Smooth Step
                float t = math.smoothstep(0f, 1f, climb.TransitionProgress);
                
                // Safety: Ensure target AND rotation are finite
                bool posValid = math.all(math.isfinite(climb.TransitionTargetPos)) && math.all(math.isfinite(climb.TransitionStartPos));
                bool rotValid = math.all(math.isfinite(climb.TransitionStartRot.value)) && math.all(math.isfinite(climb.TransitionTargetRot.value));
                
                if (posValid && rotValid)
                {
                    lt.Position = math.lerp(climb.TransitionStartPos, climb.TransitionTargetPos, t);
                    
                    // Shortest Path Slerp
                    if (math.dot(climb.TransitionStartRot, climb.TransitionTargetRot) < 0f)
                    {
                        climb.TransitionTargetRot.value = -climb.TransitionTargetRot.value;
                    }
                    lt.Rotation = math.slerp(climb.TransitionStartRot, climb.TransitionTargetRot, t);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[CLIMB_VAULT_DEBUG] Transition Data Invalid! StartPos={climb.TransitionStartPos} TargetPos={climb.TransitionTargetPos} StartRot={climb.TransitionStartRot.value} TargetRot={climb.TransitionTargetRot.value}");
                }
                
                vel.Linear = float3.zero;
                
                if (climb.TransitionProgress >= 1.0f)
                {
                    climb.IsTransitioning = false;
                    climb.TransitionProgress = 0f;
                    
                    // FAILSAFE: Ensure we don't get stuck in climbing state if animation event fails
                    if (climb.IsClimbingUp || climb.IsMantling)
                    {
                         // UnityEngine.Debug.Log($"[CLIMB_VAULT_DEBUG] Transition Reached 100% Progress. Vault/Mantle Complete.");
                         climb.IsClimbingUp = false;
                         climb.IsMantling = false;
                         
                         // CRITICAL FIX: Recursion Loop prevention.
                         // When vault completes, we are STANDING on top. We must EXIT climbing mode.
                         // Otherwise FreeClimbLedgeSystem sees IsClimbing=true and triggers StartClimbUp AGAIN.
                         
                         climb.IsClimbing = false;
                         climb.IsAdhered = false;
                         climb.IsFreeHanging = false;
                         climb.SurfaceEntity = Entity.Null;
                         
                         // Determine exit rotation (Upright)
                         float3 flatForward = math.mul(lt.Rotation, new float3(0, 0, 1));
                         flatForward.y = 0;
                         if (math.lengthsq(flatForward) > 0.001f)
                            lt.Rotation = quaternion.LookRotation(math.normalize(flatForward), new float3(0, 1, 0));
                            
                         // Restore Gravity
                         gravity.Value = 1f; 
                    }
                }
            }
        }
        
        [BurstCompile]
        public struct IgnoreEntityCollector : ICollector<Unity.Physics.RaycastHit>
        {
            public Entity IgnoreEntity;
            public Unity.Physics.RaycastHit ClosestHit;
            public bool HasHit;
            public float MaxFraction { get; private set; }
            public int NumHits => HasHit ? 1 : 0;
            public bool EarlyOutOnFirstHit => false;

            public IgnoreEntityCollector(Entity ignore)
            {
                IgnoreEntity = ignore;
                ClosestHit = default;
                HasHit = false;
                MaxFraction = 1.0f;
            }

            public bool AddHit(Unity.Physics.RaycastHit hit)
            {
                // Ignore ourself
                if (hit.Entity == IgnoreEntity)
                {
                    return false;
                }

                // Standard Closest Hit logic
                if (hit.Fraction < MaxFraction)
                {
                    MaxFraction = hit.Fraction;
                    ClosestHit = hit;
                    HasHit = true;
                    return true;
                }
                return false;
            }
        }
    }
}
